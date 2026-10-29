# Mac 接手交接

更新日期：2026-10-29（Asia/Taipei）。**Windows 首發版 0.1.0 已正式公開發布及本機交付；本文件交接下一串的 Mac 研究，不是 Mac 成品或可行性結論。** 標籤 `v0.1.0` 固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`，[GitHub Release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0) ID `427775269`，API 已核對 `prerelease=false` 且為 Latest。四個公開附件已匿名下載並通過大小及 SHA-256 核對；本機版本、封存與捷徑均另建 `0.1.0`，代理未啟動交付版。alpha.12 標籤與公開附件保留原樣。

歷史版本 **0.1.0-alpha.12 已公開發布及本機交付**：標籤 `v0.1.0-alpha.12` 固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`，[GitHub pre-release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.12) ID `427756025`。四個附件已匿名下載，大小及 SHA-256 全數相同；代理未啟動交付版。舊本地檔案清理已部分執行後中斷，最終保留版本、剩餘項目與總量仍待 0.1.0 交付後核對。

目前 **Mac 版尚未開發，未在 Mac 編譯、安裝、建立虛擬裝置或操作 Sony**。Windows 的成功不能外推為 macOS 免實體手把可行。使用者希望下一串在自己的 Mac 繼續，本串不替換目前 Windows 環境。

## 來源與閱讀順序

儲存庫：<https://github.com/xiunlee/ps-remote-play-keyboard>。目前工作及預設分支為 **`codex/commercial-preview`**；分支名稱沿用歷史，產品已改為免費開源。不要以舊 `main` 覆蓋工作，也不要把 Mac 的新工作目錄猜成 Windows OneDrive 路徑。

接手先讀文件，再按所處驗證階段讀原始碼：

1. [HANDOFF.md](HANDOFF.md) → [CHANGELOG.md](../CHANGELOG.md) → [VERSIONING.md](VERSIONING.md) → 本文件。以最新已提交證據更新版本認知，不以聊天中的「最新版」字樣代替來源提交。
2. 0.1.0 的 [BUILD](../packaging/build-records/0.1.0.json)、[公開附件清單](../packaging/build-records/0.1.0-archives.json)及 [九語離線畫面](../assets/previews/0.1.0)，再依需要讀 alpha.12 的 [BUILD](../packaging/build-records/0.1.0-alpha.12.json)與 [alpha.11 發布紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)理解歷史界線。固定版本的原始碼取對應 tag；文件補記可以超前 tag，不移動 tag 追齊文件。
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
| Windows alpha.12 | 固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`，Release `427756025`；主介面與設定頁九語、2D／JRPG 定位，已發布及本機交付 | 代理未啟動交付版；三個執行元件仍未簽章，本版仍未取得實機逐項驗收 |
| Windows 0.1.0 | 固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`，Release `427775269`；正式版且為 Latest，四附件匿名下載通過，已另建本機交付 | 代理未啟動交付版；三個執行元件仍未簽章，未取得本版實機逐項驗收；移除 alpha 字樣不等於新增相容性證據 |
| 單款遊戲回報 | 使用者回報 Final Fantasy Resonance 可正常操作，但未指明當時工具 build | 不能歸屬為 alpha.12 或 0.1.0 驗收，也不代表其他 JRPG、3D 遊戲或所有操作情境適用 |
| 舊檔清理 | Apply 已執行後因準備 0.1.0 中斷；私人逐項紀錄確認刪除 92 項，共 4,006,258,013 邏輯 bytes | 不是完整清理結果或實際磁碟回收量；中斷時未完成末次保護狀態核對，剩餘及部分刪除項目須另核對 |
| Mac | 尚無環境與虛擬輸出驗證 | 不保證免實體手把、某個 API、某種驅動或 Windows 後端能直接使用 |

0.1.0 最終乾淨建置通過 **5834 項 C#**（162+2472+149+276+101+36+167+2471）、**5743 項 native** 與 C# ABI，另有 **67 項版號邊界純檢查**；公開包 **35／35 項稽核**通過。沿用固定後端 Go 4 個頂層測試及 2 個子情境的既有證據。這些沒有擷取真實鍵盤、建立使用者電腦裝置或操作 Sony，不是 macOS 或 PS5 實機測試。

九語共 18 張離線 PNG：**9 張主畫面因版號變更新產並逐張目視**；**9 張設定頁與 alpha.12 的 SHA-256 相同，沿用已完成的目視結果**。alpha.12 另有 18 個主畫面、36 個設定頁排版情境的離線檢查；它們是既有排版證據，不記成 0.1.0 新跑的實機 DPI 驗證。

主介面 50 個資源、共用／設定頁 93 個資源均涵蓋繁中、簡中、英文、日文、韓文、德文、法文、西班牙文及巴西葡語；一般使用依 Windows 介面語言自動選擇，不支援的語言回退英文。品牌、控制器名稱與技術診斷碼保留原文。長文可換行、內容可捲動；母語校閱、真實高 DPI、多螢幕、逐鍵／連點、重連、長時間與重開機仍待本版實機驗證。

目前產品定位為 **2D 遊戲，尤其以選單及方向操作為主的 JRPG**。沒有滑鼠瞄準或右搖桿方向映射，不適合需要靈活視角或精細類比控制的 3D 遊戲；不能以單款回報承諾所有 JRPG 適用。Mac 版應先保留此功能界線，再依實際輸出能力另訂擴充範圍。

## 不得改變的產品及操作限制

- **使用 Sony 官方 Remote Play。** 不以第三方 Remote Play 用戶端替代、不索取 PSN 密碼、不注入 Sony 程序或讀取其記憶體。不自行關閉、重啟、更新或重新安裝 Sony；難得建立的連線優先保護。
- 使用者已決定所有功能免費，原創碼 GPL-3.0-only，作者 xiunlee；不引入付費依賴、DRM 或付費解鎖。上游、編譯器和 runtime 的授權分別保留。
- 鍵盤與連點只在官方 Remote Play 位於前景時作用；切窗停止並清除連點，切回不自動恢復，F8 暫停。Mac 上是否需要調整硬體功能鍵的觸發方式必須實測，不能因此放寬前景限制。
- 不擅自改裝、降級或替換驅動；Windows 既有成套 usbip-win2 0.9.8.1 不動。Mac 若研究需要新增系統元件、權限或可能影響現有連線的動作，先提出具體可檢視方案及影響，再安排必要的本人操作。
- 不記錄原始打字、帳號、裝置序號或付款資料。測試用合成事件；公開收據只保留必要的版本、來源、結果與限制。
- 保留使用者按鍵設定與備份，只有按儲存且成功才套用。不得將 Windows 整數鍵碼不加轉換地當成 Mac 鍵碼，也不得為遷移直接覆寫唯一原檔。
- 只清理本程式建立且完整核對過的裝置／bus／port；不全域 detach、不停止其他軟體裝置或程序。未確認清理成功時先停止新接入。
- 新版採新目錄、新版本與新證據；不覆寫已公開附件或移動舊 tag。使用者授權本機只留最新完成版，但清理必須在建置／交付驗證完成後逐項核查，保留個人設定、備份與 GitHub 歷史。
- 使用者已指定後續版號依序為 **0.1.0 → 0.1.1 → … → 0.1.9 → 0.2.0**，新版本不再加 alpha 標記；歷史 alpha 版本、tag 與附件照原樣保留。版號命名不取代每版的實機與相容性驗收。

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

## 收尾紀錄與下一串仍待驗的項目

- 0.1.0 首發版的建置、固定提交、交付、正式發布與四附件匿名下載核對已完成；alpha.12 作為歷史版本保留。Mac 下一串先核對最新版交接與各固定來源，不將 Windows 執行包拿到 Mac 當成原生程式。
- 逐鍵、設定、連點、失焦／F8、重連、長時間／重開機仍待相應版本的實機逐項回報。Final Fantasy Resonance 的單款正常操作回報因工具 build 未指明，獨立記錄，不補猜版本。
- **清理最終結果待補。** 第一段中斷收據確認 92 項已刪除、4,006,258,013 邏輯 bytes；部分刪除或尚未寫入收據的內容不加算。0.1.0 交付後的新 dry-run 已通過，該次刪除數為 0；最終 Apply 及剩餘路徑、個人設定／備份、驅動檔、Sony 程序與舊 tag 的核對結果另補。不將邏輯檔案大小或 dry-run 候選總量當成實際磁碟回收量。
- Mac 環境、所需權限、虛擬輸出可行性及 Sony 接受與否都尚未驗證；下一串依上方順序先做唯讀盤點與隔離研究。
