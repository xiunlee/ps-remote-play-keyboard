#!/usr/bin/env python3
"""Package square brand PNG artwork into native macOS icon sizes without redrawing."""
import argparse
from pathlib import Path
import struct
import subprocess
import tempfile


def build_icon(source: Path, destination: Path):
    if source.is_symlink() or not source.is_file():
        raise ValueError("Expected a regular brand PNG")
    with source.open("rb") as stream:
        header = stream.read(24)
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError("Expected a PNG with an IHDR header")
    width, height = struct.unpack(">II", header[16:24])
    if width != height or width < 1024:
        raise ValueError("Brand PNG must be square and at least 1024 pixels")
    if destination.exists() or destination.is_symlink():
        raise ValueError("Refusing to overwrite an existing icon")
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".pskeyboard-icon-", dir=destination.parent) as temporary:
        iconset = Path(temporary) / "app.iconset"
        iconset.mkdir()
        for point_size in (16, 32, 128, 256, 512):
            for scale in (1, 2):
                pixels = point_size * scale
                suffix = "@2x" if scale == 2 else ""
                target = iconset / f"icon_{point_size}x{point_size}{suffix}.png"
                subprocess.run(["/usr/bin/sips", "-z", str(pixels), str(pixels),
                                str(source), "--out", str(target)], check=True,
                               stdout=subprocess.DEVNULL)
        subprocess.run(["/usr/bin/iconutil", "-c", "icns", str(iconset),
                        "-o", str(destination)], check=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    build_icon(args.source, args.destination)
