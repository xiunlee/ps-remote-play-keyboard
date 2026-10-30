#!/usr/bin/env python3
"""One-shot research relay. Explicit execution creates one owned guest bus.

Host API 127.0.0.1:33742 must already relay to the pinned guest backend;
frontend 127.0.0.1:33743 receives GREETING then sends 49-byte VPCM v5 frames.
No VM, USB/IP, VirtualHere, keyboard or Sony operations occur here.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import selectors
import socket
import struct
import time
import zlib

COMMIT = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"
API = ("127.0.0.1", 33742)
FRONTEND = ("127.0.0.1", 33743)
GREETING = b"PSKeyboard-DualSenseV5-Relay/1\n"
INTERVAL = 0.008
STALE_SECONDS = 0.250
FRAME_TIMEOUT = 1.5
MAX_BUFFER = 65536
NEUTRAL = bytes(15) + b"\x80" + bytes(4) + b"\x80" + bytes(11) + b"\xe0"


class RelayError(Exception):
    """Codes intentionally contain no packet, keyboard or private path data."""


class APIError(RelayError):
    def __init__(self, status):
        self.status = status
        super().__init__("api_status_" + str(status))


def uint32(value, label):
    if type(value) is not int or not 0 < value <= 0xFFFFFFFF:
        raise RelayError("invalid_" + label)
    return value


def validate_ping(data):
    required = {
        "server": "PSKeyboard-DualSenseV5-GuestCandidate", "version": "research.guest.1",
        "labMarker": "pskeyboard-dualsense-v5-guest", "sourceCommit": COMMIT,
        "apiAddress": "127.0.0.1:33642", "usbAddress": "127.0.0.1:33641",
        "autoAttach": False, "hostAutoAttach": False, "loopbackOnly": True,
        "stdinEofStops": True, "sessionLifetimeLimit": False,
    }
    if not isinstance(data, dict) or any(type(data.get(k)) is not type(v) or data[k] != v for k, v in required.items()):
        raise RelayError("guest_identity_mismatch")


def validate_payload(payload):
    # Keyboard-only subset of ConvertLegacyKeyboardState + EncodeInput. No
    # physical touch/motion, right-stick, microphone, Edge or raw metadata.
    if len(payload) != 33:
        raise RelayError("input_length")
    buttons = struct.unpack_from("<I", payload, 4)[0]
    if buttons & ~0x0003FFF0 or payload[8] & 0xF0:
        raise RelayError("input_reserved_controls")
    if payload[2:4] != b"\0\0":
        raise RelayError("input_right_stick")
    if payload[11:15] != bytes(4) or payload[16:20] != bytes(4) or payload[15] != 0x80 or payload[20] != 0x80:
        raise RelayError("input_touch_state")
    if payload[21:32] != bytes(11) or payload[32] != 0xE0:
        raise RelayError("input_motion_state")
    if payload[9] != (255 if buttons & 0x400 else 0) or payload[10] != (255 if buttons & 0x800 else 0):
        raise RelayError("input_trigger_mismatch")


def encode(payload, sequence):
    fields = struct.pack("<BBHI", 5, 1, 33, sequence)
    return b"VPCM" + fields + struct.pack("<I", zlib.crc32(fields + payload)) + payload


class FrameParser:
    def __init__(self, feedback=False):
        self.feedback = feedback
        self.buffer = bytearray()
        self.expected_sequence = 0
        self.partial_since = None
        self.failed = False

    def check_deadline(self, now):
        if self.partial_since is not None and now - self.partial_since >= FRAME_TIMEOUT:
            self.failed = True
            raise RelayError("frame_deadline")

    def feed(self, data, now):
        if self.failed:
            raise RelayError("parser_closed")
        try:
            self.check_deadline(now)
            if not data:
                raise RelayError("feedback_eof" if self.feedback else "frontend_eof")
            if len(self.buffer) + len(data) > MAX_BUFFER:
                raise RelayError("frame_buffer_limit")
            if not self.buffer:
                self.partial_since = now
            self.buffer.extend(data)
            frames = []
            while len(self.buffer) >= 16:
                magic, version, kind, size, sequence, crc = struct.unpack_from("<4sBBHII", self.buffer)
                sizes = {0x81: 474, 0x83: 2396, 0x84: 474} if self.feedback else {1: 33}
                if magic != b"VPCM" or version != 5 or kind not in sizes or size != sizes[kind]:
                    raise RelayError("frame_header")
                if sequence != self.expected_sequence:
                    raise RelayError("frame_sequence")
                if len(self.buffer) < 16 + size:
                    break
                payload = bytes(self.buffer[16:16 + size])
                if crc != zlib.crc32(bytes(self.buffer[4:12]) + payload):
                    raise RelayError("frame_crc")
                if self.feedback:
                    if kind == 0x83 and payload[:2] != b"\xda\x01":
                        raise RelayError("feedback_atomic_boundary")
                else:
                    validate_payload(payload)
                frames.append(payload)
                del self.buffer[:16 + size]
                self.expected_sequence = (self.expected_sequence + 1) & 0xFFFFFFFF
                self.partial_since = now if self.buffer else None
            return frames
        except RelayError:
            self.failed = True
            raise


class FeedClock:
    """Pure latest-state scheduler. Feedback never advances its deadline."""
    def __init__(self, now, wait_seconds=120, session_seconds=7200):
        if not 1 <= wait_seconds <= 600 or not 1 <= session_seconds <= 7200:
            raise RelayError("invalid_session_bounds")
        self.started = now
        self.wait_seconds = wait_seconds
        self.session_seconds = session_seconds
        self.connected = None
        self.last_input = None
        self.latest = NEUTRAL
        self.next_output = now
        self.output_sequence = 0
        self.last_now = now

    def connect(self, now):
        self.check_limits(now)
        if self.connected is not None:
            raise RelayError("second_frontend")
        self.connected = now
        self.last_input = now

    def input(self, payload, now):
        if self.connected is None:
            raise RelayError("input_before_connect")
        self.check_limits(now)
        self.latest, self.last_input = payload, now

    def check_limits(self, now):
        if now < self.last_now:
            raise RelayError("clock_reversed")
        self.last_now = now
        if self.connected is None:
            if now - self.started >= self.wait_seconds:
                raise RelayError("frontend_wait_timeout")
        else:
            if now - self.connected >= self.session_seconds:
                raise RelayError("session_timeout")
            if now - self.last_input >= STALE_SECONDS:
                raise RelayError("frontend_stale")

    def tick(self, now):
        self.check_limits(now)
        if now < self.next_output:
            return None
        packet = encode(self.latest if self.connected is not None else NEUTRAL, self.output_sequence)
        self.output_sequence = (self.output_sequence + 1) & 0xFFFFFFFF
        # Skip missed slots; never replay queued states or send catch-up bursts.
        self.next_output += (math.floor((now - self.next_output) / INTERVAL) + 1) * INTERVAL
        return packet


def request(command, timeout=1.5):
    deadline = time.monotonic() + timeout
    with socket.create_connection(API, timeout=timeout) as connection:
        connection.sendall(command.encode("ascii") + b"\0")
        response = bytearray()
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise RelayError("api_deadline")
            connection.settimeout(remaining)
            block = connection.recv(4096)
            if time.monotonic() >= deadline:
                raise RelayError("api_deadline")
            if not block:
                break
            response.extend(block)
            if len(response) > 16384:
                raise RelayError("api_response_limit")
    try:
        data = json.loads(response.decode("utf-8"))
    except (ValueError, UnicodeError):
        raise RelayError("api_json") from None
    if not isinstance(data, dict):
        raise RelayError("api_object")
    if "status" in data:
        raise APIError(uint32(data["status"], "api_status"))
    return data


class Ownership:
    def __init__(self, api_request=None):
        self.request = api_request or request
        self.bus_id = None
        self.device_id = None
        self.creation_uncertain = False
        self.identity_verified = False

    def create(self):
        validate_ping(self.request("ping"))
        self.identity_verified = True
        self.creation_uncertain = True
        self.bus_id = uint32(self.request("bus/create").get("busId"), "bus_id")
        self.creation_uncertain = False
        result = self.request('bus/%d/add {"type":"dualsensegamepadv5"}' % self.bus_id)
        device_id = result.get("devId")
        if type(result.get("busId")) is not int or result["busId"] != self.bus_id or result.get("type") != "dualsensegamepadv5":
            raise RelayError("added_device_identity")
        if not isinstance(device_id, str) or not device_id.isascii() or not device_id.isdecimal() or device_id.startswith("0"):
            raise RelayError("invalid_device_id")
        self.device_id = uint32(int(device_id), "device_id")

    def remove(self):
        if self.bus_id is None:
            if self.creation_uncertain:
                raise RelayError("bus_creation_uncertain")
            return "not_created"
        try:
            result = self.request("bus/remove %d" % self.bus_id)
            if type(result.get("busId")) is not int or result["busId"] != self.bus_id:
                raise RelayError("removed_bus_identity")
        except APIError as error:
            if error.status == 404:
                return "already_absent"
            raise
        return "removed"


def write_receipt(path, record):
    temporary = path.with_name(path.name + ".pending")
    with temporary.open("w", encoding="utf-8") as output:
        json.dump(record, output, indent=2)
        output.write("\n")
    os.replace(temporary, path)


def send_complete(connection, packet):
    # Nonblocking full-or-abort. A partial TCP write has uncertain framing;
    # do not retry/reuse its sequence or leave a buffered backlog of commands.
    if connection.send(packet) != len(packet):
        raise RelayError("partial_write")


def run(args):
    receipt = Path(args.receipt).resolve()
    if receipt.exists() or receipt.with_name(receipt.name + ".pending").exists():
        raise RelayError("receipt_exists")
    receipt.parent.mkdir(parents=True, exist_ok=True)
    stop_file = Path(args.stop_file).resolve() if args.stop_file else None
    if stop_file and stop_file.exists():
        raise RelayError("stop_file_already_present")
    record = {"schema": 1, "status": "starting", "sourceCommit": COMMIT,
              "relaySourceSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              "startedAtUTC": datetime.now(timezone.utc).isoformat(), "pid": os.getpid(),
              "api": "127.0.0.1:33742", "frontend": "127.0.0.1:33743",
              "greeting": GREETING.decode("ascii"), "busId": None, "devId": None,
              "cleanup": "not_started", "outputFrames": 0, "inputFrames": 0, "feedbackFrames": 0,
              "waitSeconds": args.wait_seconds, "sessionSeconds": args.session_seconds,
              "inputStaleMilliseconds": 250, "outputIntervalMilliseconds": 8,
              "guestIdentityVerified": False, "ownerStreamOpened": False, "frontendConnected": False,
              "keyboardLogging": False, "usbAttachManagedExternally": True, "sonyTested": False}
    write_receipt(receipt, record)
    ownership = Ownership()
    selector = selectors.DefaultSelector()
    listener = owner = frontend = None
    outcome = "unknown_failure"
    cleanup_ok = True
    try:
        # Reserve the frontend port before creating a guest resource.
        listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        listener.bind(FRONTEND)
        listener.listen(1)
        listener.setblocking(False)
        ownership.create()
        record.update(busId=ownership.bus_id, devId=ownership.device_id)
        owner = socket.create_connection(API, timeout=1.5)
        owner.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        owner.sendall(("bus/%d/%d\0" % (ownership.bus_id, ownership.device_id)).encode("ascii"))
        record.update(guestIdentityVerified=True, ownerStreamOpened=True)
        owner.setblocking(False)
        selector.register(listener, selectors.EVENT_READ, "accept")
        selector.register(owner, selectors.EVENT_READ, "feedback")
        scheduler = FeedClock(time.monotonic(), args.wait_seconds, args.session_seconds)
        inputs, feedback = FrameParser(), FrameParser(feedback=True)
        record["status"] = "waiting_frontend"
        write_receipt(receipt, record)
        print("RELAY_WAITING frontend=127.0.0.1:33743 bus=%d device=%d neutral_only=true" %
              (ownership.bus_id, ownership.device_id), flush=True)
        while True:
            now = time.monotonic()
            if stop_file and stop_file.exists():
                raise RelayError("stop_file")
            inputs.check_deadline(now)
            feedback.check_deadline(now)
            packet = scheduler.tick(now)
            if packet is not None:
                send_complete(owner, packet)
                record["outputFrames"] += 1
            timeout = max(0, min(INTERVAL, scheduler.next_output - time.monotonic()))
            for key, _ in selector.select(timeout):
                now = time.monotonic()
                if key.data == "accept":
                    frontend, peer = listener.accept()
                    if peer[0] != "127.0.0.1":
                        raise RelayError("non_loopback_frontend")
                    selector.unregister(listener)
                    listener.close()
                    listener = None  # No reconnection, even after frontend EOF.
                    frontend.setblocking(False)
                    frontend.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                    send_complete(frontend, GREETING)
                    scheduler.connect(now)
                    selector.register(frontend, selectors.EVENT_READ, "input")
                    record["status"] = "frontend_connected"
                    record["frontendConnected"] = True
                    write_receipt(receipt, record)
                elif key.data == "feedback":
                    blocks = feedback.feed(owner.recv(8192), now)
                    record["feedbackFrames"] += len(blocks)
                else:
                    # Read at most 4 KiB so hostile backlog cannot starve the
                    # independent timer or cause unbounded parsing work.
                    blocks = inputs.feed(frontend.recv(4096), now)
                    for block in blocks:
                        scheduler.input(block, now)
                    record["inputFrames"] += len(blocks)
    except RelayError as error:
        outcome = str(error)
    except (OSError, ValueError):
        outcome = "io_or_value_failure"
    except KeyboardInterrupt:
        outcome = "interrupted"
    finally:
        # Failure cleanup closes ownership immediately, without a possibly
        # blocking final write; the pinned guest stream lifecycle clears state.
        for connection in [frontend, owner, listener]:
            if connection is not None:
                try:
                    connection.close()
                except OSError:
                    cleanup_ok = False
        selector.close()
        record.update(busId=ownership.bus_id, devId=ownership.device_id)
        record["guestIdentityVerified"] = ownership.identity_verified
        try:
            record["cleanup"] = ownership.remove()
        except (RelayError, OSError, ValueError):
            record["cleanup"] = "unconfirmed"
            cleanup_ok = False
        record.update(status="stopped", reason=outcome, cleanupConfirmed=cleanup_ok)
        record["stoppedAtUTC"] = datetime.now(timezone.utc).isoformat()
        write_receipt(receipt, record)
        print("RELAY_STOPPED reason=%s cleanup=%s" % (outcome, record["cleanup"]), flush=True)
    return 0 if cleanup_ok and outcome in ["stop_file", "frontend_eof", "frontend_wait_timeout", "session_timeout", "interrupted"] else 1


def arguments(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt", required=True)
    parser.add_argument("--wait-seconds", type=int, default=120)
    parser.add_argument("--session-seconds", type=int, default=7200)
    parser.add_argument("--stop-file")
    result = parser.parse_args(argv)
    if not 1 <= result.wait_seconds <= 600 or not 1 <= result.session_seconds <= 7200:
        parser.error("wait must be 1..600 seconds and session 1..7200 seconds")
    return result


if __name__ == "__main__":
    try:
        raise SystemExit(run(arguments()))
    except RelayError as error:
        print("RELAY_REFUSED " + str(error), flush=True)
        raise SystemExit(2)
