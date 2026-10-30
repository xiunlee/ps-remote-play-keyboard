# OpenJoystickDriver 0.6.0-alpha.1：發布檔與啟動行為核對

日期：2026-10-30（Asia/Taipei）。使用者授權研究免費路線；本輪只下載、唯讀掛載、驗簽、複製已驗證程式及閱讀原始碼，**沒有執行 OJD、啟動服務、安裝 DEXT、建立 HID、改隱私權限或操作 Sony**。本機原始收據為 [OJD-AUDIT-RECEIPT.json](OJD-AUDIT-RECEIPT.json)。

**結果：OJD 已發布 app 的簽章、公證和 virtual HID entitlement/profile 在本機檢查通過，具備不用我方重新簽章的免費候選路線；Sony 接受其虛擬手把仍未驗證。完整 app 啟動會偵測實體裝置並建立 Bluetooth central，沒有在已讀入口、runtime、設定或 CLI 中找到純 feed-only 模式。因此不能將啟動視為單純測試虛擬輸出。**

## 下載與簽章實證

- [作者 release](https://github.com/xsyetopz/OpenJoystickDriver/releases/tag/0.6.0-alpha.1)：2026-10-23，asset ID `669916104`，大小 `29,419,483` bytes。
- Tag 對應 commit：`3552be925d7dacef3fd895ac0c6cbf39e9d3cd70`。文件與程式碼使用這個固定 revision。
- Release API 宣告與本機下載 SHA-256 一致：`d2b771ffe4382b7bfd218932c7003227216d334a14154e95976c4f114b1c2056`。
- 下載位置：`.local/ojd-audit-20261030/OpenJoystickDriver-0.6.0-alpha.1-macOS.dmg`。GitHub connector 不支援二進位下載，因此只在取得官方 asset URL 後使用標準 HTTPS 下載。
- `hdiutil verify` 通過；`imageinfo` 明列 `Software License Agreement=false`。以唯讀、不可瀏覽、不自動開啟方式掛載，沒有接受授權對話框；作業結束已卸載。

| 對象／檢查 | 實際結果 |
| --- | --- |
| DMG 簽章 | `codesign --verify --strict` exit 0；Developer ID Application，team `9PQP6CDMQT` |
| DMG 公證政策 | `spctl --type open --context context:primary-signature` exit 3，`Unnotarized Developer ID`；`stapler validate` exit 65，無 stapled ticket |
| app 簽章 | `codesign --verify --deep --strict --all-architectures` exit 0；universal x86_64／arm64，hardened runtime |
| app 公證政策 | `spctl --type execute` exit 0，`accepted / Notarized Developer ID`；`stapler validate` exit 0 |
| app 發布者 | `Developer ID Application: Krystian Järveäär (9PQP6CDMQT)`，Apple Root CA 鏈 |
| app 身分 | `com.openjoystickdriver`，版本 `0.6.0-alpha.1`，build `1.19.64`，最低系統 `12.0` |
| app 已簽 entitlement | `com.apple.developer.hid.virtual.device=true`；另有 system-extension.install 及 XboxUSBDevice userclient-access |
| embedded profile | `OpenJoystickDriver DevID`，team `9PQP6CDMQT`，`ProvisionsAllDevices=true`，有效至 `2044-08-20T11:11:40Z` |
| restricted claims 與 profile | application identifier、team identifier、HID key、system-extension.install、Xbox userclient-access 均一致 |

app 本體與 DMG 包裝的結果不同，不能說整個 DMG 已公證。沒有移除 quarantine、停用 Gatekeeper／SIP／AMFI 或重新簽章。`security cms -D` 僅用於讀取公開 embedded profile；沒有讀取任何私人金鑰或使用者帳號。

作者宣稱 tester DMG 使用者不需要 Apple Developer 帳號，現在有發布 app 的 Developer ID、公證與 all-devices profile 作為本機佐證；**尚未實際執行 HID API，故不宣稱權限或 Sony 相容性已通過**。[作者建置文件](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/wiki/Building-from-Source.md)

## 備妥的本機副本

主代理後續授權建立新測試目錄，已完整複製為：

```text
.local/ojd-runtime-0.6.0-alpha.1/OpenJoystickDriver.app
.local/ojd-runtime-0.6.0-alpha.1/ojd
```

第二個檔案是指向 `OpenJoystickDriver.app/Contents/MacOS/OpenJoystickDriver` 的相對 symlink；未寫入 `/Applications` 或 `/usr/local/bin`，未註冊登入項目。原本沒有同名 runtime 目錄，沒有覆寫既有程式。

`ditto` 複製後，第一次 strict 檢查因根 app 的額外 `com.apple.FinderInfo` metadata 失敗。只移除了本輪新副本根 app 的這一個屬性，沒有遞迴清空 xattr。其後副本的 strict／deep／all-architectures 簽章檢查、Gatekeeper execute、stapled ticket 檢查全部 exit 0；完整結果與被移除屬性原值已寫入收據。未改 Mach-O、resource、entitlement 或 profile。

**不要直接用 `OpenJoystickDriver --help` 測 CLI。** [main.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriver/main.swift)只在 `argv[0]` 的檔名是 `ojd` 時進入 CLI；其他名稱會進完整 app。作者自己的 CLI 安裝也是同樣 symlink 機制，保留原 app 簽章。[CommandLineTool.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverPresentation/Settings/Preferences/CommandLineTool.swift)

## 完整 app 啟動的實際影響

| 行為 | 固定版本程式碼與判定 |
| --- | --- |
| 預設登入項目 | `HeadlessApplicationHost.run()` 首先呼叫 `installByDefaultIfNeeded()`；若未 opt out，嘗試註冊主 app 為登入項目 |
| 本機 RPC | `ApplicationServiceRuntime.start()` 啟動本機 socket，內部 CLI client 須符合 app 簽章要求；供 feed CLI 使用 |
| 公開 endpoint／WebSocket | server 物件被建立，但只有 AccessGrantFile 已啟用時才監聽；未啟用時不開公開 endpoint。此試驗不需要開啟或授予 token |
| 實體 HID | runtime 啟動 DeviceManager；只有 Input Monitoring 已 granted 時啟動 HID input stream，unknown／denied 時關閉此 stream。這不等同關閉 USB／BLE |
| USB | raw USB 偵測不受上列 HID Input Monitoring 開關停用；會枚舉、分類並依支援路線處理裝置 |
| Bluetooth | runtime 無條件呼叫 `Switch2BluetoothLECentral.start()`，建立 `CBCentralManager`；source 明列首次使用會要求 Bluetooth 權限。授權後掃描周邊，只有匹配 Switch 2 廠商資料才自動 connect |
| 物理轉虛擬輸出 | DeviceManager 啟動後呼叫 `activateVirtualOutputBackendForCurrentDevices()`；是否發布依 serving policy、實體 ownership 和 profile 決定 |
| 寫入資料 | 建立自己的 port key、logs、profiles/packages/virtual-device 目錄與 watcher；設定不是全部唯讀 |
| DEXT | bundle 內附 XboxUSBDevice DEXT，但讀取 bundle／驗簽不會安裝；此方案只需 user-space feed，禁止為本輪測試呼叫 extension activation |

來源：[HeadlessApplicationHost](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverMacOSHost/HeadlessApplicationHost.swift)、[ApplicationServiceRuntime](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverService/Runtime/ApplicationServiceRuntime.swift)、[EndpointServer](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverService/Runtime/Endpoint/EndpointServer.swift)、[HID detection gate](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Device/Discovery/HID/DeviceManager%2BHIDDetection.swift)、[Bluetooth central](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverService/Runtime/Switch2BluetoothLECentral.swift)。

沒有看到能透過現有設定把偵測清單設為空或關閉 Bluetooth central 的選項。`SettingsDocument` 可設定登入、UI、通知、更新等，但沒有 `feedOnly`／`disableDiscovery`。HID matching 本身包含所有 Generic Desktop GamePad，再加 catalog models；不能靠空 user catalog 排除手把。拒絕 Input Monitoring 可關閉 HID input stream，但不保證完全不枚舉 USB、不要求 Bluetooth。

### Sony 手把會不會被讀取或 seize？

`054c:0ce6` 有 [SonyDualSense catalog record](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Resources/Controllers/054c/054c-0ce6.json)，不是被產品忽略的硬體。它未指定 `server: ojd`，catalog 預設為 `system`。

實際 admission 的條件為：

```swift
GCController.supportsHIDDevice(device) && !catalog.takesOwnership(model)
```

條件為 true 時，OJD 將它視為 native pass-through：使用 shared `IOHIDDeviceOpen(kIOHIDOptionsTypeNone)`，仍註冊 input callback 讀取資料；serving policy 不為它另建虛擬 gamepad。條件為 false、且同 location 尚未出現 native 裝置時，會先嘗試 `kIOHIDOptionsTypeSeizeDevice`，失敗後改 shared open；後續某些失敗狀態還會 retry。這個決定先於 protocol binding，所以不能以「可能不支援某手把」推論絕不會 claim。[admission 與 native 判定](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverMacOSPlatform/HID/HIDDeviceStream%2BDeviceLifecycle.swift#L94)、[HIDInputClaim](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverMacOSPlatform/HID/HIDInputClaim.swift)、[ServingPolicy](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/ApplicationService/ServingPolicy.swift)。

原生 DualSense 預期走 shared pass-through，但本輪沒有執行 OJD 的 GC 判斷，不能保證結果或物理裝置完全不受影響。維持 Sony 工作階段、由使用者拔除實體 USB 手把後再測中立 feed，可以避開這項實體手把副作用；Bluetooth 提示仍應由本人處理。此研究代理沒有啟動測試。

## 登入項目 opt-out 的精確設定

這不是 CFPreferences domain，不能用 `defaults write com.openjoystickdriver …` 代替。檔案是：

```text
/Users/xiun/Library/Application Support/OpenJoystickDriver/Settings.json
```

最小有效內容：

```json
{
  "apiVersion": "openjoystickdriver/v1alpha1",
  "kind": "Settings",
  "launchAtLoginOptOut": true
}
```

若已存在，要保留原內容、依 schema 合併這個布林值並記錄還原值；本輪最初盤點時整個 OJD Application Support 目錄不存在。研究代理未寫入此設定。source 會在登入註冊前讀取它；`true` 會跳過註冊。它不會關閉 HID／USB／Bluetooth 偵測。[SettingsStore](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Settings/SettingsStore.swift)、[ApplicationServiceManager](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/ApplicationService/Client/ApplicationServiceManager.swift)。

不要加 `metadata`：此設定文件的 `CodingKeys` 沒有它，而且 decoder 會拒絕未知欄位。`apiVersion`、`kind` 是必需的 header；`launchAtLoginOptOut` 是本次要預置的選擇。

## 可檢視的下一步命令與退出路徑

以下僅列出經 source 核對的命令，**本研究沒有執行它們**。所有命令都應使用已備妥的 `ojd` symlink。不要開公開 endpoint，不要 `extension activate`，不要 `virtual launch` Sony。

```sh
ojd_cli='/Users/xiun/Documents/ChatGPT/PS/ps-remote-play-keyboard/.local/ojd-runtime-0.6.0-alpha.1/ojd'
"$ojd_cli" status --json
```

`status` 不自動啟動服務；會讀設定、socket 狀態與 system extension 註冊狀態。未啟動時應回報 `service.state=Stopped`。只有以下 `start` 會開完整 app，會產生上節的影響，須先由主代理完成實體手把與權限條件：

```sh
"$ojd_cli" service start --json
"$ojd_cli" status --json
```

受控的全中立短 feed 可以使用下列 stdin；它會發布暫時的 Generic 虛擬 gamepad，保持全 neutral 250 ms，EOF 後 drain、close。不含任何按鍵、軸或 Sony UI 操作：

```sh
printf '%s\n' '{"holdMilliseconds":250}' | "$ojd_cli" virtual feed --as generic --json
```

自動測試可使用 `"$ojd_cli" --no-input --json --timeout 5 virtual feed --as generic`。`--no-input` 只關閉 CLI 的確認提問，feed 的 `standardInputLines`／`readLine()` 仍照常讀取 JSON；它不等於停用實體輸入，也不會抑制 macOS 權限提示。`--timeout 5` 是每次 RPC 的 deadline，**不是 feed 的總執行時間**。請以分開參數或 `--timeout=5` 傳入，不能拼成 `--timeout5`。[GlobalOptions](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Root/GlobalOptions.swift)、[CLITerminal](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Root/CLITerminal.swift)。

若要在裝置存在期間執行唯讀盤點，應由 harness 保留 stdin、只送 `{}`，設短時限並在 `finally` 送 neutral、關 stdin、等待退出；必要時對自己建立的 feed PID 送 SIGINT／SIGTERM。不要送長 hold，不要累積長佇列。CLI 會自行 heartbeat，因此我方輸入失焦仍必須 neutral／關閉 stdin，不能只等兩秒 watchdog。[VirtualFeedCommand](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Commands/Virtual/VirtualFeedCommand.swift)

`--json` feed 沒有「已建立」成功行，只輸出遊戲回傳的 rumble。stdout 空白不是成敗證據；需用既有唯讀 HID 盤點對比新增與移除。`status.virtualDevice.published` 描述物理轉映射，不能單靠空陣列斷言獨立 feed 已移除。另應檢查 process exit、stderr 與實際 HID 消失。

測試完成可停止本輪啟動的 OJD 服務：

```sh
"$ojd_cli" service stop --json
"$ojd_cli" status --json
```

`service stop` 只向已認證 OJD socket 回報的 process 送 SIGTERM，等待服務離線；runtime stop 會關閉所有 virtual feed 與自己的偵測。這不是 Sony 停止命令。必須保持 Sony Remote Play 的現有 process 與連線。[ServiceCommand](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Commands/Service/ServiceCommand.swift)、[ServiceConnection](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Root/ServiceConnection.swift)。

此版本 feed ABI 只有 `generic`、`sdl-compatible`、`game-controller-compatible`，後兩者是 Xbox 身分，沒有 Sony DualSense ABI。中立裝置成立仍不代表 Sony 可接受，也不代表遊戲內按鍵可用；後續結果必須分別記錄。

交付 audit 後，主代理另回報已用正確 symlink 執行 `status --json`，結果為 service Stopped、extension registration Absent，尚未 launch。這是後續 CLI 唯讀檢查，不改寫本文件附帶收據所記錄的驗簽階段為 HID 執行成功。
