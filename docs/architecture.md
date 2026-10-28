# 架構評估

## 產品目標

使用者希望保留 Sony 官方 PS Remote Play，並以我們的 Windows 程式將鍵盤與滑鼠轉換成手把操作；滑鼠移動控制右搖桿視角。因帳號安全顧慮，使用者不希望改用第三方 Remote Play 客戶端。

## 目前原型的缺口

目前程式透過第三方 ViGEmBus 建立虛擬 DUALSHOCK 4，必須另行安裝驅動；ViGEm 已停止維護，且有 Remote Play 6 以後不相容的回報。曾在本機確認 ViGEmBus 1.22.0 能建立 Windows 辨識的虛擬 DS4。測試期間 Remote Play 9.5 兩次當機；關閉測試程式及虛擬手把後，Remote Play 可再次啟動。這些觀察提示可能有相容性問題，但不足以確認當機的根本原因。依使用者要求已移除 ViGEmBus；PS5 操作仍未驗收。

## 候選方向

1. **自製虛擬手把驅動並整合安裝流程**：保留官方 PS Remote Play，開發我們的虛擬裝置驅動和鍵鼠映射，透過單一安裝程式安裝。這仍需在 Windows 安裝驅動；系統權限與驅動簽章需求不能省略。必須先驗證官方 Remote Play 9.5 是否能辨識並使用該虛擬裝置，不能假設普通 HID 回報就能工作。
2. **我們的程式直接連接 PS5**：將影像串流及控制功能整合到自己的 Remote Play 用戶端，鍵鼠直接轉成控制資料，不需要在 Windows 建立虛擬手把。這會取代 Sony 官方 Remote Play 視窗，不符合原先「開啟官方程式」的前提，需另行確認需求。採用現成程式碼時須核對授權條件。
3. **介入官方程式的輸入流程**：可能避免系統層虛擬裝置，但依賴官方程式的內部行為，更新後容易失效，並且目前沒有證據顯示它支援本機的 9.5 版。不能當作已驗證方案。

## 2026-10-28 網路查證

- [reWASD 的實體裝置模式](https://www.help.rewasd.com/basic-functions/how-to-apply-config-to-physical-device.html)：廠商文件描述可以把鍵盤、滑鼠和實體 DualSense 分組，並以實體手把作為輸出，也提及 PS Remote Play；文件同時指出部分搖桿映射仍需虛擬手把。目前沒有確認 Windows 官方 Remote Play 9.5 加上滑鼠視角實際可用，亦尚未安裝或測試該工具。
- [mouseplay](https://github.com/ko1N/mouseplay)：保留官方程式的開源研究案例，需實體手把，尚無正式版；其文件以舊版 32-bit Remote Play 為目標，而本機 9.5 是 64-bit，不能直接視為相容。
- [Sony 官方 Remote Play](https://www.playstation.com/en-us/remote-play/)：Windows 可以在支援實體鍵盤輸入的遊戲中傳送鍵盤輸入；這不等於把鍵鼠模擬成手把供所有遊戲使用。
- [使用者的 9.5 閃退回報](https://www.reddit.com/r/remoteplay/comments/1w2h35b/i_finally_found_the_fix_for_ps_remote_play/)：部分使用者報告移除 ViGEmBus 後恢復；這與本機現象一致，但不是根因證明。

## 驗收條件

- 一套安裝與操作流程，使用者不需另外下載第三方手把工具。
- 如選擇驅動方案，清楚說明 Windows 需要安裝及簽章，且實際在 Remote Play 9.5 與 PS5 測試通過。
- 驗證左搖桿、滑鼠視角、按鈕、扳機、觸控板按下與 PS 鍵；暫停或失焦時必須釋放輸入與游標。
- 若改用自建串流客戶端，必須明示不再使用 Sony 官方視窗，並實測連線及操作。

## 實體 PS4 手把研究

- 本機的 USB DUALSHOCK 4（VID 054C / PID 05C4）已由 Windows 辨識，使用者確認官方 Remote Play 連上 PS5 後能以實體手把正常操作。
- 一般應用程式讀取手把的 HID input report；寫入 HID output report 用於手把輸出功能，不能假設它會改變其他程式讀到的按鍵。詳見 Microsoft 的 [讀取 HID 報告](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/obtaining-hid-reports-by-user-mode-applications)及[傳送 HID 報告](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/sending-hid-reports)。
- [mouseplay 的舊版原始碼](https://github.com/ko1N/mouseplay/blob/master/mouseplay/src/hooks.rs)曾在官方程式讀取手把資料時進行攔截；它的文件針對舊版 32-bit Remote Play，本機 9.5 是 x64 且模組結構不同，不能直接沿用。介入官方程式可能造成當機或接觸程序內資料，必須先研究必要性、安全性和相容性。
- 另一種研究方向是系統層的實體裝置輸入過濾；[Microsoft HID filter 範例](https://github.com/microsoft/Windows-driver-samples/tree/main/hid/firefly)僅供理解驅動架構，並非現成的鍵盤映射解決方案。任何驅動都有安裝及簽章要求。
- 目前以[離線的 USB DS4 報告合成實驗](../research/ds4-report/README.md)驗證資料格式，不注入、修改或控制正在運行的 Remote Play，也不存取 PSN 資料。離線格式測試成功不代表官方 Remote Play 會接收合成輸入。
- 已以[只讀 HID 能力檢查](../research/ds4-report/HidCapsProbe.cs)確認本機實體 USB 手把的最大 input report 為 64 bytes；這不證明實際報告 ID 或 Remote Play 的讀取方式。本機 Remote Play 9.5 x64 已載入 HID.DLL 和 SETUPAPI.dll；靜態依賴檢查未能確定讀手把的函式或可用攔截點。
- 從磁碟只讀掃描 RemoteplayUI.dll 和 RemotePlayUIWrapper.dll 的受管類型與方法名稱，沒有找到可辨識為手把讀取路徑的方法；這並不排除程式使用原生碼、動態載入或其他名稱。
- Windows 的 ReadFile 可能採用非同步 I/O；回傳待處理時緩衝區尚未完成，若將來研究攔截，必須正確處理完成、取消與緩衝區生命週期，不能在呼叫返回時假設資料已經就緒。參考 [Microsoft ReadFile](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-readfile)。

## 參考

- [Microsoft：Virtual HID Framework](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-)
- [Microsoft：虛擬驅動的簽章要求](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-requirements--windows-vista-and-later-)
- [XI2DS：Remote Play 6 與 ViGEm 的相容性回報](https://github.com/micro-artwork/xi2ds)
- [PS4 Keyboard and Mouse Adapter：新版 Remote Play 相容性說明](https://github.com/starshinata/PS4-Keyboard-and-Mouse-Adapter)
