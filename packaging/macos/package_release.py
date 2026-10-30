#!/usr/bin/env python3
"""Package a verified Mac build for local review or GitHub release, without uploading.

Does not build, sign, install, launch the App, inspect private state, or mutate Git. A release commit is read with git show.
The source ZIP is a deterministic allowlisted working-tree snapshot, not a claim
that baseCommit alone contains these changes. Existing output is never replaced.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import re
import shutil
import stat
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]
APP = "PSKeyboardMacDualSense.app"
BINARY = "PSKeyboardMacDualSense"
IDENTIFIER = "com.xiunlee.pskeyboard.mac-dualsense"
RUNTIME_SOURCE = ROOT / ".local/mac-runtime-sources-1.tar.gz"
RUNTIME_SOURCE_SIZE = 450_919_582
RUNTIME_SOURCE_HASH = "7f15544b93960b4d31bfd42d7413cbae057af3e81d7ee613f11f74bf19403218"
BUILD_INPUTS_HASH = "35b81b04bcfee79fe0a0a5b17b1cf3ee03a48b2838d6f1c231c2120bd37cb433"
BUILD_INPUTS_SIZE = 115_775_397
LANGUAGES = ("en", "zh-Hant", "zh-Hans", "ja", "ko", "de", "fr", "es", "pt-BR")
PACKAGING_SCRIPTS = ("build_icon.py", "build_strings.py", "collect_sources.py", "prepare_runtime.py",
                     "fetch_runtime_materials.py", "package_release.py", "test_versioning.py")
DOCS = ("MAC-INSTALL.md", "MAC-INSTALL.en.md", "MAC-DEFAULT-KEYS.md", "MAC-PARITY-CHECKLIST.md",
        "MAC-RELEASE-READINESS.md", "MAC-RUNTIME-MATERIALS.md", "MAC-RELEASE-NOTES.md")
FIXED_SOURCE = (
    "LICENSE", "COPYRIGHT.md", "VERSION", "CHANGELOG.md",
    "research/macos-native/build_preview.py", "research/macos-native/local_signing.py",
    "research/macos-native/guest/prepare_guest.py",
    "research/macos-native/guest-backend/build.py", "research/macos-native/guest-backend/main.go",
    "research/macos-native/guest-backend/main_test.go", "research/macos-native/guest-backend/autoattach_darwin.go.overlay",
    "research/macos-native/guest-backend/README.md",
    "src/physical-keyboard/UiText.cs", "research/dualsense-preview/PreviewText.cs",
    "src/macos/ControlCoreTests.swift", "src/macos/DualSenseV5ProtocolTests.swift",
    "src/macos/DualSenseRelaySocketTests.swift", "src/macos/MacProfileStoreTests.swift",
    "src/macos/MacDependencyInstallerTests.swift", "src/macos/test-dualsense-v5.py",
    "src/macos/runtime/RuntimeTests.swift", "src/macos/runtime/run-tests.py", "src/macos/runtime/README.md",
    # Cross-platform protocol test inputs used by test-dualsense-v5.py.
    "research/native-merge/keyboard_merge.c", "research/native-merge/keyboard_merge.h",
    "src/keyboard-pad/KeyboardMapping.cs", "src/keyboard-pad/ViiperTransport.cs",
    "research/dualsense-v5-test/V5Protocol.cs", "research/dualsense-v5-test/V5ProtocolTests.cs",
    "assets/branding/three-cats-mark.png", "assets/branding/three-cats.ico",
    "assets/branding/Build-ThreeCatsMark.ps1", "assets/macos/strings.json", "packaging/macos/version.json",
    "assets/macos/fonts/InterVariable.ttf", "assets/macos/fonts/OFL-Inter.txt", "assets/macos/fonts/SOURCES.json",
)


def sha256(path: Path) -> str:
    if path.is_symlink() or not path.is_file():
        raise ValueError("Expected a regular file: " + path.name)
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1_048_576), b""): result.update(block)
    return result.hexdigest()


def verify(path: Path, expected: str, size: int | None = None) -> None:
    if not re.fullmatch(r"[0-9a-f]{64}", expected): raise ValueError("Invalid SHA-256")
    if size is not None and path.stat().st_size != size: raise ValueError("Size mismatch: " + path.name)
    if sha256(path) != expected: raise ValueError("SHA-256 mismatch: " + path.name)


def safe_relative(value: str) -> PurePosixPath:
    if not isinstance(value, str) or not value or "\\" in value or ":" in value or any(ord(c) < 32 for c in value):
        raise ValueError("Unsafe archive path")
    path = PurePosixPath(value.rstrip("/"))
    if path.is_absolute() or any(part in ("", ".", "..") for part in value.rstrip("/").split("/")):
        raise ValueError("Unsafe archive path")
    return path


def basename(value: str) -> str:
    path = safe_relative(value)
    if len(path.parts) != 1: raise ValueError("Expected a basename")
    return value


def check_zip(path: Path, root_name: str) -> None:
    """Validate paths/types before using ditto to restore signed bundle metadata."""
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        if not entries or len(entries) > 20_000 or sum(i.file_size for i in entries) > 1_073_741_824:
            raise ValueError("Unexpected App ZIP size")
        seen = set()
        for entry in entries:
            relative = safe_relative(entry.filename)
            if relative.parts[0] not in (root_name, "__MACOSX"):
                raise ValueError("Unexpected ZIP root")
            if relative.parts[0] == "__MACOSX":
                if len(relative.parts) == 1:
                    if not entry.is_dir(): raise ValueError("Unexpected metadata root")
                elif relative.parts[1] != root_name: raise ValueError("Unexpected metadata root")
            key = str(relative).casefold()
            if key in seen: raise ValueError("Duplicate or case-colliding ZIP path")
            seen.add(key)
            mode = stat.S_IFMT(entry.external_attr >> 16)
            if mode not in (0, stat.S_IFREG, stat.S_IFDIR): raise ValueError("ZIP links/devices are not allowed")
            if entry.flag_bits & 1: raise ValueError("Encrypted ZIP is not allowed")
        if archive.testzip() is not None: raise ValueError("ZIP CRC validation failed")


def command(arguments: list[str], timeout: int = 120) -> None:
    subprocess.run(arguments, check=True, timeout=timeout, stdout=subprocess.DEVNULL)


def validate_source_version(manifest_bytes: bytes, build: dict) -> None:
    mac_version = json.loads(manifest_bytes)
    if (mac_version["shortVersion"] != build["version"] or
            mac_version["displayVersion"] != build["version"] or
            mac_version["buildNumber"] != build["buildNumber"]):
        raise ValueError("Mac version manifest mismatch")


def source_snapshot(build: dict) -> tuple[dict[str, bytes], dict[str, str]]:
    selected = set(FIXED_SOURCE) | {"docs/" + name for name in DOCS}
    for name in build["sources"]: selected.add("src/macos/" + basename(name))
    for name in build["runtimeSources"]: selected.add("src/macos/runtime/" + basename(name))
    selected.update("packaging/macos/" + name for name in PACKAGING_SCRIPTS)
    content, hashes = {}, {}
    for relative in sorted(selected):
        safe_relative(relative)
        path = ROOT / relative
        if path.is_symlink() or not path.is_file() or path.resolve() != path.absolute():
            raise ValueError("Missing/linked source: " + relative)
        data = path.read_bytes()
        content[relative] = data; hashes[relative] = hashlib.sha256(data).hexdigest()
    expected = {"src/macos/" + name: value for name, value in build["sources"].items()}
    expected.update({"src/macos/runtime/" + name: value for name, value in build["runtimeSources"].items()})
    expected.update({
        "packaging/macos/version.json": build["versionManifestSHA256"],
        "packaging/macos/build_icon.py": build["iconBuilderSHA256"],
        "research/macos-native/build_preview.py": build["buildScriptSHA256"],
        "assets/branding/three-cats-mark.png": build["resources"]["ps-mark.png"],
        "assets/macos/strings.json": build["resources"]["strings.json"],
        "LICENSE": build["resources"]["LICENSE.txt"],
        "assets/macos/fonts/InterVariable.ttf": build["resources"]["Fonts/InterVariable.ttf"],
        "assets/macos/fonts/OFL-Inter.txt": build["resources"]["Fonts/OFL-Inter.txt"],
        "assets/macos/fonts/SOURCES.json": build["fontManifestSHA256"],
    })
    strings = json.loads(content["assets/macos/strings.json"])
    expected.update(strings["sourceSHA256"])
    for relative, digest in expected.items():
        if hashes.get(relative) != digest: raise ValueError("Source differs from build: " + relative)
    validate_source_version(content["packaging/macos/version.json"], build)
    command(["python3", str(ROOT / "packaging/macos/build_strings.py"), "--check"])
    return content, hashes


def verify_app(app: Path, build: dict) -> dict[str, str]:
    command(["/usr/bin/codesign", "--verify", "--deep", "--strict", str(app)])
    info = plistlib.loads((app / "Contents/Info.plist").read_bytes())
    expected_info = {"CFBundleIdentifier": IDENTIFIER, "CFBundleExecutable": BINARY,
                     "CFBundleShortVersionString": build["version"], "CFBundleVersion": build["buildNumber"],
                     "LSMinimumSystemVersion": "15.0"}
    if any(info.get(key) != value for key, value in expected_info.items()): raise ValueError("App identity/version mismatch")
    expected = {"Contents/MacOS/" + BINARY: build["binarySHA256"]}
    for field, prefix in (("resources", "Contents/Resources/"), ("helpers", "Contents/Helpers/"),
                          ("runtimeMaterials", "Contents/Resources/Runtime/"), ("notices", "Contents/Resources/Notices/")):
        if not build.get(field): raise ValueError("Missing build material manifest: " + field)
        for name, digest in build[field].items(): expected[prefix + str(safe_relative(name))] = digest
    for relative, digest in expected.items(): verify(app / relative, digest)
    allowed = set(expected) | {"Contents/Info.plist", "Contents/_CodeSignature/CodeResources"}
    allowed.update("Contents/Resources/" + language + ".lproj/InfoPlist.strings" for language in LANGUAGES)
    actual = {}
    for path in app.rglob("*"):
        if path.is_symlink(): raise ValueError("App contains a symlink")
        if path.is_dir(): continue
        relative = path.relative_to(app).as_posix()
        if relative not in allowed: raise ValueError("Unexpected App file: " + relative)
        actual[relative] = sha256(path)
    if set(actual) != allowed: raise ValueError("App allowlist is incomplete")
    # Only fixed resources/helpers are accepted, so private signing/runtime state,
    # vendor binaries and the large corresponding-source tar cannot enter App.
    return dict(sorted(actual.items()))


def write_source_zip(path: Path, content: dict[str, bytes], manifest: dict, version: str) -> None:
    generated = {
        "SOURCE-MANIFEST.json": (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode(),
        "README-SOURCE.txt": (
            "Three Cats Keybridge — Mac " + version + " source\n\n"
            "The exact source revision, when provided, is sourceCommit in SOURCE-MANIFEST.json.\n"
            "buildBaseCommit records build ancestry; exact build inputs are recorded by their hashes.\n"
            "Exact per-file hashes are in SOURCE-MANIFEST.json. No Git metadata or private signing material is included.\n"
            "Read docs/MAC-RUNTIME-MATERIALS.md for pinned guest/backend sources and rebuild inputs.\n"
            "Use the matching runtime-sources tar alongside this archive. VirtualHere is obtained from its publisher.\n"
            "research/macos-native/local_signing.py is developer source; end users do not run signing setup.\n"
            "Own source: GPL-3.0-only. Third-party sources retain their respective notices and licenses.\n"
        ).encode(),
    }
    with zipfile.ZipFile(path, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for relative, data in sorted({**content, **generated}.items()):
            entry = zipfile.ZipInfo("ThreeCatsKeybridge-Mac-" + version + "-source/" + relative, date_time=(1980, 1, 1, 0, 0, 0))
            entry.create_system = 3; entry.external_attr = (stat.S_IFREG | 0o644) << 16
            entry.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(entry, data, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)


def readme(version: str, release_commit: str | None = None) -> str:
    result = f"""上班不要玩 / Three Cats Keybridge — Mac {version}
本機整合候選包 / Local integrated candidate — NOT PUBLISHED

鍵盤控制與模擬手把工具。需要 Apple Silicon、macOS 15 以上，以及另行設定好的 Sony 官方 PS Remote Play。
獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係。
將完整 {APP} 複製至 ~/Applications；若資料夾不存在，請先在自己的使用者資料夾建立 Applications。
更新前按「中斷」，等待清理完成並正常結束舊版，再替換同一位置。保留個人設定與 RuntimeState。
詳細安裝、使用、更新與移除說明：docs/MAC-INSTALL.md。

首次連接由 App 從 VirtualHere 官方取得固定版本的原始元件；免費單裝置模式，不安裝為 service。
使用者不需安裝 Python、Go、Swift、Docker 或 Parallels。請在 macOS 允許此 App 的輔助使用權限。
這是專案自簽候選，不是 Developer ID 簽章、未經 Apple 公證。macOS 可能阻擋首次開啟；
若系統提供「仍要打開」，由本人依系統流程確認。不要求匯入作者憑證或降低全域安全設定。

本資料夾建立不代表 GitHub Release 已發佈、全新 Mac 安裝已驗收，或每款遊戲均相容。
App ZIP 與 DMG 使用同一份已驗簽 App。App 內附授權與 notices；完整來源在同版 source ZIP
與 runtime-sources.tar.gz，另有 SHA256SUMS 與 RELEASE.json。來源包不需安裝到 Applications。

All features are free. Donations are optional. This is a local candidate, not a published release.
Copy the complete App to ~/Applications. Quit and wait for cleanup before replacing it.
Keep ~/Library/Application Support/PSKeyboard when updating. No developer toolchain is required.
Full installation, use, update, and removal guide: docs/MAC-INSTALL.en.md.
This self-signed App is not Developer ID signed or notarized; first launch on a fresh Mac remains unverified.
VirtualHere components are downloaded separately from their publisher; their original licenses apply.
"""
    if release_commit:
        result = result.replace("本機整合候選包 / Local integrated candidate — NOT PUBLISHED", "Mac 發行包 / Mac release package")
        result = result.replace("這是專案自簽候選", "這是專案自簽 App")
        result = result.replace("本資料夾建立不代表 GitHub Release 已發佈、全新 Mac 安裝已驗收，或每款遊戲均相容。", "全新 Mac 安裝仍待驗證；相容性依電腦與遊戲而異。發布時間以 GitHub 發布頁為準。")
        result = result.replace("All features are free. Donations are optional. This is a local candidate, not a published release.",
                                "Keyboard control and virtual controller tools for compatible Remote Play environments.\n"
                                "All features are free. Donations are optional. Independent project; not an official Sony product, affiliated with or endorsed by Sony.\n"
                                "See GitHub for the release date and availability.")
        result += f"\nGitHub: https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/mac-v{version}\nSource commit: {release_commit}\n"
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True, help="New local candidate folder directly under .local")
    parser.add_argument("--source-state", choices=("dirty", "unknown"), default="dirty",
                        help="Explicit snapshot status; no Git operation is performed")
    parser.add_argument("--release-commit", help="Exact Git commit whose source files must match this release package")
    parser.add_argument("--build-inputs", type=Path, help="Optional separately supplied pinned developer build-input tar")
    parser.add_argument("--build-inputs-sha256", help="Required exact SHA-256 for --build-inputs")
    args = parser.parse_args()
    output = args.output.absolute()
    local = ROOT / ".local"
    if output.parent.resolve() != local.resolve() or output.exists() or output.is_symlink():
        parser.error("Output must be a new directory directly under .local")
    build_dir = args.build_dir.absolute()
    if build_dir.is_symlink() or build_dir.parent.resolve() != local.resolve(): parser.error("Build directory must be directly under .local")
    receipt_path = build_dir / "BUILD.json"
    build = json.loads(receipt_path.read_text())
    version = build["version"]
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version) or build["bundleIdentifier"] != IDENTIFIER:
        raise ValueError("Unexpected version or bundle identity")
    if build["architecture"] != "arm64" or build["minimumMacOS"] != "15.0": raise ValueError("Unexpected platform")
    if not re.fullmatch(r"[0-9a-f]{40}", build["baseCommit"]): raise ValueError("Invalid build base commit")
    archive_info = build["localArchive"]
    archive = build_dir / basename(archive_info["name"])
    verify(archive, archive_info["sha256"], archive_info["size"])
    check_zip(archive, APP)
    verify(RUNTIME_SOURCE, RUNTIME_SOURCE_HASH, RUNTIME_SOURCE_SIZE)
    if bool(args.build_inputs) != bool(args.build_inputs_sha256): parser.error("Provide both build-input arguments")
    if args.build_inputs:
        if args.build_inputs_sha256 != BUILD_INPUTS_HASH: raise ValueError("Unapproved developer build-input archive")
        verify(args.build_inputs, BUILD_INPUTS_HASH, BUILD_INPUTS_SIZE)
    content, hashes = source_snapshot(build)
    if args.release_commit:
        if not re.fullmatch(r"[0-9a-f]{40}", args.release_commit): raise ValueError("Invalid release commit")
        for relative, digest in hashes.items():
            committed = subprocess.check_output(["git", "show", args.release_commit + ":" + relative], cwd=ROOT)
            if hashlib.sha256(committed).hexdigest() != digest:
                raise ValueError("Release commit source differs: " + relative)
    source_manifest = {
        "schemaVersion": 1, "version": version, "buildBaseCommit": build["baseCommit"],
        "sourceState": args.source_state + "-working-tree-snapshot", "gitInspected": False,
        "buildReceiptSHA256": sha256(receipt_path), "files": hashes,
    }
    if args.release_commit:
        source_manifest.update(sourceCommit=args.release_commit, sourceState="git-commit", gitInspected=True)
    applications = Path.home() / "Applications"
    applications.mkdir(exist_ok=True)
    # Signed bundle staging stays outside the Documents/FileProvider tree.
    with tempfile.TemporaryDirectory(prefix=".pskeyboard-package-", dir=applications) as temporary:
        staging = Path(temporary)
        extracted = staging / "extracted"; extracted.mkdir()
        command(["/usr/bin/ditto", "-x", "-k", str(archive), str(extracted)])
        app = extracted / APP
        app_hashes = verify_app(app, build)
        payload = staging / ("ThreeCatsKeybridge-Mac-" + version); payload.mkdir()
        os.replace(app, payload / APP)
        (payload / "README.txt").write_text(readme(version, args.release_commit))
        (payload / "docs").mkdir()
        for name in DOCS: (payload / "docs" / name).write_bytes(content["docs/" + name])
        with tempfile.TemporaryDirectory(prefix=".mac-release-", dir=local) as pending:
            candidate = Path(pending)
            app_zip = candidate / ("ThreeCatsKeybridge-Mac-" + version + "-arm64.zip")
            command(["/usr/bin/ditto", "-c", "-k", "--keepParent", str(payload), str(app_zip)])
            check_zip(app_zip, payload.name)
            zip_check = staging / "zip-check"; zip_check.mkdir()
            command(["/usr/bin/ditto", "-x", "-k", str(app_zip), str(zip_check)])
            if verify_app(zip_check / payload.name / APP, build) != app_hashes: raise ValueError("ZIP App content changed")
            dmg = candidate / ("ThreeCatsKeybridge-Mac-" + version + "-arm64.dmg")
            command(["/usr/bin/hdiutil", "create", "-volname", "Three Cats Keybridge " + version,
                     "-srcfolder", str(payload), "-format", "UDZO", "-fs", "HFS+", str(dmg)], timeout=300)
            command(["/usr/bin/hdiutil", "verify", str(dmg)], timeout=180)
            mounted = staging / "dmg-check"; mounted.mkdir()
            command(["/usr/bin/hdiutil", "attach", "-readonly", "-nobrowse", "-mountpoint",
                     str(mounted), str(dmg)], timeout=120)
            try:
                if verify_app(mounted / APP, build) != app_hashes:
                    raise ValueError("Mounted DMG App content changed")
            finally:
                command(["/usr/bin/hdiutil", "detach", str(mounted)], timeout=120)
            project_zip = candidate / ("ThreeCatsKeybridge-Mac-" + version + "-source.zip")
            write_source_zip(project_zip, content, source_manifest, version)
            check_zip(project_zip, "ThreeCatsKeybridge-Mac-" + version + "-source")
            runtime_tar = candidate / ("ThreeCatsKeybridge-Mac-" + version + "-runtime-sources.tar.gz")
            shutil.copyfile(RUNTIME_SOURCE, runtime_tar)
            verify(runtime_tar, RUNTIME_SOURCE_HASH, RUNTIME_SOURCE_SIZE)
            if args.build_inputs:
                build_inputs = candidate / ("ThreeCatsKeybridge-Mac-" + version + "-build-inputs.tar.gz")
                shutil.copyfile(args.build_inputs, build_inputs)
                verify(build_inputs, args.build_inputs_sha256)
            (candidate / "README.txt").write_text(readme(version, args.release_commit))
            shutil.copytree(payload / "docs", candidate / "docs")
            artifacts = {p.relative_to(candidate).as_posix(): {"sha256": sha256(p), "size": p.stat().st_size}
                         for p in sorted(candidate.rglob("*")) if p.is_file()}
            release = {
                "schemaVersion": 1, "version": version, "buildNumber": build["buildNumber"],
                "createdAtUTC": datetime.now(timezone.utc).isoformat(), "status": "local-candidate",
                "published": False, "publicReleaseReady": False, "architecture": "arm64", "minimumMacOS": "15.0",
                "bundleIdentifier": IDENTIFIER, "installLocation": "~/Applications/" + APP,
                "signature": "project self-signed", "developerIDSigned": False, "notarized": False,
                "freshMacInstallVerified": False, "sourceSnapshot": source_manifest,
                "inputBuildArchive": {key: archive_info[key] for key in ("name", "sha256", "size")},
                "appFiles": app_hashes, "artifacts": artifacts,
                "dmgVerification": "hdiutil verify; read-only mounted App signatures and all payload hashes verified",
                "vendorBinariesBundled": False, "privateSigningOrRuntimeStateIncluded": False,
                "publicationActionsPerformed": False,
            }
            if args.release_commit:
                release.update(status="release-package", sourceCommit=args.release_commit,
                               releaseURL="https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/mac-v" + version,
                               publicationStatusSource="GitHub release; this manifest records package creation")
                del release["published"]
                del release["publicReleaseReady"]
            (candidate / "RELEASE.json").write_text(json.dumps(release, ensure_ascii=False, indent=2, sort_keys=True) + "\n")
            checksums = {**{name: item["sha256"] for name, item in artifacts.items()}, "RELEASE.json": sha256(candidate / "RELEASE.json")}
            (candidate / "SHA256SUMS").write_text("".join(value + "  " + name + "\n" for name, value in sorted(checksums.items())))
            # A changed source/doc/receipt cannot silently produce mismatched
            # package provenance. Do not read or export Git status or private data.
            for relative, expected in hashes.items(): verify(ROOT / relative, expected)
            verify(receipt_path, source_manifest["buildReceiptSHA256"])
            if output.exists() or output.is_symlink(): raise ValueError("Output appeared during packaging")
            os.rename(candidate, output)
    print(json.dumps({"version": version, "status": "release-package" if args.release_commit else "local-candidate", "uploaded": False,
                      "output": str(output), "artifacts": len(artifacts)}, ensure_ascii=False))


if __name__ == "__main__": main()
