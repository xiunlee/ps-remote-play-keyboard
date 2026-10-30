#!/usr/bin/env python3
"""Own one neutral-only VIIPER candidate in the isolated guest, never host-attach it."""
import argparse
import json
from pathlib import Path
import select
import socket
import struct
import time
import zlib

ADDRESS = ("127.0.0.1", 33742)
COMMIT = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"


def request(command):
    with socket.create_connection(ADDRESS, timeout=3) as sock:
        sock.sendall(command.encode() + b"\0")
        result = bytearray()
        while True:
            chunk = sock.recv(4096)
            if not chunk:
                break
            result.extend(chunk)
            if len(result) > 16384:
                raise ValueError("Management response too large")
        value = json.loads(result)
        if "status" in value:
            raise RuntimeError(f"Management status {value['status']}")
        return value


def frame(sequence):
    state = bytearray(33)
    state[15] = state[20] = 0x80
    state[32] = 0xe0
    header = bytearray(b"VPCM" + struct.pack("<BBHI", 5, 1, 33, sequence))
    return header + struct.pack("<I", zlib.crc32(header[4:12] + state)) + state


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--seconds", type=int, default=1200)
    args = parser.parse_args()
    if not 1 <= args.seconds <= 3600:
        parser.error("seconds must be 1..3600")
    args.output.mkdir()
    info = request("ping")
    if (info.get("labMarker") != "pskeyboard-dualsense-v5-guest" or
            info.get("sourceCommit") != COMMIT or info.get("autoAttach") is not False or
            info.get("loopbackOnly") is not True):
        raise ValueError("Not the pinned, loopback-only guest backend")
    bus = request("bus/create")["busId"]
    if not isinstance(bus, int) or bus <= 0:
        raise ValueError("Invalid bus id")
    stream = None
    record = {"busId": bus, "neutralOnly": True, "hostAttached": False}
    (args.output / "SESSION.json").write_text(json.dumps(record, indent=2) + "\n")
    try:
        added = request(f'bus/{bus}/add {{"type":"dualsensegamepadv5"}}')
        device = str(added["devId"])
        if added["busId"] != bus or added["type"] != "dualsensegamepadv5" or not device.isdecimal():
            raise ValueError("Unexpected candidate identity")
        stream = socket.create_connection(ADDRESS, timeout=3)
        stream.sendall(f"bus/{bus}/{device}\0".encode())
        stream.sendall(frame(0))
        record.update({"devId": device, "usbBusId": f"{bus}-{device}"})
        (args.output / "SESSION.json").write_text(json.dumps(record, indent=2) + "\n")
        print(json.dumps(record), flush=True)
        deadline = time.monotonic() + args.seconds
        sequence = 1
        feedback_bytes = 0
        while time.monotonic() < deadline and not (args.output / "stop").exists():
            if select.select([stream], [], [], 0.008)[0]:
                feedback = stream.recv(16384)
                if not feedback:
                    raise RuntimeError("Candidate owner stream closed")
                feedback_bytes += len(feedback)  # No raw feedback or input logging.
            stream.sendall(frame(sequence))
            sequence = (sequence + 1) & 0xffffffff
        record["feedbackBytesDrained"] = feedback_bytes
    finally:
        if stream is not None:
            stream.close()
        try:
            removed = request(f"bus/remove {bus}")
            record["busRemovalConfirmed"] = removed.get("busId") == bus
        except Exception as error:
            record["cleanupError"] = str(error)
        (args.output / "SESSION.json").write_text(json.dumps(record, indent=2) + "\n")
        print(json.dumps(record), flush=True)


if __name__ == "__main__":
    main()
