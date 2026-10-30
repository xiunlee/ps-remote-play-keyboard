#!/usr/bin/env python3
"""Prepare a diskless, networkless arm64 guest; never boot or execute guest code.

Uses Alpine's official minirootfs checksum and APK RSA signatures/data hashes.
Only the supplied output directory is written. Downloaded ELF files are data.
"""
import base64
import gzip
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import stat
import struct
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import zlib

ROOTFS = "alpine-minirootfs-3.22.7-aarch64.tar.gz"
ROOTFS_SHA256 = "7e9b34ff3304381909e566e2c93de450664ccc724191261423974fc121c11623"
KERNEL = "linux-lts"
KERNEL_VERSION = "6.12.114-r0"
KERNEL_RELEASE = "6.12.114-0-lts"
BASE = "https://dl-cdn.alpinelinux.org/alpine/v3.22"
PACKAGES = ["linux-tools-usbip", "eudev-libs", "socat", "libcrypto3", "libssl3",
            "readline", "libncursesw", "ncurses-terminfo-base", "usbutils",
            "libusb", "hwdata-usb"]
MODULES = ["virtio_pci", "virtio_mmio", "virtio_console", "vhci_hcd",
           "vmw_vsock_virtio_transport", "virtiofs", "dummy", "af_packet"]


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def fetch(output, name, url):
    path = output / name
    if not path.exists():
        partial = output / (name + ".part")
        with urllib.request.urlopen(url, timeout=120) as response:
            with partial.open("wb") as stream:
                while True:
                    chunk = response.read(1024 * 1024)
                    if not chunk:
                        break
                    stream.write(chunk)
        partial.replace(path)
    return path.read_bytes()


def gzip_members(raw):
    members = []
    while raw:
        decoder = zlib.decompressobj(31)
        decoded = decoder.decompress(raw) + decoder.flush()
        if not decoder.eof:
            raise ValueError("Incomplete gzip member")
        used = len(raw) - len(decoder.unused_data)
        if used == 0:
            raise ValueError("Empty gzip progress")
        members.append((raw[:used], decoded))
        raw = decoder.unused_data
    return members


def tar_members(raw):
    with tarfile.open(fileobj=io.BytesIO(raw), mode="r:") as archive:
        return [(entry, archive.extractfile(entry).read() if entry.isfile() else b"")
                for entry in archive.getmembers()]


def verify_signature(signature_tar, signed, keys, temp):
    signatures = [(entry, data) for entry, data in tar_members(signature_tar)
                  if entry.name.startswith(".SIGN.")]
    if len(signatures) != 1:
        raise ValueError("Expected one APK signature")
    entry, signature = signatures[0]
    _, _, algorithm, keyname = entry.name.split(".", 3)
    if algorithm not in ("RSA", "RSA256") or keyname not in keys:
        raise ValueError("Untrusted APK signing key or algorithm")
    key = temp / "key.pem"
    sig = temp / "signature"
    payload = temp / "signed"
    key.write_bytes(keys[keyname])
    sig.write_bytes(signature)
    payload.write_bytes(signed)
    subprocess.run(["/usr/bin/openssl", "dgst", "-sha1" if algorithm == "RSA" else "-sha256",
                    "-verify", str(key), "-signature", str(sig), str(payload)],
                   check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    return keyname


def parse_index(raw, keys, temp):
    streams = gzip_members(raw)
    if len(streams) != 2:
        raise ValueError("Expected signed APKINDEX")
    signer = verify_signature(streams[0][1], streams[1][0], keys, temp)
    contents = dict((entry.name, data) for entry, data in tar_members(streams[1][1]))
    packages = []
    for block in contents["APKINDEX"].decode().split("\n\n"):
        fields = dict(line.split(":", 1) for line in block.splitlines() if ":" in line)
        if fields:
            packages.append(fields)
    return packages, signer


def verify_apk(raw, index, keys, temp):
    streams = gzip_members(raw)
    if len(streams) != 3:
        raise ValueError("Expected APK v2 signature/control/data streams")
    signer = verify_signature(streams[0][1], streams[1][0], keys, temp)
    checksum = "Q1" + base64.b64encode(hashlib.sha1(streams[1][0]).digest()).decode()
    if checksum != index["C"]:
        raise ValueError("APK control checksum differs from signed repository index")
    control = dict((entry.name, data) for entry, data in tar_members(streams[1][1]))
    pkginfo = dict(line.split(" = ", 1) for line in control[".PKGINFO"].decode().splitlines()
                   if " = " in line)
    if sha256(streams[2][0]) != pkginfo["datahash"]:
        raise ValueError("APK payload SHA256 differs from signed control datahash")
    return tar_members(streams[2][1]), {"signer": signer, "index_control_checksum": checksum,
                                      "payload_sha256": pkginfo["datahash"], "sha256": sha256(raw)}


def add_entries(entries, members):
    for entry, data in members:
        path = PurePosixPath(entry.name)
        if path.is_absolute() or ".." in path.parts:
            raise ValueError("Unsafe archive entry")
        name = str(path)
        if name in (".", "") or name.startswith(".SIGN") or name.startswith(".PKG"):
            continue
        if entry.isdir():
            entries[name] = (stat.S_IFDIR | entry.mode, b"")
        elif entry.issym():
            entries[name] = (stat.S_IFLNK | entry.mode, entry.linkname.encode())
        elif entry.isfile():
            entries[name] = (stat.S_IFREG | entry.mode, data)
        elif entry.islnk():
            source = str(PurePosixPath(entry.linkname))
            if source not in entries or stat.S_IFMT(entries[source][0]) != stat.S_IFREG:
                raise ValueError("Unsupported hardlink ordering")
            entries[name] = entries[source]
        else:
            raise ValueError("Unexpected archive entry type")


def cpio(entries):
    output = bytearray()
    # newc archive, all files root-owned; no host ownership or timestamps copied.
    for ino, (name, (mode, data)) in enumerate(list(sorted(entries.items())) +
                                              [("TRAILER!!!", (0, b""))], 1):
        encoded = name.encode() + b"\0"
        fields = [ino, mode, 0, 0, 2 if stat.S_ISDIR(mode) else 1, 0, len(data),
                  0, 0, 0, 0, len(encoded), 0]
        header = b"070701" + b"".join(f"{value:08x}".encode() for value in fields)
        output.extend(header + encoded)
        output.extend(b"\0" * (-len(output) % 4))
        output.extend(data)
        output.extend(b"\0" * (-len(output) % 4))
    return bytes(output)


def main():
    if len(sys.argv) != 2:
        raise SystemExit("usage: prepare_guest.py OUTPUT_DIRECTORY")
    output = Path(sys.argv[1]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    root_url = BASE + "/releases/aarch64/" + ROOTFS
    root_raw = fetch(output, ROOTFS, root_url)
    if sha256(root_raw) != ROOTFS_SHA256:
        raise ValueError("Rootfs checksum mismatch")
    root_members = tar_members(gzip.decompress(root_raw))
    keys = {PurePosixPath(entry.name).name: data for entry, data in root_members
            if entry.isfile() and "etc/apk/keys/" in entry.name}
    entries = {}
    add_entries(entries, root_members)
    manifest = {"rootfs": {"url": root_url, "sha256": ROOTFS_SHA256},
                "architecture": "aarch64", "kernel_release": KERNEL_RELEASE,
                "packages": [], "indexes": [], "executed_guest_code": False}
    with tempfile.TemporaryDirectory(prefix="verify-", dir=output) as temp_path:
        temp = Path(temp_path)
        indexes = {}
        for repo in ("main", "community"):
            url = BASE + "/" + repo + "/aarch64/APKINDEX.tar.gz"
            raw = fetch(output, "APKINDEX-" + repo + ".tar.gz", url)
            packages, signer = parse_index(raw, keys, temp)
            manifest["indexes"].append({"url": url, "sha256": sha256(raw), "signer": signer})
            for package in packages:
                indexes[package["P"]] = (repo, package)
        kernel_members = None
        for name in [KERNEL] + PACKAGES:
            repo, index = indexes[name]
            if name == KERNEL and index["V"] != KERNEL_VERSION:
                raise ValueError("Kernel repository version changed; explicitly review pin")
            filename = name + "-" + index["V"] + ".apk"
            url = BASE + "/" + repo + "/aarch64/" + filename
            raw = fetch(output, filename, url)
            members, verified = verify_apk(raw, index, keys, temp)
            manifest["packages"].append({"name": name, "version": index["V"], "url": url, **verified})
            print("Verified", filename, flush=True)
            if name == KERNEL:
                kernel_members = dict((entry.name, (entry, data)) for entry, data in members)
            else:
                add_entries(entries, members)

    kernel = kernel_members["boot/vmlinuz-lts"][1]
    if kernel[4:8] != b"zimg" or kernel[24:29] != b"gzip\0":
        raise ValueError("Unexpected EFI zboot layout/compression")
    offset, size = struct.unpack_from("<II", kernel, 8)
    image = gzip.decompress(kernel[offset:offset+size])
    if image[56:60] != b"ARM\x64":
        raise ValueError("Not an arm64 Linux Image")
    (output / "Image").write_bytes(image)
    manifest["Image"] = {"sha256": sha256(image), "size": len(image), "source": "signed APK EFI zboot gzip payload"}

    prefix = "lib/modules/" + KERNEL_RELEASE + "/"
    dependencies = {}
    for line in kernel_members[prefix + "modules.dep"][1].decode().splitlines():
        name, dep = line.split(":", 1)
        dependencies[name] = dep.split()
    def module_name(path):
        return PurePosixPath(path).name.removesuffix(".ko.gz").replace("-", "_")
    by_name = {module_name(path): path for path in dependencies}
    ordered = []
    def select(path):
        if path in ordered:
            return
        for dep in dependencies[path]:
            select(dep)
        ordered.append(path)
    for name in MODULES:
        select(by_name[name])
    for path in ordered:
        destination = prefix + path.removesuffix(".gz")
        entries[destination] = (stat.S_IFREG | 0o644, gzip.decompress(kernel_members[prefix+path][1]))
    dep_text = "\n".join(path.removesuffix(".gz") + ": " +
                         " ".join(dep.removesuffix(".gz") for dep in dependencies[path]) for path in ordered) + "\n"
    entries[prefix + "modules.dep"] = (stat.S_IFREG | 0o644, dep_text.encode())
    # Supply parent directories of files added without using filesystem extraction.
    for name in list(entries):
        for parent in PurePosixPath(name).parents:
            if str(parent) != ".":
                entries.setdefault(str(parent), (stat.S_IFDIR | 0o755, b""))
    early = [path for path in ordered if module_name(path) != "vhci_hcd"]
    # vhci_hcd is loaded last; it only creates a guest controller, never a host device.
    early.append(by_name["vhci_hcd"])
    loads = "\n".join("/sbin/insmod /" + prefix + path.removesuffix(".gz") + " || exit 1" for path in early)
    init = """#!/bin/sh
export PATH=/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin
mount -t devtmpfs devtmpfs /dev
mkdir -p /proc /sys /run /tmp /mnt/host
mount -t proc proc /proc
mount -t sysfs sysfs /sys
mount -t tmpfs tmpfs /run
mount -t tmpfs tmpfs /tmp
%s
exec </dev/hvc0 >/dev/hvc0 2>&1
ip link set lo up
if mount -t virtiofs -o ro psusb-spike /mnt/host; then
  echo 'READONLY_SHARE_READY /mnt/host'
else
  echo 'READONLY_SHARE_UNAVAILABLE; backend must not be started'
fi
echo 'GUEST_READY; no NIC, no disk, no physical USB, no backend/server started'
uname -r
ls /sys/devices/platform/vhci_hcd.0
exec /bin/sh
""" % loads
    bridge = """#!/bin/sh
set -eu
mkdir -p /run/psusb
test ! -e /run/psusb/bridges.started || { echo 'bridges already started'; exit 1; }
touch /run/psusb/bridges.started
socat VSOCK-LISTEN:33642,fork TCP:127.0.0.1:33642 3>&- &
echo $! > /run/psusb/api-bridge.pid
socat VSOCK-LISTEN:7575,fork TCP:127.0.0.1:7575 3>&- &
echo $! > /run/psusb/vh-bridge.pid
echo 'VSOCK_BRIDGES_STARTED (does not start backend or VirtualHere)'
"""
    entries["init"] = (stat.S_IFREG | 0o755, init.encode())
    entries["usr/local/bin/start-loopback-bridges"] = (stat.S_IFREG | 0o755, bridge.encode())
    for name in ("usr/local", "usr/local/bin"):
        entries.setdefault(name, (stat.S_IFDIR | 0o755, b""))
    initrd = gzip.compress(cpio(entries), compresslevel=6, mtime=0)
    (output / "initrd.gz").write_bytes(initrd)
    manifest["initrd"] = {"sha256": sha256(initrd), "size": len(initrd), "entries": len(entries)}
    manifest["modules"] = ordered
    manifest["boot"] = {"command_line": "console=hvc0 rdinit=/init panic=-1", "cpus": 2,
                        "memory_mib": 1024, "network_devices": [], "storage_devices": [],
                        "vsock": True, "virtiofs_tag": "psusb-spike", "share_read_only": True}
    (output / "share").mkdir(exist_ok=True)
    (output / "guest-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    (output / "guest-init.sh").write_text(init)
    print("Prepared Image and initrd.gz; nothing booted or installed")


if __name__ == "__main__":
    main()
