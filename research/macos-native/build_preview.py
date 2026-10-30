#!/usr/bin/env python3
"""Build or update the single Mac app with a stable local signing identity.

Run local_signing.py setup once first. Default verifies a temporary build;
--install replaces only the existing matching app after it has stopped.
No ad-hoc fallback, extra app names, or retained module caches.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ["ControlCore.swift", "DualSenseV5Protocol.swift", "DualSenseRelaySocket.swift", "MacTheme.swift", "MacControlGuideView.swift",
           "MacSupportLinks.swift", "MacProfileStore.swift", "MacKeyNames.swift", "MacBindingEditor.swift", "MacText.swift",
           "MacDependencyInstaller.swift", "MacRuntimeController.swift", "DualSensePreviewApp.swift"]
RUNTIME_SOURCES = ["RuntimeProtocol.swift", "NativeRelay.swift", "RuntimeProcess.swift", "RuntimeOwner.swift", "RuntimeMain.swift"]
RUNTIME_HASHES = {
    "Image": "d6763ecc91a9b614898148642572f7f10b44377b888185816a63a7a2c76c511b",
    "initrd.gz": "1356a076d56df8d0596e5a7ccebaba12d6d139a4e373536667c2777f2eeda97f",
    "GuestDualSenseBackend-linux-arm64": "bc3bafce56289ef33f0c474c6a5c6ea78c86739af04c4cb638b79f7e46cdd951",
    "dummy.ko": "89573074aefe730879cd6d977c5e9f5531812c65a8f7f2701f898f2dd6f3951e",
    "af_packet.ko": "db354c5ec5cfa1ad11d3e826c84fd68d7579c7eba0b7752dd6915f74507f6f18",
}
IDENTIFIER = "com.xiunlee.pskeyboard.mac-dualsense"
APP_NAME = "PSKeyboardMacDualSense.app"
BINARY = "PSKeyboardMacDualSense"
ENGLISH_NAME = "Three Cats Keybridge"
BRAND_SOURCE = "assets/branding/three-cats-mark.png"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_provenance(root):
    """A published source snapshot is buildable without its developer's .git."""
    manifest = root / "SOURCE-MANIFEST.json"
    if manifest.exists():
        if manifest.is_symlink() or manifest.stat().st_size > 1024 * 1024:
            raise ValueError("Invalid source snapshot manifest")
        data = manifest.read_bytes()
        record = json.loads(data)
        commit = record.get("buildBaseCommit")
        if record.get("schemaVersion") != 1 or not isinstance(commit, str) or not re.fullmatch(r"[0-9a-f]{40}", commit):
            raise ValueError("Invalid source snapshot ancestry")
        return {"baseCommit": commit, "sourceOrigin": "source-archive",
                "sourceSnapshotManifestSHA256": hashlib.sha256(data).hexdigest()}
    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("Invalid Git ancestry")
    return {"baseCommit": commit, "sourceOrigin": "working-tree"}


def running(binary):
    lines = subprocess.check_output(["ps", "-axo", "pid=,comm="], text=True).splitlines()
    return any(line.strip().split(None, 1)[-1] == str(binary) for line in lines if line.strip())


def read_version(root):
    version_path = root / "packaging/macos/version.json"
    version_bytes = version_path.read_bytes()
    version = json.loads(version_bytes)
    version_digest = hashlib.sha256(version_bytes).hexdigest()
    project_version_bytes = (root / "VERSION").read_bytes()
    # Windows advances independently in VERSION; the Mac manifest owns both
    # its display and bundle version. Still snapshot VERSION for provenance.
    if version["displayVersion"] != version["shortVersion"]:
        raise ValueError("Mac display and bundle versions must match")
    if (version["bundleIdentifier"] != IDENTIFIER or version["appDirectory"] != APP_NAME or
            version["executable"] != BINARY or not re.fullmatch(r"[0-9]+[.][0-9]+[.][0-9]+", version["shortVersion"]) or
            not re.fullmatch(r"[1-9][0-9]*", version["buildNumber"]) or
            not re.fullmatch(r"[0-9]+[.][0-9]+[.][0-9]+(?:-[A-Za-z0-9.-]+)?", version["displayVersion"])):
        raise ValueError("Invalid version manifest or changed stable app identity")
    return version_path, version, version_digest, project_version_bytes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="New receipt directory directly under .local")
    parser.add_argument("--install", action="store_true", help="Update the fixed app in ~/Applications")
    parser.add_argument("--archive", action="store_true", help="Also create a local app ZIP for packaging verification")
    parser.add_argument("--runtime-materials", type=Path, default=ROOT / ".local/mac-usb-guest-1",
                        help="Verified guest materials directory (Image/initrd.gz plus share binaries)")
    parser.add_argument("--source-materials", type=Path, default=ROOT / ".local/mac-runtime-sources-1",
                        help="Verified corresponding-source collection supplying runtime notices")
    parser.add_argument("--dualsense", action="store_true", help=argparse.SUPPRESS)
    args = parser.parse_args()
    output = args.output.absolute()
    if output.parent.resolve() != (ROOT / ".local").resolve():
        parser.error("Receipt directory must be directly under .local")
    version_path, version, version_digest, project_version_bytes = read_version(ROOT)
    provenance = source_provenance(ROOT)
    # Check identity before spending time compiling; never silently create/rotate it.
    signer = ROOT / "research/macos-native/local_signing.py"
    identity_check = subprocess.run(["python3", str(signer), "check"], capture_output=True, text=True)
    if identity_check.returncode != 0:
        raise RuntimeError("Project signing identity check failed")
    output.mkdir()
    applications = Path.home() / "Applications"
    applications.mkdir(exist_ok=True)
    runtime = applications / APP_NAME
    runtime_binary = runtime / "Contents/MacOS" / BINARY
    source_bytes = {name: (ROOT / "src/macos" / name).read_bytes() for name in SOURCES}
    before = {name: hashlib.sha256(data).hexdigest() for name, data in source_bytes.items()}
    brand_bytes = (ROOT / BRAND_SOURCE).read_bytes()
    icon_builder = ROOT / "packaging/macos/build_icon.py"
    icon_builder_hash = digest(icon_builder)
    build_script_hash = digest(Path(__file__))
    runtime_source_bytes = {name: (ROOT / "src/macos/runtime" / name).read_bytes()
                            for name in RUNTIME_SOURCES + ["GuestRunner.swift"]}
    runtime_source_hashes = {name: hashlib.sha256(data).hexdigest() for name, data in runtime_source_bytes.items()}
    strings_path = ROOT / "assets/macos/strings.json"
    strings_bytes = strings_path.read_bytes()
    font_root = ROOT / "assets/macos/fonts"
    font_manifest_bytes = (font_root / "SOURCES.json").read_bytes()
    font_manifest = json.loads(font_manifest_bytes)
    font_files = {name: (font_root / name).read_bytes() for name in ("InterVariable.ttf", "OFL-Inter.txt")}
    for entry in font_manifest["files"]:
        data = font_files[entry["file"]]
        if len(data) != entry["size"] or hashlib.sha256(data).hexdigest() != entry["sha256"]:
            raise RuntimeError("Font material checksum mismatch")
    notice_root = args.source_materials / "notices"
    notice_files = {str(path.relative_to(notice_root)): path.read_bytes()
                    for path in notice_root.rglob("*") if path.is_file()}
    if not notice_files:
        raise RuntimeError("Runtime notices missing; collect corresponding sources first")
    verified_files = {}
    for line in (args.source_materials / "FILES.sha256").read_text().splitlines():
        expected, relative = line.split(None, 1)
        verified_files[relative.lstrip(" *")] = expected
    for name, data in notice_files.items():
        if hashlib.sha256(data).hexdigest() != verified_files.get("notices/" + name):
            raise RuntimeError("Runtime notice checksum mismatch: " + name)
    subprocess.run(["python3", str(ROOT / "packaging/macos/build_strings.py"), "--check"], check=True)
    with tempfile.TemporaryDirectory(prefix=".pskeyboard-build-", dir=applications) as temporary:
        staging = Path(temporary)
        app = staging / APP_NAME
        binary = app / "Contents/MacOS" / BINARY
        binary.parent.mkdir(parents=True)
        resources = app / "Contents/Resources"
        resources.mkdir()
        (resources / "ps-mark.png").write_bytes(brand_bytes)
        (resources / "strings.json").write_bytes(strings_bytes)
        (resources / "Fonts").mkdir()
        for name, data in font_files.items():
            (resources / "Fonts" / name).write_bytes(data)
        for language in ("en", "zh-Hant", "zh-Hans", "ja", "ko", "de", "fr", "es", "pt-BR"):
            localized = resources / (language + ".lproj")
            localized.mkdir()
            name = version["displayName"] if language.startswith("zh") else ENGLISH_NAME
            (localized / "InfoPlist.strings").write_text('"CFBundleDisplayName" = "' + name + '";\n"CFBundleName" = "' + name + '";\n')
        runtime_resources = resources / "Runtime"
        runtime_resources.mkdir()
        for name, expected in RUNTIME_HASHES.items():
            original = args.runtime_materials / (name if name in ("Image", "initrd.gz") else "share/" + name)
            if digest(original) != expected:
                raise RuntimeError("Runtime material hash mismatch: " + name)
            shutil.copyfile(original, runtime_resources / name)
        (resources / "LICENSE.txt").write_bytes((ROOT / "LICENSE").read_bytes())
        notices = resources / "Notices"
        for name, data in notice_files.items():
            target = notices / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        overview = [ENGLISH_NAME + " / " + version["displayName"], "Original application: GPL-3.0-only; author xiunlee.",
                    "Runtime components retain their own licenses, reproduced below.",
                    "VirtualHere is obtained separately from its official publisher and is not part of this bundle.",
                    "Complete runtime corresponding sources are supplied alongside the matching app and project source archive."]
        overview.extend(["Inter font — SIL Open Font License 1.1", font_files["OFL-Inter.txt"].decode("utf-8")])
        for name, data in sorted(notice_files.items()):
            overview.extend(["\n" + "=" * 72, name, data.decode("utf-8", errors="replace")])
        (resources / "THIRD-PARTY-NOTICES.txt").write_text("\n\n".join(overview) + "\n")
        subprocess.run(["python3", str(icon_builder), str(resources / "ps-mark.png"),
                        str(resources / "app.icns")], check=True)
        source_root = staging / "sources"
        snapshot = source_root / "src/macos"
        snapshot.mkdir(parents=True)
        for name, data in source_bytes.items():
            (snapshot / name).write_bytes(data)
        helper_snapshot = snapshot / "runtime"
        helper_snapshot.mkdir()
        for name, data in runtime_source_bytes.items():
            (helper_snapshot / name).write_bytes(data)
        with (app / "Contents/Info.plist").open("wb") as stream:
            plistlib.dump({
                "CFBundleExecutable": BINARY, "CFBundleIdentifier": IDENTIFIER,
                "CFBundleName": ENGLISH_NAME, "CFBundleDisplayName": ENGLISH_NAME,
                "CFBundlePackageType": "APPL", "CFBundleVersion": version["buildNumber"],
                "CFBundleDevelopmentRegion": "en",
                "CFBundleLocalizations": ["en", "zh-Hant", "zh-Hans", "ja", "ko", "de", "fr", "es", "pt-BR"],
                "CFBundleShortVersionString": version["shortVersion"],
                "PSKeyboardDisplayVersion": version["displayVersion"],
                "LSMinimumSystemVersion": "15.0", "NSHighResolutionCapable": True,
                "CFBundleIconFile": "app.icns",
            }, stream)
        # Swift can embed #filePath in runtime diagnostics. Relative source
        # arguments preserve repository paths there; the prefix flag also maps
        # debug, coverage and index paths (it alone does not map #filePath).
        source_path_flags = ["-file-prefix-map", str(snapshot) + "=src/macos"]
        command = ["swiftc", "-swift-version", "6", "-warnings-as-errors", "-parse-as-library",
                   "-target", "arm64-apple-macos15.0", "-module-cache-path", str(staging / "module-cache")]
        command += source_path_flags
        command += [str(Path("src/macos") / name) for name in SOURCES]
        subprocess.run(command + ["-o", str(binary)], cwd=source_root, check=True)
        helpers = app / "Contents/Helpers"
        helpers.mkdir()
        helper_flags = ["-warnings-as-errors", "-parse-as-library", "-target", "arm64-apple-macos15.0",
                        "-module-cache-path", str(staging / "module-cache")]
        helper_flags += source_path_flags
        subprocess.run(["swiftc", "-swift-version", "6"] + helper_flags +
                       [str(Path("src/macos/runtime") / name) for name in RUNTIME_SOURCES] +
                       ["-o", str(helpers / "PSKeyboardRuntime")], cwd=source_root, check=True)
        subprocess.run(["swiftc", "-swift-version", "5"] + helper_flags + ["src/macos/runtime/GuestRunner.swift",
                       "-o", str(helpers / "PSKeyboardGuest")], cwd=source_root, check=True)
        if (before != {name: digest(ROOT / "src/macos" / name) for name in SOURCES}
                or version_digest != digest(version_path)
                or project_version_bytes != (ROOT / "VERSION").read_bytes()
                or brand_bytes != (ROOT / BRAND_SOURCE).read_bytes()
                or icon_builder_hash != digest(icon_builder)
                or build_script_hash != digest(Path(__file__))
                or font_manifest_bytes != (font_root / "SOURCES.json").read_bytes()
                or any(data != (font_root / name).read_bytes() for name, data in font_files.items())):
            raise RuntimeError("Sources changed during build; nothing installed")
        if (runtime_source_hashes != {name: digest(ROOT / "src/macos/runtime" / name) for name in runtime_source_bytes}
                or strings_bytes != strings_path.read_bytes()):
            raise RuntimeError("Runtime or localization changed during build; nothing installed")
        for attempt in range(3):
            # Only metadata on our newly compiled bundle; never quarantine.
            for path in [app, *app.rglob("*")]:
                attributes = subprocess.check_output(["xattr", str(path)], text=True).splitlines()
                for attribute in ("com.apple.FinderInfo", "com.apple.ResourceFork"):
                    if attribute in attributes:
                        subprocess.run(["xattr", "-d", attribute, str(path)], check=True)
            signed = subprocess.run(["python3", str(signer), "sign", "--path", str(app)], capture_output=True, text=True)
            if signed.returncode == 0:
                break
            if "resource fork, Finder information" not in signed.stdout + signed.stderr or attempt == 2:
                raise RuntimeError("Local signing failed: " + signed.stdout + signed.stderr)
        subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
        described = subprocess.run(["codesign", "-d", "-r-", str(app)], capture_output=True, text=True, check=True)
        requirement = next(line.strip() for line in (described.stdout + described.stderr).splitlines() if "designated =>" in line)
        if "cdhash " in requirement or "certificate" not in requirement and "anchor" not in requirement:
            raise RuntimeError("Build lacks a stable certificate-based designated requirement")
        receipt = {
            "version": version["displayVersion"], "buildNumber": version["buildNumber"],
            "bundleIdentifier": IDENTIFIER, "runtimeLocation": "~/Applications/" + APP_NAME,
            "architecture": "arm64", "minimumMacOS": "15.0", "sources": before,
            "versionManifestSHA256": version_digest, "binarySHA256": digest(binary),
            "resources": {name: digest(resources / name) for name in ("ps-mark.png", "app.icns", "strings.json", "LICENSE.txt", "THIRD-PARTY-NOTICES.txt", "Fonts/InterVariable.ttf", "Fonts/OFL-Inter.txt")},
            "branding": {"source": BRAND_SOURCE, "sha256": hashlib.sha256(brand_bytes).hexdigest(),
                         "resource": "ps-mark.png"},
            "fontManifestSHA256": hashlib.sha256(font_manifest_bytes).hexdigest(),
            "notices": {name: hashlib.sha256(data).hexdigest() for name, data in notice_files.items()},
            "runtimeMaterials": RUNTIME_HASHES,
            "runtimeSources": runtime_source_hashes,
            "helpers": {name: digest(helpers / name) for name in ("PSKeyboardRuntime", "PSKeyboardGuest")},
            "iconBuilderSHA256": icon_builder_hash,
            "buildScriptSHA256": build_script_hash,
            "signature": "stable local self-signed certificate; no restricted entitlement",
            "designatedRequirement": requirement, "installed": False,
            **provenance,
            "sonyCompatibility": "unverified", "sourcePIDFilter": "reject own process only; not a physical-input guarantee",
        }
        if args.archive:
            archive = output / ("PSKeyboard-Mac-" + version["displayVersion"] + "-arm64.zip")
            subprocess.run(["/usr/bin/ditto", "-c", "-k", "--keepParent", str(app), str(archive)], check=True)
            receipt["localArchive"] = {"name": archive.name, "sha256": digest(archive), "size": archive.stat().st_size,
                                       "publicReleaseReady": False}
        if args.install:
            if runtime.is_symlink():
                raise RuntimeError("Refusing symlink app destination")
            if running(runtime_binary):
                raise RuntimeError("Stop the existing app before installing an update")
            # A failed rollback must survive temporary staging cleanup.
            backup = applications / ".PSKeyboardMacDualSense.rollback.app"
            if backup.exists() or backup.is_symlink():
                raise RuntimeError("A previous update backup needs recovery before installing")
            existed = runtime.exists()
            if existed:
                with (runtime / "Contents/Info.plist").open("rb") as stream:
                    if plistlib.load(stream).get("CFBundleIdentifier") != IDENTIFIER:
                        raise RuntimeError("Existing app identity does not match")
                os.replace(runtime, backup)
            installed_new = False
            try:
                os.replace(app, runtime)
                installed_new = True
                subprocess.run(["codesign", "--verify", "--deep", "--strict", str(runtime)], check=True)
            except Exception:
                if installed_new and runtime.exists(): shutil.rmtree(runtime)
                if existed: os.replace(backup, runtime)
                raise
            receipt["installed"] = True
            if existed: shutil.rmtree(backup)
        (output / "BUILD.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
        print(json.dumps({"version": version["displayVersion"], "installed": args.install,
                          "app": str(runtime) if args.install else None}, ensure_ascii=False))


if __name__ == "__main__":
    main()
