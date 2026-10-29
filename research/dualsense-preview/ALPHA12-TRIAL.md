# alpha.12 九語介面公開發布與驗證紀錄

日期：2026-10-29（Asia/Taipei）。版本 `0.1.0-alpha.12`，乾淨來源提交 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`，固定標籤 `v0.1.0-alpha.12`。

**本版已公開發布及本機交付，尚無本版逐項實機驗收。** [GitHub pre-release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.12)，Release ID `427756025`，`draft=false`、`prerelease=true`，四個附件匿名下載後大小及 SHA-256 全數相同。後續使用者指定首個三段版號為 `0.1.0`；本版標籤與附件維持原樣。

## 本版變更

主畫面、連線／錯誤訊息、按鍵設定、贊助／回饋及郵件主旨補齊九語：英文、繁中、簡中、日文、韓文、德文、法文、西班牙文、巴西葡語。依 Windows 介面語言自動選擇，未支援語言回退英文。主畫面 50 個資源鍵，共用設定頁 93 個資源鍵；品牌、技術診斷碼及必要按鍵名稱維持原文。自動換行／捲動及設定頁短按鈕避免長文字截斷；沒有新增語言選單。

程式、中英文手冊及發布頁明示 **2D 遊戲定位，尤其適合選單與方向操作為主的 JRPG**。目前沒有滑鼠瞄準或右搖桿方向映射，不適合需要靈活視角或精細類比控制的 3D 遊戲；不推論所有 JRPG 都適合。

仍採使用者選定的 `dualsensegamepadv5`、22 動作改鍵／每鍵連點、官方 Remote Play 前景限制、切窗停止並清除連點、F8 暫停、主動儲存才更新設定。未更改傳輸、後端或遊戲輸入核心。免費 GPL-3.0-only、作者 xiunlee；[PayPal US$1 贊助三隻貓咪](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)完全自願，回饋 `xiun1019@gmail.com` 只開郵件草稿。

## 使用者遊戲回報

2026-10-29，使用者明確確認是單一款 **《Final Fantasy Resonance》**，並回報可正常操作。遊戲名稱依使用者提供記錄；回報未指定工具建置版本或逐項功能結果，因此不能追認為 alpha.12 的逐鍵、改鍵、連點、重連或長時間驗收，也不延伸到整個 JRPG 類型。

[alpha.9 四方案成功回報](ALPHA9-TRIAL.md)仍是選擇第二種 DualSense 的實機依據。Windows 辨識、連線燈、離線測試或畫面輸出都不等於 PS5 收到操作；本次沒有代使用者啟動一般模式或接入裝置。

## 離線驗證

| 檢查 | 本版結果 |
| --- | ---: |
| 主程式保護條件及支援連結 | 144 通過 |
| 主畫面九語、格式參數、回饋主旨及錯誤分支 | 2472 通過 |
| fake loopback 傳輸及有限時間清理 | 149 通過 |
| 設定格式／儲存語義 | 276 通過 |
| 連點狀態機 | 101 通過 |
| 鍵盤控制流程 | 36 通過 |
| 設定頁互動／擷取提示 | 167 通過 |
| 共用九語資源與 22 動作 | 2471 通過 |
| **C# 合計** | **5816 通過** |
| native 合成報告 | 5743 通過 |
| x64 C#／native ABI | 通過 |

後端沿用固定 r4 成品與對應來源，SHA-256 未變；其 Go 4 個頂層測試、2 個子情境已通過，本版保存既有純測試紀錄，未把重用成品寫成重新執行 Go 測試。所有檢查界線保留於 [建置清單](../../packaging/build-records/0.1.0-alpha.12.json)；沒有擷取實體鍵盤、變更驅動或操作 Sony。

最終執行檔以離線預覽模式輸出 18 張圖：九語主畫面與九語設定頁，全部經目視檢查。另有 18 個主畫面案例（九語正常／最小視窗加 150% 字體、長錯誤、清理提示與合成未指派鍵）及 36 個設定頁案例（九語、兩種視窗大小、初始／擷取狀態），自動版面檢查無裁切項目。**字體放大模擬不是實際 Windows DPI 測試；母語校閱、高 DPI 及多螢幕仍待驗證。**

| 語言 | 主畫面 | 設定頁 |
| --- | --- | --- |
| English | [預覽](../../assets/previews/alpha.12/main-en.png) | [預覽](../../assets/previews/alpha.12/editor-en.png) |
| 繁體中文 | [預覽](../../assets/previews/alpha.12/main-zh-Hant.png) | [預覽](../../assets/previews/alpha.12/editor-zh-Hant.png) |
| 简体中文 | [預覽](../../assets/previews/alpha.12/main-zh-Hans.png) | [預覽](../../assets/previews/alpha.12/editor-zh-Hans.png) |
| 日本語 | [預覽](../../assets/previews/alpha.12/main-ja.png) | [預覽](../../assets/previews/alpha.12/editor-ja.png) |
| 한국어 | [預覽](../../assets/previews/alpha.12/main-ko.png) | [預覽](../../assets/previews/alpha.12/editor-ko.png) |
| Deutsch | [預覽](../../assets/previews/alpha.12/main-de.png) | [預覽](../../assets/previews/alpha.12/editor-de.png) |
| Français | [預覽](../../assets/previews/alpha.12/main-fr.png) | [預覽](../../assets/previews/alpha.12/editor-fr.png) |
| Español | [預覽](../../assets/previews/alpha.12/main-es.png) | [預覽](../../assets/previews/alpha.12/editor-es.png) |
| Português (Brasil) | [預覽](../../assets/previews/alpha.12/main-pt-BR.png) | [預覽](../../assets/previews/alpha.12/editor-pt-BR.png) |

## 公開包與交付

公開包獨立稽核 **33／33 通過**：runtime 精確 22 檔、source 1666 檔、project 202 檔，ZIP 與展開檔案一致；45 個建置來源逐一對應（3 個 bytes 相同、42 個只差 Git 換行正規化）。固定上游、三個 Go 模組來源、wrapper／native、授權與重建材料均已納入。公開文字與二進位經既定個人路徑及撤回連結掃描；上游測試的通用 Users/foo 例子另行辨識，沒有把它誤當本機資料。

| 附件 | Bytes | SHA-256 |
| --- | ---: | --- |
| [PSKeyboard-0.1.0-alpha.12-windows-x64.zip](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.12/PSKeyboard-0.1.0-alpha.12-windows-x64.zip) | 6283739 | `68339E7B2875F4380226702D8AB3685AE97E8796B7018D93C09C28CFD65E01E8` |
| [PSKeyboard-0.1.0-alpha.12-source.zip](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.12/PSKeyboard-0.1.0-alpha.12-source.zip) | 10977370 | `9B6C48F22472EEFC5695457DC4D1CC61322268D012C45DFA0A13199878982B8E` |
| [RELEASE.json](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.12/RELEASE.json) | 643 | `483BCB0E2AD550B5AD137885B608AF025C09A2C957F7C02B13C53090DF0ABC10` |
| [SHA256SUMS.txt](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.12/SHA256SUMS.txt) | 293 | `0DCD88146EEDB1C78645BC6412EBE4C74D51E1A02BB35A171497B68B6B2A344D` |

| 執行元件 | SHA-256 |
| --- | --- |
| `PSKeyboard-DualSensePreview.exe` | `2AA48790A81FA48E820B0492E96A90F2E5B2984CFC5B52146CC2FA27A0D2C355` |
| `DualSenseCandidateServer.exe` | `9CFF2F8BF15DA8C4807BD631A24909FC9EF9820E5F7914F8742B839A981C2A61` |
| `pskb-keyboard-merge.dll` | `675D8E95D768E223466622CD01093C9938009CAE476CD25D790AFF80D9196FA7` |

三個執行元件均為 **NotSigned**；SHA-256 是版本／完整性核對，不是數位簽章。使用需求仍包含 Windows x64、.NET Framework、Sony 官方 Remote Play 與既有完整匹配的 usbip-win2 0.9.8.1；公開包不附驅動。重建請使用 [alpha.12 固定標籤的說明](https://github.com/xiunlee/ps-remote-play-keyboard/blob/v0.1.0-alpha.12/docs/REBUILD.md)。

本機交付於 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.12`，桌面「上班不要玩 0.1.0-alpha.12」與同版 `versioned-archives` 已核對。未代使用者啟動程式。舊版與工具暫存清理已開始，因使用者隨後指定改用 `0.1.0` 而中斷自有清理程序，不能將這次中途收據寫成全部完成。後續先建置及交付三段版號，再完成剩餘清理；設定／備份及固定標籤不在刪除範圍。

## Mac 接手

Windows alpha.12 的建置、發布與交付已完成，使用者要求的三段版號首發及最終清理接續處理；本版尚待實機逐項回歸、乾淨安裝、重連、長時間及重開機驗收，不宣稱穩定版。Mac 尚未開發；下一串依 [MAC-HANDOFF.md](../../docs/MAC-HANDOFF.md)先核對 Mac 環境與官方 Remote Play，再隔離驗證輸出可行性，不把 Windows 成功外推成 macOS 成功。
