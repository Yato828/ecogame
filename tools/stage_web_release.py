#!/usr/bin/env python3
"""Stage only a real Unity Web build, licenses and generated hosting headers."""
import argparse
import json
from pathlib import Path
import shutil
from prepare_web_host import inspect_build, vercel_config


def stage(source, destination):
    inspect_build(source)
    if destination.exists() and any(destination.iterdir()):
        raise ValueError('Destination is not empty; preserve any linked deployment before staging again.')
    destination.mkdir(parents=True, exist_ok=True)
    for name in ('index.html', 'style.css', 'Build', 'StreamingAssets', 'TemplateData'):
        item = source / name
        if item.is_dir():
            shutil.copytree(item, destination / name, ignore=shutil.ignore_patterns('*.meta', '*.pdb', '*.map', '*.symbols.json'))
        elif item.is_file():
            shutil.copy2(item, destination / name)
    repo = Path(__file__).resolve().parents[1]
    notices = ['WATTSHIFT - Third-party asset notices\nKenney City Kits: CC0.\n']
    for name in ('Suburban', 'Roads', 'Commercial', 'Industrial'):
        notices.append((repo / 'Assets/_WATTSHIFT/Art/Imported' / name / 'License.txt').read_text(encoding='utf-8-sig'))
    notices.append((repo / 'Assets/_WATTSHIFT/Resources/Fonts/OFL.txt').read_text())
    (destination / 'THIRD_PARTY_NOTICES.txt').write_text('\n\n'.join(notices), encoding='utf-8')
    config = vercel_config(inspect_build(destination))
    config.update({'framework': None, 'buildCommand': None, 'installCommand': None, 'outputDirectory': '.'})
    (destination / 'vercel.json').write_text(json.dumps(config, indent=2) + '\n')
    files = [p for p in destination.rglob('*') if p.is_file()]
    size = sum(p.stat().st_size for p in files)
    if size >= 100_000_000:
        raise ValueError(f'BLOCKED: {size} upload bytes exceed the conservative 100 MB Hobby limit.')
    print(json.dumps({'directory': str(destination), 'files': len(files), 'uploadBytes': size,
                      'largest': sorted([(str(p.relative_to(destination)), p.stat().st_size) for p in files], key=lambda pair: -pair[1])[:5]}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('build', type=Path)
    parser.add_argument('--destination', type=Path, default=Path('release/web-deploy'))
    args = parser.parse_args()
    stage(args.build, args.destination)
