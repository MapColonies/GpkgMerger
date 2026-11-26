import io
import os
import sqlite3
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout

from PIL import Image
from PIL.PngImagePlugin import PngInfo

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import compare  # noqa: E402


def png(color, note=""):
    info = PngInfo()
    info.add_text("note", note)
    out = io.BytesIO()
    Image.new("RGBA", (4, 4), color).save(out, "PNG", pnginfo=info)
    return out.getvalue()


def write_dir(root, tiles):
    for (z, x, y), data in tiles.items():
        os.makedirs(os.path.join(root, str(z), str(x)), exist_ok=True)
        with open(os.path.join(root, str(z), str(x), f"{y}.png"), "wb") as fh:
            fh.write(data)
    return root


def write_gpkg(path, tiles):
    con = sqlite3.connect(path)
    con.execute("create table gpkg_contents (table_name text, data_type text)")
    con.execute("insert into gpkg_contents values ('t', 'tiles')")
    con.execute("create table t (zoom_level int, tile_column int, tile_row int, tile_data blob)")
    con.executemany("insert into t values (?, ?, ?, ?)", [(z, x, y, d) for (z, x, y), d in tiles.items()])
    con.commit()
    con.close()
    return path


def run(a, b):
    out = io.StringIO()
    with redirect_stdout(out):
        rc = compare.main(a, b)
    return rc, out.getvalue()


class CompareTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)

    def path(self, name):
        return os.path.join(self.tmp.name, name)

    def test_same_pixels_in_different_bytes_match(self):
        a = write_dir(self.path("a"), {(1, 0, 0): png("red", "one"), (2, 1, 1): png("blue")})
        b = write_gpkg(self.path("b.gpkg"), {(1, 0, 0): png("red", "two"), (2, 1, 1): png("blue")})
        rc, out = run(a, b)
        self.assertEqual(rc, 0, out)
        self.assertIn("identical bytes=1 same pixels/different bytes=1 pixel diffs=0", out)

    def test_changed_pixels_mismatch(self):
        a = write_dir(self.path("a"), {(1, 0, 0): png("red")})
        b = write_dir(self.path("b"), {(1, 0, 0): png("green")})
        rc, out = run(a, b)
        self.assertEqual(rc, 1)
        self.assertIn("diff (1, 0, 0)", out)

    def test_missing_tile_mismatches(self):
        a = write_dir(self.path("a"), {(1, 0, 0): png("red"), (1, 1, 0): png("red")})
        b = write_dir(self.path("b"), {(1, 0, 0): png("red")})
        rc, out = run(a, b)
        self.assertEqual(rc, 1)
        self.assertIn("only in A=1 only in B=0", out)

    def test_unreadable_input_exits_2_not_mismatch(self):
        a = write_dir(self.path("a"), {(1, 0, 0): png("red")})
        script = os.path.join(os.path.dirname(os.path.abspath(__file__)), "compare.py")
        proc = subprocess.run([sys.executable, script, a, self.path("missing.gpkg")], capture_output=True, text=True)
        self.assertEqual(proc.returncode, 2, proc.stdout + proc.stderr)
        self.assertIn("ERROR", proc.stdout)


if __name__ == "__main__":
    unittest.main()
