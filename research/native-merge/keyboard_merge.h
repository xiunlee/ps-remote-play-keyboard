#ifndef PSKB_KEYBOARD_MERGE_H
#define PSKB_KEYBOARD_MERGE_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define PSKB_MERGE_CALL __cdecl
#if defined(PSKB_MERGE_EXPORTS)
#define PSKB_MERGE_API __declspec(dllexport)
#else
#define PSKB_MERGE_API
#endif
#else
#define PSKB_MERGE_CALL
#define PSKB_MERGE_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define PSKB_DS4_USB_REPORT_SIZE 64u

/* These are held actions supplied by a caller, not Windows virtual-key codes.
 * W/A/S/D identify the default left-stick mapping; other bindings are chosen
 * by the caller. No keyboard, device or process access occurs in this module. */
enum PskbKeyAction {
    PSKB_KEY_W        = 1u << 0,
    PSKB_KEY_S        = 1u << 1,
    PSKB_KEY_A        = 1u << 2,
    PSKB_KEY_D        = 1u << 3,
    PSKB_DPAD_UP      = 1u << 4,
    PSKB_DPAD_DOWN    = 1u << 5,
    PSKB_DPAD_LEFT    = 1u << 6,
    PSKB_DPAD_RIGHT   = 1u << 7,
    PSKB_CROSS        = 1u << 8,
    PSKB_CIRCLE       = 1u << 9,
    PSKB_SQUARE       = 1u << 10,
    PSKB_TRIANGLE     = 1u << 11,
    PSKB_L1           = 1u << 12,
    PSKB_R1           = 1u << 13,
    PSKB_L2           = 1u << 14,
    PSKB_R2           = 1u << 15,
    PSKB_SHARE        = 1u << 16,
    PSKB_OPTIONS      = 1u << 17,
    PSKB_L3           = 1u << 18,
    PSKB_R3           = 1u << 19,
    PSKB_PS           = 1u << 20,
    PSKB_TOUCH_CLICK  = 1u << 21
};

#define PSKB_ALL_KEY_ACTIONS ((UINT32_C(1) << 22) - 1u)

enum PskbMergeResult {
    PSKB_MERGE_OK = 0,
    PSKB_MERGE_INVALID_ARGUMENT = 1,
    PSKB_MERGE_UNSUPPORTED_REPORT = 2,
    PSKB_MERGE_INVALID_KEYS = 3
};

typedef struct PskbKeyboard {
    uint32_t held;
    int enabled; /* exactly 0 or 1; disabled returns the physical sample */
} PskbKeyboard;

/* Every call must receive a fresh, unmodified physical USB report, never the
 * previous merged result. Output is changed only after all validation passes.
 * Source/output may overlap. Both lengths must be exactly 64. Right-stick
 * bytes are always preserved; this engine has no mouse mapping.
 * A non-neutral keyboard direction overrides the physical left stick / D-pad;
 * cancelling opposing keys return control to the physical input. */
PSKB_MERGE_API enum PskbMergeResult PSKB_MERGE_CALL pskb_merge_keyboard(
    const uint8_t *source, size_t source_length,
    const PskbKeyboard *keyboard,
    uint8_t *output, size_t output_length);

#ifdef __cplusplus
}
#endif
#endif
