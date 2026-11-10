#!/usr/bin/env python3
"""Inspect an existing Unity Web build, then prepare headers or serve it locally.

Never builds Unity, deploys, or fabricates output. --write-config explicitly writes
vercel.json inside the validated build directory. Tests use synthetic fixtures.
"""
import argparse
import functools
import http.server
import json
import mimetypes
from pathlib import Path
import re


MIME = {".wasm": "application/wasm", ".js": "application/javascript",
        ".data": "application/octet-stream", ".json": "application/json",
        ".html": "text/html; charset=utf-8", ".css": "text/css; charset=utf-8"}


def headers_for(path):
    """Choose encoding only from actual suffixes; .unityweb uses JS fallback."""
    name = path.name
    encoding = None
    if name.endswith(".br"):
        encoding, name = "br", name[:-3]
    elif name.endswith(".gz"):
        encoding, name = "gzip", name[:-3]
    if name.endswith(".unityweb"):
        # Unity's JS decompression fallback consumes the compressed bytes itself.
        content_type = "application/octet-stream"
    else:
        suffix = Path(name).suffix.lower()
        content_type = MIME.get(suffix, mimetypes.guess_type(name)[0] or "application/octet-stream")
    # Unity hashed filenames carry a full 128-bit hash. Never cache named
    # loaders/frameworks/StreamingAssets immutably just because they're assets.
    hashed = bool(re.search(r"(?:^|[._-])[a-fA-F0-9]{32}(?=[._-]|$)", name))
    result = {"Content-Type": content_type,
              "Cache-Control": "public, max-age=31536000, immutable" if hashed and Path(name).suffix != ".html" else "no-cache",
              "X-Content-Type-Options": "nosniff"}
    if encoding:
        result["Content-Encoding"] = encoding
    return result


def inspect_build(root):
    root = root.resolve()
    index = root / "index.html"
    if not index.is_file():
        raise ValueError("BLOCKED: no generated index.html; run a real Unity Web build first")
    html = index.read_text(encoding="utf-8")
    if "{{{" in html or "createUnityInstance" not in html:
        raise ValueError("BLOCKED: index.html is an unexpanded template or not a Unity loader page")
    build = root / "Build"
    if not build.is_dir():
        raise ValueError("BLOCKED: generated Build directory is absent")
    files = sorted(p for p in root.rglob("*") if p.is_file() and p.name != "vercel.json")
    for path in files:
        if not path.resolve().is_relative_to(root):
            raise ValueError("BLOCKED: build contains a file symlink outside its root")
    generated = [p for p in files if p.parent == build and p.stat().st_size > 0]
    loaders = [p for p in generated if p.name.endswith(".loader.js")]
    if not loaders or not any(p.name in html for p in loaders):
        raise ValueError("BLOCKED: referenced generated .loader.js is absent or empty")
    for pattern, label in [(r"\.framework\.js(?:\.br|\.gz|\.unityweb)?$", "framework"),
                           (r"\.wasm(?:\.br|\.gz|\.unityweb)?$", "WebAssembly"),
                           (r"\.data(?:\.br|\.gz|\.unityweb)?$", "data")]:
        matches = [p for p in generated if re.search(pattern, p.name)]
        if not matches or not any(p.name in html for p in matches):
            raise ValueError("BLOCKED: referenced generated " + label + " payload is absent or empty")
    return {"/" + p.relative_to(root).as_posix(): headers_for(p) for p in files}


def vercel_config(headers):
    rules = [{"source": path, "headers": [{"key": k, "value": v} for k, v in values.items()]}
             for path, values in headers.items()]
    rules.append({"source": "/", "headers": [{"key": k, "value": v} for k, v in headers["/index.html"].items()]})
    return {"headers": rules}


class BuildHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        from urllib.parse import unquote, urlsplit
        route = unquote(urlsplit(self.path).path)
        if route == "/":
            route = "/index.html"
        values = self.server.build_headers.get(route, {})
        # send_head emits guessed Content-Type; replace it with the actual type.
        if values:
            self._headers_buffer = [line for line in self._headers_buffer
                                    if not line.lower().startswith(b"content-type:")]
            for key, value in values.items():
                self.send_header(key, value)
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("build", type=Path)
    parser.add_argument("--write-config", action="store_true", help="write vercel.json into the validated build")
    parser.add_argument("--serve", action="store_true", help="serve validated build locally, no deployment")
    parser.add_argument("--port", type=int, default=8080)
    args = parser.parse_args()
    try:
        headers = inspect_build(args.build)
    except (ValueError, OSError) as error:
        parser.exit(2, str(error) + "\n")
    if args.write_config:
        (args.build / "vercel.json").write_text(json.dumps(vercel_config(headers), indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "inspected-existing-files", "files": len(headers),
                      "bytes": sum(p.stat().st_size for p in args.build.rglob("*") if p.is_file()),
                      "headers": headers}, indent=2))
    if args.serve:
        handler = functools.partial(BuildHandler, directory=str(args.build.resolve()))
        with http.server.ThreadingHTTPServer(("127.0.0.1", args.port), handler) as server:
            server.build_headers = headers
            print(f"Local existing build: http://127.0.0.1:{args.port}/", flush=True)
            server.serve_forever()


if __name__ == "__main__":
    main()
