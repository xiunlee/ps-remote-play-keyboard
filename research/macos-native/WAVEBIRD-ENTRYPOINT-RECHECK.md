# WaveBird v0.9.0：診斷／測試入口再核對

2026-10-30；固定 source [`b04a072f1b17df58965efb14a066873a0b48da2a`](https://github.com/murphyjt/wavebird/tree/b04a072f1b17df58965efb14a066873a0b48da2a)。此輪接續 [WAVEBIRD-AUDIT.md](WAVEBIRD-AUDIT.md)，只查「已發布程式是否另有可用的正式輸入入口」，沒有啟動 app、變更 defaults、建立 HID、掃描 Bluetooth 或操作 Sony Remote Play。上游 Releases API 最新仍為 v0.9.0。

**沒有找到可讓本專案鍵盤狀態進入 stock WaveBird 的官方診斷／測試路徑。** 它有真正的 DualSense output codec，但可達到該 codec 的輸入仍受實體 Nintendo BLE 資料流限制。以下逐項排除的是這個公開 source 版本；不是對其他程式或未來版本的通論。

## 所有會發布 input 的路徑

1. [`AppDelegate.swift:6`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/AppDelegate.swift#L6) 固定註冊 GameCube／Switch 2 Pro／Joy-Con L/R profiles，唯一 transport 為 `BLETransport()`。[`BridgeCoordinator.start():387`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator.swift#L387) 的消費者只讀這些 transport 的 event stream。
2. [`BLETransport.handleDiscovery():240`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Transports/BLE/BLETransport.swift#L240) 要求可解析的 Nintendo manufacturer data、VID `057e` 與匹配 PID。完成 BLE characteristic 初始化後才送 `.ready`；[`handleNotification():365`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Transports/BLE/BLETransport.swift#L365) 才送 `.reportReceived`。
3. [`activateWithProfile():8`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BOutputMode.swift#L8) 要求已存在且等待選 profile 的 `DeviceRecord`；按 UI 選 DualSense 不會憑空建立 live record。
4. [solo `startDispatch():13`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BTransportEvents.swift#L13) 與 [pair `startPairDispatch():75`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BTransportEvents.swift#L75) 都從該 record 的 raw report stream 解析狀態，再執行 `session.buildReport`／`vhid.dispatch`。唯一往此 continuation `yield` 的 [.reportReceived 分支](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BTransportEvents.swift#L274) 先要求 `devices[id]` 存在。

另有 Switch Pro session 對 host output 的協定回覆，這是 Switch Pro subcommand response，不是 DualSense 的鍵盤 input 入口。DualSense 的 host `handleSetReport` 使用[預設空實作](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/HID/HIDOutputProfile.swift#L153)，對它寫 output report 不能轉成 input。

## 疑似入口逐項核對

| 入口 | 原始碼實際行為 | 能否餵本專案的 DualSense 狀態 |
|---|---|---|
| 啟動參數／環境變數 | App source 不讀 `CommandLine`、`ProcessInfo.environment`，無自訂 command parser；[App scenes](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/WaveBirdApp.swift) 是視窗、設定與 MenuBarExtra | 否；沒有 `--test`／`--stdin`／`--replay` 或環境變數 feed |
| DEBUG raw 模式 | [`HIDOutputCatalog:67`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/HID/HIDOutputProfile.swift#L67) 只加 `ns2Passthrough`、`switch2ProSDL`；raw branch 仍由已知 Nintendo `.reportReceived` 驅動。DualSense 使用 state translation，`transformRawReport` 預設 nil | 否；不是任意外部 raw HID broker，而且發布流程用 Release |
| Test Rumble | [`BridgeCoordinator+TestRumble.swift:9`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator%2BTestRumble.swift#L9) 篩選 `.ready` 的實體記錄，再透過 `encodeRumble`／`transport.sendVibration` 送出馬達指令 | 否；不呼叫 `buildReport` 或產生按鍵輸入 |
| `WaveBird.knownControllers` | [`loadKnownControllers():252`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Bridge/BridgeCoordinator.swift#L252) 載入 remembered metadata；`listEntries` 對 offline record 寫 `live:nil, vhidActive:false`。`devices` 初始仍為空 | 否；把裝置寫進 defaults 只產生離線清單，不產生可派送的 live record |
| mapping／output mode defaults | [`MappingProfileStore`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Profiles/MappingProfileStore.swift) 載入按鍵對應；[`MappingSource`](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird/Core/MappingSource.swift) 只有 physical button 與兩個 analog trigger | 否；沒有常數按鍵、鍵盤、回放檔案或外部 state source |
| 其餘 defaults | hideDockIcon、openInBackground、idleDisconnectMinutes、skippedUpdateVersion、rumble 與 Xbox settings。AppDelegate defaults observer 僅重算 Dock activation policy | 否；沒有裝置建立或按放事件入口 |
| URL／AppleEvent／XPC／network／stdin | 在 app 與 project target 未找到 URL types、onOpenURL、文件 handler、scripting dictionary、NSXPC、NWListener、socket、CFMessagePort、DistributedNotificationCenter、standardInput、keyboard capture。URLSession 只有 GitHub update checker | 沒有找到正式入口；此負面搜尋是資料流證據的補充 |
| Tools／XCTest | `Tools/` 是另行編譯的觀察／dump／rumble 工具。[app target](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/WaveBird.xcodeproj/project.pbxproj#L111) 只包含 `WaveBird/`；tests 是分開 target。UI tests 只 `XCUIApplication().launch()`／量測啟動，沒有 launchEnvironment／launchArguments 測試模式 | 不在 stock app 中，不能憑測試 source 呼叫已簽 app 內部方法 |

[Release workflow](https://github.com/murphyjt/wavebird/blob/b04a072f1b17df58965efb14a066873a0b48da2a/.github/workflows/release.yml#L69) 明確選 `-configuration Release`。本輪沒有下載或改簽 app，也沒有注入 test bundle 或連接 debugger 借用其 entitlement。

## 新搜尋命中的不同專案

- [ControllerKeys](https://github.com/NSEvent/xbox-controller-mapper)：雖有 Mac-to-Mac controller handoff 與 signed/notarized 發行宣稱，來源描述的是接收端執行自身鍵鼠／腳本 profile；並且為 14 天試用後付費。這不提供免費 Mac 虛擬 DualSense output 路線。
- [ps5-mcp](https://github.com/leolobato/ps5-mcp)：TCP 9305 的 virtual DualSense 在 **PS5 上的 padd payload**，要求已 jailbreak 的主機；Mac app 自身是 ad-hoc。不是可配合目前官方 Sony Remote Play 的 Mac HID helper。
- [Polaris](https://github.com/papi-ux/polaris)：virtual DualSense 是 Linux host 的能力；README 明載 Linux only，Windows/macOS hosts 不在計畫；最新版 v1.4.13 的資產也只有 Linux packages。

這三個是針對本輪新命中的介面／平台查證；沒有以名稱或搜尋摘要宣稱可用，也沒有為它們安裝／啟動程式。目前仍沒有可立即要求使用者按鍵測試的已簽章 DualSense feed 程式。

若採 WaveBird 的工程路線，真正缺少的是可單獨建立／移除 virtual session 的本機 state-feed transport 與安全停止機制；加入 source 後仍要由具合法 HID entitlement 授權的發布者簽章新版本。選它或 OJD 都不能靠 defaults 或現有測試工具免除此發布步驟。沒有對外送訊息、issue 或 PR。

## C／IOKit API 與 CoreHID 的權限差別再核對

這一段用本機 SDK 與 Apple 公開 source 交叉檢查，沒有建立任何裝置：

- 本機 **macOS SDK 26.5** 的 `hidsystem/IOHIDUserDevice.h:119–121` 明寫 `IOHIDUserDeviceCreateWithProperties` 需要 `com.apple.developer.hid.virtual.device`；TCC PostEvent／Accessibility 是另一層使用者授權，不是此 entitlement 的替代品。
- Apple **IOKitUser-100231.100.18.0.1** [`IOHIDUserDevice.c:504–546`](https://github.com/apple-oss-distributions/IOKitUser/blob/323ead896d04424f87184d8f6ff0cce811aab106/hid.subproj/IOHIDUserDevice.c#L504)：舊 `IOHIDUserDeviceCreate` 與新 `CreateWithProperties` 都呼叫 `CreateWithOptions`，再 `IOServiceOpen` 同一個 `IOHIDResource`／`kIOHIDResourceUserClientTypeDevice`。換舊入口沒有不同的公開授權路徑。
- Apple **IOHIDFamily-2238.100.59** [`IOHIDResourceDeviceUserClient::initWithTask():117–157`](https://github.com/apple-oss-distributions/IOHIDFamily/blob/777ccd9698845aadf711e32d843c8c9b777431d9/IOHIDFamily/IOHIDResourceUserClient.cpp#L117)：檢查 Apple 私有 `user-access-device` 或公開 `virtual.device` entitlement，皆無就失敗；該檢查沒有 root／UID／TCC 例外。不能據此建議用 sudo 代替合法簽章。
- 同版本 [`IOHIDUserDevice.cpp:137–145`](https://github.com/apple-oss-distributions/IOHIDFamily/blob/777ccd9698845aadf711e32d843c8c9b777431d9/IOHIDFamily/IOHIDUserDevice.cpp#L137) 對只有公開 entitlement 的 app 強制回傳 **`Virtual` transport**；即使 properties 寫 `USB` 也不等於真正 USB transport。同檔 `65–90` 強制 virtual-device flag。公開 HID helper 可以輸出 DualSense 格式，但不能把 VID/PID、descriptor 與 `USB` 字串視為完整 USB 裝置枚舉。

兩個 Apple source 匯入日期均為 2026-04-17；未確認它們與執行中 macOS 26.6.2 kernel 完全相同。SDK 26.5 契約則明確要求該 entitlement。**`Virtual` transport 不足以單獨判定 Sony 接受或拒絕**，因此仍需真正有授權的 DualSense helper 進行獨立測試；本輪不把 source 推論寫成已成功或已失敗的裝置實測。
