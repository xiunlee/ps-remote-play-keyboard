#!/usr/bin/env python3
"""Build a local research app without installing or starting any component."""
import argparse
import hashlib
import json
import pathlib
import plistlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCES = ["ControlCore.swift", "OJDFeed.swift", "PreviewPipe.swift", "KeyboardPreviewApp.swift"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True,
                        help="New directory directly under the repository's .local directory")
    args = parser.parse_args()
    output = args.output.absolute()
    if output.parent.resolve() != (ROOT / ".local").resolve():
        parser.error("Output must be directly under .local so the audited OJD path resolves correctly")
    output.mkdir()  # Refuse to overwrite an existing build or its permissions identity.
    app = output / "PSKeyboardMacResearch.app"
    contents = app / "Contents"
    binary = contents / "MacOS" / "PSKeyboardMacResearch"
    binary.parent.mkdir(parents=True)
    with (contents / "Info.plist").open("wb") as stream:
        plistlib.dump({
            "CFBundleExecutable": binary.name,
            "CFBundleIdentifier": "com.xiunlee.pskeyboard.mac-research",
            "CFBundleName": "上班不要玩 Mac 輸出研究",
            "CFBundlePackageType": "APPL", "CFBundleVersion": "1",
            "CFBundleShortVersionString": "0.0.1",
            "LSMinimumSystemVersion": "15.0", "NSHighResolutionCapable": True,
        }, stream)
    command = ["swiftc", "-swift-version", "6", "-warnings-as-errors", "-parse-as-library",
               "-target", "arm64-apple-macos15.0", "-module-cache-path", str(output / "module-cache")]
    command += [str(ROOT / "src/macos" / name) for name in SOURCES]
    command += ["-o", str(binary)]
    subprocess.run(command, check=True)
    # Synced Documents can add Finder metadata to newly generated bundles.
    # Strip only signing-incompatible metadata from our new build, never quarantine.
    for path in [app, *app.rglob("*")]:
        attributes = subprocess.check_output(["xattr", str(path)], text=True).splitlines()
        for attribute in ("com.apple.FinderInfo", "com.apple.ResourceFork"):
            if attribute in attributes:
                subprocess.run(["xattr", "-d", attribute, str(path)], check=True)
    subprocess.run(["codesign", "--force", "--sign", "-", str(app)], check=True)
    subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
    digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
    receipt = {
        "candidate": "mac-native.1", "architecture": "arm64", "minimumMacOS": "15.0",
        "swift": subprocess.check_output(["swiftc", "--version"], text=True).strip(),
        "baseCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "sources": {name: digest(ROOT / "src/macos" / name) for name in SOURCES},
        "binarySHA256": digest(binary), "signature": "ad-hoc; no restricted entitlement",
        "started": False, "sonyCompatibility": "unverified",
    }
    (output / "BUILD.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
    print(app)


if __name__ == "__main__":
    main()
