# Mac DualSense helper：OJD 能力邊界與免費候選核對

檢查日期：2026-10-30。使用者回報 Generic 試跑時 W／S 與 Enter 無反應，並明確要求 PS5／DualSense。這次結果不能證明 Sony Remote Play 接受 Generic；後續不再把 Generic 或 Xbox profile 當成目標方案。

**目前核對的公開版本中，沒有找到可直接交付的「免費、已授權簽章、接受外部 DualSense 狀態或任意 HID descriptor／report」Mac helper。** 這是以下版本與入口的查核結論，不是宣稱所有未來或未查的專案都不可能。此輪只讀 source／release metadata，另下載 Satellite zip 做靜態驗簽；沒有執行下載 app、操作 OJD 服務、建立裝置、修改已簽章 binary 或操作 Sony。

## OJD 0.6.0-alpha.1：不能由官方介面切換成 DualSense

固定 source commit：[`3552be925d7dacef3fd895ac0c6cbf39e9d3cd70`](https://github.com/xsyetopz/OpenJoystickDriver/tree/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70)。發布 app 的 Developer ID、受限 HID entitlement、embedded profile 與公證已在 [OJD-AUDIT.md](OJD-AUDIT.md) 驗證；有效簽章不會擴充它實際提供的 API。

| 檢查點 | 原始碼結果 | 對 DualSense 的影響 |
|---|---|---|
| Profile 選擇 | [`VirtualHIDProfileID`／`makeProfile()`](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Output/Profiles/VirtualHIDProfile.swift#L7) 只有 `Generic`、`SDLCompatible`、`GameControllerCompatible`，分派至 Generic、Xbox GIP、Xbox Bluetooth 三種編譯內建 codec | 沒有 DualSense profile，也沒有 plugin／動態 codec 入口 |
| JSON 設定 | [`virtual-device.schema.json`](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Resources/Schemas/virtual-device.schema.json#L6) 的 persona/layout 是上述固定 enum，`additionalProperties: false`；[實際 decoder](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Output/Profiles/VirtualDevice%2BDecoding.swift) 也拒絕未知欄位 | 沒有 descriptor、raw report 或 feature callback 欄位。identity 的 VID/PID 自訂不能改報告格式 |
| 建立 HID | [`IOHIDVirtualDevicePublisher`](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverMacOSPlatform/Output/IOHIDVirtualDevicePublisher.swift) 使用所選 compiled format 的 descriptor／report size | 將名字或 VID/PID 改成 `054c:0ce6` 仍不是 DualSense ABI |
| 外部輸入 | [`VirtualFeedFrame`](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/ApplicationService/Payloads/VirtualFeedPayloads.swift#L34) 僅接受 `buttons`、`dpad`、`axes`、`holdMilliseconds` 與 envelope，未知 key 拒絕；[`virtual feed`](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Commands/Virtual/VirtualFeedCommand.swift#L38) 也使用同一 profile enum | CLI／service feed 不是可輸入任意 64-byte HID report 的通道；HTTP feed 使用同一 frame contract |
| Host feature reports | [`InputReportState.hostReport()`](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Output/Backends/UserSpaceOutputDispatcher/InputReportState.swift#L84) 只接受符合目前 input ID 的 `.input` GetReport；SetReport 只接受 `.output`。其他 type 回 unsupported | 不提供 DualSense feature GetReport／SetReport，也無外部 callback 接管功能 |

底層 [`IOHIDVirtualDeviceBackend`](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverMacOSPlatform/Output/IOHIDVirtualDeviceBackend.swift#L39) 雖已註冊 IOKit get/set-report callback，實際會交給上表受限 handler；「底層認識 feature type」不等於已實作 DualSense feature。修改 bundle 內建 JSON、patch binary 或重新簽章都不屬於可用的官方配置路徑，本次未做。

## 其他最可能候選

| 候選與查核版本 | 已確認能力／限制 | 判斷 |
|---|---|---|
| [WaveBird v0.9.0](https://github.com/murphyjt/wavebird/releases/tag/v0.9.0) | MIT；上游聲稱已簽章公證；確有 `054c:0ce6` 與 64-byte DualSense output。完整 source audit 未找到 stdin／CLI／IPC／socket／URL scheme 可外部餵狀態，輸入來自已連線 Nintendo BLE 裝置。詳細證據見 [WAVEBIRD-AUDIT.md](WAVEBIRD-AUDIT.md) | Stock app 不能當本專案的鍵盤 feed broker；本次不把上游聲稱視為本機驗簽實證 |
| [Satellite 2.1.9](https://github.com/TinkerNorth/satellite/releases/tag/2.1.9) | LGPL-3.0-or-later；有網路控制協定，但 Mac backend 只有 DS4。官方 Mac zip 本機驗出 ad-hoc、無 team／HID entitlement／profile | 現有公開包不可作已授權 DualSense helper；詳見下節 |
| [ControlDeck v0.4.0](https://github.com/ViniZap4/ControlDeck/releases/tag/v0.4.0) | Release notes 明載 ad-hoc、無受限 HID entitlement，Virtual Pad 在該發行版停用；需要自有簽章 team 才能另建 | 不能解決本機無開發者帳號的輸出需求；沒有必要啟動它再驗失敗 |
| [xow-mac](https://github.com/aberguecio/xow-mac#the-signing-problem) | README 提供 Unix socket pad host，但同時明載要 provisioning profile；替代法是放寬 SIP／AMFI；輸出為 Xbox | 沒有符合需求的現成授權包證據；不採降低系統安全設定路線 |
| [Karabiner-DriverKit-VirtualHIDDevice v8.7.0](https://github.com/pqrs-org/Karabiner-DriverKit-VirtualHIDDevice/blob/v8.7.0/README.md#L4) | 公開 C++ client 控制已提供的虛擬 keyboard／mouse，要求 root client | 該介面不是任意 HID descriptor／gamepad broker，不能把鍵鼠 driver 當 DualSense driver |
| [VirtualHID-macOS](https://github.com/LizardByte-research/VirtualHID-macOS) | 上游 README 警告 critical bug，安裝步驟要求關閉 SIP；公開 C++ 使用介面仍寫 Coming soon | 不符合本輪維持系統安全與現成可用介面條件 |

另核對 [Tactile](https://github.com/theisegoria/tactile)：它處理實體 DualSense 的完整 Bluetooth 能力，README 明載 system-wide virtual controller 與其受限 entitlement 不在範圍。實體手把 library 不等於可發布虛擬手把的已簽章 helper。

## Satellite：將 README 宣稱與公開發行物分開

固定 source：[`453292c704538d8bd48cdd0d95e12704353dfeba`](https://github.com/TinkerNorth/satellite/tree/453292c704538d8bd48cdd0d95e12704353dfeba)（2.1.9）。[Mac backend](https://github.com/TinkerNorth/satellite/blob/453292c704538d8bd48cdd0d95e12704353dfeba/src/platform/macos/mac_hid_gamepad_adapter.cpp#L201) 固定使用 DS4v2 descriptor 與 identity；[`supportsIdentity()`](https://github.com/TinkerNorth/satellite/blob/453292c704538d8bd48cdd0d95e12704353dfeba/src/platform/macos/mac_hid_gamepad_adapter.cpp#L346) 只接受 Xbox／DS4，註解明確寫 DualSense 尚未接上。[codec 常數](https://github.com/TinkerNorth/satellite/blob/453292c704538d8bd48cdd0d95e12704353dfeba/src/core/ds4_report.h#L30) 是 `054c:09cc`。其 64-byte DS4 input 不可當成 64-byte DualSense input。

協定中的 `ControllerDescriptor` 是控制器類型／功能設定物件，不是 HID report descriptor byte array。[contract 的 Mac 說明](https://github.com/TinkerNorth/satellite/blob/453292c704538d8bd48cdd0d95e12704353dfeba/docs/contract.md#L542) 也寫 DS4、DualSense pending；Windows 的 HIDMaestro DualSense 能力不能外推到 Mac。

截至檢查時，官方 Releases API 回傳的 29 個 releases 中，凡含 Mac 資產者名稱均為 `satellite-macos-stub-…-unsigned`。最新 zip 的實際檢查：

- URL：[satellite-macos-stub-2.1.9-unsigned.zip](https://github.com/TinkerNorth/satellite/releases/download/2.1.9/satellite-macos-stub-2.1.9-unsigned.zip)。838,380 bytes。
- SHA-256：`1c505978fd96cb6f4ce96cdb5aa640a8967f86d5b859c0de52d338b84965d5d5`，吻合 GitHub asset digest。
- `codesign --display --verbose=4`：`Signature=adhoc`、`flags=0x20002(adhoc,linker-signed)`、`TeamIdentifier=not set`、`Sealed Resources=none`。
- `codesign --display --entitlements :-` 沒有 entitlement 輸出；bundle 沒有 `embedded.provisionprofile`。`codesign --verify --deep --strict` 回 code 1，訊息為 `code has no resources but signature indicates they must be present`。
- [release workflow](https://github.com/TinkerNorth/satellite/blob/453292c704538d8bd48cdd0d95e12704353dfeba/.github/workflows/release.yml#L344) 的 Developer ID 簽章步驟本身是有憑證時才執行，未指定 HID entitlement 或 provisioning profile。README 的 production build 描述並非已發布受限權限 app 的證明。

資料曾解壓在 `.local/satellite-audit-2.1.9/extracted/`，未開啟／執行 app；收據見 [SATELLITE-SIGNATURE-RECEIPT.json](SATELLITE-SIGNATURE-RECEIPT.json)。確認沒有該 app 程序或展開目錄的開啟檔案後，已回收本輪下載的 zip／app，保留驗簽收據與文字 logs；回收檔案數及 logical bytes 記於收據的 `cleanup`。這是簽章／能力不合需求的證據，沒有據此判斷 app 是否惡意。

## 可繼續推進的開發方向

OJD 採 [MIT license](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/LICENSE)，且已有我們驗證過的合法簽章與外部 feed 入口，最直接的工程方向是新增真正的 DualSense profile：固定 descriptor／`054c:0ce6` identity、完整 64-byte USB input codec、必要的 feature GetReport／SetReport 與 host output 處理，並把它接到 profile 選擇、service feed、生命週期及中立／斷線測試。已有的本機 encoder 測試可作輸入，但不能取代 host feature 與 Sony 相容性驗證。

**無 Apple Developer 帳號的使用者要執行此改版，仍需由上游維護者在其合法授權的簽章／profile 發布流程中納入並發布新版本**，或另一個同樣具有合法授權的發布者提供對應 helper。MIT 允許修改 source，不會讓重編譯版本繼承原 app 的 restricted entitlement 授權；加 entitlement 的 ad-hoc 包已在本機以 AMFI `-424` 證實無法進入 main，見 [SIGNING-RESEARCH.md](SIGNING-RESEARCH.md)。

這個方向可先在本機完成 source patch 與純資料測試，再由實際有授權的發布者決定是否採納；本輪沒有送 issue／PR、聯絡維護者或承諾其會發布。拿到合格簽章包後，才有條件測中立建立／移除與 Sony 對真正 DualSense profile 的接受度。不能以只改 VID/PID、沿用 Generic codec，或只是 64-byte 長度相同來跳過這些工作。
