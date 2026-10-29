# Mac 接手交接草稿

更新日期：2026-10-29（Asia/Taipei）。**這是下一串切換到 Mac 的接手草稿，不是 Mac 成品或可行性結論。** Windows alpha.11 已公開發布；使用者隨後要求補齊主畫面與所有訊息的九語，Windows alpha.12 正在進行。主代理應在 Windows 收尾完成後更新本文件的最終版本、提交、發布與清理結果；Mac 下一串必須先讀更新後的紀錄。

目前 **Mac 版尚未開發，未在 Mac 編譯、安裝、建立虛擬裝置或操作 Sony**。Windows 的成功不能外推為 macOS 免實體手把可行。使用者希望下一串在自己的 Mac 繼續，本串不替換目前 Windows 環境。

## 來源與閱讀順序

儲存庫：<https://github.com/xiunlee/ps-remote-play-keyboard>。目前工作及預設分支為 **`codex/commercial-preview`**；分支名稱沿用歷史，產品已改為免費開源。不要以舊 `main` 覆蓋工作，也不要把 Mac 的新工作目錄猜成 Windows OneDrive 路徑。

接手先讀文件，再按所處驗證階段讀原始碼：

1. [HANDOFF.md](HANDOFF.md) → [CHANGELOG.md](../CHANGELOG.md) → [VERSIONING.md](VERSIONING.md) → 本文件。以最新已提交證據更新版本認知，不以聊天中的「最新版」字樣代替來源提交。
2. [alpha.11 發布紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)與對應 [BUILD](../packaging/build-records/0.1.0-alpha.11.json)、[公開附件清單](../packaging/build-records/0.1.0-alpha.11-archives.json)；若 Windows alpha.12 已完成，再讀其新增紀錄，不覆寫 alpha.11 的歷史結果。
3. [alpha.9 使用者操作回報](../research/dualsense-preview/ALPHA9-TRIAL.md) → [alpha.10 功能整合](../research/dualsense-preview/ALPHA10-TRIAL.md) → [現行功能說明](../research/dualsense-preview/README.md) → [預設鍵位](DEFAULT-KEYS.md)。
4. [V5 隔離結果](../research/dualsense-v5-test/RESULTS-20261029.md) → [Windows 0.9.8.1 四方案測試](../research/dualsense-driver-test/RESULTS-20261029.md)與 [fixture 限制](../research/dualsense-driver-test/README.md) → [候選來源研究](../research/dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md)。這些是 Windows／協定證據，不是 Mac 驅動方案。
5. 原始碼先讀 [V5Protocol.cs](../research/dualsense-v5-test/V5Protocol.cs)、[CandidateTransport.cs](../research/dualsense-preview/CandidateTransport.cs)、[backend/main.go](../research/dualsense-preview/backend/main.go)及其測試，再讀 [DualSensePreview.cs](../research/dualsense-preview/DualSensePreview.cs)的 Windows 平台依賴與生命週期。
6. 需移植功能時才讀 [BindingProfile.cs](../src/physical-keyboard/BindingProfile.cs)、[RapidFireEngine.cs](../src/physical-keyboard/RapidFireEngine.cs)、[KeyboardControlPipeline.cs](../src/physical-keyboard/KeyboardControlPipeline.cs)、[KeyboardInputState.cs](../src/keyboard-pad/KeyboardInputState.cs)、[native 映射核心](../research/native-merge/keyboard_merge.c)與對應測試。接著讀設定頁、翻譯和 [SupportLinks.cs](../research/dualsense-preview/SupportLinks.cs)。
7. 開始封裝前讀 [REBUILD.md](REBUILD.md)、[LICENSE](../LICENSE)、[COPYRIGHT.md](../COPYRIGHT.md)與 [發行工作表](../packaging/RELEASE-CHECKLIST.md)。舊 PS4 成功或失敗原因需要追查時，再讀 [首次免手把回報](../research/controller-free-preview/LIVE-TRIAL.md)及 [alpha.6 結果](../research/controller-free-preview/ALPHA6-TRIAL.md)。

## 現有證據與尚未完成的事

| 項目 | 已知結果 | 不可推論 |
| --- | --- | --- |
| Windows PS4 模擬 | alpha.5／6 有操作 PS5 成功回報，alpha.5 有一次重開機成功 | 不保證 Mac、所有機器或所有重連順序 |
| Windows DualSense | alpha.9 四種均有使用者 Sony／PS5 操作成功回報，指定第二種 `dualsensegamepadv5` | 不等於逐鍵、音訊、觸覺、長時間或其他版本實機通過 |
| Windows alpha.10 | 第二種模式、22 動作改鍵／連點整合、取消 30 分鐘期限、離線測試及本機交付 | 無本版使用者實機回報 |
| Windows alpha.11 | 固定來源 `6dbc154a6a887398796d442fd34b73455672aba3`；[Release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11) ID `427698167`，四附件公開下載雜湊通過 | 三個執行元件仍未簽章；沒有新 Sony 實機驗收；主畫面仍未完整九語 |
| Windows alpha.12 | 使用者已要求主畫面、狀態、錯誤與提示補齊九語，正在開發 | 不預填提交、測試數、發布網址或完成狀態 |
| 舊檔清理 | 有使用者授權及私人計畫／dry-run；因 alpha.12 工作暫停 | 尚未 Apply，不宣稱已回收容量或只剩最新版 |
| Mac | 尚無環境與虛擬輸出驗證 | 不保證免實體手把、某個 API、某種驅動或 Windows 後端能直接使用 |

alpha.11 離線證據為 801 項 C#（117+149+276+101+36+122）、5743 項 native、C# ABI、Go 4 個頂層／2 個子情境及 27 項公開包稽核；完整界線見該版發布紀錄。這些不是 macOS 測試。

## 不得改變的產品及操作限制

- **使用 Sony 官方 Remote Play。** 不以第三方 Remote Play 用戶端替代、不索取 PSN 密碼、不注入 Sony 程序或讀取其記憶體。不自行關閉、重啟、更新或重新安裝 Sony；難得建立的連線優先保護。
- 使用者已決定所有功能免費，原創碼 GPL-3.0-only，作者 xiunlee；不引入付費依賴、DRM 或付費解鎖。上游、編譯器和 runtime 的授權分別保留。
- 鍵盤與連點只在官方 Remote Play 位於前景時作用；切窗停止並清除連點，切回不自動恢復，F8 暫停。Mac 上是否需要調整硬體功能鍵的觸發方式必須實測，不能因此放寬前景限制。
- 不擅自改裝、降級或替換驅動；Windows 既有成套 usbip-win2 0.9.8.1 不動。Mac 若研究需要新增系統元件、權限或可能影響現有連線的動作，先提出具體可檢視方案及影響，再安排必要的本人操作。
- 不記錄原始打字、帳號、裝置序號或付款資料。測試用合成事件；公開收據只保留必要的版本、來源、結果與限制。
- 保留使用者按鍵設定與備份，只有按儲存且成功才套用。不得將 Windows 整數鍵碼不加轉換地當成 Mac 鍵碼，也不得為遷移直接覆寫唯一原檔。
- 只清理本程式建立且完整核對過的裝置／bus／port；不全域 detach、不停止其他軟體裝置或程序。未確認清理成功時先停止新接入。
- 新版採新目錄、新版本與新證據；不覆寫已公開附件或移動舊 tag。使用者授權本機只留最新完成版，但清理必須在建置／交付驗證完成後逐項核查，保留個人設定、備份與 GitHub 歷史。

唯一現行贊助連結為 [PayPal 自願 US$1](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)，回饋 `xiun1019@gmail.com`。只由點擊開啟瀏覽器或郵件草稿，不自動付款／寄信。收款身分、金融與稅務資料由本人處理，不使用任何已撤回的連結。

## 哪些部分不能直接搬到 Mac

Windows 現有路徑是：鍵盤擷取 → 前景／F8 → 設定與連點 → 33-byte 動作內容／V5 framing → 受限後端 → 成套 Windows USBip CLI／DLL／驅動 → Windows HID → 官方 Remote Play → PS5。

| 現有部分 | Mac 接手方式 |
| --- | --- |
| WinForms／.NET Framework 視窗、Windows ICO、Desktop `.lnk` | Windows 成品與建置流程不能當成原生 Mac 產品；先確定輸出可行，再選 UI 與 Mac 封裝方式 |
| `user32.dll` keyboard hook、`GetForegroundWindow`、`GetAsyncKeyState`、`RegisterHotKey` | 需依目標 macOS 的公開 API、權限及實際前景識別重新設計；不能只換函式名稱而保證語義相同 |
| `kernel32.dll`、`iphlpapi.dll`、Windows PID／TCP listener 歸屬檢查 | 需建立對應的程序與資源歸屬檢查，維持只處理自有資源的保護 |
| `usbip.exe`、`libusbip.dll`、Windows 0.9.8.1 驅動與 HID 介面 | 是本次 Windows 路線的依賴；不搬到 Mac 安裝，不預設 macOS 有等價接入端 |
| `pskb-keyboard-merge.dll` | C 動作邏輯及合成測試可供重新編譯研究；Windows DLL／ABI 及輸入裝置接入仍不能直接套用 |
| V5 framing、報告轉換、按住／放開與連點狀態機 | 可作行為規格與純測試基準；重用程式碼前核對平台耦合、時間來源、鍵碼及執行緒行為 |
| `BindingProfile` 格式 1／2 與 Windows 設定路徑 | 先做無損、明確的遷移設計；Mac 路徑與鍵碼未定，不沿用 `%LOCALAPPDATA%` 字串 |
| Go 受限後端 | 固定上游與協定可供研究，但成功建置 Go 不代表 Mac 有可用虛擬 HID 輸出或 Sony 會接受 |

後端固定 `hbashton/VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2`。Windows wrapper 僅 loopback API `33642`、USB/IP `33641`，禁止 autoattach／native attach，stdin stop／EOF／error 會清理，正常 session 沒有到期時間，停止時才有 15 秒上限。這些限制可作新平台設計參考，不是直接在 Mac 啟動的指令。

`research/dualsense-driver-test` 的 fixture 只供一次性 GitHub hosted Windows VM；不可偽造環境、拿掉 guard 或移作 Mac／使用者電腦的安裝工具。

## 下一串的首輪工作

1. **唯讀確認來源與 Mac 環境。** 核對工作目錄、remote、分支、HEAD、未提交變更及最新 Windows 收尾證據；記錄 macOS 版本、CPU 架構、現有編譯工具與 Sony 官方 Remote Play 的實際版本／安裝位置／執行狀態。不要啟動、終止或更新 Sony，不讀帳號資料；本機序號及個人完整路徑不要放進公開報告。
2. **先寫輸出可行性問題清單。** 根據該 macOS 與 Sony 版本查 Apple／Sony 官方文件及候選原始碼：虛擬手把輸出是否有可用公開介面、所需權限與簽章條件、是否需額外系統元件、是否能讓官方 Remote Play 接受。將已知事實、推測與待測項分開；未解決的輸出問題不能用完成 UI 代替。
3. **建立不接觸 Sony 的隔離驗證。** 先用合成報告測動作編碼、neutral／release、資料長度、序列、失連及清理。僅證明協定與測試程序行為；記錄新目錄、依賴版本、來源雜湊與如何回收。
4. **再驗證 Mac 裝置層。** 只有在方案、權限與操作範圍已具體確定後，才安排自有虛擬 HID 的建立、輸入讀回與完全移除；不要求使用者先重啟／重裝 Sony。裝置列舉成功仍不能宣稱官方 Remote Play 已接受。
5. **最後安排官方 Remote Play 實機。** 由使用者保有連線控制，先做一個按鈕按下／放開，再測方向、其餘動作、前景／F8、改鍵／連點、重連與長時間；分別記錄協定、Mac HID、Sony 程序與 PS5 實際反應。需要中斷連線的項目另行協調，不隱含在研究授權內。
6. **輸出路線有證據後才完整做 UI／封裝。** 沿用品牌與已確認語言需求，但先檢查文字在該平台的真實顯示。若免費且不干擾官方用戶端的免實體手把路線不可行，如實記錄阻礙與可選方向，不能改用付費依賴或第三方用戶端冒充完成。

第一階段可交付的是 Mac 環境盤點、候選比較、純測試紀錄與下一個具體試驗，不是先承諾可用的 Mac App。既有授權內的唯讀與隔離工作直接推進；只有新出現、確實需要本人處理的系統權限或連線中斷才提出具體說明。

## Windows 完成後須補記的交接項

- alpha.12 最終來源提交、tag、建置／語言覆蓋與離線畫面證據、交付及發布狀態；九語名稱為繁中、簡中、英文、日文、韓文、德文、法文、西班牙文、巴西葡語。尚未完成前維持本草稿的「進行中」。
- 最新版是否取得使用者實機回報，以及逐鍵、設定、連點、失焦／F8、重連、長時間／重開機各自的結果；沒有回報就明確保留待驗。
- 清理真正執行後的精準範圍、確認刪除的邏輯位元組、跳過／部分失敗、最後保留的版本及設定／備份核對；不把候選總容量或 dry-run 當成已回收空間。
- 重新核對工作分支與公開首頁。文件提交可以超前固定版本 tag，不能移動 tag 來追齊文件。更新後由 Mac 下一串再開始唯讀環境盤點。
