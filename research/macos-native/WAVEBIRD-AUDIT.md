# WaveBird v0.9.0：已簽章 helper 可否接收鍵盤狀態

檢查日期：2026-10-30。範圍為公開 source 與 release metadata；未啟動 WaveBird、未執行其 Tools、未掃描／連線 Bluetooth、未存取帳號，也未變更正在使用的 Sony Remote Play 或實體 DualSense。

**結論：這個版本不能直接當本專案的外部虛擬 DualSense broker。** 它確實有 DualSense output，但 stock app 沒有公開 IPC、CLI、stdin、本機 socket、URL scheme 或其他接受按鍵／搖桿狀態的入口。它也沒有鍵盤或任意標準 HID controller 的輸入 adapter；目前接著的 USB DualSense 不能用來替它提供輸入。此結論根據啟動註冊、輸入資料流、mapping 與 HID callback 的實作，而非只根據產品的 Switch 2 定位。

## 固定版本與可重現範圍

| 項目 | 值 |
|---|---|
| 官方 repo | [murphyjt/wavebird](https://github.com/murphyjt/wavebird) |
| release | [v0.9.0](https://github.com/murphyjt/wavebird/releases/tag/v0.9.0)，2026-08-31 發布 |
| annotated tag object | `704985998e72ffef10c8b5c0dd4b7d404b8ba31d` |
| resolved source commit | `b04a072f1b17df58965efb14a066873a0b48da2a` |
| 本機唯讀 source checkout | `.local/wavebird-source-0.9.0`；`git status --short` 無修改 |
| release asset | `WaveBird-0.9.0.dmg`，2,722,972 bytes |
| GitHub release 公布的 asset SHA-256 | `164b72bf44670371e91dd3b949267dfa440be1c6ccdaeed0fbed6c525229f3d4` |
| license | [MIT](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/LICENSE) |

DMG 的 size/hash 為 GitHub release API metadata，**不是本機下載計算或簽章驗證結果**。本輪未下載／掛載 DMG；缺少輸入介面已可由 source 確認，因此沒有為了驗簽而安裝或啟動它。以下結論限定該 commit／release，不涵蓋未來版本，也不宣稱 source 與發布 binary 已做可重現建置比對。

## 輸入如何進入 DualSense output

1. [`AppDelegate.swift:6`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/AppDelegate.swift#L6) 只建立 GameCube、Switch 2 Pro、Joy-Con L/R 四類 profile，並只註冊 `BLETransport()`。啟動會要求 HID PostEvent 權限及自動開始掃描。沒有 Sony profile、keyboard transport 或通用 HID input transport。
2. [`BLETransport.swift:365`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Transports/BLE/BLETransport.swift#L365) 把已連線 Nintendo BLE characteristic notification 轉成 `.reportReceived`。[`BridgeCoordinator.swift:387`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator.swift#L387) 消費註冊 transport 的 event stream。
3. [`BridgeCoordinator+TransportEvents.swift:155`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BTransportEvents.swift#L155) 在實體裝置 `.ready` 後選 profile、建立該裝置的 virtual HID/session。[同檔案 `:274`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BTransportEvents.swift#L274) 只把已知 device record 的 raw report 餵進 process-local `AsyncStream`。
4. [同檔案 `:13`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BTransportEvents.swift#L13) 解析 Nintendo report、套 mapping、呼叫 `session.buildReport(state)`，最後以 [`VirtualHIDDevice.swift:58`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/HID/VirtualHIDDevice.swift#L58) 的 CoreHID `dispatchInputReport` 送出。這是程序內部呼叫，沒有可讓本專案跨程序使用的 dispatch endpoint。

因此不能只啟動 stock signed app，從我們的 `DualSenseUSBReport.encode(...)` 把 64-byte report 寫入它。也沒有不依賴已連線 Nintendo device record 的「建立空虛擬控制器」操作。

## 易混淆的入口已逐項核對

| 看似可用的能力 | 原始碼實際行為 | 對本專案的結果 |
|---|---|---|
| DualSense output | [`DualSenseOutputProfile.swift:17`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/HID/DualSenseOutputProfile.swift#L17) 設 VID `054c` / PID `0ce6`，從 `ControllerState` 建立 64-byte report | 有輸出 encoder；沒有外部輸入接口 |
| USB enum / `parseUSBReport` | [Pro `usbMatcher`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Profiles/ProControllerProfile.swift#L45)、[GameCube](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Profiles/GameCubeProfile.swift#L64)、[Joy-Con](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Profiles/JoyConProfile.swift#L66) 均為 `nil`；AppDelegate 只註冊 BLE | 不是可用的 USB controller 輸入 |
| 虛擬裝置 transport | [`hidTransport(for:)`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BOutputMode.swift#L283) 實際回傳 `.virtual`；鄰近 `.usb` 註解過時 | 輸出裝置屬性不能當成實體 USB 支援證據 |
| 自訂 mapping profile | [`MappingSource.swift:7`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Core/MappingSource.swift#L7) 只有實體 Nintendo button、左右 analog trigger；[`ButtonMapping.swift:8`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Core/ButtonMapping.swift#L8) 定義 Nintendo 按鍵集合 | 沒有 keycode、keyboard event、固定外部狀態來源 |
| JSON / UserDefaults | [`MappingProfileStore.swift:89`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Profiles/MappingProfileStore.swift#L89) 載入 mapping 設定；不是輸入報告佇列。AppDelegate 的 defaults observer 只更新 Dock 顯示 | 修改設定不會提供按放事件或建立可控制的 controller |
| HID SetReport | [`BridgeCoordinator+OutputMode.swift:218`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BOutputMode.swift#L218) 解析 output rumble 並傳給實體 Nintendo device；DualSense 的 `handleSetReport` 使用[空的預設實作](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/HID/HIDOutputProfile.swift#L153) | 對虛擬裝置發 output report 不能改變它的按鍵／搖桿 input |
| `Tools/` CLI | [`GameControllerProbe.swift`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/Tools/GameControllerProbe.swift#L1) 觀察 GameController；[`RumbleTest.swift`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/Tools/RumbleTest.swift#L1) 發 haptic；其餘為 dump/trace/probe | 不提供 state feed；[`app target`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird.xcodeproj/project.pbxproj#L111) 的 source group 只有 `WaveBird/`，未包含 `Tools/` |

另外檢查了 [`WaveBirdApp.swift`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/WaveBirdApp.swift) 的全部 scene 與 AppDelegate；未見 URL handler、scripting command 或文件式 input feed。以 `rg` 搜尋 `WaveBird/` 與 Xcode project 的 socket、stdin、NSXPC、NWListener、NWConnection、CFMessagePort、DistributedNotificationCenter、AppleEvent、onOpenURL、CFBundleURLTypes、CommandLine、NSEvent、CGEvent、GCKeyboard 等符號，沒有找到對應入口。`URLSession` 是 GitHub update checker；`FileHandle` 是 stderr 記錄；`NotificationCenter` 處理 app/window/defaults/sleep-wake，並非外部控制協定。負面搜尋只是輔助證據，上面的可到達資料流才是主要判斷依據。

## 已簽章發布的意義與下一步

上游 [README](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/README.md#L33) 聲稱 release 已簽章及 notarize。[release workflow](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/.github/workflows/release.yml#L52) 使用 Developer ID certificate、provisioning profile，build 後帶明確 entitlement 簽章、驗簽、送 notarization。其 [entitlements](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/WaveBird.entitlements#L5) 含 `com.apple.developer.hid.virtual.device`。這些是上游發布流程的證據；本輪未獨立驗證 DMG 的 certificate、notarization ticket 或執行能力。

可行的開發方向是由上游加入明確的本機 state-feed transport，以及不依賴實體 Nintendo 配對的 virtual-device lifecycle，然後由有授權的維護者發布新簽章版本。MIT source 也可用於自有實作，但修改 source／重編譯本身不會繼承上游 binary 的簽章及受限 entitlement 授權。**目前不把 WaveBird v0.9.0 列為可以立即交付的免費 helper。**

即使將來提供 feed，也還要獨立驗證 Sony Remote Play 接受度：上游測試清單為 Apple GameController、SDL 與 Web Gamepad，未見官方 Sony Remote Play 的相容性實測。DualSense profile 雖使用 Sony VID/PID，但 descriptor 是簡化的 input descriptor，feature handler 只對 `0x05` 提供 calibration；它不能單憑名稱／識別碼視為等價於實體 DualSense。這是待測條件，不是本輪宣稱 Sony 一定拒絕它。
