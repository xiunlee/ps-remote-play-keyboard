# 版本紀錄

所有日期採 Asia/Taipei。最新公開 Windows 版為 **v0.1.4**；Mac 0.1.3 的來源與封裝另存，尚未確認正式公開發布。Git 標籤只固定原始碼；公開下載與實機驗收分開記錄。

## Windows 0.1.4 — 檔案整理（2026-10-30）

- 檔案整理。

## Mac 0.1.3 — 美術優化

- 美術優化。

## Windows 0.1.3 — 名稱與圖示更新（2026-10-30）

- 中文名稱保留「上班不要玩」，其他語系改為 Three Cats Keybridge，更新貓咪與按鍵圖示。
- 固定來源 `8f1918e48bfadc1b8d7120b1991b497d060fc764`；[v0.1.3 發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.3)發布時間為 2026-10-30 17:15:32（台北）。Windows 與 Mac 產物及驗證分開記錄。

## Windows 0.1.2 — 美術優化（2026-10-30）

- 美術優化。

## 0.1.2 — Mac 介面整理（本機開發候選，尚未發布）

- 前一份 Mac 0.1.0-alpha.1 固定簽章測試版已取得官方 Remote Play 遊戲操作成功回報：按鍵放開停止、Enter／Options 有效；中斷後自有虛擬手把移除也已核對。[實測紀錄](research/macos-native/DUALSENSE-TRIAL-2.json)。本版控制核心／協定／socket 原始碼相同，未另進行遊戲試驗。
- 新增 Mac 四面鍵位置圖：△ I、□ J、○ L、× K，明示 Enter／Options 與 Esc 控制暫停／啟用；補上其餘固定鍵位的圖示說明。
- 沿用原品牌圖，加入 Mac App 圖示與視窗品牌區；輔助使用授權狀態會更新，彙總診斷收進展開區。
- 保持同一個 App、bundle identifier、安裝路徑與本機簽章憑證。0.1.2 尚無公開附件；Windows v0.1.1 與其標籤不變。自動啟停、首次安裝、依賴取得和發行封裝仍待完成，見 [Mac 發佈缺項](docs/MAC-RELEASE-READINESS.md)。
- 已完成 Swift 6 嚴格編譯、簽章驗證及本機替換安裝；本次更新後 App 自行確認輔助使用仍已允許，沒有要求重新授權。主畫面與診斷展開已目視確認。[建置收據](packaging/build-records/macos/0.1.2.json)、[介面與更新驗證](research/macos-native/inspection/MAC-0.1.2-UI.json)。

## Mac 首輪研究歷史 — 2026-10-30（當時成品為 Windows 0.1.1）

- 在 macOS 26.6.2 arm64 完成首輪交接，唯讀確認官方 PS Remote Play 9.5.0 及簽章，核對 GitHub 最新正式版與固定來源。見 [Mac 首輪紀錄](research/macos-handoff/README.md)。
- 原 native C 核心在 Mac 編譯並通過 5743 項既有合成檢查；另有 124 項 native dylib／Python 協定參考檢查與 22 動作向量，清楚區分原碼與參考模型。未執行原 C#／Go 測試、鍵盤擷取、虛擬裝置或 Sony／PS5 操作。
- 新增 CoreHID 等候選比較、所需權限與具體下一步試驗。尚無 Mac 成品或相容性結論，不改產品版號、不移動舊 tag、不覆寫公開附件。

## 0.1.1 — 國際化品牌修正（2026-10-29）

- 依使用者要求，中文介面保留「上班不要玩」，英文及其他非中文語系統一使用 **PS Remote Play Keyboard**；修正主畫面、設定頁、說明與回饋主旨的產品名，英文手冊及英文發行說明也不混入中文品牌。
- 保留九語、2D／JRPG 定位、22 動作改鍵／連點、F8／前景保護、設定儲存規則及目前 0.9.8.1 相依需求；未簽章、逐項實機待驗及《Final Fantasy Resonance》回報未指定 build 的界線不變。
- 固定來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629` 完成獨立乾淨建置：**6912 項 C# 檢查**（162＋2890＋149＋276＋101＋36＋167＋3131）、5743 項 native 及 C# ABI 驗證通過；公開包 39／39 項稽核通過。後端沿用 r4 與先前 Go 證據，未重跑 Go 測試；三個 runtime 二進位仍為 NotSigned。
- 已發布 [PS Remote Play Keyboard V 0.1.1](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.1)，Release ID `427807747`，`prerelease=false` 且為 GitHub latest；四附件匿名下載雜湊全數吻合，本機程式、封存及桌面入口交付完成。不覆寫 v0.1.0 或歷史 alpha 附件／標籤，詳見[發布紀錄](research/dualsense-preview/RELEASE-0.1.1.md)。
- 九張最終主畫面全部目視；九張設定頁 SHA-256 與已審閱 v0.1.0 圖片相同，沿用該視覺證據。另有開發階段 11 個離線排版情境、0 項問題；母語校閱、實際高 DPI、多螢幕及本版逐項實機仍待驗。
- 本輪舊版與重建暫存清理完成：移除 9 項、753,491,748 logical bytes，失敗 0 項；99 項原已不存在，2 份小型 log 保留。`versions`／`versioned-archives` 均只剩 0.1.1，個人設定／備份、驅動、Sony 及標籤前後一致。這是邏輯刪除大小，不是淨磁碟差；歷史 v0.1.0 的清理數字不重複計入。

## 0.1.0 — 正式首發（2026-10-29）

- 依使用者要求，對外版號簡化為 `0.1.0`，移除 alpha 後綴；保留 alpha.11／12 的公開測試版與所有歷史標籤、來源及附件。
- 後續依使用者慣例採 **V0.1.0 → V0.1.1 → … → V0.1.9 → V0.2.0**，不再用 alpha，也不改成 `0.1.10`；Git 標籤維持小寫 `v`，更高位進位另依後續決定。
- 沿用九語介面、2D／JRPG 定位、22 動作改鍵與連點、F8／前景保護及自願贊助。命名調整不代表控制協定或實機驗收改善；三元件未簽章、外部成套 0.9.8.1 與逐項實機待驗的限制保留。
- 《Final Fantasy Resonance》正常操作回報仍記為 2026-10-29 使用者回報、工具 build 未指定，不當作 0.1.0 逐鍵／連點驗收或所有 JRPG 的保證。
- 固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe` 已完成獨立乾淨建置：5834 項 C#（162＋2472＋149＋276＋101＋36＋167＋2471）、5743 項 native 及 ABI 檢查、67 項版號純邏輯檢查通過；35／35 項公開包稽核通過。三個 runtime 二進位仍為 NotSigned，後端沿用 r4 與其先前 Go 測試證據。
- [v0.1.0 正式發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0)已公開，Release ID `427775269`，`prerelease=false`，發布時以 `make_latest=true` 設為最新，當時 GitHub latest API 已核對為 v0.1.0。執行包與完整對應原始碼另存新附件，不覆寫 alpha.11／12；詳見[發布紀錄](research/dualsense-preview/RELEASE-0.1.0.md)。
- 九張新版主畫面已逐張目視；九張設定頁圖片與已審閱的 alpha.12 SHA-256 相同，沿用該視覺證據。母語校閱、實際高 DPI、多螢幕及逐項實機仍待驗。
- 四個公開附件匿名下載的 bytes／SHA-256 全部吻合；本機 v0.1.0 程式、封存與桌面入口已交付，沒有啟動一般模式。舊版清理已完成：最後 26 項 1,583,433,983 bytes，與先前 92 項 4,006,258,013 bytes 累計 118 項、5,589,691,996 logical bytes，包含重建暫存，**不是淨磁碟差**。97 項原已不存在，2 份小型 log 刻意保留；個人設定／備份、驅動、Sony 與 Git 標籤比對通過。

## 0.1.0-alpha.12 — 公開 alpha（2026-10-29）

- 使用者指出主畫面尚未完成先前約定的多語支援，因此先補 Windows 介面九語，再進行 Windows 最終交接與後續 Mac 研究；不覆寫已公開的 alpha.11。
- 新增 `PreviewText` 的 50 個文字鍵、每鍵九語，涵蓋主畫面、連線狀態、錯誤、贊助、回饋、郵件主旨與 2D／JRPG 定位；共用設定頁 93 鍵亦有九語資源檢查。沿用 `UiText` 的 Windows 介面語言選擇，未支援語言回退英文。品牌、按鍵、技術名稱、診斷代碼及贊助 US$1 保持一致。
- 主畫面依翻譯長度調整文字行高及按鈕換行，保留 22 動作改鍵／連點、F8／前景、儲存成功才套用與自有裝置清理規則。包內保留繁中及英文手冊，英文按鈕名稱與介面一致。
- 最終固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a` 通過 **5816 項 C# 檢查**（144＋2472＋149＋276＋101＋36＋167＋2471）、5743 項 native 與 ABI 檢查，33／33 項公開包稽核；九語主畫面及設定頁共 18 張最終離線圖片已逐張目視。後端沿用 r4 及先前 4 個 Go 頂層測試、2 個子情境證據，未把沿用結果列為本輪重跑。
- 已發布 [GitHub pre-release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.12)，Release ID `427756025`。四個公開附件匿名下載後雜湊全數吻合；本機獨立版本目錄及桌面入口已交付，沒有代使用者啟動一般模式。三個 runtime 二進位均為 NotSigned。見 [alpha.12 發布紀錄](research/dualsense-preview/ALPHA12-TRIAL.md)。
- 主介面與中英文指南清楚定位 2D 遊戲，尤其選單與方向操作為主的 JRPG；不適合需要靈活視角或精細類比控制的 3D 遊戲，不保證所有 JRPG 都適合。2026-10-29 使用者回報單款《Final Fantasy Resonance》可正常操作，未指定工具 build，不算 alpha.12 逐鍵／連點驗收。
- 母語校閱、實際高 DPI、多螢幕及本版逐項實機仍待驗；Mac 尚未開發。當時清理曾因準備 0.1.0 中斷，後續完成結果記於 v0.1.0 段，不改寫本版固定附件。

## 0.1.0-alpha.11 — 公開 alpha（2026-10-29）

- 已以 `v0.1.0-alpha.11` 固定來源 `6dbc154a6a887398796d442fd34b73455672aba3` 並推送，發布 [GitHub pre-release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11)；Windows 包、完整對應原始碼包、RELEASE.json 及 SHA256SUMS.txt 四項公開下載後雜湊全數吻合。儲存庫保持公開，依使用者指出首頁舊資訊而將預設分支改為 `codex/commercial-preview`，`main` 保留歷史，沒有合併或移動舊標籤。
- 最終乾淨建置通過 801 項 C# 檢查、5743 項 native 映射檢查及 C# ABI 測試、4 個 Go 頂層純函式測試（含 2 個子情境）；公開包 27 項稽核通過。三個 runtime 二進位均為 NotSigned。詳見 [alpha.11 發布與交付紀錄](research/dualsense-preview/ALPHA11-TRIAL.md)。

- 依使用者決定維持公開儲存庫，自製原始碼採 GPL-3.0-only，作者 xiunlee；所有功能免費，原商售、付費解鎖及 DRM 規劃不再適用。
- 主畫面新增免費開源、作者署名及自願贊助 US$1 幫三隻貓咪買罐頭的說明。使用者最後指定[PayPal 贊助入口](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)；點擊「贊助貓咪 US$1」才開瀏覽器，由 PayPal 處理收款，本程式不接收或保存付款資料，不解鎖功能或建立訂閱。唯讀 GET 已確認公開頁設定啟用、金額 USD 1.00；尚未實付驗收入帳，HTTP GET 不代表付款成功。
- 新增「意見回饋」：使用者點擊後才開啟寄往 `xiun1019@gmail.com` 的郵件草稿，只預填版本主旨，不自動寄出或附加個人／診斷資料。離線渲染及測試不開啟外部程式。
- 公開建置改為明列 Zig 工具、從未變更的 native 映射來源編譯 DLL，不再要求舊版 mapping 二進位；同頁提供完整對應原始碼、第三方 notices、材料清單與雜湊。最終後端 r4 的 go.mod 使用相對 replace，MATERIALS 與二進位不帶本機個人來源路徑。
- 沿用 alpha.10 的單一純手把、設定／連點、F8／前景與清理保護；0.9.8.1 仍是外部先決條件，不隨包散布或自動安裝／更換驅動。
- alpha.11 已交付本機獨立版本目錄、封存與桌面捷徑，沒有代使用者啟動一般模式。**該版使用者實機仍待驗**，不把 alpha.9 的成功回報改寫為本版已通過。舊版清理結果另依交付紀錄補記；個人設定及備份保留，GitHub 舊標籤不移動。
- 使用者要求 Windows 發布收尾後切換到 Mac 開發；已準備 [Mac 接手文件](docs/MAC-HANDOFF.md)，後續因主介面語言需求先處理 alpha.12。Mac 尚未開發或驗證可行性，不能把 Windows 產物直接當作 Mac 版本。

## 0.1.0-alpha.10 — 2026-10-29

- 乾淨建置及本機交付已完成，來源／本地標籤固定於 `8359c1ded5cf3310ad85ed3b42e0f47fd55da586`；本次補記時尚未推送至遠端。完整[交付紀錄](research/dualsense-preview/ALPHA10-TRIAL.md)另存，尚無本版使用者實機回報。
- 使用者回報 alpha.9 四種方案都能操作 PS5，指定保留第二種；本版固定「一般 DualSense／純手把」（`dualsensegamepadv5`），移除其他選項。
- 整合原有 22 動作改鍵與每鍵連點設定頁：關閉／按住／按一下啟停、20–5000 毫秒、恢復預設。載入現有設定，僅使用者儲存才更新；儲存失敗不替換目前設定。
- 編輯時暫停，F8 與官方 Remote Play 前景保護保留；切窗停止並清除連點，回來不自動恢復連點。移除 alpha.9 的 30 分鐘測試期限，退出時仍有清理期限。
- 仍使用既有 0.9.8.1 驅動，不自動更換；歷史 PS4／四方案原始碼與標籤保留。依使用者後續空間需求，本機交付後只留最新版，清除舊程式與重複建置，保留個人設定及其備份。alpha.9 的操作證據與 alpha.10 新版驗收分開記錄。

## 0.1.0-alpha.9 — 2026-10-29

- 新增獨立的 DualSense 相容性測試程式，可逐一選擇一般 DualSense／Edge 各自的含音訊介面與純手把共四方案。沿用現有 0.9.8.1 成套驅動，不更換或降級；沒有修改 alpha.8 的 PS4 程式、執行檔或標籤。
- 四方案已在 GitHub hosted Windows Server 完成驅動接入、22 個動作按下／放開與完整移除；每方案 63 項檢查通過，詳見 [驅動實測](research/dualsense-driver-test/RESULTS-20261029.md)。交付後使用者另回報「四種都可以」，指定保留第二種；此 [Sony 操作回報](research/dualsense-preview/ALPHA9-TRIAL.md) 與 CI 證據分開，未擴張為逐鍵、長時間或重開機驗收。
- 候選採固定來源的受限後端、獨立 `33641/33642` 埠、V5 傳輸與持續回饋讀取；同版 CLI 對接驅動，不使用候選舊 native attach。後端隨 stdin 關閉或 30 分鐘上限停止。
- 啟動不接入、控制先暫停；沿用官方 Remote Play 前景、切窗清連點與 F8 保護，與舊版共用單實例限制。個人設定只讀，移除未確認時鎖住重新連接；回退使用保留的 alpha.8 PS4 模式。
- 專用建置／校驗流程與操作說明位於 [dualsense-preview](research/dualsense-preview/README.md)，不啟用安裝器、支付或公開二進位 Release。

## 研究補記 — 2026-10-29（成品仍為 alpha.8）

- 新增僅限 GitHub hosted Windows VM 的 0.9.8.1 驅動測試流程，分別驗證一般 DualSense／Edge 的複合及純手把四方案；使用相同成套 CLI／DLL 與固定來源網路後端，避開候選不相容的舊 native attach ABI。本機不執行安裝或接入。原始碼與限制見 [驅動研究](research/dualsense-driver-test/README.md)；測試結果另行記錄，沒有預先宣稱 Sony 通過。
- 新增固定 hbashton 候選的獨立 V5 adapter、離線測試與使用者空間 USB/IP 工具；修正兩個觸控狀態 `00`／`80` 的語義差異，未改主程式或現行傳輸。
- 核對候選下載檔未簽章，並確認其正式 Windows CLI 強制要求 usbip-win2 0.9.7.7；現有 0.9.8.1 驅動保留。
- 隔離協定階段通過不代表 Windows 或 Sony 相容。結果、原先失敗與後續實機門檻見 [研究證據](research/dualsense-v5-test/RESULTS-20261029.md)；未新增成品、Release 或移動標籤。

## 0.1.0-alpha.8 — 2026-10-29

- 新增白色切角 PS 品牌圖示，嵌入視窗、工作列與執行檔；提供 16–256 像素 Windows ICO。
- 主介面與按鍵設定統一莓紅 `#D62355`、白色卡片、淡玫瑰背景與深色文字，調整標題、字體層級、選取及按鈕狀態。
- 主畫面新增手把／鍵盤狀態燈及實際已儲存的常用鍵位摘要；儲存改鍵後同步更新。狀態燈不表示 PS5 遊戲已接受輸入。
- 新增獨立的離線畫面預覽，不讀取個人設定、不擷取鍵盤、不啟動後端或操作 Sony。九種語言的按鍵設定畫面已逐張檢視；實際高 DPI 仍待驗收。
- 保留 alpha.7 鍵位與連點行為；本輪沒有變更模擬後端、驅動或 PS5 相容性。
- 圖示生成方式、提示與色彩限制見[素材說明](assets/branding/PROMPT.md)，UI 色彩與預覽見[視覺設計](docs/VISUAL-DESIGN.md)。

## 0.1.0-alpha.7 — 2026-10-29

- 依使用者指定重排預設鍵位：方向鍵依位置對應 △／□／×／○；E、Q 對應 L1、L2；Delete、Page Down 對應 R1、R2。
- WASD 保留左搖桿；Insert／Home／Page Up／End 排列十字方向鍵，F／空白鍵為 L3／R3，Tab／Enter 為 Create／Options，R 為觸控板按下，F1 為 PS。F8 保留啟用／暫停。
- 已儲存的格式 1／2 鍵位及連點不自動修改；新設定或按「恢復預設」才套用新版排列。完整配置與套用方式見 [預設鍵位](docs/DEFAULT-KEYS.md)。
- 預設仍使用已實測可用的 PS4 模擬模式；PS5 模式保留為未通過 Sony 操作測試的實驗選項，未替換後端或驅動。
- 設定與舊格式保留、連點、按鍵流程及編輯器共 535 項離線檢查通過；不讀寫使用者的個人設定，也不操作目前連線。

## 0.1.0-alpha.6 — 2026-10-29

- 加入根目錄 `VERSION`，視窗、Windows 檔案內容、狀態紀錄與建置清單使用一致版本。
- 每次建置記錄 Git 提交、未提交變更狀態、來源與產物 SHA-256；使用獨立目錄，拒絕覆寫舊版本。
- 增加可選的 PS5 DualSense 實驗模式；預設仍是已通過使用者操作與重開機確認的 PS4 模式。只能在未接入時選擇型號。
- DualSense 沿用目前 22 個鍵盤動作，正確轉換 PS／觸控板按鈕與 33-byte 輸入、6-byte 回饋，不宣稱完整體感、音訊、自適應扳機或進階觸覺支援。
- 開發建置通過 36 項控制保護、106 項傳輸／轉換及 13 項版本完整性測試；獨立後端的 62 項使用者空間 USB/IP 回讀通過，沒有匯入 Windows 裝置或操作 Sony。
- **實機結果：** DualSense 已接入 Windows，但使用者在官方 Remote Play 操作無反應；同一版改回 PS4 模式後，使用者確認正常。桌面主捷徑預設 PS4，PS5 暫留實驗選項。完整[驗收紀錄](research/controller-free-preview/ALPHA6-TRIAL.md) 與[建置清單](packaging/build-records/0.1.0-alpha.6.json)另存，不移動既有標籤。

## 0.1.0-alpha.5 — 2026-10-29

- 首次免手把模式成功：實體手把拔除，使用者確認可用鍵盤操作官方 Remote Play 裡的 PS5。
- 同日使用者重新啟動 Windows 後，再回報「開機了，功能正常」。這是一次重開機後的操作確認，尚非全部啟動順序與長時間穩定性驗收。
- 原始碼基準：`4e4b8a3`；標籤 `v0.1.0-alpha.5`。**此號碼為事後整理的成功基準，原執行檔未內嵌版本號。** 本機另保存原執行檔、必要元件及 SHA-256，以供回退。
- 使用模擬 DUALSHOCK 4（`054C:09CC`）、VIIPER v0.9.0 與既有 usbip-win2；不需要實體手把。

## 0.1.0-alpha.4 — 2026-10-29

- 實體中繼內部測試包，新增每鍵關閉／按住／按一下切換連點，間隔 20–5000 毫秒。
- 只在官方 Remote Play 前景接受鍵盤；切窗或 F8 停止並清除連點。
- 原始碼基準：`52af244`；此版本屬實體手把路線，非目前免手把主線。

更早的自訂鍵、九語介面與名稱調整，保留於 Git 歷史，不事後假造完整驗收或執行檔版本。
