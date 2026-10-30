// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Probe only: creates an inactive object, never activates or sends input.
#import <Foundation/Foundation.h>
#import <IOKit/hidsystem/IOHIDUserDevice.h>

int main(int argc, const char *argv[]) { @autoreleasepool {
    if (argc != 2) return 64;
    NSString *hex = [NSString stringWithContentsOfFile:@(argv[1])
                         encoding:NSUTF8StringEncoding error:nil];
    if (!hex) return 65;
    hex = [hex stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet];
    if (hex.length != 578) return 65;
    NSMutableData *descriptor = [NSMutableData data];
    for (NSUInteger index = 0; index < hex.length; index += 2) {
        NSScanner *scanner = [NSScanner scannerWithString:[hex substringWithRange:NSMakeRange(index, 2)]];
        unsigned int value = 0;
        if (![scanner scanHexInt:&value] || !scanner.isAtEnd || value > 255) return 65;
        uint8_t byte = (uint8_t)value; [descriptor appendBytes:&byte length:1];
    }
    if (descriptor.length != 289) return 65;
    NSDictionary *properties = @{
        @kIOHIDReportDescriptorKey: descriptor,
        @kIOHIDVendorIDKey: @0x054c, @kIOHIDProductIDKey: @0x0ce6,
        @kIOHIDProductKey: @"PSKeyboard DualSense inactive authorization probe",
        @kIOHIDTransportKey: @"USB"
    };
    IOHIDUserDeviceRef device = IOHIDUserDeviceCreateWithProperties(
        kCFAllocatorDefault, (__bridge CFDictionaryRef)properties,
        IOHIDUserDeviceOptionsCreateOnActivate);
    printf("{\"objectCreated\":%s,\"activated\":false,\"reportsSent\":0}\n",
           device ? "true" : "false");
    // No dispatch queue was set and activation was deliberately never called.
    if (device) CFRelease(device);
    return device ? 0 : 2;
} }
