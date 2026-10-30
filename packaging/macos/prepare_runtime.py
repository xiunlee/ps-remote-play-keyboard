#!/usr/bin/env python3
"""Reproduce the tested kernel/initrd from pinned local inputs, without booting.

Never downloads, starts a VM, executes an ELF file, or reads guest identities.
The destination must not exist. Only the explicit binary input allowlist is
copied; share/, logs and per-install identity material are never read.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys

INPUT_SHA256 = {
    "APKINDEX-community.tar.gz": "9a85631b579b4da6f2cc9ec880211d11437fb3862662eb85326b52a424f730cc",
    "APKINDEX-main.tar.gz": "31e5b83b9205704156674d9594afac100ba8822658c45e0404c8b206c84e38a5",
    "alpine-minirootfs-3.22.7-aarch64.tar.gz": "7e9b34ff3304381909e566e2c93de450664ccc724191261423974fc121c11623",
    "eudev-libs-3.2.14-r5.apk": "395c832a65081563b82cd52776afe16ac3de483c776753bae376280caf8bb0cd",
    "hwdata-usb-0.395-r0.apk": "803866c2bf218a9862c07be9f12a185f9a8c860e87eb0eb3c81c51f2ef87c094",
    "libcrypto3-3.5.9-r0.apk": "ede2ef7eca3b65a42e902aaf7890f9c5647dca0c55123a817179ad9f3bc6ef24",
    "libncursesw-6.5_p20250503-r0.apk": "419b375e8a4345e7172b1f0f3a3c57db61374f5408cdb875d9e860bd4c243aca",
    "libssl3-3.5.9-r0.apk": "338c9f6154582b4b76455a59f95538bcea8df039c5c72916dd2d8d0d844c9583",
    "libusb-1.0.28-r0.apk": "aa86b69e30a127fe0662a00a15e9eecaa0056aae08a208b0d1e105d62dbab2e5",
    "linux-lts-6.12.114-r0.apk": "183ee1eec6913ff6c150b9ca08024ba1edc189c4da08bf6f1b68b86c7cb34242",
    "linux-tools-usbip-6.15.0-r0.apk": "c43931bcdea215d8061c3a5f031610f080ef38bc2ca8be69ed981ed14162b4e6",
    "ncurses-terminfo-base-6.5_p20250503-r0.apk": "3d37403e0b5ab9eb0c1ce269444e4a385faec9fe6af452c1c6956806b13d2bd6",
    "readline-8.2.13-r1.apk": "334af29dbf6b5a71a87af4d6a58e2967a8f711a51d00093de0e1498daf83ceb2",
    "socat-1.8.1.3-r0.apk": "6b9c824b429e2727af62e4433de3979e7985fd7aea9c26f1f7f85aab0fbbf5aa",
    "usbutils-018-r0.apk": "748ba2822da48f0defc3e63fc7e3e4b7be90535aa14dbe7d36e650bc599b7bcd",
}
OUTPUT_SHA256 = {
    "Image": "d6763ecc91a9b614898148642572f7f10b44377b888185816a63a7a2c76c511b",
    "initrd.gz": "1356a076d56df8d0596e5a7ccebaba12d6d139a4e373536667c2777f2eeda97f",
}
TESTED_MODULES = ["virtio_pci", "virtio_mmio", "virtio_console", "vhci_hcd",
                  "vmw_vsock_virtio_transport", "virtiofs"]


def verify(path, expected):
    if path.is_symlink() or not path.is_file():
        raise ValueError("Missing or non-regular pinned input: " + path.name)
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != expected:
        raise ValueError("SHA256 mismatch: " + path.name)


def prepare(cache, output):
    cache = cache.resolve(strict=True)
    # Reject an existing destination, including dangling symlinks. Never modify
    # the tested cache or reuse an old staging directory after a partial failure.
    if output.exists() or output.is_symlink():
        raise ValueError("Output must be a new directory")
    output = output.absolute()
    for filename, expected in INPUT_SHA256.items():
        verify(cache / filename, expected)
    output.mkdir(parents=True, exist_ok=False)
    for filename in INPUT_SHA256:
        shutil.copyfile(cache / filename, output / filename)

    repository = Path(__file__).resolve().parents[2]
    recipe = repository / "research/macos-native/guest/prepare_guest.py"
    spec = importlib.util.spec_from_file_location("pskeyboard_guest_recipe", recipe)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.MODULES = TESTED_MODULES.copy()

    def pinned_fetch(directory, name, url):
        # Prevent upstream recipe changes from adding an unpinned download.
        if name not in INPUT_SHA256:
            raise ValueError("Unpinned recipe input: " + name)
        path = directory / name
        verify(path, INPUT_SHA256[name])
        return path.read_bytes()

    module.fetch = pinned_fetch
    previous_argv = sys.argv
    try:
        sys.argv = [str(recipe), str(output)]
        module.main()
    finally:
        sys.argv = previous_argv
    for filename, expected in OUTPUT_SHA256.items():
        verify(output / filename, expected)
    manifest_path = output / "guest-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    manifest["boot"]["memory_mib"] = 768
    manifest["reproduction"] = {
        "recipe": "packaging/macos/prepare_runtime.py",
        "offline": True,
        "image_and_initrd_match_tested_bytes": True,
        "early_dummy_or_af_packet": False,
    }
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    print("Pinned Image/initrd reproduced; no guest code executed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path, required=True, help="Pinned APK/rootfs cache directory")
    parser.add_argument("--output", type=Path, required=True, help="New output directory")
    args = parser.parse_args()
    prepare(args.cache, args.output)


if __name__ == "__main__":
    main()
