#!/usr/bin/env python3
"""Offline regression: Windows updates must not relabel or block Mac builds.

Only temporary manifests are written. No signing, App install or runtime start.
"""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]


def load_module(name, relative):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


builder = load_module("mac_preview_builder", "research/macos-native/build_preview.py")
packager = load_module("mac_release_packager", "packaging/macos/package_release.py")


class IndependentPlatformVersions(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="threecats-version-tests-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.manifest = self.root / "packaging/macos/version.json"
        self.manifest.parent.mkdir(parents=True)
        self.original = json.loads((ROOT / "packaging/macos/version.json").read_text())
        self.manifest.write_text(json.dumps(self.original))

    def test_windows_advance_keeps_mac_version_and_identity(self):
        for windows_version in ("0.1.4", "0.1.5", "0.2.0"):
            with self.subTest(windows_version=windows_version):
                (self.root / "VERSION").write_text(windows_version + "\n")
                path, version, digest, windows_bytes = builder.read_version(self.root)
                self.assertEqual(version, self.original)
                self.assertEqual(path, self.manifest)
                self.assertEqual(digest, builder.digest(self.manifest))
                self.assertEqual(windows_bytes, (windows_version + "\n").encode())
                packager.validate_source_version(self.manifest.read_bytes(), {
                    "version": version["shortVersion"], "buildNumber": version["buildNumber"]})

    def test_invalid_mac_version_or_identity_is_still_rejected(self):
        (self.root / "VERSION").write_text("0.1.4\n")
        for field, value in (("displayVersion", "9.9.9"), ("buildNumber", "0"),
                             ("bundleIdentifier", "unrelated.app"), ("appDirectory", "Other.app")):
            with self.subTest(field=field):
                self.manifest.write_text(json.dumps({**self.original, field: value}))
                with self.assertRaises(ValueError):
                    builder.read_version(self.root)

    def test_packager_rejects_different_mac_build(self):
        for receipt in ({"version": "9.9.9", "buildNumber": self.original["buildNumber"]},
                        {"version": self.original["shortVersion"], "buildNumber": "999"}):
            with self.subTest(receipt=receipt), self.assertRaises(ValueError):
                packager.validate_source_version(self.manifest.read_bytes(), receipt)


if __name__ == "__main__":
    unittest.main()
