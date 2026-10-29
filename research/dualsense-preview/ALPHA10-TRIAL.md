# alpha.10 功能整合與交付紀錄

日期：2026-10-29（Asia/Taipei）。版本 `0.1.0-alpha.10`，乾淨來源提交 `8359c1ded5cf3310ad85ed3b42e0f47fd55da586`。

## 已交付的功能

- 依 [alpha.9 使用者操作回報](ALPHA9-TRIAL.md)，只保留第二種一般 DualSense／純手把 `dualsensegamepadv5`。
- 接回共用 22 動作改鍵及每鍵連點設定：關閉／按住／按一下啟停，20–5000 毫秒，恢復預設。僅儲存成功後才套用，取消或失敗保留原設定。
- 保留 Sony 官方 Remote Play 前景限制、切窗清除連點、F8 暫停、編輯期間暫停與自有裝置清理保護。
- 移除 30 分鐘測試期限；後端停止時才啟動 15 秒清理期限，正常連線沒有時間上限。不代表長時間或異常恢復已實機驗收。

## 建置與離線驗證

使用 `Build-Candidate.ps1 -RunTests -RequireClean`，指定新受限後端與原雜湊的 mapping DLL，完整建置通過：

| 檢查 | 結果與範圍 |
| --- | --- |
| 前端保護 | 90 項；選定模式、裝置歸屬、合成設定儲存及鎖檔失敗保留 |
| 假伺服器傳輸 | 149 項；V5 framing、持續回饋、停止與自有 bus 清理 |
| 設定 | 276 項；格式、獨立草稿、儲存／重新載入 |
| 連點 | 101 項；假時鐘與合成事件 |
| 輸入流程 | 36 項；失焦、重按及原生映射 |
| 設定頁互動 | 122 項；22 動作、保留鍵、改鍵與連點草稿 |
| 後端 | 4 個頂層純函式測試，含清理 watchdog 的 2 個子情境 |
| 產物 | 安裝目錄的版本、來源、雜湊、單一模式、無使用期限及 notices 全部核對通過 |

最終執行檔以離線模式產生[主畫面](../../assets/previews/alpha.10/main-preview.png)與[設定頁](../../assets/previews/alpha.10/editor-preview.png)，均已目視檢查。預覽採預設合成設定；沒有讀寫個人設定、鍵盤擷取、F8 註冊、啟動玩家後端、Windows 接入或 Sony 操作。

## 本機交付

- 目錄：`%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.10`；入口 `PSKeyboard-DualSensePreview.exe`。
- 桌面捷徑：「上班不要玩 0.1.0-alpha.10」。參數只有本版 `status.json` 路徑，開啟不自動連接或啟用。
- 最新封存：`%USERPROFILE%\.pskeyboard-tools\versioned-archives\0.1.0-alpha.10`，含 runtime ZIP、本專案 source ZIP、固定上游來源 ZIP 與清單。
- [建置紀錄](../../packaging/build-records/0.1.0-alpha.10.json)、[封存雜湊](../../packaging/build-records/0.1.0-alpha.10-archives.json)可追溯實際來源與產物；沒有公開二進位 Release 或開始販售。
- 未替使用者啟動一般模式；使用者開啟新版、按「連接手把」，切回 Remote Play 後按 F8。

## 驗收界線

alpha.9 四方案有使用者透過 Sony 官方 Remote Play 控制 PS5 的成功回報；**alpha.10 此一執行檔尚未取得使用者實機驗收**。待驗：既有設定沿用、改鍵後重開保留、三種連點、切窗與 F8 全停、重新連接、長時間及重開機。此次沒有重跑驅動安裝測試，沒有更動 0.9.8.1，也沒有對 Sony 發出啟動、關閉、更新或重啟命令。

本機清舊結果另於完成後補記；個人設定與備份不隨程式清除。舊標籤與歷史 PS4 原始碼保留；本機清理後不再假定舊執行檔存在。
