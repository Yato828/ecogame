"""Synthetic HTTP/header fixtures only; these tests do not run Unity or a browser."""
import importlib.util
from pathlib import Path
import tempfile
import functools
import http.server
import threading
import urllib.request
import unittest

spec = importlib.util.spec_from_file_location("prepare_web_host", Path(__file__).parents[1] / "prepare_web_host.py")
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)


class HostingFixtureTests(unittest.TestCase):
    def test_encoding_and_types(self):
        for name, mime, encoding in [("test.wasm.br", "application/wasm", "br"),
                                     ("test.framework.js.gz", "application/javascript", "gzip"),
                                     ("test.data.gz", "application/octet-stream", "gzip"),
                                     ("test.wasm.unityweb", "application/octet-stream", None),
                                     ("test.wasm", "application/wasm", None)]:
            headers = host.headers_for(Path(name))
            self.assertEqual(headers["Content-Type"], mime)
            self.assertEqual(headers.get("Content-Encoding"), encoding)

    def test_cache_requires_hash(self):
        self.assertEqual(host.headers_for(Path("x.loader.js"))["Cache-Control"], "no-cache")
        self.assertIn("immutable", host.headers_for(Path("a" * 32 + ".data.br"))["Cache-Control"])
        self.assertEqual(host.headers_for(Path("a" * 32 + ".html"))["Cache-Control"], "no-cache")

    def test_absent_build_refused_without_writes(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "no generated index"):
                host.inspect_build(Path(directory))
            self.assertEqual(list(Path(directory).iterdir()), [])

    def test_template_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "index.html").write_text("{{{ PRODUCT_NAME }}} createUnityInstance")
            with self.assertRaisesRegex(ValueError, "unexpanded"):
                host.inspect_build(root)

    def test_local_server_matches_inspected_headers(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "fixture.wasm.br").write_bytes(b"fixture")
            handler = functools.partial(host.BuildHandler, directory=directory)
            with http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler) as server:
                expected = host.headers_for(root / "fixture.wasm.br")
                server.build_headers = {"/fixture.wasm.br": expected}
                thread = threading.Thread(target=server.serve_forever, daemon=True)
                thread.start()
                try:
                    with urllib.request.urlopen(f"http://127.0.0.1:{server.server_port}/fixture.wasm.br") as response:
                        for key, value in expected.items():
                            self.assertEqual(response.headers.get_all(key), [value])
                finally:
                    server.shutdown()
                    thread.join()

    def test_generated_fixture_and_missing_payload(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Build").mkdir()
            names = ["fixture.loader.js", "fixture.framework.js.br", "fixture.wasm.br", "fixture.data.br"]
            (root / "index.html").write_text("createUnityInstance " + " ".join(names))
            for name in names:
                (root / "Build" / name).write_bytes(b"fixture bytes, not Unity output")
            headers = host.inspect_build(root)
            self.assertEqual(len(headers), 5)
            self.assertEqual(host.vercel_config(headers)["headers"][-1]["source"], "/")
            (root / "Build" / names[-1]).unlink()
            with self.assertRaisesRegex(ValueError, "data payload"):
                host.inspect_build(root)


if __name__ == "__main__":
    unittest.main()
