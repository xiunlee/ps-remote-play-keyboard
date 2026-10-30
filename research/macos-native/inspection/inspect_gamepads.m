// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Metadata only. Does not open/seize/read reports or enumerate serial numbers.
#import <Foundation/Foundation.h>
#import <IOKit/hid/IOHIDManager.h>
#import <IOKit/hid/IOHIDKeys.h>
int main(void) { @autoreleasepool {
    IOHIDManagerRef manager = IOHIDManagerCreate(kCFAllocatorDefault, kIOHIDOptionsTypeNone);
    NSDictionary *match = @{@kIOHIDDeviceUsagePageKey:@1,@kIOHIDDeviceUsageKey:@5};
    IOHIDManagerSetDeviceMatching(manager, (__bridge CFDictionaryRef)match);
    NSSet *devices = CFBridgingRelease(IOHIDManagerCopyDevices(manager));
    NSMutableArray *rows = [NSMutableArray array];
    for (id object in devices) {
        IOHIDDeviceRef d = (__bridge IOHIDDeviceRef)object;
        NSMutableDictionary *row=[NSMutableDictionary dictionary];
        for (NSString *key in @[@kIOHIDProductKey,@kIOHIDVendorIDKey,@kIOHIDProductIDKey,@kIOHIDTransportKey,@kIOHIDPrimaryUsagePageKey,@kIOHIDPrimaryUsageKey]) {
            id value=(__bridge id)IOHIDDeviceGetProperty(d,(__bridge CFStringRef)key);
            if ([value isKindOfClass:[NSString class]] || [value isKindOfClass:[NSNumber class]]) row[key]=value;
        }
        uint64_t entry=0; IORegistryEntryGetRegistryEntryID(IOHIDDeviceGetService(d),&entry);
        row[@"registryEntry"] = @(entry);
        [rows addObject:row];
    }
    NSData *data=[NSJSONSerialization dataWithJSONObject:rows options:NSJSONWritingSortedKeys error:nil];
    fwrite(data.bytes,1,data.length,stdout);fputc('\n',stdout);CFRelease(manager);return 0;
} }
