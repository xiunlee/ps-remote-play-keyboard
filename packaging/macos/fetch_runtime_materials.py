#!/usr/bin/env python3
"""Fetch or reuse exact release inputs and prepare runtime build materials.

Requires the public guest manifest from the matching runtime source/input
archive and an explicitly supplied, rebuilt Linux arm64 backend. An extracted
pinned build-input archive can be supplied with --input-cache for offline use.
Only new cache/output directories are written. No VM, USB, guest executable,
signing key, installed application, private identity or global config is used.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import gzip
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import tarfile
import tempfile
import urllib.parse
import urllib.request

sys.dont_write_bytecode = True
BASE = "https://dl-cdn.alpinelinux.org/alpine/v3.22"
MANIFEST_SHA256 = "c2021bd548484b77632cfdcf20e70fb13a2d00348b66c6c905541e9a59562a5f"
BACKEND_NAME = "GuestDualSenseBackend-linux-arm64"
BACKEND_SHA256 = "bc3bafce56289ef33f0c474c6a5c6ea78c86739af04c4cb638b79f7e46cdd951"
KERNEL_APK = "linux-lts-6.12.114-r0.apk"
MODULES = {
    "dummy.ko": ("lib/modules/6.12.114-0-lts/kernel/drivers/net/dummy.ko.gz",
                 "89573074aefe730879cd6d977c5e9f5531812c65a8f7f2701f898f2dd6f3951e"),
    "af_packet.ko": ("lib/modules/6.12.114-0-lts/kernel/net/packet/af_packet.ko.gz",
                     "db354c5ec5cfa1ad11d3e826c84fd68d7579c7eba0b7752dd6915f74507f6f18"),
}


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def digest_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_file(path, expected):
    if path.is_symlink() or not path.is_file() or digest_file(path) != expected:
        raise ValueError("Missing, non-regular or checksum-mismatched input: " + path.name)


def official_url(url):
    parsed = urllib.parse.urlsplit(url)
    if (parsed.scheme != "https" or parsed.netloc != "dl-cdn.alpinelinux.org"
            or parsed.query or parsed.fragment or not parsed.path.startswith("/alpine/v3.22/")
            or "%" in parsed.path or ".." in parsed.path.split("/")):
        raise ValueError("Only the pinned official Alpine HTTPS origin is permitted")
    return url


class AlpineRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        official_url(new_url)
        return super().redirect_request(request, response, code, message, headers, new_url)


def read_manifest(path, expected_inputs):
    verify_file(path, MANIFEST_SHA256)
    manifest = json.loads(path.read_text())
    if manifest["architecture"] != "aarch64" or manifest["kernel_release"] != "6.12.114-0-lts":
        raise ValueError("Unsupported release architecture or kernel")
    items = []

    def add(name, record, expected_url):
        if record["url"] != expected_url or record["sha256"] != expected_inputs.get(name):
            raise ValueError("Manifest URL or hash differs from the release pin: " + name)
        official_url(record["url"])
        items.append({"name": name, "url": record["url"], "sha256": record["sha256"]})

    name = "alpine-minirootfs-3.22.7-aarch64.tar.gz"
    add(name, manifest["rootfs"], BASE + "/releases/aarch64/" + name)
    for package in manifest["packages"]:
        name = package["name"] + "-" + package["version"] + ".apk"
        repository = "community" if package["name"] == "linux-tools-usbip" else "main"
        add(name, package, BASE + "/" + repository + "/aarch64/" + name)
    for index in manifest["indexes"]:
        repository = index["url"].split("/")[-3]
        name = "APKINDEX-" + repository + ".tar.gz"
        add(name, index, BASE + "/" + repository + "/aarch64/APKINDEX.tar.gz")
    names = [item["name"] for item in items]
    if len(names) != len(set(names)) or set(names) != set(expected_inputs):
        raise ValueError("Manifest does not enumerate exactly the 15 pinned inputs")
    return items


def download(item, destination, opener):
    partial = destination.with_name("." + destination.name + ".part")
    try:
        digest = hashlib.sha256()
        size = 0
        with opener.open(official_url(item["url"]), timeout=30) as response, partial.open("xb") as stream:
            official_url(response.geturl())
            for chunk in iter(lambda: response.read(1024 * 1024), b""):
                size += len(chunk)
                if size > 160 * 1024 * 1024:
                    raise ValueError("Input exceeds the release download size bound")
                stream.write(chunk)
                digest.update(chunk)
        if digest.hexdigest() != item["sha256"]:
            raise ValueError("Official download differs from the pinned release: " + item["name"])
        partial.replace(destination)
        return size
    finally:
        partial.unlink(missing_ok=True)


def extract_identity_modules(cache, output, repository):
    # The preparation step has already checked APK/index signatures and payload
    # hashes. Recheck the complete APK before reading just these two modules.
    recipe = load_module("pskeyboard_module_recipe", repository / "research/macos-native/guest/prepare_guest.py")
    streams = recipe.gzip_members((cache / KERNEL_APK).read_bytes())
    if len(streams) != 3:
        raise ValueError("Expected signed APK v2 layout")
    wanted = {path: name for name, (path, _) in MODULES.items()}
    found = set()
    with tarfile.open(fileobj=io.BytesIO(streams[2][1]), mode="r:") as archive:
        for entry in archive:
            if entry.name not in wanted:
                continue
            name = wanted[entry.name]
            if not entry.isfile() or name in found:
                raise ValueError("Unexpected kernel module archive entry")
            data = gzip.decompress(archive.extractfile(entry).read())
            if hashlib.sha256(data).hexdigest() != MODULES[name][1]:
                raise ValueError("Kernel module does not match release hash: " + name)
            target = output / "share" / name
            target.write_bytes(data)
            target.chmod(0o644)
            found.add(name)
    if found != set(MODULES):
        raise ValueError("The pinned kernel APK is missing an identity module")


def new_destination(path):
    if path.exists() or path.is_symlink():
        raise ValueError("Destination must be a new directory: " + path.name)
    return path.resolve()


def prepare_release(manifest, cache, output, backend, input_cache=None, opener=None):
    repository = Path(__file__).resolve().parents[2]
    prepare = load_module("pskeyboard_pinned_prepare", repository / "packaging/macos/prepare_runtime.py")
    items = read_manifest(manifest, prepare.INPUT_SHA256)
    verify_file(backend, BACKEND_SHA256)
    cache, output = new_destination(cache), new_destination(output)
    if cache == output or cache in output.parents or output in cache.parents:
        raise ValueError("Cache and runtime output must be separate directories")
    if input_cache is not None:
        input_cache = input_cache.resolve(strict=True)
        for item in items:
            verify_file(input_cache / item["name"], item["sha256"])
    else:
        opener = opener or urllib.request.build_opener(AlpineRedirects())
    cache.parent.mkdir(parents=True, exist_ok=True)
    output.parent.mkdir(parents=True, exist_ok=True)
    # Failure removes only these newly created temporary staging directories;
    # no partial final cache/output replaces an existing directory.
    with tempfile.TemporaryDirectory(prefix=".pskeyboard-inputs-", dir=cache.parent) as temporary_inputs, \
            tempfile.TemporaryDirectory(prefix=".pskeyboard-materials-", dir=output.parent) as temporary_output:
        staged_cache = Path(temporary_inputs) / "inputs"
        staged_cache.mkdir()
        if input_cache is not None:
            for item in items:
                shutil.copyfile(input_cache / item["name"], staged_cache / item["name"])
                print("Verified local input:", item["name"], flush=True)
        else:
            with ThreadPoolExecutor(max_workers=4) as pool:
                pending = {pool.submit(download, item, staged_cache / item["name"], opener): item for item in items}
                for task in as_completed(pending):
                    size = task.result()
                    print("Verified official input:", pending[task]["name"], "(" + str(size) + " bytes)", flush=True)
        for item in items:
            verify_file(staged_cache / item["name"], item["sha256"])
        staged_output = Path(temporary_output) / "runtime"
        prepare.prepare(staged_cache, staged_output)
        verify_file(staged_cache / KERNEL_APK, prepare.INPUT_SHA256[KERNEL_APK])
        extract_identity_modules(staged_cache, staged_output, repository)
        backend_target = staged_output / "share" / BACKEND_NAME
        shutil.copyfile(backend, backend_target)
        backend_target.chmod(0o755)
        verify_file(backend_target, BACKEND_SHA256)
        for name, expected in prepare.OUTPUT_SHA256.items():
            verify_file(staged_output / name, expected)
        # prepare_runtime copies its source inputs for a standalone cache.
        # Here a separate verified input cache already exists, so keep one copy.
        for name in prepare.INPUT_SHA256:
            (staged_output / name).unlink()
        receipt = {"schemaVersion": 1, "publicGuestManifestSha256": MANIFEST_SHA256,
                   "inputMode": "explicit-local-cache" if input_cache is not None else "official-https",
                   "inputs": items, "outputs": dict(prepare.OUTPUT_SHA256,
                       **{name: expected for name, (_, expected) in MODULES.items()},
                       **{BACKEND_NAME: BACKEND_SHA256}),
                   "executedGuestCode": False, "startedVM": False,
                   "copiedPrivateIdentity": False}
        (staged_output / "runtime-materials.json").write_text(json.dumps(receipt, indent=2) + "\n")
        (staged_cache / "guest-manifest.json").write_bytes(manifest.read_bytes())
        if cache.exists() or output.exists() or cache.is_symlink() or output.is_symlink():
            raise ValueError("Destination appeared while preparing materials")
        staged_cache.rename(cache)
        staged_output.rename(output)
    print("READY: pass the new runtime directory to build_preview.py --runtime-materials.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True, help="Public guest-manifest.json from matching source/input archive")
    parser.add_argument("--cache", type=Path, required=True, help="New directory for exact downloaded/copied inputs")
    parser.add_argument("--output", type=Path, required=True, help="New runtime materials directory for the App builder")
    parser.add_argument("--backend", type=Path, required=True, help="Explicit rebuilt Linux arm64 backend, checked against release SHA256")
    parser.add_argument("--input-cache", type=Path, help="Extracted pinned build-input archive; disables network downloads")
    args = parser.parse_args()
    prepare_release(args.manifest, args.cache, args.output, args.backend, args.input_cache)


if __name__ == "__main__":
    main()
