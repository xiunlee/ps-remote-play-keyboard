#!/usr/bin/env python3
"""Synthetic-only Mac baseline. No sockets, HID calls, hooks, or Sony access.

Runs the ORIGINAL native C suite and native dylib. The Python V5 mapping and
memory reader below are research reference models, NOT the original C#/Go
implementations. Their results must not be reported as C#/Go or HID acceptance.
"""
import argparse
import ctypes
import datetime
import hashlib
import io
import json
import pathlib
import platform
import re
import struct
import subprocess
import sys
import tempfile
import zlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCES = [
    "research/native-merge/keyboard_merge.c",
    "research/native-merge/keyboard_merge.h",
    "research/native-merge/keyboard_merge_test.c",
    "research/dualsense-v5-test/V5Protocol.cs",
    "research/dualsense-v5-test/V5ProtocolTests.cs",
    "research/dualsense-preview/CandidateTransport.cs",
    "research/dualsense-preview/backend/main.go",
    "research/dualsense-preview/backend/main_test.go",
    "src/keyboard-pad/KeyboardMapping.cs",
    "src/keyboard-pad/ViiperTransport.cs",
    "research/macos-handoff/run_offline.py",
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(args):
    return subprocess.run(args, cwd=ROOT, check=True, text=True,
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT).stdout.strip()


class Checks:
    def __init__(self):
        self.passed = 0

    def check(self, condition, label):
        if not condition:
            raise AssertionError(label)
        self.passed += 1

    def rejects(self, action, error, label):
        try:
            action()
        except error:
            self.check(True, label)
        else:
            raise AssertionError(label)


class HeldKeys(ctypes.Structure):
    _fields_ = [("held", ctypes.c_uint32), ("enabled", ctypes.c_int)]


def native_frame(library, held, enabled=True):
    sample = bytearray(64)
    sample[0] = 1
    sample[1:5] = bytes([128] * 4)
    sample[5] = 8
    array = ctypes.c_uint8 * 64
    source = array.from_buffer_copy(sample)
    output = array()
    keys = HeldKeys(held, int(enabled))
    result = library.pskb_merge_keyboard(source, 64, ctypes.byref(keys), output, 64)
    if result != 0:
        raise ValueError("native merge rejected synthetic input: %d" % result)
    if bytes(source) != sample:
        raise AssertionError("native call mutated its source")
    return bytes(output)


def reference_state(report):
    """Research equivalent of KeyboardMapping + legacy-to-V5 conversion.

    Input is the actual native C output. This does not execute either C# class.
    Only neutral sensor/touch state is constructed; no physical report is read.
    """
    state = bytearray(33)
    state[:4] = bytes((value - 128) & 255 for value in report[1:5])
    buttons = (report[5] & 0xf0) | (report[6] << 8) | ((report[7] & 3) << 16)
    struct.pack_into("<I", state, 4, buttons)
    state[8] = (1, 9, 8, 10, 2, 6, 4, 5, 0)[report[5] & 15]
    state[9:11] = report[8:10]
    state[15] = state[20] = 0x80
    state[32] = 0xe0
    return bytes(state)


def reference_packet(state, sequence):
    if len(state) != 33:
        raise ValueError("expected 33 bytes")
    if not 0 <= sequence <= 0xffffffff:
        raise ValueError("sequence outside uint32")
    if struct.unpack_from("<I", state, 4)[0] & ~0x00f7fff0 or state[8] & 0xf0:
        raise ValueError("unknown controls")
    fields = struct.pack("<BBHI", 5, 1, 33, sequence)
    return b"VPCM" + fields + struct.pack("<I", zlib.crc32(fields + state)) + state


class ReferenceInputReader:
    """In-memory input-frame oracle; NOT V5FeedbackReader or transport.

    Rejects corruption/replay/truncation and closes its owned memory buffer.
    No clock, network, bus/device cleanup or production session is exercised.
    """
    def __init__(self, payload):
        self.buffer = io.BytesIO(payload)
        self.expected = 0

    def read(self):
        try:
            header = self.buffer.read(16)
            if len(header) != 16:
                raise EOFError("truncated header")
            magic, version, kind, length, sequence, crc = struct.unpack("<4sBBHII", header)
            if (magic, version, kind, length, sequence) != (b"VPCM", 5, 1, 33, self.expected):
                raise ValueError("invalid header or sequence")
            state = self.buffer.read(length)
            if len(state) != length:
                raise EOFError("truncated payload")
            if zlib.crc32(header[4:12] + state) != crc:
                raise ValueError("bad CRC")
            self.expected = (self.expected + 1) & 0xffffffff
            return state
        except Exception:
            self.buffer.close()
            raise


def exercise(library):
    checks = Checks()
    neutral = bytes.fromhex("0000000000000000000000000000008000000000800000000000000000000000e0")
    checks.check(reference_state(native_frame(library, 0)) == neutral, "native neutral")
    checks.check(zlib.crc32(b"123456789") == 0xcbf43926, "standard IEEE CRC")
    checks.check(reference_packet(neutral, 0) == bytes.fromhex("5650434d050121000000000021b88db8") + neutral,
                 "existing C# neutral golden frame")
    cross = bytearray(neutral)
    cross[4] = 0x20
    checks.check(reference_packet(cross, 0x12345678) == bytes.fromhex("5650434d050121007856341284ac4f68") + cross,
                 "existing C# cross golden frame")

    # Explicit wire offsets/values provide a separate expected outcome for each
    # action bit, including signed axes, PS/touch relocation and digital triggers.
    cases = [
        ("left-stick-up", {1: 128}), ("left-stick-down", {1: 127}),
        ("left-stick-left", {0: 128}), ("left-stick-right", {0: 127}),
        ("dpad-up", {8: 1}), ("dpad-down", {8: 2}),
        ("dpad-left", {8: 4}), ("dpad-right", {8: 8}),
        ("cross", {4: 0x20}), ("circle", {4: 0x40}),
        ("square", {4: 0x10}), ("triangle", {4: 0x80}),
        ("l1", {5: 1}), ("r1", {5: 2}),
        ("l2", {5: 4, 9: 255}), ("r2", {5: 8, 10: 255}),
        ("create", {5: 0x10}), ("options", {5: 0x20}),
        ("l3", {5: 0x40}), ("r3", {5: 0x80}),
        ("ps", {6: 1}), ("touch-click", {6: 2}),
    ]
    vectors = []
    for bit, (name, changes) in enumerate(cases):
        expected = bytearray(neutral)
        for offset, value in changes.items():
            expected[offset] = value
        pressed = reference_state(native_frame(library, 1 << bit))
        checks.check(pressed == expected, name + " encoding")
        checks.check(reference_state(native_frame(library, 0)) == neutral, name + " release")
        checks.check(reference_state(native_frame(library, 1 << bit, False)) == neutral, name + " disabled")
        packet = reference_packet(pressed, bit)
        checks.check(len(packet) == 49, name + " frame length")
        vectors.append({"action": name, "action_bit": bit, "state_hex": pressed.hex(), "frame_hex": packet.hex()})
    checks.check(reference_state(native_frame(library, 3)) == neutral, "up/down cancel")
    checks.check(reference_state(native_frame(library, 12)) == neutral, "left/right cancel")
    checks.check(reference_state(native_frame(library, 5))[:2] == bytes([166, 166]), "normalized diagonal")
    checks.check(reference_state(native_frame(library, 0xf0)) == neutral, "opposing dpad cancel")
    checks.rejects(lambda: native_frame(library, 1 << 22), ValueError, "invalid native action")
    for size in (0, 31, 32, 34, 53):
        checks.rejects(lambda size=size: reference_packet(bytes(size), 0), ValueError, "reference length validation")
    bad = bytearray(neutral)
    bad[4] = 1
    checks.rejects(lambda: reference_packet(bad, 0), ValueError, "unknown button bits")
    checks.check(reference_packet(neutral, 0xffffffff)[8:12] == bytes([255] * 4), "uint32 sequence encoding")

    # Memory-only faults. Closing BytesIO establishes only the reference reader's
    # own cleanup, not CandidateTransport socket/bus or macOS device lifecycle.
    first, second = reference_packet(neutral, 0), reference_packet(cross, 1)
    reader = ReferenceInputReader(first + second)
    checks.check(reader.read() == neutral and reader.buffer.tell() == 49, "first frame boundary")
    checks.check(reader.read() == cross and reader.buffer.tell() == 98, "second sequence")
    reader.buffer.close()
    for fault in (second, first[:48], first[:15], first[:20] + bytes([1]) + first[21:]):
        reader = ReferenceInputReader(fault)
        checks.rejects(reader.read, (ValueError, EOFError), "reference malformed frame rejected")
        checks.check(reader.buffer.closed, "reference fault closes owned memory buffer")
        checks.rejects(reader.read, ValueError, "reference closed buffer cannot resume")
    for wrong_sequence in (0, 2):
        reader = ReferenceInputReader(first + reference_packet(cross, wrong_sequence))
        reader.read()
        checks.rejects(reader.read, ValueError, "reference replay/gap rejected")
        checks.check(reader.buffer.closed, "reference sequence fault closes owned memory buffer")
    reader = ReferenceInputReader(first)
    reader.read()
    checks.rejects(reader.read, EOFError, "reference disconnect at frame boundary")
    checks.check(reader.buffer.closed, "reference EOF closes owned memory buffer")
    return checks.passed, vectors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=pathlib.Path,
                        help="New directory for JSON receipt and synthetic vectors; refuses existing path")
    args = parser.parse_args()
    if platform.system() != "Darwin":
        parser.error("this runner builds a macOS dylib and must run on macOS")
    args.output.mkdir(parents=True, exist_ok=False)
    before = {name: sha(ROOT / name) for name in SOURCES}
    compiler = run(["xcrun", "--find", "clang"])
    compiler_version = run([compiler, "--version"]).splitlines()[0]
    with tempfile.TemporaryDirectory(prefix="pskb-mac-offline-") as temporary:
        temp = pathlib.Path(temporary)
        suite, dylib = temp / "keyboard-merge-test", temp / "keyboard-merge.dylib"
        flags = ["-std=c11", "-Wall", "-Wextra", "-Werror", "-O2"]
        native = "research/native-merge/keyboard_merge.c"
        run([compiler] + flags + [native, "research/native-merge/keyboard_merge_test.c", "-o", str(suite)])
        result = run([str(suite)])
        match = re.search(r"checks=(\d+) failures=0", result)
        if not match:
            raise AssertionError("native suite result missing or failed")
        run([compiler] + flags + ["-dynamiclib", native, "-o", str(dylib)])
        artifact_hashes = {"native_suite": sha(suite), "native_dylib": sha(dylib)}
        library = ctypes.CDLL(str(dylib))
        library.pskb_merge_keyboard.argtypes = [ctypes.POINTER(ctypes.c_uint8), ctypes.c_size_t,
                                              ctypes.POINTER(HeldKeys), ctypes.POINTER(ctypes.c_uint8), ctypes.c_size_t]
        library.pskb_merge_keyboard.restype = ctypes.c_int
        reference_checks, vectors = exercise(library)
    after = {name: sha(ROOT / name) for name in SOURCES}
    if after != before:
        raise AssertionError("test sources changed during this run")
    receipt = {
        "schema": 1, "research_run": "mac-offline.1", "success": True,
        "time_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "timezone_for_report": "Asia/Taipei",
        "repository_head": run(["git", "rev-parse", "HEAD"]),
        "product_version_unchanged": (ROOT / "VERSION").read_text().strip(),
        "macos": run(["sw_vers", "-productVersion"]), "macos_build": run(["sw_vers", "-buildVersion"]),
        "architecture": platform.machine(), "compiler": compiler_version,
        "sdk": run(["xcrun", "--show-sdk-version"]), "python": platform.python_version(),
        "native_original_suite": {"checks": int(match.group(1)), "failures": 0, "stdout": result},
        "native_ctypes_and_python_reference": {"checks": reference_checks, "action_vectors": len(vectors),
                                               "original_csharp_executed": False, "original_go_executed": False},
        "scope": {"synthetic_only": True, "devices_created": False, "network_listeners": False,
                  "keyboard_captured": False, "sony_accessed": False,
                  "temporary_build_directory_removed": not temp.exists(),
                  "production_socket_bus_device_cleanup_tested": False},
        "source_sha256": before, "temporary_artifact_sha256": artifact_hashes,
        "limitations": ["Python reference is not the original C# codec or transport.",
                        "Memory-buffer EOF/cleanup is not network, backend, HID or Sony acceptance.",
                        "Original C#/Go tests were not run; no Mac keyboard key codes are mapped."]
    }
    (args.output / "RESULT.json").write_text(json.dumps(receipt, indent=2) + "\n")
    (args.output / "SYNTHETIC-VECTORS.json").write_text(json.dumps(vectors, indent=2) + "\n")
    print(result)
    print("PYTHON_REFERENCE checks=%d synthetic_vectors=%d original_csharp=false original_go=false" % (reference_checks, len(vectors)))
    print("TEMPORARY_BUILD_REMOVED=%s devices_created=false sony_accessed=false" % str(not temp.exists()).lower())


if __name__ == "__main__":
    main()
