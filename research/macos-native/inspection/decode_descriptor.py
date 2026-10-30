#!/usr/bin/env python3
"""Parse HID report sizes/field metadata from a hex descriptor; no device access."""
import argparse
import json
from pathlib import Path


def decode(data):
    global_state = {"usage_page": 0, "report_id": 0, "size": 0, "count": 0}
    stack, local, fields, totals = [], {}, [], {}
    offset = 0
    while offset < len(data):
        prefix = data[offset]
        offset += 1
        if prefix == 0xfe:
            raise ValueError("long items are not supported by this bounded research parser")
        size = (0, 1, 2, 4)[prefix & 3]
        if offset + size > len(data):
            raise ValueError("truncated item")
        value = int.from_bytes(data[offset:offset + size], "little")
        offset += size
        kind, tag = (prefix >> 2) & 3, prefix >> 4
        if kind == 1:
            key = {0: "usage_page", 7: "size", 8: "report_id", 9: "count"}.get(tag)
            if key:
                global_state[key] = value
            elif tag == 10:
                stack.append(global_state.copy())
            elif tag == 11:
                if not stack:
                    raise ValueError("unbalanced global stack")
                global_state = stack.pop()
        elif kind == 2:
            if tag == 0:
                local.setdefault("usages", []).append(value)
            elif tag == 1:
                local["usage_min"] = value
            elif tag == 2:
                local["usage_max"] = value
        elif kind == 0:
            report_type = {8: "input", 9: "output", 11: "feature"}.get(tag)
            if report_type:
                key = (report_type, global_state["report_id"])
                start = totals.get(key, 0)
                fields.append({"type": report_type, **global_state,
                               "payload_bit_offset": start, "flags": value, **local})
                totals[key] = start + global_state["size"] * global_state["count"]
            local = {}
    if stack:
        raise ValueError("unbalanced global stack")
    reports = [{"type": key[0], "report_id": key[1], "payload_bits": bits,
                "payload_bytes": (bits + 7) // 8,
                "bytes_including_report_id": (bits + 7) // 8 + bool(key[1])}
               for key, bits in sorted(totals.items())]
    return {"reports": reports, "fields": fields}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("hex_file", type=Path)
    args = parser.parse_args()
    print(json.dumps(decode(bytes.fromhex(args.hex_file.read_text())), indent=2))
