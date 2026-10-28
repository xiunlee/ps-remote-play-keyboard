# reWASD API and physical DS4 output

Research date: 2026-10-28. No local reWASD API requests, driver installation, account login, or input remapping were performed for this note. The official 9.5.0 installer was later read as data to extract its built-in profiles; neither the installer nor its embedded assemblies were executed.

The promising route is reWASD's **Apply to physical device** mode with a group containing one USB DualShock 4, one keyboard, and one mouse. The vendor documents that all group members can send mappings through the selected physical gamepad. This mode does not create a virtual gamepad. It is a plausible route for Sony Remote Play 9.5; this machine and Remote Play version have **not** been validated with it.

## What the official API can and cannot do

The [official C# client](https://github.com/reWASD/reWASDClient) requires reWASD 9.2 or newer. Its current main branch uses `http://127.0.0.1:35474` and protocol `v2.3`, whereas its linked [Swagger document](https://api.swaggerhub.com/apis/disk-soft/re-wasd_api/v2.0) only describes `v2.0` and was last modified in 2024. Read `/Version` first and compare the result with the implemented protocol; do not silently guess or treat v2.0 as the current version.

The official source and Swagger confirm these operations:

| Operation | What it provides |
| --- | --- |
| `GET /Version` | Protocol version, e.g. `{"Version":"v2.3"}`; example only, not a local response |
| `GET /v2.3/ControllerService/Controllers` | Device GUIDs, types, IDs, online state, current remap state |
| `POST /v2.3/ControllerService/CompositeDevices` | Group devices by their GUIDs |
| `POST /v2.3/GameService/Configs` | New config name, game ID, and virtual gamepad type only |
| `POST /v2.3/GameService/ImportConfig` | Import complete `.rewasd` configuration text |
| `GET /v2.3/GameService/Games/{id}/Configs` | Imported configuration's path and ID |
| `POST /v2.3/ControllerService/ApplyConfig` | Apply a config file path to a device/group and slot |
| `GET /v2.3/ControllerService/OutputGamepad/{guid}` | Check which physical or virtual controller is the output |
| `POST /v2.3/ControllerService/DisableRemap` | Release remapping for a device/group |

**The public API does not expose a per-key/per-axis config editor or a raw input injection endpoint.** `NewConfigParams` contains only `GameId`, `ConfigName`, and `VirtualGamepadType`. The current source has no extra physical selection or mappings field. `OutputGamepadInfo.PhysicalControllerId` is output status, not a writable selection endpoint. There is no API field named `PhysicalDeviceId` in these request DTOs.

Thus a new front end can manage and apply mappings, but it first needs a valid complete configuration with physical output selected. Merely creating an empty config through `NewConfigParams` is not a working keyboard/mouse mapper.

## Single practical route

1. In reWASD, group the connected USB DS4, the real keyboard, and the real mouse. Only one physical gamepad should be eligible in this group.
2. Use the built-in PS4 Remote Play keyboard/mouse template as a starting point. Choose the physical DS4 in **Output device settings**. Configure WASD as the left stick and ordinary mouse movement as the right stick. Preserve the template's unlock shortcut.
3. Save/export that actual config. Check that its physical output is retained. Do not fabricate undocumented JSON members to force this selection.
4. Our app can then import this complete config, apply it to the composite group, observe status, and disable it. It should reject a result with a virtual gamepad output when the user requested the physical route.
5. Validate DS4 HID input/`joy.cpl` changes while pressing keys/moving the mouse, then validate the official Remote Play window remains alive and the PS5 responds. These are separate checks; neither API success nor a Windows controller test alone proves Sony compatibility.

The vendor says a group with exactly one eligible physical controller chooses it by default. A previously saved virtual output selection can still exist; do not assume the generic template automatically turns physical after grouping.

## Request examples

The following JSON is a recipe with placeholders, **not** ready-to-apply local device IDs. Use strings for actual controller GUIDs returned by the API. Preserve 64-bit controller IDs exactly when comparing them in JavaScript; do not round them through Number if above the safe integer range.

Create the group (physical DS4 first):

```json
{
  "Guids": [
    "<DS4 GUID>",
    "<KEYBOARD GUID>",
    "<MOUSE GUID>"
  ]
}
```

After creating it, re-enumerate Controllers and locate the composite whose children match these devices. Do not assume its GUID is the first member's GUID. The official `CompositeControllerDto.Children` has four positions; missing children are null.

Import config:

```json
{
  "GameId": "<EXISTING OR CREATED GAME GUID>",
  "Name": "PS5 Keyboard Physical DS4",
  "Content": "<THE ENTIRE SAVED .rewasd FILE AS A JSON STRING>",
  "IsCloning": true,
  "RewriteExisting": false
}
```

The `Content` member is text, not a nested JSON object. Build it with a JSON serializer. This request returns import status/name, not the path; list that game's configs to obtain the new path.

Apply config:

```json
{
  "ControllerGuid": "<COMPOSITE GUID>",
  "Path": "<IMPORTED CONFIG PATH RETURNED BY API>",
  "Slot": "Slot1",
  "Bundle": null
}
```

Apply can return HTTP 200 with dialogs and `IsSucceded` false. Process the response instead of treating HTTP 200 as completion. Trial/license, invalid config, and unsupported-feature dialogs must not be silently accepted or downgraded. The API's documented continuation mechanism resends the request with `Bundle.UserActions` containing the corresponding actual choices.

Check output:

```text
GET /v2.3/ControllerService/OutputGamepad/<COMPOSITE GUID>
```

The official DTO initializes either a non-null `VirtualGamepadType`, or `PhysicalControllerId` with `VirtualGamepadType` null. Confirm the physical ID matches the selected DS4's ID. Do not rely on an empty device display name or a successful Apply response to infer output identity.

Disable:

```json
{
  "Guid": "<COMPOSITE GUID>"
}
```

## Configuration-format evidence and limits

An actual [vendor support attachment](https://forum.rewasd.com/filedata/fetch?id=235224) is readable JSON, identifies author `reWASD_team`, appVersion `6.4.0`, and schemaVersion `3.0`. It includes `devices.hardware`, `devices.virtual.gamepads`, `masks`, `commands`, and `mappings`. This confirms ImportConfig can carry a complete non-executable mapping document. Its version predates physical output (introduced in 7.1), so **it cannot establish the current physical-output JSON schema** and should not be used unchanged for this route.

The current public API DTOs do not document how the physical-output choice is serialized inside `.rewasd`. Subsequent read-only extraction of the official installer supplied a concrete field example, documented below. Import and output verification remain required; static JSON inspection does not prove physical output is active.

## Offline installer extraction and candidate preparation

The locally downloaded `reWASD950-11346.exe` was read with .NET `PEReader`, `MetadataReader`, and `ResourceReader.GetResourceData`. Raw ZIP payloads were extracted using `ZipFile`; none of the installer code or embedded product assemblies were loaded for execution. These local extracted vendor files and the candidate were kept outside the public repository.

The included profiles supply these real examples:

| Bundled profile | appVersion | Relevant fields |
| --- | --- | --- |
| PS4 Remote Play (2023) / For Keyboard & Mouse | 6.5.0 | `devices.hardware[gamepad].convertToVirtual = true`; `devices.virtual.gamepads[0].type = "ds4"`; `virtualUsbHub = true` |
| Modke Mimic / WASD on Mimic | 9.3.3 | `devices.hardware[gamepad].applyToPhysicalPriority = true`; a virtual gamepad definition is still present |
| Fortnite / Double movement | 9.3.0 | `devices.hardware[gamepad].applyToPhysicalPriority = false` |

A **static-only, not imported or applied** candidate was made locally from the bundled PS4 keyboard/mouse profile using only these changes:

1. Add `applyToPhysicalPriority: true` to its hardware gamepad (ID 1).
2. Remove `convertToVirtual` from that hardware gamepad and `virtualUsbHub` from the DS4 gamepad definition. Preserve the DS4 definition and other ordinary mappings; the physical-priority example demonstrates that the mere presence of `devices.virtual.gamepads` does not by itself prove virtual output.
3. Remove the four special high-movement mouse mappings using relative-stick macros (`absolute: false`) because the vendor documents relative-stick mappings as unsupported for physical output. Remove their now-unused masks 114 and 115 and any references to those removed masks. Preserve normal mouse-to-right-stick mappings, which use absolute deflection.

The resulting local candidate has 110 mappings and 115 masks. JSON-reference inspection found zero missing mask references, zero missing command references, and zero remaining relative-stick macro mappings. No importer, driver, HID report, Remote Play, or PS5 check has been run against it.

Preserved controls are WASD to the left stick, mouse movement to the right stick, and **Left Windows key** to toggle between the normal mapping layer and layer 4 (desktop/unlocked input). The left-Windows bindings are actual mappings in the extracted template, not an inferred shortcut.

For review, the candidate SHA-256 is `2A47D30E46A30AEAC58BD4B01655CFEE9D062B2AC46EEBEDE195D49535E62D41`. The source files and candidate remain local; this note records the transformation without republishing the vendor's complete profiles.

Installer metadata contained `/s` and `/showgui` strings, but no command-line behavior was executed or established. The string `/install /quiet /norestart` appears alongside .NET SDK installer URLs and cannot establish that the reWASD installer itself honors those switches. Do not use it as proof of a supported no-restart installation command.

## Source links

- [Physical output, including keyboard + mouse + DS4 groups](https://help.rewasd.com/basic-functions/how-to-apply-config-to-physical-device.html)
- [Output selection and physical-device preference persisted in a config](https://help.rewasd.com/basic-functions/virtual-controller-settings.html)
- [Current official protocol constant](https://github.com/reWASD/reWASDClient/blob/main/reWASDProtocol/Constants.cs)
- [Current new-config request fields](https://github.com/reWASD/reWASDClient/blob/main/reWASDProtocol/DataTransferObjects/NewConfigParams.cs)
- [Import-config request fields](https://github.com/reWASD/reWASDClient/blob/main/reWASDProtocol/DataTransferObjects/ImportConfigInfo.cs)
- [OutputGamepadInfo source](https://github.com/reWASD/reWASDClient/blob/main/reWASDProtocol/Infrastructure/OutputGamepadInfo.cs)
- [API startup address and version check](https://github.com/reWASD/reWASDClient/blob/main/reWASDHttpClient/Client.cs)
- [Vendor support: physical mode remains a physical controller for PS Remote Play](https://forum.rewasd.com/forum/rewasd/technical-questions-aa/244059-ps-remote-play)
