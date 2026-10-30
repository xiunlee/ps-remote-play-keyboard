#!/usr/bin/env python3
"""Build/test the guest entry point from preverified, locally unpacked materials.

Never executes either backend. No global Go installation or settings changes.
Usage: python3 build.py EXISTING_MATERIALS_DIRECTORY
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

COMMIT = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"
ARCHIVES = {
    "go1.27.3.darwin-arm64.tar.gz": "dd3908cf20959d798c1e06ebe5b72213f0505ce01373b40ce3110975a6cd99c2",
    "VIIPER-" + COMMIT + ".zip": "8305750e881e7e955dcf26dc5fedc2b43d55902c9f1fa5b4eca31fe51901b2a5",
}
out = Path(sys.argv[1]).resolve()
resume = len(sys.argv) == 3 and sys.argv[2] == "--resume-failed-build"
if (out / "BUILD.json").exists():
    raise RuntimeError("completed build outputs are immutable; use a new directory")
source = Path(__file__).resolve().parent
upstream = out / "upstream" / ("VIIPER-" + COMMIT)
go = out / "toolchain/go/bin/go"
gofmt = out / "toolchain/go/bin/gofmt"
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
for name, expected in ARCHIVES.items():
    if sha(out / "downloads" / name) != expected:
        raise RuntimeError("archive hash mismatch: " + name)
module = out / "module"
module.mkdir(exist_ok=resume)
for name in ["main.go", "main_test.go"]:
    shutil.copyfile(source / name, module / name)
subprocess.run([str(gofmt), "-w", str(module / "main.go"), str(module / "main_test.go")], check=True)
shutil.copyfile(upstream / "go.sum", module / "go.sum")
(module / "go.mod").write_text(
    "module github.com/Alia5/VIIPER/pskeyboardguest\n\ngo 1.26.2\n\n"
    "require github.com/Alia5/VIIPER v0.0.0\n\n"
    'replace github.com/Alia5/VIIPER => "../upstream/VIIPER-' + COMMIT + '"\n')
env = os.environ.copy()
env.update(GOENV="off", GOWORK="off", GOTOOLCHAIN="local", CGO_ENABLED="0",
           GOOS="darwin", GOARCH="arm64", GOFLAGS="", GOPROXY="https://proxy.golang.org",
           GOSUMDB="sum.golang.org", GOPRIVATE="", GONOSUMDB="", GONOPROXY="")
for key, directory in [("GOPATH", "gopath"), ("GOMODCACHE", "gomodcache"),
                       ("GOCACHE", "gocache"), ("GOTMPDIR", "gotmp"),
                       ("TEST_TELEMETRY_DIR", "telemetry")]:
    (out / directory).mkdir(exist_ok=resume)
    env[key] = str(out / directory)
(out / "telemetry/mode").write_text("off")
overlay_source = out / "autoattach_darwin.go.overlay"
shutil.copyfile(source / "autoattach_darwin.go.overlay", overlay_source)
overlay = out / "DARWIN-OVERLAY.json"
overlay.write_text(json.dumps({"Replace": {
    str(upstream / "internal/server/api/autoattach_darwin.go"): str(overlay_source)
}}, indent=2) + "\n")

def command(args, filename=None, target="darwin"):
    run_env = dict(env, GOOS=target)
    if target == "darwin" and args[0] in ["build", "test", "list"]:
        args = args[:1] + ["-overlay=" + str(overlay)] + args[1:]
    result = subprocess.run([str(go)] + args, cwd=module, env=run_env,
                            text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if filename:
        (out / filename).write_text(result.stdout)
    if result.returncode:
        print(result.stdout, flush=True)
        raise RuntimeError("Go command failed: " + repr(args))
    return result.stdout

toolchain_version = command(["version"]).strip()
if toolchain_version != "go version go1.27.3 darwin/arm64":
    raise RuntimeError("unexpected toolchain " + toolchain_version)
binaries = []
for target in ["linux", "darwin"]:
    name = "GuestDualSenseBackend-" + target + "-arm64"
    command(["build", "-mod=mod", "-trimpath", "-buildvcs=false", "-o", str(out / name), "."],
            "BUILD-" + target + ".log", target)
    if str(out).encode() in (out / name).read_bytes():
        raise RuntimeError("binary embeds private build directory")
    binaries.append({"path": name, "sha256": sha(out / name), "target": target + "/arm64"})
    print("BUILT " + name + " sha256=" + sha(out / name), flush=True)
command(["test", "-mod=readonly", "-trimpath", "-count=1", "-json", "-run",
         "^Test(Candidate(Arguments|Allowlist|StdinStops|ShutdownWatchdog)|Guest(Restrictions|NeutralWire))$", "."],
        "PURE-TESTS.jsonl")
if sha(upstream / "go.sum") != sha(module / "go.sum"):
    raise RuntimeError("pinned go.sum changed during build")
command(["list", "-mod=readonly", "-m", "-json", "all"], "MODULES.jsonl")
runtime = set()
for target in ["linux", "darwin"]:
    runtime.update(command(["list", "-mod=readonly", "-deps", "-f",
        "{{if .Module}}{{.Module.Path}}|{{.Module.Version}}|{{.Module.Dir}}{{end}}", "."],
        "RUNTIME-" + target + ".txt", target).splitlines())
notices = out / "notices"
notices.mkdir(exist_ok=resume)
materials = []
for line in sorted(runtime):
    if not line: continue
    identity, version, directory = line.split("|")
    if identity == "github.com/Alia5/VIIPER/pskeyboardguest": continue
    licenses = [Path(directory) / name for name in ["LICENSE.txt", "LICENSE"]]
    license_source = next((path for path in licenses if path.is_file()), None)
    if license_source is None: raise RuntimeError("missing license: " + identity)
    name = identity.replace("/", "_") + "-LICENSE.txt"
    shutil.copyfile(license_source, notices / name)
    materials.append({"module": identity, "version": version, "license": "notices/" + name,
                      "licenseSha256": sha(notices / name)})
shutil.copyfile(out / "toolchain/go/LICENSE", notices / "Go-LICENSE.txt")
materials.append({"module": "Go standard library", "version": "go1.27.3",
                  "license": "notices/Go-LICENSE.txt", "licenseSha256": sha(notices / "Go-LICENSE.txt")})
(out / "MATERIALS.json").write_text(json.dumps(materials, indent=2) + "\n")
record = {"sourceCommit": COMMIT, "goVersion": toolchain_version, "archives": ARCHIVES,
          "sourceSha256": {name: sha(source / name) for name in ["main.go", "main_test.go", "build.py", "autoattach_darwin.go.overlay"]},
          "moduleSourceSha256": {name: sha(module / name) for name in ["main.go", "main_test.go"]},
          "binaries": binaries, "pureTestsPassed": True, "testResultsSha256": sha(out / "PURE-TESTS.jsonl"),
          "dependencyChecksumsUnchanged": True, "goSumSha256": sha(module / "go.sum"),
          "materialsSha256": sha(out / "MATERIALS.json"), "cgoEnabled": False,
          "backendRuntimeLaunched": False, "deviceCreated": False, "sonyAccessed": False,
          "darwinOverlay": "fail-closed missing platform autoattach stub; no upstream file modified",
          "apiAddress": "127.0.0.1:33642", "usbAddress": "127.0.0.1:33641", "autoAttach": False,
          "stdinEofStops": True, "shutdownTimeoutSeconds": 15, "sessionLifetimeLimit": False}
(out / "BUILD.json").write_text(json.dumps(record, indent=2) + "\n")
print("PURE TESTS PASSED; backend binaries were not executed", flush=True)
