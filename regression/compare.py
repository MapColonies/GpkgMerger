#!/usr/bin/env python3
"""Compare two tile sets (gpkg file or z/x/y directory tree) tile by tile.

Usage: compare.py <a> <b>     each is a .gpkg path or a directory
Exit 0 when both hold the same tile keys and every tile decodes to the same pixels.
"""
import io
import os
import sqlite3
import sys

import numpy as np
from PIL import Image


def s3_client():
    import boto3

    return boto3.client("s3", endpoint_url=os.environ["S3_ENDPOINT"], region_name="us-east-1")


def load(path):
    if path.startswith("s3://"):
        bucket, _, prefix = path[5:].partition("/")
        client, tiles = s3_client(), {}
        for page in client.get_paginator("list_objects_v2").paginate(Bucket=bucket, Prefix=prefix.rstrip("/") + "/"):
            for obj in page.get("Contents", []):
                parts = obj["Key"][len(prefix.rstrip("/")) + 1:].split("/")
                try:
                    key = (int(parts[0]), int(parts[1]), int(os.path.splitext(parts[2])[0]))
                except (ValueError, IndexError):
                    continue
                tiles[key] = ("s3", client, bucket, obj["Key"])
        return tiles
    if path.endswith(".gpkg"):
        con = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
        tables = [r[0] for r in con.execute("select table_name from gpkg_contents where data_type='tiles'")]
        tiles = {}
        for t in tables:
            for z, x, y, data in con.execute(f'select zoom_level, tile_column, tile_row, tile_data from "{t}"'):
                tiles[(z, x, y)] = data
        return tiles
    tiles = {}
    for root, _, files in os.walk(path):
        rel = os.path.relpath(root, path).split(os.sep)
        for f in files:
            parts = rel[-2:] + [os.path.splitext(f)[0]]
            try:
                key = tuple(int(p) for p in parts)
            except ValueError:
                continue
            tiles[key] = os.path.join(root, f)
    return tiles


def blob(v):
    if isinstance(v, tuple) and v[0] == "s3":
        return v[1].get_object(Bucket=v[2], Key=v[3])["Body"].read()
    if isinstance(v, str):
        with open(v, "rb") as fh:
            return fh.read()
    return v


def pixels(data):
    return np.asarray(Image.open(io.BytesIO(data)).convert("RGBA"), dtype=np.int16)


def main(a, b):
    ta, tb = load(a), load(b)
    only_a, only_b = sorted(ta.keys() - tb.keys()), sorted(tb.keys() - ta.keys())
    same_bytes = pixel_equal = 0
    diffs = []
    for k in sorted(ta.keys() & tb.keys()):
        da, db = blob(ta[k]), blob(tb[k])
        if da == db:
            same_bytes += 1
            continue
        pa, pb = pixels(da), pixels(db)
        if pa.shape == pb.shape and not np.any(pa != pb):
            pixel_equal += 1
        else:
            delta = int(np.abs(pa - pb).max()) if pa.shape == pb.shape else -1
            diffs.append((k, delta))
    zooms = sorted({k[0] for k in ta})
    print(f"A={a}\nB={b}")
    print(f"tiles A={len(ta)} B={len(tb)} zooms={zooms[0] if zooms else '-'}..{zooms[-1] if zooms else '-'}")
    print(f"identical bytes={same_bytes} same pixels/different bytes={pixel_equal} pixel diffs={len(diffs)}")
    print(f"only in A={len(only_a)} only in B={len(only_b)}")
    for k in only_a[:5]:
        print("  only A", k)
    for k in only_b[:5]:
        print("  only B", k)
    for k, d in diffs[:10]:
        print("  diff", k, "max channel delta", d)
    if os.environ.get("DIFF_LIST"):
        with open(os.environ["DIFF_LIST"], "w") as fh:
            for k, d in diffs:
                fh.write(f"{k[0]} {k[1]} {k[2]} {d}\n")
    ok = not only_a and not only_b and not diffs
    print("RESULT", "MATCH" if ok else "MISMATCH")
    return 0 if ok else 1


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv[1], sys.argv[2]))
    except Exception as exc:  # exit 1 means the outputs differ, so a crash needs its own code
        print("ERROR", repr(exc))
        sys.exit(2)

