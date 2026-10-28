#include "keyboard_merge.h"
#include <stdio.h>
#include <string.h>

static unsigned checks, failures;
#define CHECK(condition, label) do { ++checks; if (!(condition)) { \
    ++failures; printf("FAIL line=%d %s\n", __LINE__, label); } } while (0)

static void physical(uint8_t report[64])
{
    unsigned i;
    for (i = 0; i < 64; ++i) report[i] = (uint8_t)(i * 7 + 3);
    report[0] = 1;
    report[1] = 17; report[2] = 229;
    report[3] = 71; report[4] = 201;
    report[5] = 0x16; /* square + physical D-pad left */
    report[6] = 0x81; /* R3 + L1 */
    report[7] = 0xac; /* counter bits */
    report[8] = 35; report[9] = 143;
}

static enum PskbMergeResult merge(const uint8_t *src, uint32_t held,
                                   int enabled, uint8_t *out)
{
    PskbKeyboard keys = {held, enabled};
    return pskb_merge_keyboard(src, 64, &keys, out, 64);
}

static void check_preserved(const uint8_t *src, const uint8_t *out)
{
    CHECK(out[0] == src[0], "report identifier preserved");
    CHECK(out[3] == src[3] && out[4] == src[4], "physical right stick preserved");
    CHECK((out[7] & 0xfc) == (src[7] & 0xfc), "counter bits preserved");
    CHECK(memcmp(src + 10, out + 10, 54) == 0, "sensor/touch/status bytes preserved");
}

static void single_buttons(void)
{
    static const struct { uint32_t key; unsigned offset; uint8_t mask; } cases[] = {
        {PSKB_CROSS,5,0x20}, {PSKB_CIRCLE,5,0x40}, {PSKB_SQUARE,5,0x10},
        {PSKB_TRIANGLE,5,0x80}, {PSKB_L1,6,1}, {PSKB_R1,6,2},
        {PSKB_L2,6,4}, {PSKB_R2,6,8}, {PSKB_SHARE,6,0x10},
        {PSKB_OPTIONS,6,0x20}, {PSKB_L3,6,0x40}, {PSKB_R3,6,0x80},
        {PSKB_PS,7,1}, {PSKB_TOUCH_CLICK,7,2}
    };
    uint8_t src[64], out[64], expected[64];
    unsigned i;
    physical(src); src[5] = 8; src[6] = 0; src[7] &= 0xfc;
    for (i = 0; i < sizeof(cases)/sizeof(cases[0]); ++i) {
        memcpy(expected, src, 64);
        expected[cases[i].offset] |= cases[i].mask;
        if (cases[i].key == PSKB_L2) expected[8] = 255;
        if (cases[i].key == PSKB_R2) expected[9] = 255;
        CHECK(merge(src, cases[i].key, 1, out) == PSKB_MERGE_OK, "single action accepted");
        CHECK(memcmp(out, expected, 64) == 0, "single action changes only expected bits");
    }
}

static void directions(void)
{
    /* Index is W,S,A,D bitset. Explicit expected pairs catch wrong signs,
     * opposing-key priority mistakes and excessively strong diagonals. */
    static const uint8_t axes[16][2] = {
        {17,229}, {128,0}, {128,255}, {17,229},
        {0,128}, {38,38}, {38,218}, {0,128},
        {255,128}, {218,38}, {218,218}, {255,128},
        {17,229}, {128,0}, {128,255}, {17,229}
    };
    static const uint8_t hats[16] = {6,0,4,6,6,7,5,6,2,1,3,2,6,0,4,6};
    uint8_t src[64], out[64]; unsigned i;
    physical(src);
    for (i = 0; i < 16; ++i) {
        CHECK(merge(src, i, 1, out) == PSKB_MERGE_OK, "WASD combination accepted");
        CHECK(out[1] == axes[i][0] && out[2] == axes[i][1], "normalized WASD or physical fallback");
        CHECK(out[5] == src[5], "WASD does not change D-pad or face buttons");
        check_preserved(src, out);
        CHECK(merge(src, i << 4, 1, out) == PSKB_MERGE_OK, "D-pad combination accepted");
        CHECK(out[5] == (uint8_t)(0x10 | hats[i]), "D-pad hat and neutral physical fallback");
        CHECK(out[1] == src[1] && out[2] == src[2], "D-pad does not change left stick");
    }
}

static void rejection(void)
{
    uint8_t src[64], out[64], untouched[64]; PskbKeyboard keys = {0,1};
    unsigned i;
    physical(src); memset(out, 0xcd, sizeof(out)); memcpy(untouched, out, sizeof(out));
    CHECK(pskb_merge_keyboard(NULL,64,&keys,out,64) == PSKB_MERGE_INVALID_ARGUMENT, "null source rejected");
    CHECK(pskb_merge_keyboard(src,64,NULL,out,64) == PSKB_MERGE_INVALID_ARGUMENT, "null key state rejected");
    CHECK(pskb_merge_keyboard(src,64,&keys,NULL,64) == PSKB_MERGE_INVALID_ARGUMENT, "null output rejected");
    for (i = 0; i < 80; ++i) if (i != 64) {
        CHECK(pskb_merge_keyboard(src,i,&keys,out,64) == PSKB_MERGE_UNSUPPORTED_REPORT, "wrong input length rejected");
        CHECK(pskb_merge_keyboard(src,64,&keys,out,i) == PSKB_MERGE_INVALID_ARGUMENT, "wrong output length rejected");
    }
    for (i = 0; i < 256; ++i) if (i != 1) {
        src[0] = (uint8_t)i;
        CHECK(pskb_merge_keyboard(src,64,&keys,out,64) == PSKB_MERGE_UNSUPPORTED_REPORT, "other report identifier rejected");
    }
    src[0] = 1;
    for (i = 9; i < 16; ++i) {
        src[5] = (uint8_t)(0x10 | i);
        CHECK(pskb_merge_keyboard(src,64,&keys,out,64) == PSKB_MERGE_UNSUPPORTED_REPORT, "reserved physical hat rejected");
    }
    src[5] = 8; keys.held = UINT32_C(1) << 22;
    CHECK(pskb_merge_keyboard(src,64,&keys,out,64) == PSKB_MERGE_INVALID_KEYS, "unknown key mask rejected");
    keys.held = 0; keys.enabled = 2;
    CHECK(pskb_merge_keyboard(src,64,&keys,out,64) == PSKB_MERGE_INVALID_ARGUMENT, "invalid enabled value rejected");
    CHECK(memcmp(out, untouched, 64) == 0, "all rejection paths leave destination unchanged");
}

static void lifecycle_and_invariants(void)
{
    uint8_t src[64], pristine[64], out[64], overlap[65];
    uint32_t rng = UINT32_C(0x41d28477); unsigned i, j;
    physical(src); memcpy(pristine, src, 64);
    CHECK(merge(src, PSKB_CROSS, 1, out) == PSKB_MERGE_OK && out[5] == 0x36, "first acceptance target: cross overlays physical square and hat");
    CHECK(merge(src, 0, 1, out) == PSKB_MERGE_OK && memcmp(src,out,64) == 0, "released keyboard restores current physical report");
    CHECK(merge(src, PSKB_ALL_KEY_ACTIONS, 0, out) == PSKB_MERGE_OK && memcmp(src,out,64) == 0, "disable discards all synthetic input");
    CHECK(merge(src, PSKB_ALL_KEY_ACTIONS, 1, out) == PSKB_MERGE_OK, "all actions accepted");
    CHECK(out[5] == 0xf6 && out[6] == 0xff && out[7] == 0xaf, "physical and synthetic button union");
    CHECK(out[8] == 255 && out[9] == 255, "digital triggers use full analog pressure");
    CHECK(memcmp(src, pristine, 64) == 0, "source immutable with distinct destination");
    memcpy(out,src,64); CHECK(merge(out,0,1,out) == PSKB_MERGE_OK && memcmp(out,src,64) == 0, "in-place fresh report supported");
    memcpy(overlap,src,64); CHECK(merge(overlap,PSKB_CROSS,1,overlap+1) == PSKB_MERGE_OK && overlap[6] == 0x36, "partially overlapping output supported");
    for (i = 0; i < 512; ++i) {
        uint32_t actions;
        for (j = 0; j < 64; ++j) { rng = rng * UINT32_C(1664525) + UINT32_C(1013904223); src[j] = (uint8_t)(rng >> 24); }
        src[0] = 1; src[5] = (uint8_t)((src[5] & 0xf0) | (i % 9));
        memcpy(pristine,src,64); actions = rng & PSKB_ALL_KEY_ACTIONS;
        CHECK(merge(src,actions,1,out) == PSKB_MERGE_OK, "varied report accepted");
        check_preserved(src,out);
        CHECK((out[5] & 0xf0 & src[5]) == (src[5] & 0xf0), "physical face button holds retained");
        CHECK((out[6] & src[6]) == src[6] && (out[7] & src[7]) == src[7], "physical other button holds retained");
        CHECK(out[8] >= src[8] && out[9] >= src[9], "physical trigger pressure never reduced");
        CHECK(memcmp(src,pristine,64) == 0, "varied sources never mutated");
        CHECK(merge(src,0,1,out) == PSKB_MERGE_OK && memcmp(out,src,64) == 0, "neutral is byte-exact identity for varied reports");
    }
}

int main(void)
{
    single_buttons(); directions(); rejection(); lifecycle_and_invariants();
    printf("KEYBOARD_MERGE_TEST checks=%u failures=%u; synthetic reports only; no device or Remote Play access\n", checks, failures);
    return failures ? 1 : 0;
}
