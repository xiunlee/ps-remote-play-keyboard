#!/usr/bin/env python3
"""Offline tests: fake time, fake sockets/API/selectors; never binds a port."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import zlib

spec = importlib.util.spec_from_file_location("guest_input_relay", Path(__file__).with_name("guest-input-relay.py"))
relay = importlib.util.module_from_spec(spec)
spec.loader.exec_module(relay)


def ping():
    return {"server": "PSKeyboard-DualSenseV5-GuestCandidate", "version": "research.guest.1",
            "labMarker": "pskeyboard-dualsense-v5-guest", "sourceCommit": relay.COMMIT,
            "apiAddress": "127.0.0.1:33642", "usbAddress": "127.0.0.1:33641",
            "autoAttach": False, "hostAutoAttach": False, "loopbackOnly": True,
            "stdinEofStops": True, "sessionLifetimeLimit": False}


def frame(kind, sequence, payload):
    fields = struct.pack("<BBHI", 5, kind, len(payload), sequence)
    return b"VPCM" + fields + struct.pack("<I", zlib.crc32(fields + payload)) + payload


def cross():
    state = bytearray(relay.NEUTRAL)
    state[4] = 0x20
    return bytes(state)


class ProtocolTests(unittest.TestCase):
    def test_pinned_guest_identity(self):
        relay.validate_ping(ping())
        for key, value in ping().items():
            changed = ping()
            changed[key] = not value if type(value) is bool else "wrong"
            with self.subTest(key=key), self.assertRaises(relay.RelayError):
                relay.validate_ping(changed)
        changed = ping(); changed["loopbackOnly"] = 1
        with self.assertRaises(relay.RelayError): relay.validate_ping(changed)

    def test_golden_frames(self):
        self.assertEqual(len(relay.NEUTRAL), 33)
        self.assertEqual(relay.encode(relay.NEUTRAL, 0)[:16], bytes.fromhex("5650434d050121000000000021b88db8"))
        self.assertEqual(relay.encode(cross(), 0x12345678)[:16], bytes.fromhex("5650434d050121007856341284ac4f68"))
        self.assertEqual(relay.GREETING, b"PSKeyboard-DualSenseV5-Relay/1\n")

    def test_input_fragments_coalescing_and_wrap(self):
        parser = relay.FrameParser()
        wire = relay.encode(relay.NEUTRAL, 0) + relay.encode(cross(), 1)
        result = []
        for index, byte in enumerate(wire):
            result += parser.feed(bytes([byte]), index / 1000)
        self.assertEqual(result, [relay.NEUTRAL, cross()])
        self.assertEqual(parser.expected_sequence, 2)
        parser = relay.FrameParser()
        self.assertEqual(parser.feed(wire, 0), [relay.NEUTRAL, cross()])
        parser.expected_sequence = 0xFFFFFFFF
        parser.feed(relay.encode(relay.NEUTRAL, 0xFFFFFFFF) + relay.encode(relay.NEUTRAL, 0), 0.1)
        self.assertEqual(parser.expected_sequence, 1)

    def test_bad_headers_crc_sequences_latch_failure(self):
        original = relay.encode(relay.NEUTRAL, 0)
        damaged = []
        for offset in [0, 4, 5, 6, 7, 8, 12, 20]:
            value = bytearray(original); value[offset] ^= 1; damaged.append(bytes(value))
        damaged += [relay.encode(relay.NEUTRAL, 1), original + original]
        for wire in damaged:
            parser = relay.FrameParser()
            with self.subTest(wire=wire[:16].hex()), self.assertRaises(relay.RelayError): parser.feed(wire, 0)
            with self.assertRaisesRegex(relay.RelayError, "parser_closed"): parser.feed(original, 0.1)

    def test_input_payload_keyboard_subset(self):
        relay.validate_payload(relay.NEUTRAL)
        relay.validate_payload(cross())
        digital = bytearray(relay.NEUTRAL)
        struct.pack_into("<I", digital, 4, 0x3FFF0)
        digital[9] = digital[10] = 255
        relay.validate_payload(bytes(digital))
        invalid = []
        for offset in [2, 3, 4, 7, 8, 11, 14, 15, 16, 19, 20, 21, 31, 32]:
            value = bytearray(relay.NEUTRAL)
            value[offset] = 16 if offset == 8 else 1
            invalid.append(bytes(value))
        for offset in [9, 10]:
            value = bytearray(relay.NEUTRAL); value[offset] = 255; invalid.append(bytes(value))
        invalid += [bytes(32), bytes(34)]
        for value in invalid:
            with self.subTest(value=value.hex()), self.assertRaises(relay.RelayError): relay.validate_payload(value)
        microphone = bytearray(relay.NEUTRAL); microphone[6] = 4
        with self.assertRaises(relay.RelayError): relay.validate_payload(microphone)

    def test_limits_eof_and_partial_absolute_deadline(self):
        parser = relay.FrameParser()
        parser.feed(b"V", 0)
        parser.feed(b"P", 1)
        with self.assertRaisesRegex(relay.RelayError, "frame_deadline"): parser.feed(b"C", 1.5)
        with self.assertRaisesRegex(relay.RelayError, "frontend_eof"): relay.FrameParser().feed(b"", 0)
        with self.assertRaisesRegex(relay.RelayError, "frame_buffer_limit"):
            relay.FrameParser().feed(b"x" * 65537, 0)
        parser = relay.FrameParser(feedback=True)
        parser.feed(b"V", 0)
        with self.assertRaisesRegex(relay.RelayError, "frame_deadline"): parser.check_deadline(1.5)

    def test_feedback_all_existing_nonmicrophone_lanes(self):
        patterned = bytes(index % 256 for index in range(474))
        atomic = b"\xda\x01" + patterned + bytes(1920)
        parser = relay.FrameParser(feedback=True)
        wire = frame(0x81, 0, patterned) + frame(0x83, 1, atomic) + frame(0x84, 2, patterned)
        results = []
        for start in range(0, len(wire), 7): results += parser.feed(wire[start:start + 7], start / 10000)
        self.assertEqual(results, [patterned, atomic, patterned])
        for invalid in [frame(0x85, 0, bytes(9)), frame(1, 0, relay.NEUTRAL), frame(0x81, 0, bytes(473)),
                        frame(0x83, 0, bytes(2396)), frame(0x82, 0, patterned), frame(0x84, 1, patterned)]:
            with self.assertRaises(relay.RelayError): relay.FrameParser(feedback=True).feed(invalid, 0)


class ClockTests(unittest.TestCase):
    def test_neutral_wait_and_deadline_not_feedback(self):
        clock = relay.FeedClock(0)
        self.assertEqual(clock.tick(0), relay.encode(relay.NEUTRAL, 0))
        for now in [.001, .002, .007]: self.assertIsNone(clock.tick(now))
        self.assertEqual(clock.tick(.008), relay.encode(relay.NEUTRAL, 1))
        self.assertEqual(clock.tick(.016), relay.encode(relay.NEUTRAL, 2))
        self.assertEqual(clock.tick(.5)[16:], relay.NEUTRAL)
        self.assertIsNone(clock.tick(.5))  # no catch-up burst
        with self.assertRaisesRegex(relay.RelayError, "frontend_wait_timeout"): clock.tick(120)

    def test_stale_hard_deadline_cannot_be_reset_by_late_input(self):
        clock = relay.FeedClock(0)
        clock.connect(1)
        clock.input(cross(), 1.1)
        self.assertEqual(clock.tick(1.2)[16:], cross())
        with self.assertRaisesRegex(relay.RelayError, "frontend_stale"): clock.input(relay.NEUTRAL, 1.351)
        with self.assertRaisesRegex(relay.RelayError, "frontend_stale"): clock.tick(1.351)
        clock = relay.FeedClock(0); clock.connect(0)
        with self.assertRaisesRegex(relay.RelayError, "frontend_stale"): clock.tick(.250)

    def test_latest_payload_resequence_limits_and_single_client(self):
        clock = relay.FeedClock(0, session_seconds=1)
        clock.tick(0); clock.connect(.01)
        clock.input(cross(), .02); clock.input(relay.NEUTRAL, .03)
        packet = clock.tick(.04)
        self.assertEqual(packet, relay.encode(relay.NEUTRAL, 1))
        with self.assertRaisesRegex(relay.RelayError, "second_frontend"): clock.connect(.05)
        for now in [.2, .4, .6, .8, 1.0]: clock.input(cross(), now)
        with self.assertRaisesRegex(relay.RelayError, "session_timeout"): clock.tick(1.01)
        with self.assertRaises(relay.RelayError): relay.FeedClock(0, 601)
        with self.assertRaises(relay.RelayError): relay.FeedClock(0, 0)
        with self.assertRaises(relay.RelayError): relay.FeedClock(0, session_seconds=7201)
        clock = relay.FeedClock(0)
        clock.tick(1)
        with self.assertRaisesRegex(relay.RelayError, "clock_reversed"): clock.tick(.9)
        with self.assertRaisesRegex(relay.RelayError, "input_before_connect"): relay.FeedClock(0).input(cross(), 0)


class OwnershipTests(unittest.TestCase):
    def test_only_owned_bus_removed_and_404_is_absence(self):
        calls = []
        def api(command):
            calls.append(command)
            if command == "ping": return ping()
            if command == "bus/create": return {"busId": 7}
            if command.startswith("bus/7/add"): return {"busId": 7, "devId": "3", "type": "dualsensegamepadv5"}
            if command == "bus/remove 7": return {"busId": 7}
            self.fail("unowned command")
        own = relay.Ownership(api); own.create()
        self.assertEqual((own.bus_id, own.device_id), (7, 3))
        self.assertEqual(own.remove(), "removed")
        self.assertEqual(calls, ["ping", "bus/create", 'bus/7/add {"type":"dualsensegamepadv5"}', "bus/remove 7"])
        def missing(_): raise relay.APIError(404)
        own.request = missing
        self.assertEqual(own.remove(), "already_absent")

    def test_uncertain_create_and_add_failure_cleanup(self):
        def unknown(command):
            if command == "ping": return ping()
            raise OSError("lost response")
        own = relay.Ownership(unknown)
        with self.assertRaises(OSError): own.create()
        with self.assertRaisesRegex(relay.RelayError, "bus_creation_uncertain"): own.remove()
        def wrong(command):
            if command == "ping": return ping()
            if command == "bus/create": return {"busId": 9}
            if command == "bus/remove 9": return {"busId": 9}
            return {"busId": 10, "devId": "1", "type": "dualsensegamepadv5"}
        own = relay.Ownership(wrong)
        with self.assertRaises(relay.RelayError): own.create()
        self.assertEqual(own.remove(), "removed")
        own.request = lambda _: {"busId": 10}
        with self.assertRaisesRegex(relay.RelayError, "removed_bus_identity"): own.remove()

    def test_invalid_ids_never_coerce_boolean_or_wrong_persona(self):
        for value in [True, False, 0, -1, 2**32, "1", None]:
            with self.assertRaises(relay.RelayError): relay.uint32(value, "id")


class FakeSocket:
    def __init__(self, received=None):
        self.received = list(received or [])
        self.sent = []
        self.closed = False
        self.bound = None
        self.peer = None
    def bind(self, address): self.bound = address
    def listen(self, backlog): self.backlog = backlog
    def setblocking(self, value): pass
    def settimeout(self, value): pass
    def setsockopt(self, *args): pass
    def sendall(self, data): self.sent.append(data)
    def send(self, data): self.sent.append(data); return len(data)
    def recv(self, count): return self.received.pop(0)
    def accept(self): return self.peer, ("127.0.0.1", 50000)
    def close(self): self.closed = True
    def __enter__(self): return self
    def __exit__(self, *args): self.close()


class APITests(unittest.TestCase):
    def test_management_nul_command_eof_and_loopback_target(self):
        sock = FakeSocket([b'{"busId":', b'7}', b''])
        with patch.object(relay.socket, "create_connection", return_value=sock) as connect, \
             patch.object(relay.time, "monotonic", return_value=0):
            self.assertEqual(relay.request("bus/create"), {"busId": 7})
        connect.assert_called_once_with(("127.0.0.1", 33742), timeout=1.5)
        self.assertEqual(sock.sent, [b"bus/create\0"])
        self.assertTrue(sock.closed)

    def test_management_size_json_and_status_rejection(self):
        for chunks, error in [([b"x" * 16385], "api_response_limit"), ([b"[]", b""], "api_object"),
                              ([b"bad", b""], "api_json"), ([b"\xff", b""], "api_json"),
                              ([b'{"status":404}', b""], "api_status_404")]:
            sock = FakeSocket(chunks)
            with patch.object(relay.socket, "create_connection", return_value=sock), \
                 patch.object(relay.time, "monotonic", return_value=0), \
                 self.assertRaisesRegex(relay.RelayError, error): relay.request("ping")
            self.assertTrue(sock.closed)

    def test_management_final_read_cannot_reset_absolute_deadline(self):
        sock = FakeSocket([b"{}"])
        with patch.object(relay.socket, "create_connection", return_value=sock), \
             patch.object(relay.time, "monotonic", side_effect=[0, 0, 2]), \
             self.assertRaisesRegex(relay.RelayError, "api_deadline"): relay.request("ping")
        self.assertTrue(sock.closed)


class RuntimeTests(unittest.TestCase):
    def run_fake(self, frontend_data, expected_reason, fail_cleanup=False):
        listener, owner, frontend = FakeSocket(), FakeSocket(), FakeSocket([frontend_data])
        listener.peer = frontend
        commands = []
        clock = [0.0]
        class Selector:
            def __init__(self): self.events = ["accept", "input"]
            def register(self, *args): pass
            def unregister(self, *args): pass
            def close(self): pass
            def select(self, timeout):
                clock[0] += .008
                return [(SimpleNamespace(data=self.events.pop(0)), 1)]
        def api(command):
            commands.append(command)
            if command == "ping": return ping()
            if command == "bus/create": return {"busId": 42}
            if command.startswith("bus/42/add"): return {"busId": 42, "devId": "5", "type": "dualsensegamepadv5"}
            if command == "bus/remove 42":
                self.assertTrue(owner.closed)
                if fail_cleanup: raise OSError("cleanup unavailable")
                return {"busId": 42}
            self.fail("unexpected command")
        with tempfile.TemporaryDirectory() as directory:
            receipt = Path(directory) / "receipt.json"
            args = SimpleNamespace(receipt=str(receipt), wait_seconds=120, session_seconds=7200, stop_file=None)
            with patch.object(relay, "request", api), patch.object(relay.socket, "socket", return_value=listener), \
                 patch.object(relay.socket, "create_connection", return_value=owner), \
                 patch.object(relay.selectors, "DefaultSelector", Selector), \
                 patch.object(relay.time, "monotonic", side_effect=lambda: clock[0]):
                result = relay.run(args)
            record = json.loads(receipt.read_text())
        self.assertEqual(listener.bound, ("127.0.0.1", 33743))
        self.assertEqual(listener.backlog, 1)
        self.assertEqual(frontend.sent, [relay.GREETING])
        self.assertEqual(owner.sent[0], b"bus/42/5\0")
        self.assertTrue(all(packet[16:] == relay.NEUTRAL for packet in owner.sent[1:]))
        self.assertTrue(listener.closed and owner.closed and frontend.closed)
        self.assertEqual(commands[-1], "bus/remove 42")
        self.assertEqual(record["reason"], expected_reason)
        self.assertEqual(record["cleanupConfirmed"], not fail_cleanup)
        self.assertEqual(record["cleanup"], "unconfirmed" if fail_cleanup else "removed")
        self.assertTrue(record["guestIdentityVerified"] and record["frontendConnected"])
        self.assertNotIn("payload", record)
        self.assertEqual(result, 0 if expected_reason == "frontend_eof" and not fail_cleanup else 1)

    def test_runtime_eof_owns_one_bus_and_sends_greeting(self): self.run_fake(b"", "frontend_eof")
    def test_runtime_bad_crc_closes_owner_before_remove(self):
        data = bytearray(relay.encode(relay.NEUTRAL, 0)); data[12] ^= 1
        self.run_fake(bytes(data), "frame_crc")
    def test_runtime_cleanup_failure_stays_unconfirmed(self): self.run_fake(b"", "frontend_eof", fail_cleanup=True)
    def test_partial_output_fails_without_retry(self):
        sock = FakeSocket()
        sock.send = lambda _: 10
        with self.assertRaisesRegex(relay.RelayError, "partial_write"): relay.send_complete(sock, bytes(49))


if __name__ == "__main__":
    unittest.main(verbosity=2)
