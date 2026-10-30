# macOS 虛擬手把輸出：首輪可行性研究

研究日期：2026-10-30（Asia/Taipei）。本輪只讀取文件、SDK 與候選原始碼，未建立虛擬 HID、安裝驅動、要求權限或操作 Sony。

**結論：優先準備 CoreHID 的單一虛擬 gamepad 隔離試驗。macOS 有公開的跨 App 虛擬 HID API，但本機權限、正式簽章／授權、GameController 辨識及 Sony 接受仍未驗證，不能宣稱 Mac 免實體手把已可用。**

## 研究適用環境

- macOS 26.6.2（25G83）、arm64；Command Line Tools SDK 26.5、Swift 6.3.3、Apple clang 21.0.0。
- 本機 Sony 官方程式：`/Applications/RemotePlay.app`，bundle ID `com.playstation.RemotePlay`，版本／build `9.5.0`，Universal x86_64／arm64。初始程序快照未發現該程式執行中，稍後快照已發現執行中；原因未確認，本輪沒有啟停操作。完整觀測見 [環境紀錄](ENVIRONMENT.json)。
- 專案基準為 `codex/commercial-preview` 的交接來源；Windows 0.1.1 成功不作為 macOS 證據。

## 候選比較

| 路線 | 已核對事實 | 權限／額外元件 | 本輪判定 |
| --- | --- | --- | --- |
| **CoreHID `HIDVirtualDevice`** | Apple 公開 API，可把合成 HID 報告交給其他 App；本機 SDK 宣告 macOS 15+ | `com.apple.developer.hid.virtual.device`；簽章與 provisioning 能否滿足須確認；可能另遇 Accessibility 提示 | **首選研究候選**，免自行開發 DEXT；尚未實跑 |
| IOKit `IOHIDUserDeviceCreateWithProperties` | 本機公開 header 明載建立虛擬 IOHIDDevice，要求同一 HID entitlement | 不能把 root 當成已證明的 entitlement 替代；需明確 cancel／release 生命週期 | 同一輸出家族的低階備選，不是繞過 CoreHID 權限的方法 |
| HIDDriverKit `IOUserHIDDevice`／DEXT | 可實作 HID provider；Apple DTS 說與 CoreHID 發布裝置的架構相同 | 需 DriverKit entitlement、正確簽章／profile、host App 的 System Extension 安裝權限與使用者啟用 | 暫不優先，沒有證據顯示本案必須多裝一個 DEXT |
| `GCVirtualController` | 文件列 iOS／iPadOS／Mac Catalyst；供自己的遊戲顯示虛擬操作介面 | 不是文件保證的原生 macOS 跨 App HID 輸出介面 | 不作官方 Remote Play 的替代輸出路線 |
| USB/IP 接入端 `karstx/usbip-macos` | 透過 `IOUSBHostControllerInterface` 建立合成 USB host；舊 `carlossless` URL 已轉址 | `com.apple.developer.usb.host-controller-interface`；上游列出的無授權替代涉及關閉 SIP 等安全設定 | 有條件的次要候選；本案不採關閉 SIP／AMFI 作為首輪試驗 |
| `beriberikix/usbipd-mac` | 是把 Mac 的實體 USB 裝置分享給別台機器的 **server** | README 的無 DEXT 路線針對可被它取得的實體裝置 | 方向不符，不能拿來接入既有 VIIPER 的虛擬手把 |

CoreHID 的用途由 [Apple Core HID 總覽](https://developer.apple.com/documentation/corehid)、[Creating virtual devices](https://developer.apple.com/documentation/corehid/creatingvirtualdevices)與 [HIDVirtualDevice](https://developer.apple.com/documentation/corehid/hidvirtualdevice)確認；低階 API 的 entitlement 要求來自本機 SDK `IOKit.framework/Headers/hidsystem/IOHIDUserDevice.h`。`GCVirtualController` 的平台及用途見 [Apple 類別文件](https://developer.apple.com/documentation/gamecontroller/gcvirtualcontroller)。

### 必須區分的兩個 entitlement

Apple 的靜態 entitlement 頁面仍列 `com.apple.developer.driverkit.family.hid.virtual.device`，但 **2026-08 Apple DTS 的 [HID Entitlement Configuration Guide](https://developer.apple.com/forums/thread/843327) 明確指出該 key 已無功能，不應加入產品**。同文說明：

- `com.apple.developer.hid.virtual.device` 控制 CoreHID 虛擬裝置 API，並不是 DEXT entitlement。
- DEXT 的 HID entitlement 應依實際 kernel support class 選擇，例如 `AppleUserHIDDevice` 對應 `com.apple.developer.driverkit.family.hid.device`；不應照抄別的產品所有權限。

本研究依據較新的 Apple DTS 說明保留這項文件差異，不以名稱相似而混用兩種 key。DEXT 其他簽章與 profile 條件見 [Requesting Entitlements for DriverKit Development](https://developer.apple.com/documentation/driverkit/requesting-entitlements-for-driverkit-development)。

### 權限及簽章的證據邊界

1. **已知：** 公開 SDK 明訂 HID entitlement。Apple 開源 `IOHIDResourceUserClient::initWithTask` 會核對 Apple 內部權限或 developer virtual HID 權限；該段沒有 root 替代分支。檢視來源為 [IOHIDFamily `777ccd9698845aadf711e32d843c8c9b777431d9`](https://github.com/apple-oss-distributions/IOHIDFamily/blob/777ccd9698845aadf711e32d843c8c9b777431d9/IOHIDFamily/IOHIDResourceUserClient.cpp)。這是公開來源分析，不代表已證明本機作業系統與此 revision 完全相同。
2. **已知：** 2026-09 [Apple DTS 對 virtual gamepad 的答覆](https://developer.apple.com/forums/thread/845599)確認 HID Virtual Device capability 申請仍有處理，但有案件等待較久。這不是本專案已取得權限的證據，也不能承諾申請通過或時間。
3. **待核對：** 本專案是否有適用簽章身分與被 Apple 授予的 profile、development 與 distribution 各自的限制。本輪沒有讀取私人憑證、開啟開發者帳號、申請 capability 或付款；也沒有把 entitlement 寫進 ad-hoc 簽章後冒充有效授權。
4. **第三方親身回報，尚非本機事實：** [Apple 論壇 thread 820676](https://developer.apple.com/forums/thread/820676)的作者描述 `HIDVirtualDevice(properties:)` 觸發 Accessibility 提示。該文不是 Apple 對本專案的核准或拒絕，不據此推論所有 gamepad 都有相同提示，也不據此宣布不能發布。

### 官方 Remote Play 的接受條件仍未知

[Sony PC／Mac Remote Play 說明](https://www.playstation.com/en-za/support/games/playstation-remote-play-on-pc-and-mac/)列出 Mac 與 DualSense、DualSense Edge、DUALSHOCK 4 實體控制器。文件沒有承諾支援第三方虛擬 HID，也沒有公開足以保證接受合成 DualSense 的判定規則。

Apple DTS 在 [thread 845599](https://developer.apple.com/forums/thread/845599)也特別區分「建立 HID」與「被 GameController 辨識」：後者會依未公開的硬體細節配對，工程師認為 CoreHID 應可行，但仍保留有細節未涵蓋的可能。因此 **CoreHID 建立成功、HID 讀回成功、GameController 列舉成功、Sony 接受、PS5 反應是五個不同結果**。

推測：若 Sony 只使用可由 CoreHID 提供的 HID 屬性、descriptor 及 get／set reports，則有機會直接建立候選 DualSense；若它另需真正 USB interface 或其他識別行為，generic gamepad 不會足夠。這兩種可能都必須靠分階段試驗辨別，不先修改或注入 Sony。

## USB/IP 候選來源核對

`carlossless/usbip-macos` 已轉址至 `karstx/usbip-macos`。本輪固定讀取 `e98cfa750170afc8fa1f32b2283fa6fa60c75b4d`：

- [README](https://github.com/karstx/usbip-macos/blob/e98cfa750170afc8fa1f32b2283fa6fa60c75b4d/README.md)列出授權 app bundle／profile 路線；另列安全設定降級的開發路線，本研究不採用。
- [entitlements.plist](https://github.com/karstx/usbip-macos/blob/e98cfa750170afc8fa1f32b2283fa6fa60c75b4d/entitlements.plist)只宣告 USB host-controller entitlement；有這份檔案不代表使用者或本專案已獲 Apple 授權。
- [Cargo.toml](https://github.com/karstx/usbip-macos/blob/e98cfa750170afc8fa1f32b2283fa6fa60c75b4d/Cargo.toml)是 Rust／objc2 實作；[hci.rs](https://github.com/karstx/usbip-macos/blob/e98cfa750170afc8fa1f32b2283fa6fa60c75b4d/src/hci.rs)定義 host-controller 訊息格式。未建置或接入裝置。
- 此 revision 的 tree 未見 LICENSE／COPYING，GitHub license metadata 為 null。**尚未確認可重用／散布的明確授權，不將其加入本專案交付依賴。**
- 本機 SDK `IOUSBHostControllerInterface.h` 同樣要求該 entitlement，並說明此 API 可建遠端或合成 USB 裝置。這是 API 層存在的證據，不是免費可散布產品或 Sony 成功的證據。[Apple API 文件](https://developer.apple.com/documentation/iousbhost/iousbhostcontrollerinterface)

`beriberikix/usbipd-mac` 另固定查看 `1c2ab4594653db5859d6773bdd010303a641e98e` 的 [README](https://github.com/beriberikix/usbipd-mac/blob/1c2ab4594653db5859d6773bdd010303a641e98e/README.md)，確認它的角色是 server。未將安裝、bind 或實體裝置擷取指令帶到本機執行。

## 下一個具體試驗

### A. 可先完成的準備

- 建立一個可檢視的 CoreHID probe：預設只檢查 descriptor／合成報告並結束；建立裝置必須使用獨立且明確的參數。
- 使用一般 gamepad descriptor 與固定 synthetic product／unique ID，不宣稱是 Sony，第一輪不發 PS／Home、不發鍵盤或滑鼠事件。
- 先編譯與純資料測試。把候選來源、編譯器、SDK、SHA-256 與所需 entitlement 寫入收據；編譯成功不列作裝置測試。
- 裝置讀回端只匹配這次自有 unique ID／device reference，拒絕選取其他現存手把；不列出或記錄其他裝置序號。

### B. 權限齊備後的單裝置隔離試驗

執行前先具體確認簽章／profile 是否滿足上述 entitlement。若需要本人授予 Accessibility，先交付可檢視 probe 與影響說明，再由本人處理；不以 `sudo`、關閉 SIP、關閉 AMFI 或改用別的 App 的身分嘗試繞過。**此研究文件不是執行 B 的授權紀錄。**

1. 記錄 Sony 是否執行的狀態，不啟動或停止 Sony。若它正在連線中，協調獨立時段，因虛擬 gamepad 是系統可見裝置，不能保證其他 App 不觀察到熱插拔。
2. 建立一個 generic virtual gamepad，先以 neutral 報告開始；只讓自有讀回程序開啟該裝置。沒有鍵盤擷取、網路 listener、常駐 daemon 或系統擴充安裝。
3. 合成單一普通按鈕：neutral → press → release → neutral；確認資料長度、值、次序與回讀。最長 10 秒；遇到失連、讀回錯誤或 timeout 立即停止發送。
4. 正常退出前發 neutral；取消任務、釋放所有 client／device reference，確認只屬於 probe 的裝置消失。CoreHID 公開 actor 介面未提供獨立 `cancel()`，所以需額外驗證物件釋放與程序退出的移除語義，不能只以主程式 return 當成清理通過。
5. 另外測試 probe 被中止的清理；先清楚定義移除 timeout 與程序 owner。若未能確認移除，停在此處，不再次建立裝置、不全域清理其他裝置。

這輪只驗證 macOS generic HID。通過後才能另行準備 DualSense descriptor／feature-report 候選，最後由使用者控制官方 Remote Play 連線測 PS5 單鍵，再擴充到 22 動作、前景限制、F8、改鍵及連點。

## 尚待回答的問題

| 問題 | 下一個證據 |
| --- | --- |
| 本機開發與免費公開散布是否能合法取得所需 capability／簽章條件？ | 由本人確認開發者帳號能力／取得 profile；不把產品免費等同開發者流程無成本 |
| 本機 generic gamepad 是否需要 Accessibility，能否建立並讀回？ | B 階段實測，保存錯誤碼與必要版本，不保存原始打字 |
| 物件釋放、程序退出及異常退出是否能完全移除裝置？ | 自有 device reference 的移除通知與有限 timeout |
| GameController 是否辨識合成 DualSense？ | 核對必要 descriptor／屬性／feature reports，再作獨立列舉測試 |
| Sony 9.5.0 是否接受？PS5 是否回應？ | 使用者保有連線控制的單鍵按下／放開驗證 |
| 原有 33-byte／V5 framing 能重用多少？ | 只重用純動作語義；V5 是後端傳輸，不直接當成 Mac HID input report |

## 本機 SDK 證據雜湊

下列路徑均相對於 SDK `System/Library/Frameworks/`；未複製 Apple SDK 到本專案。

| 檔案 | SHA-256 |
| --- | --- |
| `CoreHID.framework/Versions/A/Modules/CoreHID.swiftmodule/arm64e-apple-macos.swiftinterface` | `b83941a2d4e440d0f7625e035dc99f4aea719365ea53843ee877457816a8da8e` |
| `IOKit.framework/Versions/A/Headers/hidsystem/IOHIDUserDevice.h` | `5ceaab19c16294f82b5c51967de919c58a0ce4d688e33419fa26e56753aba15a` |
| `IOUSBHost.framework/Headers/IOUSBHostControllerInterface.h` | `0a919a22aed548950f6607bd5d380d687fbae4fd3d3c1037f89fad5faba7f1b3` |

本報告沒有實機 HID、GameController 或 Sony 成功結果。所有新裝置、權限及散布可行性的未驗項都保留，不把來源研究當成已完成 Mac App。
