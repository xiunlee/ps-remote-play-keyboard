#!/usr/bin/env python3
"""Developer-only offline native-runtime checks; never starts a VM or vendor client."""
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
COMMON = [HERE / name for name in (
    "RuntimeProtocol.swift", "NativeRelay.swift", "RuntimeProcess.swift", "RuntimeOwner.swift")]

with tempfile.TemporaryDirectory(prefix="pskeyboard-runtime-check-") as temporary:
    target = Path(temporary)
    compiler = ["swiftc", "-swift-version", "6", "-warnings-as-errors", "-parse-as-library",
                "-module-cache-path", str(target / "cache")]
    subprocess.run(compiler + [str(ROOT / "src/macos/ControlCore.swift"),
                    str(ROOT / "src/macos/DualSenseV5Protocol.swift"),
                    *map(str, COMMON), str(HERE / "RuntimeTests.swift"), "-o", str(target / "tests")], check=True)
    subprocess.run([str(target / "tests")], check=True, timeout=30)
    subprocess.run(compiler + [*map(str, COMMON), str(HERE / "RuntimeMain.swift"),
                              "-o", str(target / "PSKeyboardRuntime")], check=True)
    subprocess.run(["swiftc", "-swift-version", "5", "-warnings-as-errors", "-parse-as-library",
                    "-module-cache-path", str(target / "cache"), str(HERE / "GuestRunner.swift"),
                    "-o", str(target / "PSKeyboardGuest")], check=True)
    subprocess.run(["swiftc", "-swift-version", "6", "-warnings-as-errors", "-typecheck",
                    "-module-cache-path", str(target / "cache"), str(ROOT / "src/macos/MacRuntimeController.swift")], check=True)
    print("Runtime/guest compilation and App controller typecheck passed; temporary binaries removed on exit.")
