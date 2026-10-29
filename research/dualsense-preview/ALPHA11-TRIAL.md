# alpha.11 免費公開發布與驗證紀錄

日期：2026-10-29（Asia/Taipei）。版本 `0.1.0-alpha.11`，乾淨來源提交 `6dbc154a6a887398796d442fd34b73455672aba3`，固定標籤 `v0.1.0-alpha.11`。

**本版已公開發布及本機交付，尚無本版使用者實機驗收。** alpha.11 的主畫面仍以繁體中文為主；設定頁九語及中英文件不代表整個程式已完成九語。使用者指出此缺口後，Windows alpha.12 的主畫面／訊息完整本地化已列為接續工作，不能追寫成 alpha.11 已完成。

## 發布與來源

- [GitHub pre-release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11)，Release ID `427698167`，`draft=false`、`prerelease=true`；未設為正式 latest release。
- 工作分支及發布後已核對的 GitHub 預設分支為 `codex/commercial-preview`。名稱沿用歷史，產品方向已改為免費開源；沒有將舊 `main` 或歷史標籤移到新內容。
- 作者 **xiunlee**，原創程式採 [GPL-3.0-only](../../LICENSE)；第三方保留各自授權，見 [COPYRIGHT.md](../../COPYRIGHT.md)。所有功能免費，不設付費解鎖、訂閱或 DRM。
- 公開 [建置清單](../../packaging/build-records/0.1.0-alpha.11.json)記錄來源、測試與執行元件雜湊；[附件清單](../../packaging/build-records/0.1.0-alpha.11-archives.json)記錄兩個 ZIP 的大小與雜湊。來源重建方式見 [alpha.11 固定版本的 REBUILD.md](https://github.com/xiunlee/ps-remote-play-keyboard/blob/v0.1.0-alpha.11/docs/REBUILD.md)。

以下四個附件均完成上傳後的名稱、大小及 GitHub SHA-256 digest 核對，並從公開下載網址重新下載、核對實際位元組。發布收據完成時間為 `2026-10-29T10:03:16.7735844Z`；這是發布與下載的證據，不是遊戲操作結果。

| 附件 | Bytes | SHA-256 |
| --- | ---: | --- |
| [Windows x64 執行包](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.11/PSKeyboard-0.1.0-alpha.11-windows-x64.zip) | 6268292 | `04A7880DA46985C17632895169F119BC7DA16D37F8F3550856E1767254556053` |
| [完整對應原始碼包](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.11/PSKeyboard-0.1.0-alpha.11-source.zip) | 10748493 | `B1EE922128434EA74299D042647397E91E7C9AF2E33A473C9EA6E7685664D702` |
| [RELEASE.json](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.11/RELEASE.json) | 643 | `2F2EB6764F1F9B4A8628EFF8827831B8AECE240F8D86236629123C6CF8A3D7F0` |
| [SHA256SUMS.txt](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.0-alpha.11/SHA256SUMS.txt) | 293 | `B6834B7D5A8E4A32FF5BBC1B0BFCA30F6EC9F39A28B484425E1956190BBA3C24` |

## 本版變更

- 沿用 [alpha.10](ALPHA10-TRIAL.md) 的第二種一般 DualSense／純手把 `dualsensegamepadv5`、22 動作改鍵及三種每鍵連點模式，沒有再讓使用者重做四方案選型。
- 沿用官方 Remote Play 前景限制、切窗清除連點、F8 暫停、編輯期間暫停、僅儲存成功才套用及自有裝置清理。沒有恢復 30 分鐘連線期限。
- 加入免費開源、作者、贊助及意見回饋入口。唯一現行 [PayPal 連結](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)為自願 US$1，幫三隻貓咪買罐頭；僅使用者點擊才開瀏覽器，不收集付款資料。回饋固定 `xiun1019@gmail.com`，只開郵件草稿，不自動寄出或附加個人資料。公開收款頁的唯讀核對不等於已付款或驗收入帳。
- 公開首頁及執行包提供繁體中文、英文說明；執行包的 `README.md`、`README.en.md` 使用適合解壓後閱讀的版本，沒有沿用研究目錄的失效相對連結。
- 原生 `pskb-keyboard-merge.dll` 改為由固定 Zig 0.17.0 從未變更的 C 映射來源編譯，不再依賴 alpha.8 的舊 DLL；輸出 `NATIVE-BUILD.json`、`NATIVE-MATERIALS.json` 與 Zig／MinGW／LLVM 通知。新二進位雜湊不冒稱與舊版相同。
- 後端仍使用固定 `hbashton/VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2`、Go 1.27.3 及既有 V5 協定。最終建置使用相對 module replacement，公開材料不含本機個人來源路徑；沒有更動上游正式 CLI 的舊驅動限制。
- 成套 usbip-win2 **0.9.8.1** 仍是外部前置條件，不隨包散布、不自動安裝／更換／降級；Sony 官方 Remote Play 仍是唯一主機連線用戶端。

## 乾淨建置與離線測試

最終建置以 `Build-Candidate.ps1 -RunTests -RequireClean` 完成，`workingTreeDirty=false`；建置清單時間為 `2026-10-29T09:48:57.1592727Z`。C# 合計 **801 項**，計算為 `117 + 149 + 276 + 101 + 36 + 122`，不將 native 或 Go 測試混入此總數。

| 檢查 | 結果及範圍 |
| --- | --- |
| 前端保護 | 117 項；合成設定、單一模式、來源／連結及控制保護，不開一般 GUI 或裝置 |
| 假伺服器傳輸 | 149 項、0 失敗；僅 fake loopback，未操作驅動、Sony 或真鍵盤 |
| 設定 | 276 項；獨立暫存檔與純按鍵事件 |
| 連點 | 101 項；假時鐘與合成動作 |
| 輸入流程 | 36 項；假時鐘與 native merge，不擷取真輸入 |
| 設定頁互動 | 122 項；無真實輸入或裝置 |
| Native C 核心 | 5743 項、0 失敗；合成報告與保留／放開規則 |
| Native C# ABI | 通過；x64、cdecl、8-byte struct、叉鍵覆蓋、放開恢復、右搖桿保留 |
| Go 後端 | 4 個頂層純函式測試，另含停止 watchdog 的 2 個子情境；不啟動後端入口或 socket |

最終執行檔另以離線模式產生[主畫面](../../assets/previews/alpha.11/main-preview.png)與[設定頁](../../assets/previews/alpha.11/editor-preview.png)，交付階段已目視檢查。使用合成預設設定，未註冊真鍵盤 hook／F8、啟動玩家後端或匯入 Windows 裝置。離線預覽不是正在操作 PS5 的畫面，也沒有證明主畫面九語或真實高 DPI 已驗收。

## 公開包稽核與簽章

只讀公開產物稽核 **27 項全部通過**，包括來源提交一致、runtime 白名單、檔案／ZIP／SHA 清單相符、native 建置材料、固定上游、三個實際連結的 Go 模組及授權、wrapper 原始碼、最終贊助設定，以及指定私人路徑與已撤回連結的字串檢查。

- Runtime 為 22 個檔案；其清單列 21 個檔案雜湊，`SHA256SUMS.txt` 本身不自我列入。
- Source 包含 1658 個檔案；清單列 1657 個雜湊。內含本專案精確提交的 `project.zip`／展開來源、固定上游 ZIP、三個 Go 模組來源、wrapper 與重建說明。
- 42 個建置來源與封存內容比對通過：3 個位元組完全相同、39 個僅需將工作樹 CRLF 正規化為 Git LF 後相同，無其他內容差異。
- 字串稽核只支持已列出的 UTF-8／UTF-16LE 模式沒有命中，不能推論已證明不存在任何形式的敏感資料。來源內容核對通過，但**未在全新電腦執行獨立重建**，也不宣稱完全離線或逐位元相同的重建。

三個執行元件的 Authenticode 狀態都是 **`NotSigned`**：

| 檔案 | SHA-256 |
| --- | --- |
| `PSKeyboard-DualSensePreview.exe` | `5A4225D43D5052C0BF6497520F5B26EA4844B7D229DE2D871639E7A0730D4A18` |
| `DualSenseCandidateServer.exe` | `9CFF2F8BF15DA8C4807BD631A24909FC9EF9820E5F7914F8742B839A981C2A61` |
| `pskb-keyboard-merge.dll` | `5E0ED7E5E54E512371F656EE3BCB2742464A26D782714A548C7DB47C923A9E74` |

SHA-256 用於完整性及版本核對，不是數位簽章、惡意程式檢查或 Sony 相容性證明。

## 本機交付、驗收與後續

- 已交付 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.11`，入口 `PSKeyboard-DualSensePreview.exe`；桌面「上班不要玩 0.1.0-alpha.11」指向該版。另有同版 `versioned-archives` 保存公開 ZIP、清單與固定上游來源 ZIP。
- 沒有代使用者啟動一般模式、接入裝置或操作 Sony。私人收據保存在工作區 `.local/alpha11-verification`，包括 build log、`transport-tests.json`、`backend-pure-tests.jsonl`、`public-audit.json`、`public-downloads.json` 與發布結果；不將含個人路徑的完整私人收據公開。
- [alpha.9 四方案成功](ALPHA9-TRIAL.md)是使用者透過官方 Remote Play 控制 PS5 的回報，足以解釋為何保留第二種；**不是 alpha.11 新二進位、逐鍵、設定保存、連點、重連、重開機或長時間驗收**。Windows 裝置辨識與本機連線燈也不能替代此驗收。
- **舊版清理尚未執行。** 清理只做到計畫／dry-run 與祖先路徑檢查；使用者追加完整多語要求後暫停清理，保留編譯器、後端及 cache 供 alpha.12 建置。此文件不填寫推測的回收容量。
- Windows 收尾先完成 alpha.12 主畫面及訊息九語、全新版本建置／測試／交付與證據，再依最新保留規則清理。不得覆寫 alpha.11 附件或移動其標籤。Mac 下一串依 [Mac 交接](../../docs/MAC-HANDOFF.md)先唯讀盤點與隔離研究；Mac 尚未開發。
