# v0.1.2 發布與驗證紀錄

日期：2026-10-30（Asia/Taipei）。本版更新說明：**美術優化**。

[GitHub Release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.2) ID `428778947`，正式發行、非 prerelease；固定來源 `da12240649df74dc4e2846404317e8a91b231a05`。四個附件已匿名下載核對 bytes 與 SHA-256；正式發布不等於 PS5 逐項實機驗收。此次沒有代使用者啟動一般模式。

## 驗證與限制

- 最終乾淨建置通過 6912 項 C# 檢查（162＋2890＋149＋276＋101＋36＋167＋3131）、5743 項 native 合成報告及 C# ABI；後端沿用原 r4 與既有 Go 測試證據，未重跑 Go。
- 40／40 項公開包稽核通過，包含版本、來源、封裝、依賴、授權、回饋及不包含本機路徑的檢查。九語主畫面已目視；九語設定頁與已審 v0.1.0 畫面雜湊相同。另有 11 個開發階段離線排版情境；不代表真實高 DPI、母語或多螢幕驗收。
- 個人鍵位、連點、F8、官方 Remote Play 前景限制、切窗停止連點、贊助與回饋的既有程式流程保留；沒有新增 Sony／PS5 逐項實機回報。alpha.9 的使用者實機回報與未指定 build 的 Final Fantasy Resonance 單款回報不代替本版驗收。
- 三個執行元件未簽章。仍需外部完整匹配的 usbip-win2 0.9.8.1；沒有更換、降級或重啟驅動，沒有啟停 Sony。舊 PS4 來源及 Git 標籤保留；僅最新與上一個正式版本提供公開下載，更早的 Release 與附件轉為草稿保存。

## 附件

| 附件 | Bytes | SHA-256 |
| --- | ---: | --- |
| Windows ZIP | 6284535 | `9EC49BFAAB4A5928D92A8229E776DBB8121882763A64EB0D5688F6B3FC36E56A` |
| Windows 對應來源 ZIP | 15349342 | `11E7B71AA9B59407D9E7E597473A501EACCACF89570342A3F47D426133E95641` |
| RELEASE.json | 618 | `DC482D54CF8DCF7FBE54050A241CBC37C36105821A8997A60DF5D8ACDA8483E4` |
| SHA256SUMS.txt | 275 | `E17C90E7844C1F97628E3FD443C25E2F9319AC1F0E1E0E9597E27AFA35B5CB79` |

Windows 對應來源包包含固定上游、wrapper、已連結 Go 模組、Windows 專案來源與重建材料；獨立的 Mac 研究與 Mac 程式不納入 Windows 來源包，完整儲存庫仍保存於該固定提交。詳見 [BUILD](../../packaging/build-records/0.1.2.json)、[附件清單](../../packaging/build-records/0.1.2-archives.json)和[重建說明](https://github.com/xiunlee/ps-remote-play-keyboard/blob/v0.1.2/docs/REBUILD.md)。

本機新版位於 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.2`，已另建封存與桌面捷徑。本輪清理確認刪除 11 項、848,558,583 logical bytes，失敗 0；102 項原已不存在，2 份小型歷史 log 保留。這是檔案邏輯大小，不等於實際磁碟淨增加空間。新版、個人設定及備份、驅動、Sony 程序和舊標籤均經前後保護核對。
