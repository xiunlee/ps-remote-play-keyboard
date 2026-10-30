# macOS USB DualSense 輸出路線查證

檢查日期：2026-10-30；本機 macOS 26.6.2 / arm64。這輪只讀公開 source、SDK header、官方文件與發布檔簽章。未啟動下載的程式、未建立 USB/HID 裝置、未安裝 driver／system extension、未改安全性、未存取帳號或操作 Sony Remote Play／實體手把。

**結論：本輪檢查的方案中，尚無可以現在接上本專案 VIIPER server、保留原系統安全設定、免費且已驗證有效的 Mac DualSense 接入端。** USB 路線在技術上存在，VirtualHere 的發布檔甚至已取得所需 Apple entitlement；缺的是可供本專案使用的公開裝置建立／資料輸入接口及協定互通證據。不能把有簽章、支援 USB、或帶 Sony VID/PID，直接當成 Sony Remote Play 已接受真正 DualSense 的證明。

## 已查證的候選

| 候選 | 可以確認 | 目前判定 |
|---|---|---|
| Apple IOUSBHostControllerInterface | SDK 明確支援遠端及 synthetic USB；要求 `com.apple.developer.usb.host-controller-interface` | 可設計 backend，但自行 ad-hoc 簽章不等於取得該 entitlement；不能當本機現成免費輸出端 |
| `karstx/usbip-macos`（原 `carlossless`） | 標準 USB/IP client，昨天加入可選 Developer ID/profile 包裝流程 | **最接近 VIIPER 協定的後續候選；目前沒有可驗證的正式簽章 release**。本輪最新 CI 的 signing 步驟明確 skipped，不應下載其 artifact 便直接當可運作 |
| SagerNet `sing-usbip` / sing-box | 有 macOS IOUSBHostControllerInterface client 原碼；1.14 文件列出支援 | Apple app 已查的全部 entitlement 檔未見 host-controller key；client 又要求 sing 專有控制擴充。未證實可直接用已發布 app 接 VIIPER |
| `calibrae/extender` | 開源 USB/IP、作者聲稱 Mac bundle 有簽章及 notarization | 作者明載其目前 Mac 26 路線需 MDM 或 Reduced Security；不符合本次環境，不執行 |
| VirtualHere 6.0.3 | 本機驗證 arm64、Developer ID、notarization、binary/profile 均有 USB host-controller entitlement；Generic Linux Server 單裝置可免費使用 | **可合法作單一實體 USB 的 end-user 遠端接線工具，但不是已證實可餵入我們 DualSense 狀態的 helper**。公開 API 只控制它自己的 Server 裝置，沒有建立 synthetic USB／送任意 input report 的公開接口 |
| `usbipd-mac` | 是 Mac USB export server | 方向相反，不能讓 Mac Sony 收到 VIIPER 裝置 |

上述「尚無」限於這次核對的版本及接口，不宣稱所有免費方案都不可能，也不把未來上游簽章發布視為永遠不會發生。

## Apple API 的授權邊界

本機 SDK 的 `IOUSBHostControllerInterface.h` 第 17–19 行描述 synthetic/remote USB 能力及受限 entitlement。SDK 相對位置：`IOUSBHost.framework/Versions/A/Headers/IOUSBHostControllerInterface.h`。對照 [Apple IOUSBHost 文件](https://developer.apple.com/documentation/iousbhost)。

這個 class 與讀取／控制既有 USB 裝置的 IOUSBHostDevice 不同。一般 sandbox 的 `com.apple.security.device.usb` 也不是 virtual host controller 授權。已有自製 DualSense encoder、USB descriptors、GET/SET_REPORT 回應及 USB/IP server，仍不能省略 Mac 端的虛擬控制器建立權限。

## 標準 USB/IP：最新原碼與發布狀態

本專案 Windows 固定 VIIPER 為 `hbashton/VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2`；Mac 需要能 import 它的 USB/IP client。Windows 的 `.exe`／driver 不是 Mac 接入端，見本 repo `docs/MAC-HANDOFF.md`。

`usbip-macos` 已改名至 [karstx/usbip-macos](https://github.com/karstx/usbip-macos)。固定 commit **`e98cfa750170afc8fa1f32b2283fa6fa60c75b4d`**，2026-10-29。不能再只引用舊 README「作者還沒有 entitlement」當作現況。

- [目前 README](https://github.com/karstx/usbip-macos/blob/e98cfa750170afc8fa1f32b2283fa6fa60c75b4d/README.md) 要求帶授權 profile 的 signed bundle，或變更 SIP 等安全設定。這輪不採後者。
- [CI workflow](https://github.com/karstx/usbip-macos/blob/e98cfa750170afc8fa1f32b2283fa6fa60c75b4d/.github/workflows/push.yml) 的 Developer ID/profile/notary 設定是可選的，缺憑證會產生 ad-hoc app。
- [該 commit 實際 CI](https://github.com/karstx/usbip-macos/actions/runs/40951580893)：build job `123721845041` 的 `Set up signing` = **skipped**、`Remove signing keychain` = skipped；Package = success。release job = skipped。`GET /repos/karstx/usbip-macos/releases` 此次返回 `[]`。
- 因此「signed DMG from CI」的 commit 摘要僅描述流程能力，**不是這次 artifact 已有 Apple 授權的證據**。沒有必要再下載並嘗試 attach。若將來發布授權 binary，可先唯讀驗簽/profile，再做明確的 DualSense 裝置相容性測試。

SagerNet 固定版本：[`sing-usbip@28bd42667ecac597d3e19492b9e81ee04e22792f`](https://github.com/SagerNet/sing-usbip/tree/28bd42667ecac597d3e19492b9e81ee04e22792f)；[`sing-box-for-apple@0d1b59fc5e58c8e6b2239e576e4fd979c502a8fb`](https://github.com/SagerNet/sing-box-for-apple/tree/0d1b59fc5e58c8e6b2239e576e4fd979c502a8fb)。[官方 client 文件](https://sing-box.sagernet.org/configuration/service/usbip-client/) 明確要求 sing-box/sing-usbip server。其 README 說 server 可服務標準 client，但自己的 client import 需要額外 sing control extension；不能反向推論可接任意標準 server。`usbhost_darwin.m` 使用受限 host-controller class，integration test 遇該 class 不可用會 skip。Apple repo 的 `SFM/SFM.entitlements`、`SFM.System/SFM.entitlements`、`SystemExtension/SystemExtension.entitlements`、`HelperService/RootHelper.entitlements` 未列該 key；前兩者的 sandbox USB key 不足。這不是已驗證的 signed Mac USB/IP helper。

[Extender README 固定版本](https://github.com/calibrae/extender/blob/52eeb5a84fa0c9e99fe178848c98bf2021a07512/README.md) 自行列出 Mac 端目前載入限制、MDM／Recovery 設定需求。本紀錄只採用它對自己產品現況的說明，不把作者的系統政策解釋當成所有 DriverKit app 都成立的 Apple 官方規則。[usbipd-mac](https://github.com/beriberikix/usbipd-mac) 則是 export server，不能解決本案的 import 方向。

## VirtualHere：簽章可用，不等於自製 DualSense 可接

官方 [下載頁](https://www.virtualhere.com/usb_client_software) 提供 6.0.3、Intel/Apple Silicon、macOS 12+。本輪從該頁下載官方 DMG，以 `hdiutil attach -readonly -nobrowse -noautoopen` 掛載，再使用系統工具唯讀檢查：

| 檢查項目 | 結果 |
|---|---|
| DMG | `VirtualHereUniversal.dmg` |
| 官方 URL | `https://www.virtualhere.com/sites/default/files/usbclient/VirtualHereUniversal.dmg` |
| SHA-256 | `edfbba188187711a3707929aefff68a9b0c260224f28831d1103aa4c325aa437` |
| bundle/version | `com.virtualhere.client` / `6.0.3` |
| architecture | `x86_64`, `arm64` |
| identity | `Developer ID Application: VirtualHere Pty. Ltd. (N8C8ZTN347)` |
| signature timestamp | `2026-10-01 05:16:35 +0800`（本機 codesign 顯示） |
| codesign verify | `valid on disk`; `satisfies its Designated Requirement` |
| Gatekeeper assess | `accepted`; `source=Notarized Developer ID` |
| binary entitlement | `com.apple.developer.usb.host-controller-interface = true` |
| embedded profile | 同 key = true；Team `N8C8ZTN347`；有效期至 `2040-10-03T01:56:10Z` |

完整去識別收據：`inspection/VIRTUALHERE-6.0.3-VERIFY.json`。這些結果確認簽章及靜態授權，**未確認執行後能建立裝置，更未確認 Sony 相容性**。官方論壇有 [macOS 26.5.1 discovery regression 與 6.0.0 修正](https://www.virtualhere.com/node/4831)；它不是 DualSense 或本機 26.6.2 的驗證紀錄。

### 協定與狀態輸入

[官方 Client API](https://www.virtualhere.com/client_api) 的 `/tmp/vhclient`、`/tmp/vhclient_response` 是文字命令 IPC；`LIST`、`USE`、`STOP USING`、hub／auto-use 設定及 `CUSTOM EVENT` 用來控制已有 VirtualHere Server 裝置。文件沒有宣告建立任意 USB 裝置、供應 descriptor、提交 HID input report，或將 standard USB/IP server 當來源的 API。

官方下載頁也限定來源為 VirtualHere USB Server。它的 Linux client 使用 kernel USB/IP driver，只說明 client 與本機 driver 之間的實作，**不能據此斷言 wire protocol 與 VIIPER 相容**。另一標準 USB/IP server 作者在 [trackpad-over-ip 的 Windows client 文件](https://github.com/mushhzz/trackpad-over-ip/blob/37af05e449085988e23a92312ea36d995ac60262/docs/windows-client.md) 明確記錄 VirtualHere proprietary protocol 不能接其 daemon。這是該 server 作者的一手相容性說明，並非本輪封包互通測試。

所以現在不能把 VIIPER port 改給 VirtualHere 就宣稱成功，也不應只重用它的簽章去載入自製程式。這輪沒有逆向 VirtualHere、修改其 binary 或借其權限執行其他 code。

### 免費範圍及轉接構想

[官方 Generic Linux Server license](https://www.virtualhere.com/embedded_server_license) 允許每台 server 同時分享一個 USB 裝置而不用付款；optimized build、多裝置另有付費條件。這讓一般使用者單裝置透傳具有免費選項，但不是開源授權，亦不代表可以改包或隨產品轉售。沒有把「商用軟體」誤判成「所有用途都一定付費」。

仍缺少它能接收自製 synthetic DualSense 的公開入口。把實體手把透傳回 Mac 不會使本程式的鍵盤狀態變成該手把的 input。另一個構想是 Linux VM 先 attach VIIPER，再由 VirtualHere Server 重新 export；但 [官方 FAQ](https://www.virtualhere.com/oem_faq) 的原問為 “Can VirtualHere Server run in a VM or Container?”，回答 “No, it may not function correctly.”，接著說明 Server 需要沒有 hypervisor 隔層的實體硬體存取，另註 Client 可在 VM 執行。本輪亦未找到 Server 支援重新 export `vhci_hcd` 虛擬裝置的官方證據，因此不列為可執行方案。

### DualSense feature 支援不能推論

沒有找到這些 Mac 接入端對本專案 emulated DualSense 的 descriptor、interrupt input、控制端 GET_REPORT／SET_REPORT、取消／斷線、Sony Remote Play 接受度的完整測試。單有 input encoder 及 Sony VID/PID 不足；缺少的 feature 不能回假的成功。本 repo 已有純報告 encoder 可留作日後 adapter，但在接入端通過前不建立裝置。VirtualHere 下載頁亦明說 Mac 不支援 microphones/webcams，因此不能承諾完整 DualSense composite/audio 功能。

## 可接續的具體工作與本輪清理

1. 可立即繼續純碼工作：保留既有 DualSense encoder／fixtures，以及 VIIPER control/feature 協定測試；這不會解決簽章，應清楚分開報告。
2. 標準 USB/IP 最小外部前提：`usbip-macos` 上游發布可驗證的授權 bundle/profile。先查簽章和 CI，符合才準備單裝置、neutral、可清理的測試，不把 ad-hoc CI 產物當正式版。
3. VirtualHere 若要採用，需要官方提供可用的 custom/synthetic server 接口或明確互通支援及授權；目前未有，不能排定真機 DualSense 測試。也未聯絡作者或送出訊息。
4. 唯讀映像於檢查後卸載；僅保留收據，移除本輪 DMG 與暫存解碼 profile。未將 app 複製到 Applications、未註冊服務、未執行第三方程式。沒有觸碰 Sony 或其他 agent 的裝置 session。
