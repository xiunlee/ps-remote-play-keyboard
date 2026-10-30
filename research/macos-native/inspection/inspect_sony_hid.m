// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Read-only metadata enumeration. No HID open/seize, reports, callbacks or TCC requests.
#import <Foundation/Foundation.h>
#import <IOKit/hid/IOHIDManager.h>
#import <IOKit/hid/IOHIDKeys.h>

static NSString *hex(NSData *data) {
    NSMutableString *s = [NSMutableString stringWithCapacity:data.length * 2];
    const unsigned char *b = data.bytes;
    for (NSUInteger i = 0; i < data.length; ++i) [s appendFormat:@"%02x", b[i]];
    return s;
}

int main(void) {
    @autoreleasepool {
        IOHIDManagerRef manager = IOHIDManagerCreate(kCFAllocatorDefault, kIOHIDOptionsTypeNone);
        if (!manager) return 2;
        // Match only Sony Generic Desktop gamepads. Never enumerate typing devices.
        NSDictionary *matching = @{@kIOHIDVendorIDKey: @0x054c,
                                   @kIOHIDDeviceUsagePageKey: @1,
                                   @kIOHIDDeviceUsageKey: @5};
        IOHIDManagerSetDeviceMatching(manager, (__bridge CFDictionaryRef)matching);
        CFSetRef found = IOHIDManagerCopyDevices(manager);
        NSMutableArray *devices = [NSMutableArray array];
        const char *keys[] = { kIOHIDVendorIDKey, kIOHIDProductIDKey, kIOHIDProductKey,
            kIOHIDTransportKey, kIOHIDPrimaryUsagePageKey, kIOHIDPrimaryUsageKey,
            kIOHIDMaxInputReportSizeKey, kIOHIDMaxOutputReportSizeKey,
            kIOHIDMaxFeatureReportSizeKey };
        if (found) {
            for (id item in (__bridge NSSet *)found) {
                IOHIDDeviceRef device = (__bridge IOHIDDeviceRef)item;
                NSMutableDictionary *row = [NSMutableDictionary dictionary];
                for (size_t i = 0; i < sizeof(keys) / sizeof(keys[0]); ++i) {
                    NSString *key = [NSString stringWithUTF8String:keys[i]];
                    CFTypeRef value = IOHIDDeviceGetProperty(device, (__bridge CFStringRef)key);
                    if (value && (CFGetTypeID(value) == CFStringGetTypeID() ||
                                  CFGetTypeID(value) == CFNumberGetTypeID())) {
                        row[key] = (__bridge id)value;
                    }
                }
                CFTypeRef descriptor = IOHIDDeviceGetProperty(device, CFSTR(kIOHIDReportDescriptorKey));
                if (descriptor && CFGetTypeID(descriptor) == CFDataGetTypeID()) {
                    NSData *data = (__bridge NSData *)descriptor;
                    row[@"descriptor_bytes"] = @(data.length);
                    row[@"descriptor_hex"] = hex(data);
                }
                [devices addObject:row];
            }
            CFRelease(found);
        }
        CFRelease(manager);
        NSDictionary *output = @{@"schemaVersion": @1, @"metadataOnly": @YES,
            @"openedDevices": @NO, @"capturedReports": @NO, @"devices": devices};
        NSError *error = nil;
        NSData *json = [NSJSONSerialization dataWithJSONObject:output
            options:NSJSONWritingPrettyPrinted | NSJSONWritingSortedKeys error:&error];
        if (!json) return 3;
        fwrite(json.bytes, 1, json.length, stdout);
        fputc('\n', stdout);
        return 0;
    }
}
