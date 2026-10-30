// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Read-only GameController recognition snapshot. Does not inspect any button,
// axis, touch, motion, haptics, player index or physical input state. Does not
// request wireless discovery, open/seize a device, create devices or access Sony.
// Official local SDK contracts: GCController.h, GCDevice.h, GCProductCategories.h.
import Foundation
import GameController

private struct ControllerMetadata: Encodable {
    let vendorName: String?
    let productCategory: String
    let hasExtendedGamepad: Bool
    let isDualSenseCategory: Bool
}

private struct MetadataSnapshot: Encodable {
    let schema = 1
    let enumerationWaitMilliseconds = 1000
    let inputValuesRead = false
    let wirelessDiscoveryRequested = false
    let manualDeviceOpenRequested = false
    let controllers: [ControllerMetadata]
}

@main
struct GameControllerMetadataInspector {
    @MainActor
    static func main() throws {
        // Prime only the existing-controller enumeration, then allow its local
        // connection notifications to settle. Never start wireless discovery.
        _ = GCController.controllers()
        RunLoop.current.run(until: Date().addingTimeInterval(1))
        let metadata = GCController.controllers().map { controller in
            ControllerMetadata(
                vendorName: controller.vendorName,
                productCategory: controller.productCategory,
                hasExtendedGamepad: controller.extendedGamepad != nil,
                isDualSenseCategory: controller.productCategory == GCProductCategoryDualSense)
        }
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let output = try encoder.encode(MetadataSnapshot(controllers: metadata))
        print(String(decoding: output, as: UTF8.self))
    }
}
