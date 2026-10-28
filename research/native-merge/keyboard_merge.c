#include "keyboard_merge.h"
#include <string.h>

static int held(uint32_t keys, uint32_t action)
{
    return (keys & action) != 0;
}

static uint8_t hat(int x, int y)
{
    if (y < 0) return (uint8_t)(x < 0 ? 7 : x > 0 ? 1 : 0);
    if (y > 0) return (uint8_t)(x < 0 ? 5 : x > 0 ? 3 : 4);
    return (uint8_t)(x < 0 ? 6 : x > 0 ? 2 : 8);
}

PSKB_MERGE_API enum PskbMergeResult PSKB_MERGE_CALL pskb_merge_keyboard(
    const uint8_t *source, size_t source_length,
    const PskbKeyboard *keyboard,
    uint8_t *output, size_t output_length)
{
    uint8_t merged[PSKB_DS4_USB_REPORT_SIZE];
    uint32_t keys;
    int x, y;

    if (!source || !keyboard || !output || output_length != sizeof(merged) ||
        (keyboard->enabled != 0 && keyboard->enabled != 1))
        return PSKB_MERGE_INVALID_ARGUMENT;
    if (source_length != sizeof(merged))
        return PSKB_MERGE_UNSUPPORTED_REPORT;
    if (source[0] != 0x01 || (source[5] & 0x0f) > 8)
        return PSKB_MERGE_UNSUPPORTED_REPORT;
    if (keyboard->held & ~PSKB_ALL_KEY_ACTIONS)
        return PSKB_MERGE_INVALID_KEYS;

    memcpy(merged, source, sizeof(merged));
    keys = keyboard->enabled ? keyboard->held : 0;
    x = held(keys, PSKB_KEY_D) - held(keys, PSKB_KEY_A);
    y = held(keys, PSKB_KEY_S) - held(keys, PSKB_KEY_W);
    if (x || y) {
        if (x && y) {
            /* Round 127.5 / sqrt(2) to 90: a diagonal has approximately
             * the same radial deflection as a cardinal direction. */
            merged[1] = (uint8_t)(128 + x * 90);
            merged[2] = (uint8_t)(128 + y * 90);
        } else {
            merged[1] = (uint8_t)(x < 0 ? 0 : x > 0 ? 255 : 128);
            merged[2] = (uint8_t)(y < 0 ? 0 : y > 0 ? 255 : 128);
        }
    }

    x = held(keys, PSKB_DPAD_RIGHT) - held(keys, PSKB_DPAD_LEFT);
    y = held(keys, PSKB_DPAD_DOWN) - held(keys, PSKB_DPAD_UP);
    if (x || y) merged[5] = (uint8_t)((merged[5] & 0xf0) | hat(x, y));

    if (keys & PSKB_SQUARE)   merged[5] |= 0x10;
    if (keys & PSKB_CROSS)    merged[5] |= 0x20;
    if (keys & PSKB_CIRCLE)   merged[5] |= 0x40;
    if (keys & PSKB_TRIANGLE) merged[5] |= 0x80;
    if (keys & PSKB_L1)       merged[6] |= 0x01;
    if (keys & PSKB_R1)       merged[6] |= 0x02;
    if (keys & PSKB_L2) { merged[6] |= 0x04; merged[8] = 255; }
    if (keys & PSKB_R2) { merged[6] |= 0x08; merged[9] = 255; }
    if (keys & PSKB_SHARE)    merged[6] |= 0x10;
    if (keys & PSKB_OPTIONS)  merged[6] |= 0x20;
    if (keys & PSKB_L3)       merged[6] |= 0x40;
    if (keys & PSKB_R3)       merged[6] |= 0x80;
    if (keys & PSKB_PS)       merged[7] |= 0x01;
    if (keys & PSKB_TOUCH_CLICK) merged[7] |= 0x02;

    memcpy(output, merged, sizeof(merged));
    return PSKB_MERGE_OK;
}
