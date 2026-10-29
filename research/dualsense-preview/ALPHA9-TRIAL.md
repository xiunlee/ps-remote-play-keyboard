# alpha.9 四方案交付與待驗收紀錄

日期：2026-10-29（Asia/Taipei）。來源 `18c370cb528bbc2107827bebb7799bdb095e133b`，版本 `0.1.0-alpha.9`。此版是獨立 DualSense 測試工具，原 alpha.8 PS4 保留。

## 已完成

- [四方案的 0.9.8.1 hosted Windows 驗證](../dualsense-driver-test/RESULTS-20261029.md)：每方案 63 項檢查，22 個動作按下／放開與自有裝置移除通過。
- 候選後端三個純函式測試通過；來源固定、四種 persona、stdin 關閉與 30 分鐘限制，未在本機啟動後端。
- 最終乾淨建置：52 項前端保護、149 項假伺服器傳輸、276 項設定、101 項連點、36 項輸入流程檢查通過。只用合成事件／獨立暫存檔，沒有捕捉真鍵盤或本機 Windows attach。
- 開發建置曾因測試編譯漏列 `PhysicalReportMerge.cs` 而停止；修正建置來源後，以上最終建置完整通過。未將失敗的開發產物交付，也未覆寫其目錄。
- 最終執行檔以離線模式輸出畫面並檢視：四方案選單、未連接／暫停狀態、版本與操作說明正常。此模式使用預設假設定，不執行 hook、F8 註冊、後端或 Sony 操作。
- 交付至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.9`，入口 `PSKeyboard-DualSensePreview.exe`；桌面新增「上班不要玩 0.1.0-alpha.9 四方案測試」。捷徑只有候選目錄的 status 路徑，**沒有自動接入參數**。
- 封存至 `%USERPROFILE%\.pskeyboard-tools\versioned-archives\0.1.0-alpha.9`。公開 [建置紀錄](../../packaging/build-records/0.1.0-alpha.9.json)、[封存雜湊](../../packaging/build-records/0.1.0-alpha.9-archives.json)；沒有上傳第三方執行檔或建立二進位 Release。

交付時唯讀核對：個人設定雜湊、alpha.8 三個執行元件及四個舊標籤均與基準一致。Sony Remote Play 執行中；本輪期間使用者回報已開好 Remote Play，觀測到的 Sony 程序識別與先前基準不同，因此不宣稱整段研究期間都是同一程序，也不判定變動原因。工具沒有對 Sony 發出啟動、關閉、重啟或更新命令。未啟動本機玩家 GUI／驅動接入；只執行其離線繪圖模式。

## 等待使用者操作結果

| 選項 | 本機 Windows 接入 | 官方 Remote Play 控制 PS5 |
| --- | --- | --- |
| 一般 DualSense，含音訊介面 | 待測 | 待測 |
| 一般 DualSense，僅手把 | 待測 | 待測 |
| DualSense Edge，含音訊介面 | 待測 | 待測 |
| DualSense Edge，僅手把 | 待測 | 待測 |

由使用者保持 Remote Play 開啟，逐一選方案、手動連接、切回 Remote Play 並按 F8。換方案先中斷自己的裝置；清理未確認時不繼續建立。按鍵以實際設定／畫面提示為準；新預設的 Home／End 才是十字上下。

通過標準是使用者確認 PS5 畫面真的接受操作，再檢查切窗停止、連點清除、F8 暫停及切換／回退；不把綠燈、Windows 裝置名稱或 Sony handle 當作遊戲成功。音訊串流、觸覺、長時間與重開機仍另列待驗收。
