# 架構評估

## 產品目標

使用者希望保留 Sony 官方 PS Remote Play，並以我們的 Windows 程式將鍵盤與滑鼠轉換成手把操作；滑鼠移動控制右搖桿視角。因帳號安全顧慮，使用者不希望改用第三方 Remote Play 客戶端。

使用者於 2026-10-28 明確排除付費軟體依賴，要求維持自製方案；reWASD 不再是候選實作依賴，不安裝或啟用試用。免費開源函式庫可用於自製程式，現行目標仍是官方 Remote Play 與實體 USB DS4。

後續範圍收斂為**只做鍵盤**：按下、長按、放開與複合按鍵應等同手把輸入，不考慮個別遊戲是否原生支援鍵盤。滑鼠及右搖桿視角暫不實作。

## 目前方向：實體手把中繼

使用者要求讓鍵盤操作併入實體手把的輸入，而非繼續建立獨立手把。候選路徑為「實體 USB DS4 → usbipd-win → 自製本機 USB/IP 中繼 → 已安裝的 usbip-win2 → 官方 Remote Play」。中繼只改寫格式已知的按鍵 input report，保留控制、feature 與輸出請求及回覆；不仿造實體手把的驗證回覆。此路徑仍會改變 USB 拓撲和 Windows 裝置路徑，不能聲稱 Sony 一定接受。

經使用者明確授權後，已安裝 usbipd-win 5.3.0，確認服務與簽章有效，並將服務限制在本機使用。針對唯一核對的實體 DS4 切換至 VBoxUSB，避開同機重新捕獲的篩選器；Windows 經中繼回讀兩分鐘正常，Sony 也已開啟匯入手把的介面。這還不能證明 PS5 接受鍵盤：畫面操作結果與原始驅動還原仍待驗證，詳見[本機實測紀錄](../research/physical-usb-relay/LIVE-TRIAL.md)。

## 已測試的獨立 USB 輸出候選

[鍵盤專用程式](../src/keyboard-pad/README.md)把鍵盤轉成 DS4 狀態，透過獨立的 VIIPER 0.9.0 本機服務及 usbip-win2 0.9.8.1 提供另一個 USB DS4。它不是實體 PS4 手把的輸入過濾器；官方程式與實體／虛擬手把同時存在時的選擇行為尚待驗證。

實際映射程式透過 VIIPER 回讀 USB/IP 報告的 16 項檢查已通過。後續[Windows HID 實測](../research/windows-hid-test/README.md)也通過 20 項檢查，證明真正的 Windows 手把介面能讀到合成狀態。再以[唯讀觀測程式](../research/windows-hid-observer/README.md)確認使用者真實鍵盤按鍵能進入 Windows，但使用者在 Sony 中看不到控制效果，暫時拔掉實體手把後仍然無反應。未做只保留虛擬裝置後重新啟動 Sony 的另一輪測試。部分認證 feature report 未實作，但尚未證明這就是無效原因。詳見 [候選評估](../research/static-analysis/VIIPER-ASSESSMENT.md)。

使用者已明確同意安裝及實測。安裝前建立了系統還原點，官方安裝檔通過 SHA-256 與 Authenticode 核對；安裝回傳 3010，驅動目前已啟動，系統尚未重開機。沒有重新安裝 ViGEmBus、變更 PSN 登入、啟用測試簽章或關閉 Windows 保護。

## 先前 ViGEm 原型的缺口

目前程式透過第三方 ViGEmBus 建立虛擬 DUALSHOCK 4，必須另行安裝驅動；ViGEm 已停止維護，且有 Remote Play 6 以後不相容的回報。曾在本機確認 ViGEmBus 1.22.0 能建立 Windows 辨識的虛擬 DS4。測試期間 Remote Play 9.5 兩次當機；關閉測試程式及虛擬手把後，Remote Play 可再次啟動。這些觀察提示可能有相容性問題，但不足以確認當機的根本原因。依使用者要求已移除 ViGEmBus；PS5 操作仍未驗收。

## 候選方向

1. **自製虛擬手把驅動並整合安裝流程**：保留官方 PS Remote Play，開發我們的虛擬裝置驅動和鍵鼠映射，透過單一安裝程式安裝。這仍需在 Windows 安裝驅動；系統權限與驅動簽章需求不能省略。必須先驗證官方 Remote Play 9.5 是否能辨識並使用該虛擬裝置，不能假設普通 HID 回報就能工作。
2. **我們的程式直接連接 PS5**：將影像串流及控制功能整合到自己的 Remote Play 用戶端，鍵鼠直接轉成控制資料，不需要在 Windows 建立虛擬手把。這會取代 Sony 官方 Remote Play 視窗，不符合原先「開啟官方程式」的前提，需另行確認需求。採用現成程式碼時須核對授權條件。
3. **介入官方程式的輸入流程**：可能避免系統層虛擬裝置，但依賴官方程式的內部行為，更新後容易失效，並且目前沒有證據顯示它支援本機的 9.5 版。不能當作已驗證方案。

## 2026-10-28 網路查證

- [reWASD 的實體裝置模式](https://help.rewasd.com/basic-functions/how-to-apply-config-to-physical-device.html)：廠商文件明確涵蓋鍵盤、滑鼠和實體 DUALSHOCK 4 群組，也提及 PS Remote Play。文件限制的 relative sticks 是組合動作中的搖桿偏移，不代表一般滑鼠轉右搖桿不支援。目前沒有確認本機官方 Remote Play 9.5 加上滑鼠視角實際可用，亦尚未安裝或測試該工具。
- [mouseplay](https://github.com/ko1N/mouseplay)：保留官方程式的開源研究案例，需實體手把，尚無正式版；其文件以舊版 32-bit Remote Play 為目標，而本機 9.5 是 64-bit，不能直接視為相容。
- [Sony 官方 Remote Play](https://www.playstation.com/en-us/remote-play/)：Windows 可以在支援實體鍵盤輸入的遊戲中傳送鍵盤輸入；這不等於把鍵鼠模擬成手把供所有遊戲使用。
- [使用者的 9.5 閃退回報](https://www.reddit.com/r/remoteplay/comments/1w2h35b/i_finally_found_the_fix_for_ps_remote_play/)：部分使用者報告移除 ViGEmBus 後恢復；這與本機現象一致，但不是根因證明。

## 驗收條件

- 一套安裝與操作流程，使用者不需另外下載第三方手把工具。
- 如選擇驅動方案，清楚說明 Windows 需要安裝及簽章，且實際在 Remote Play 9.5 與 PS5 測試通過。
- 依目前鍵盤限定範圍驗證左搖桿、按鈕、扳機、觸控板按下與 PS 鍵；暫停或失焦時必須停止合成輸入。實體手把仍維持原本操作，滑鼠不在本階段驗收範圍。
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

## 先前實體手把方案的結果

本次未能交付「官方 Remote Play 9.5 + 實體 PS4 手把 + 鍵盤與滑鼠模擬手把」的可用成品。靜態檢查尚未定位官方程式實際讀取手把的路徑；離線資料測試不能驗證這條路可用。使用者確認官方 Remote Play 的鍵盤在系統及部分遊戲可用，問題集中在不接受鍵盤輸入的遊戲。現有程式仍是 ViGEmBus 舊原型，無法滿足目前需求。

## 歷史研究：reWASD 實體輸出（已排除）

這是一條有廠商明確文件、但尚待本機驗證的路線，不能再把未定位 Sony 內部函式等同於所有軟體方案不可行。計畫保留 Sony 官方視窗與現有 USB DS4，先驗證單鍵轉成 ×，再加入 WASD 左搖桿及滑鼠右搖桿。必須明確選用實體 DS4 輸出，避免直接使用預設的虛擬 DS4 設定。

官方 API 支援建立裝置群組、匯入配置、套用及停用。它沒有逐鍵編輯或原始手把輸入注入介面；因此需要一份現版、實體輸出已正確選定的完整配置，才可能整合進本專案前端。[API 研究](../research/rewasd-api/API-INTEGRATION-LIMITS.md)記錄了此限制。

目前已下載並驗證安裝檔，另以唯讀方式提取內附配置，製作本機候選檔；未執行安裝、登入或套用映射：

- 來源：reWASD 官網的公開下載端點 `https://www.rewasd.com/api/download?platform=win`。
- 版本：9.5.0.11346；大小：81,272,296 bytes。
- Authenticode：Valid，簽署者 SIA "AVB Disc Soft"。
- SHA-256：`FE883671B41CD00AA100DD183B895BA681E0067263CEFC27F55A7F3762D02EAA`。

候選配置使用內附現版範本中實際存在的 `applyToPhysicalPriority` 欄位，移除舊範本的虛擬輸出強制設定及四個不支援實體模式的 relative-stick 動作。靜態檢查無缺失的 mask／command 參照，但尚未匯入 reWASD，不能據此聲稱輸出已生效。完整候選及原廠範本保留本機，公開儲存庫只記錄差異。

這個候選方案新增商用授權依賴。官方提供 7 天免費試用，首次需 reWASD 帳號，安裝驅動後需重啟；不涉及第三方 PSN 登入。使用者已拒絕此依賴，維持未安裝狀態。文件與 API 能力不等於 Remote Play 9.5 實機通過。

## 參考

- [Microsoft：Virtual HID Framework](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-)
- [Microsoft：虛擬驅動的簽章要求](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-requirements--windows-vista-and-later-)
- [XI2DS：Remote Play 6 與 ViGEm 的相容性回報](https://github.com/micro-artwork/xi2ds)
- [PS4 Keyboard and Mouse Adapter：新版 Remote Play 相容性說明](https://github.com/starshinata/PS4-Keyboard-and-Mouse-Adapter)
