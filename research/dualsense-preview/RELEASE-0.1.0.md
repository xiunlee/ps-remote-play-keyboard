# v0.1.0 正式發布與驗證紀錄

日期：2026-10-29（Asia/Taipei）。固定標籤 `v0.1.0`，乾淨來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`。

**本版已正式公開發布及本機交付。** [GitHub Release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0)，ID `427775269`，`draft=false`、`prerelease=false`，發布時已設定並 GET 核對為 latest。四附件均匿名重新下載，大小及 SHA-256 相同；未代使用者啟動一般模式。本版尚無逐項實機驗收。

依使用者要求，首發由 alpha 名稱改為三段 `0.1.0`；後續 `0.1.1` 至 `0.1.9`，再到 `0.2.0`，不改寫歷史 alpha 標籤或附件。命名不會改變實測範圍。發布後使用者要求非中文介面與英文文件改用 **PS Remote Play Keyboard**，該修正接續於 `0.1.1`，不回填到本版二進位。

## 內容與使用範圍

沿用一般 DualSense 純手把 `dualsensegamepadv5`、22 動作改鍵、每鍵連點、官方 Remote Play 前景限制、切窗停止並清除連點、F8 暫停與主動儲存設定。主畫面和設定頁九語，中英文手冊說明 2D／JRPG 定位；沒有滑鼠瞄準或右搖桿方向映射，不適合需要靈活視角或精細類比控制的 3D 遊戲。

2026-10-29 使用者回報單一款《Final Fantasy Resonance》可正常操作，遊戲名依使用者提供記錄。回報未指定本工具 build，因此不等於本版逐鍵、連點、重連或長時間驗收，也不保證所有 JRPG。alpha.9 的四方案成功操作回報保留於 [原紀錄](ALPHA9-TRIAL.md)。

本版所有功能免費，作者 xiunlee，原創碼 GPL-3.0-only，第三方授權保留。[PayPal 自願 US$1](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)幫三隻貓咪買罐頭，不影響功能；回饋 `xiun1019@gmail.com` 僅開郵件草稿。

## 驗證

| 項目 | 結果 |
| --- | --- |
| C# 檢查 | 5834 通過：162 保護條件／支援連結、2472 主畫面資源、149 fake loopback 傳輸、276 設定、101 連點、36 輸入流程、167 UI 互動、2471 共用語言資源 |
| native／ABI | 5743 項合成報告及 x64 C#／native ABI 通過 |
| 版號規則 | 抽取實際腳本版本決策的 67 項純邏輯檢查通過；正式版仍強制九語驗證 |
| 後端 | 沿用相同 r4 成品與其先前 4 個 Go 頂層／2 個子情境結果；本輪未重跑 Go |
| 公開包 | 35／35 獨立檢查通過，含來源、ZIP、雜湊、授權、版本中繼資料及回饋主旨 |
| 畫面 | [九語主畫面](../../assets/previews/0.1.0)重新目視；九語設定頁與已審 alpha.12 同語 PNG 的 SHA-256 相同，沿用該像素檢查 |

以上均未擷取實體鍵盤、接入驅動或操作 Sony。母語校閱、實際高 DPI／多螢幕、逐鍵、連點、重連、長時間與新電腦安裝仍待驗證。正式發行名稱及 Windows 裝置辨識不等於這些驗收通過。

## 封裝與檔案身分

詳見 [BUILD](../../packaging/build-records/0.1.0.json)、[附件清單](../../packaging/build-records/0.1.0-archives.json)及 [固定版本重建說明](https://github.com/xiunlee/ps-remote-play-keyboard/blob/v0.1.0/docs/REBUILD.md)。三個執行元件均為 **NotSigned**；SHA-256 只供版本與完整性核對。

| 附件 | Bytes | SHA-256 |
| --- | ---: | --- |
| Windows ZIP | 6283796 | `9C28E917B8343C18591BB4A37142EBA32ACA5EEC57F2914A31889DB147F1552F` |
| 完整來源 ZIP | 12416180 | `B29C57B0C7628ADAE8B9DBCB149D788C9D4BE2489CC7A07BCD293742ECD2F939` |
| RELEASE.json | 618 | `35FB2BA5A0C3237FDF2CB76E4F01408A065B0EB11EA1B6E4D9CAC1CBA1BAF1C1` |
| SHA256SUMS.txt | 275 | `FB8298A72DDC35E4E53E0F8E1C4A9A837046B482B4E98A1DA1DD5B3B611C48D2` |

前端 SHA-256 `1764D8A4D37D62AD27F0BE3FD9D1C1E30979AD5B4C5FC6DC3FF77DED401B5D57`；後端 `9CFF2F8BF15DA8C4807BD631A24909FC9EF9820E5F7914F8742B839A981C2A61`；native `7CF2E8DD4FCFF0A8F49589C1E426508D7DB58A5AE263550E0072C6CD669A6978`。

## 本機交付與清理

交付於 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0`，桌面「上班不要玩 0.1.0」指向該版；同版 `versioned-archives` 保存四個公開附件及固定上游來源 ZIP。沒有啟動交付版，沒有改動 Sony 或既有完整 usbip-win2 0.9.8.1。

舊版清理分兩階段：第一階段 92 項、4,006,258,013 bytes 已刪，為版號調整而中斷；第二階段 26 項、1,583,433,983 bytes 完成，`preservationVerified=true`、失敗 0。第一階段 92 路徑另核對皆已不存在。合計 **118 項、5,589,691,996 bytes** 是累計刪除的檔案邏輯大小，包含重建暫存，不等於磁碟淨增加容量。第二階段的 97 個原已不存在項目不重複計算，2 個小型歷史 log 刻意保留。

最終核對保留 0.1.0 執行檔／完整來源封存、個人設定與備份；設定／備份雜湊、驅動檔雜湊、Sony 程序身分與 Git 標籤前後一致。後續 `0.1.1` 開發重新產生的工具與暫存，須等該版交付後另行清理。

Mac 尚未開發。國際品牌修正完成後，下一串從 [MAC-HANDOFF.md](../../docs/MAC-HANDOFF.md)接手，先唯讀核對 Mac 環境與官方 Remote Play，再做隔離輸出可行性驗證。
