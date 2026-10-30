#!/bin/sh
# Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/../../.." && pwd)
output=${1:?Pass a new output directory (normally under repo/.local/).}
if [ -e "$output" ]; then
  echo 'Refusing to overwrite an existing output directory.' >&2
  exit 1
fi
mkdir -p "$output"
swiftc -parse-as-library -swift-version 6 -target arm64-apple-macos15.0 \
  -module-cache-path "$output/module-cache" \
  "$repo/research/macos-handoff/probe/GenericGamepadProbe.swift" \
  -o "$output/GenericGamepadProbe"
"$output/GenericGamepadProbe" --self-test
"$output/GenericGamepadProbe" --dry-run
# No custom entitlement, provisioning, installation, or live invocation.
