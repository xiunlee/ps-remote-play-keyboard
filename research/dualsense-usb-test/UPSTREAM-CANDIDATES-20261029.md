# DualSense 上游候選核對（2026-10-29）

本次即時核對 GitHub 發布資料、最新提交及固定版本原始碼。以下日期均為台灣時間。結論：目前使用的 Alia5 VIIPER v0.9.0 仍是原版最新正式版，但另一個 hbashton 分支已有更完整的 DualSense 複合裝置；不能將原版測試失敗推論為所有 DualSense 模擬都不可行。

## 版本與來源

| 來源 | 最新核對結果 | 與本案相關的差異 |
| --- | --- | --- |
| Alia5 原版正式版 | [v0.9.0](https://github.com/Alia5/VIIPER/releases/tag/v0.9.0)，2026-10-11 06:31 | 目前使用版本；DualSense 為單一 HID 介面。 |
| Alia5 原版開發版 | [20aa112](https://github.com/Alia5/VIIPER/commit/20aa112c6ea47187526dd88d0fd666a68a5f4416)，2026-10-16 00:06；dev-snapshot 同日 00:13 | [比 v0.9.0 多 4 次提交](https://github.com/Alia5/VIIPER/compare/v0.9.0...20aa112c6ea47187526dd88d0fd666a68a5f4416)，變更安裝／啟動介面、建置與版本顯示，沒有更新 DualSense 裝置實作。 |
| hbashton 分支正式版 | [v0.1.2](https://github.com/hbashton/VIIPER/releases/tag/v0.1.2)，2026-08-28 05:29 | 已有含 HID、喇叭／觸覺與麥克風的複合裝置，以及 V5 傳輸。發布說明指出獨立下載檔未使用 Authenticode 簽章。 |
| hbashton 分支候選版 | [v0.1.9-rc4.6.6](https://github.com/hbashton/VIIPER/releases/tag/v0.1.9-rc4.6.6)，2026-09-23 22:33 | 改善啟動／停止清理與 Edge 回覆。對應目前 main [b78e31e](https://github.com/hbashton/VIIPER/commit/b78e31e93e84b4cb7c4b4c15b8aace10749106d2)；實際下載檔簽章尚未核驗。 |

原版與分支分別編號，不能只看 `0.9.0` 與 `0.1.9` 的大小，或只看發布日期，判斷哪一個 DualSense 實作較完整。GitHub 的「latest」介面通常不包含候選版，因此本次另外核對含 prerelease 的發布清單。

## 候選的實質差異

- [原版 v0.9.0 描述](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualsense/descriptor.go) 只有 HID interface 0；[上游 issue #26](https://github.com/Alia5/VIIPER/issues/26) 也指出文件宣稱完整模擬，但當時實作缺少音訊部分。
- [hbashton 固定候選描述](https://github.com/hbashton/VIIPER/blob/v0.1.9-rc4.6.6/device/dualsense/descriptor.go) 使用音訊控制介面 0、音訊輸出 1、音訊輸入 2，以及 HID 介面 3。這提供更接近實體 DualSense 的裝置結構，但尚未證明它能解決本專案遇到的 Sony 辨識問題。
- [候選版協定](https://github.com/hbashton/VIIPER/blob/v0.1.9-rc4.6.6/docs/devices/dualsense.md) 要求明確的 V5 裝置名稱，例如 `dualsensecombinedaudioduplexv5`。控制資料外須加上 16-byte `VPCM` 標頭、版本、封包種類、長度、連續序號與 CRC32；回饋也改為有框架的事件。不能直接把目前的原始 33-byte 輸入與 6-byte 回饋程式接上，也不能只替換後端執行檔。

## 相依條件與下一步界線

[候選版 Windows 安裝文件](https://github.com/hbashton/VIIPER/blob/v0.1.9-rc4.6.6/docs/getting-started/installation.md) 固定 usbip-win2 **0.9.7.7** 的使用者程式／驅動 ABI；本機正常版使用 **0.9.8.1**。文件記載 0.9.7.8 的裝置接入曾造成核心記憶體問題，不應把它延伸成所有其他版本都必然有同一故障，也不應假定目前驅動可直接搭配這個分支。

本次只記錄研究，**未下載或啟動新後端、未更換驅動，也未操作目前正常的程式或 Remote Play**。若後續實作，先在獨立目錄與連接埠完成 V5 協定及使用者空間 USB/IP 驗證，再評估 Windows 驅動相容性；保留目前正常的 DS4 模擬版。正式散布前仍須核對 GPL-3.0-or-later、產生的 MIT client，以及新增的 WDL 等第三方 notices。

本次查閱的發布說明、README 與 issue 搜尋中，**未找到此候選已通過 Sony 官方 Remote Play 9.5 的明確實測證據**。目前只能稱為具有實質差異的下一個候選，不能宣稱已找到已驗證修正，或認定缺少音訊介面就是本次失敗根因。

## 其他參考

[DS5Dongle](https://github.com/awalol/DS5Dongle) 的正式標籤 [v0.7.2-hotfix](https://github.com/awalol/DS5Dongle/releases/tag/v0.7.2-hotfix) 發布於 2026-06-21；主分支最後核對至 [77d378e](https://github.com/awalol/DS5Dongle/commit/77d378effcda11abee87f9be1ac31d6518e87560)，提交日期為 2026-10-28。它需要實體 DualSense 與 Pico 等硬體，僅適合作為複合裝置描述與協定參考，不能當成本專案免手把、免額外硬體的直接解法。
