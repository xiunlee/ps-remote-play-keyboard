#!/usr/bin/env python3
"""Compile/run pure V5 tests with the unchanged native C mapping as oracle.

Usage from any directory:
  python3 src/macos/test-dualsense-v5.py --output .local/mac-v5-protocol-1
Never starts an app/backend or opens a keyboard, socket or USB device.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--output", required=True, help="new output directory, relative to repository")
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
out = Path(args.output)
if not out.is_absolute():
    out = root / out
out = out.resolve()
out.mkdir(parents=True)  # Refuse to overwrite any previous build.
cache = out / "module-cache"
cache.mkdir()
sources = ["src/macos/ControlCore.swift", "src/macos/DualSenseV5Protocol.swift",
           "src/macos/DualSenseV5ProtocolTests.swift", "src/macos/test-dualsense-v5.py",
           "research/native-merge/keyboard_merge.c", "research/native-merge/keyboard_merge.h",
           "src/keyboard-pad/KeyboardMapping.cs", "src/keyboard-pad/ViiperTransport.cs",
           "research/dualsense-v5-test/V5Protocol.cs", "research/dualsense-v5-test/V5ProtocolTests.cs"]
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
hashes = {name: sha(root / name) for name in sources}
commands = [
    ["clang", "-std=c11", "-Wall", "-Wextra", "-Werror", "-c", sources[4], "-o", str(out / "keyboard_merge.o")],
    ["swiftc", "-swift-version", "6", "-warnings-as-errors", "-parse-as-library",
     "-module-cache-path", str(cache), "-import-objc-header", sources[5],
     sources[0], sources[1], sources[2], str(out / "keyboard_merge.o"), "-o", str(out / "DualSenseV5ProtocolTests")],
    [str(out / "DualSenseV5ProtocolTests")],
]
outputs = []
for index, command in enumerate(commands):
    result = subprocess.run(command, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (out / ("step-" + str(index + 1) + ".log")).write_text(result.stdout)
    outputs.append(result.stdout)
    if result.stdout:
        print(result.stdout, end="", flush=True)
    result.check_returncode()
if hashes != {name: sha(root / name) for name in sources}:
    raise RuntimeError("source changed during verification")
removed = sum(p.stat().st_size for p in cache.rglob("*") if p.is_file() and not p.is_symlink())
shutil.rmtree(cache)
record = {"verifiedAtUTC": datetime.now(timezone.utc).isoformat(), "sourceSha256": hashes,
          "testExecutableSha256": sha(out / "DualSenseV5ProtocolTests"), "result": outputs[-1].strip(),
          "swiftVersion": subprocess.check_output(["swiftc", "--version"], text=True).strip(),
          "clangVersion": subprocess.check_output(["clang", "--version"], text=True).strip(),
          "syntheticOnly": True, "socketsOpened": False, "devicesCreated": False, "sonyAccessed": False,
          "removedOwnModuleCacheLogicalBytes": removed}
(out / "RESULT.json").write_text(json.dumps(record, indent=2) + "\n")
print("Receipt: " + str(out / "RESULT.json"), flush=True)
