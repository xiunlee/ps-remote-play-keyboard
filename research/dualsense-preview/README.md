# 上班不要玩：DualSense 鍵盤與連點

**免費開源 · 作者 xiunlee · GPL-3.0-only。** 所有功能免費，贊助完全自願。

目前準備 **alpha.11 公開測試版**，不是穩定版，[下載頁待發布](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11)。alpha.9 四種方案已有使用者透過官方 Remote Play 操作 PS5 的成功回報，指定第二種一般 DualSense／純手把 `dualsensegamepadv5`；alpha.10 已整合 22 動作改鍵及每鍵連點並本機交付。alpha.11 加入贊助、回饋與公開封裝，尚未取得該版使用者實機驗收。

## 需求與啟動

需要 Windows x64、.NET Framework、可連上 PS5 的 Sony 官方 Remote Play，以及完整相互匹配的 **usbip-win2 0.9.8.1 驅動／CLI／DLL**。本包不含驅動，不會自動安裝、更換、降級或重啟；不是免驅動或一鍵安裝程式。版本不符即停止接入，原始套件見 [上游 0.9.8.1](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1)。

1. 完整解壓縮執行包並保留隨附檔案。保持 Sony Remote Play 開啟，先正常退出舊版「上班不要玩」，兩版不能同時接管 F8。
2. 開啟 `PSKeyboard-DualSensePreview.exe`，按「連接手把」。啟動不自動接入，鍵盤控制先暫停。
3. 切回官方 Remote Play，按 **F8** 啟用；再按 F8 暫停。實際鍵位沿用個人設定；未改預設時，十字上下是 **Home／End**，叉鍵是 **↓**。
4. 要改鍵或連點，按「按鍵與連點設定」，選取動作後設定；**按儲存才套用與寫入**，取消保留原設定。編輯及儲存後保持暫停，切回 Remote Play 再按 F8。
5. 結束時正常中斷或退出。若清理未確認，程式阻擋重新接入；回報顯示訊息，不反覆建立裝置、不重開 Remote Play 或改驅動。

| 功能 | 用法 |
| --- | --- |
| 22 動作改鍵 | 選取動作後設定新鍵，可清除；重複鍵、F8 等保留鍵會拒絕 |
| 關閉連點 | 一般按住／放開操作 |
| 按住連點 | 按住該鍵時重複，放開停止 |
| 按一下啟停 | 按一次啟動，再按一次停止；切窗或 F8 也會清除 |
| 間隔 | 每鍵 20–5000 毫秒，預設 100 毫秒；遊戲可能忽略很短的脈衝 |
| 恢復預設 | 先改編輯中的草稿，連點全關、間隔 100 毫秒；儲存才生效 |

## 贊助與回饋

覺得好用的話，歡迎[透過 PayPal 贊助 US$1，幫三隻貓咪買罐頭](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)。

[意見回饋：xiun1019@gmail.com](mailto:xiun1019@gmail.com)

贊助完全自願，不解鎖功能、不建立訂閱。只有點擊「贊助貓咪 US$1」才會開啟瀏覽器，由 PayPal 處理收款；本程式不接收或保存付款資料。回饋信只預填收件人與版本主旨，不自動寄出、不附個人設定或診斷資料。授權見 [LICENSE](../../LICENSE)與 [COPYRIGHT.md](../../COPYRIGHT.md)。

## 保留與限制

- 畫面連線燈表示目前 Windows 輸出通道，不代表自動確認 PS5 接受操作。alpha.9 成功回報不替代 alpha.11 的改鍵、連點、長時間、重連或重開機驗收。
- 鍵盤與連點只在官方 Remote Play 前景生效；切窗清除連點，切回不自動恢復，F8 暫停。連線或輸出失敗即停用控制。
- 開啟只載入既有設定，不重設；僅使用者儲存成功才原子更新 `%LOCALAPPDATA%\PSKeyboard\bindings.json`。取消或失敗不替換現行設定。
- 每次只接一個本程式建立的裝置，只移除核對過的自有 bus／port。使用 loopback USB/IP 33641、API 33642。
- 自 alpha.10 起沒有 30 分鐘使用期限；退出或失連仍清理。音訊、觸覺、自適應扳機、滑鼠、錄製／重播及 macOS 尚未完成支援或驗收。
- 不自行停止、重啟或更新 Sony，不索取 PSN 帳密。個人設定及備份不隨舊程式清除；本機只留最新版的整理不刪 GitHub 舊標籤／PS4 來源。清除舊版後若需回退，依歷史標籤重新準備。

## 建置與公開來源

[Build-Candidate.ps1](Build-Candidate.ps1)從根目錄 `VERSION` 產生版本及雜湊，使用全新目錄並嵌入固定後端雜湊。先用 `backend/Build-Backend.ps1` 建置受限後端，再明列 `-BackendDirectory`、`-Zig`、`-OutputDirectory`、`-RunTests -RequireClean`，最後以 `Verify-Candidate.ps1` 唯讀核對；完整指令見 [版本控管](../../docs/VERSIONING.md)。

alpha.11 改為使用固定 Zig 0.17.0 從未變更的 native 映射來源編譯 DLL，不再要求 `-MappingDirectory` 或舊版二進位檔。後端固定 `hbashton/VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2`，不走原候選的舊 native attach；MATERIALS 公開輸出移除本機個人路徑，沒有改換控制協定。

公開發布前，必須在同一 Release 提供執行包、完整對應原始碼包、適用第三方 notices、重建說明與雜湊。對應原始碼包括本專案精確版本、固定上游、實際 Go 依賴、native 來源及建置腳本；以 [發行工作表](../../packaging/RELEASE-CHECKLIST.md)核對完成情況。

上一階段的 hosted driver fixture 僅供一次性 GitHub Windows VM；不要偽造 runner 環境或將安裝流程帶到使用者電腦。玩家後端使用本目錄另一個受限入口；歷史證據見 [驅動驗證](../dualsense-driver-test/RESULTS-20261029.md)、[alpha.9 操作回報](ALPHA9-TRIAL.md)及 [alpha.10 交付紀錄](ALPHA10-TRIAL.md)。
