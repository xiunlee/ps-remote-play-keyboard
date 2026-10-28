#ifndef PSKB_SHARED_H
#define PSKB_SHARED_H
#include <windows.h>
#include <stdint.h>

#define PSKB_MAGIC 0x504B4231u
#define PSKB_VERSION 1u
#define PSKB_MAX_HANDLES 8
#define PSKB_COUNT 16

enum PskbCounter {
    CNT_NT_READ, CNT_NT_SUCCESS, CNT_NT_PENDING, CNT_NT_FAILURE,
    CNT_READFILE, CNT_OVERLAPPED, CNT_IOCP, CNT_HID_INPUT,
    CNT_HIDP_PARSE, CNT_VALID_REPORT, CNT_MERGED, CNT_OTHER_COMPLETION,
    CNT_READFILE_EX, CNT_WAIT, CNT_HOOK_COUNT, CNT_RESERVED
};

/* Only controller handles independently identified as VID054C/PID05C4 belong
 * in this allowlist. The test host may use an ordinary test file solely when
 * its process filename is pskb-probe-test.exe and test_mode is 1.
 * command: 0 observe, 1 merge (reserved until implemented), 2 stop.
 * state: 0 uninitialized, 1 initializing, 2 observing, 3 failed, 4 stopped.
 * heartbeat is GetTickCount64(), not a wall-clock timestamp.
 * No input report payload, account data, or general file contents are logged.
 */
typedef struct PskbShared {
    uint32_t magic, version, size, target_pid;
    volatile LONG command, state, error;
    uint32_t reserved;
    volatile LONG64 heartbeat;
    uint64_t handles[PSKB_MAX_HANDLES];
    uint32_t handle_mode[PSKB_MAX_HANDLES];
    uint32_t handle_count, test_mode;
    volatile LONG counts[PSKB_COUNT];
    uint64_t last_caller[8];
    uint32_t last_nt_status, report_len;
    /* byte0 flags(left/right/dpad),1..4 axes,5 dpad,6 face,7 other,
       8 special,9 L2,10 R2; remaining bytes reserved. */
    unsigned char overlay[16];
    volatile LONG overlay_sequence;
    uint32_t padding;
} PskbShared;

typedef char PskbSharedSizeCheck[(sizeof(PskbShared) == 304) ? 1 : -1];
#endif
