// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// No-op: tests whether macOS permits entry into main. No HID or permission calls.
#include <stdio.h>
int main(void) { puts("SIGNING_SMOKE_MAIN_REACHED no_hid_calls=true"); return 0; }
