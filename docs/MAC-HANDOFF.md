# Mac 接手交接

更新日期：2026-10-30（Asia/Taipei）。**目前公開 Windows 最新版為 [v0.1.2](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.2)；下列 0.1.1 發布資料保留作歷史交接，不是 Mac 成品或完整相容性結論。** 標籤 `v0.1.1` 固定來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`，[GitHub Release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.1) ID `427807747`，發布當時 API 已核對 `prerelease=false` 且為 Latest。四個公開附件已匿名下載並通過大小及 SHA-256 核對；本機版本、封存與捷徑均另建 `0.1.1`，代理未啟動交付版。0.1.0 與歷史 alpha 的標籤、附件及 Release 保留；依現行政策，較早 Release 轉為草稿而非繼續對外下載。

歷史首發版 **0.1.0** 固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`，Release ID `427775269`；歷史 **0.1.0-alpha.12** 固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`，Release ID `427756025`。兩版當時的交付與四附件下載核對均通過；此段為歷史紀錄；目前下載以 0.1.2 為準，不能把歷史 Latest 狀態當成現況。

**2026-10-30 Mac 最新進度：目前 Mac 0.1.3／build 3 沿用同一 App、安裝路徑與固定簽章憑證；中文名稱「上班不要玩」、非中文名稱 Three Cats Keybridge。** 這是鍵盤控制與模擬手把工具，實際支援環境仍為 Apple Silicon／macOS 15+ 與 Sony 官方 PS Remote Play。 已接回 Windows 的改鍵、連點、持久化、九語、贊助與回饋。原生 supervisor／relay、自動官方元件取得及開源 runtime 材料已實作；建置 9 已確認完整 App 連接、中斷、再次連接與退出後清理，亦已查看淺／深色主畫面和設定；本版遊戲、新 Mac、睡眠與崩潰仍待驗；後續品牌調整須另記核對，不能改寫建置 9 證據。Mac 使用獨立 `mac-v0.1.3` 標籤，實際公開狀態以發行頁為準，不與 Windows 已發布的 `v0.1.3` 衝突，也不因 Mac 首發歸檔 Windows 版本。已測設定取消不寫入、儲存與重新讀回、更新後保留及恢復預設；各次本機更新後輔助使用仍已允許。[功能對齊清單](MAC-PARITY-CHECKLIST.md)、[發佈驗證](MAC-RELEASE-READINESS.md)、[安裝說明](MAC-INSTALL.md)。

本程式是獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係。品牌協作要求見 [Mac 品牌交接](MAC-BRANDING-HANDOFF.md)；內部協定識別、技術路徑及歷史試驗不為改名而重寫。

最近使用者遊戲證據仍屬 **Mac 0.1.0-alpha.1 的 [trial 2](../research/macos-native/DUALSENSE-TRIAL-2.json)**：可操作、W 放開停止、Enter／Options 有效，當時只有自有 USB DualSense；結束後已獨立確認裝置移除及 client／guest 正常結束。新版原生 supervisor 的遊戲驗收須另外記錄。0.1.2 的歷史 [建置收據](../packaging/build-records/macos/0.1.2.json)與 [UI 紀錄](../research/macos-native/inspection/MAC-0.1.2-UI.json)保持原樣。

同日較早的 [Mac 首輪紀錄](../research/macos-handoff/README.md)保留環境盤點、輸出候選研究與 native 核心離線編譯／合成測試；首輪當時尚未建立虛擬裝置或操作 Sony。後續進展按上述新紀錄接續，不把 Windows 成功、Mac 純測試或單次中立裝置建立外推為 macOS 免實體手把遊戲可用。

## 來源與閱讀順序

儲存庫：<https://github.com/xiunlee/ps-remote-play-keyboard>。預設／Windows 工作分支為 **`codex/commercial-preview`**；本次 Mac 發行工作分支為 **`codex/mac-0.1.3-release`**，從同步的 Windows 品牌交接來源延續，公開標籤用 `mac-v0.1.3`。分支名稱沿用工作流程，產品免費開源。不要以舊 `main` 覆蓋工作，也不要把 Mac 的新工作目錄猜成 Windows OneDrive 路徑。

接手先讀文件，再按所處驗證階段讀原始碼：

1. [HANDOFF.md](HANDOFF.md) → [CHANGELOG.md](../CHANGELOG.md) → [VERSIONING.md](VERSIONING.md) → 本文件。以最新已提交證據更新版本認知，不以聊天中的「最新版」字樣代替來源提交。
2. [0.1.1 發布及收尾紀錄](../research/dualsense-preview/RELEASE-0.1.1.md)、[BUILD](../packaging/build-records/0.1.1.json)、[公開附件清單](../packaging/build-records/0.1.1-archives.json)及 [九語離線畫面](../assets/previews/0.1.1)，再依需要讀 [0.1.0 發布紀錄](../research/dualsense-preview/RELEASE-0.1.0.md)與 [alpha.11 發布紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)理解歷史界線。固定版本的原始碼取對應 tag；文件補記可以超前 tag，不移動 tag 追齊文件。
3. [alpha.9 使用者操作回報](../research/dualsense-preview/ALPHA9-TRIAL.md) → [alpha.10 功能整合](../research/dualsense-preview/ALPHA10-TRIAL.md) → [現行功能說明](../research/dualsense-preview/README.md) → [預設鍵位](DEFAULT-KEYS.md)。
4. [V5 隔離結果](../research/dualsense-v5-test/RESULTS-20261029.md) → [Windows 0.9.8.1 四方案測試](../research/dualsense-driver-test/RESULTS-20261029.md)與 [fixture 限制](../research/dualsense-driver-test/README.md) → [候選來源研究](../research/dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md)。這些是 Windows／協定證據，不是 Mac 驅動方案。
5. 原始碼先讀 [V5Protocol.cs](../research/dualsense-v5-test/V5Protocol.cs)、[CandidateTransport.cs](../research/dualsense-preview/CandidateTransport.cs)、[backend/main.go](../research/dualsense-preview/backend/main.go)及其測試，再讀 [DualSensePreview.cs](../research/dualsense-preview/DualSensePreview.cs)的 Windows 平台依賴與生命週期。
6. 需移植功能時才讀 [BindingProfile.cs](../src/physical-keyboard/BindingProfile.cs)、[RapidFireEngine.cs](../src/physical-keyboard/RapidFireEngine.cs)、[KeyboardControlPipeline.cs](../src/physical-keyboard/KeyboardControlPipeline.cs)、[KeyboardInputState.cs](../src/keyboard-pad/KeyboardInputState.cs)、[native 映射核心](../research/native-merge/keyboard_merge.c)與對應測試。接著讀設定頁、翻譯和 [SupportLinks.cs](../research/dualsense-preview/SupportLinks.cs)。
7. 開始封裝前讀 [REBUILD.md](REBUILD.md)、[LICENSE](../LICENSE)、[COPYRIGHT.md](../COPYRIGHT.md)與 [發行工作表](../packaging/RELEASE-CHECKLIST.md)。舊 PS4 成功或失敗原因需要追查時，再讀 [首次免手把回報](../research/controller-free-preview/LIVE-TRIAL.md)及 [alpha.6 結果](../research/controller-free-preview/ALPHA6-TRIAL.md)。
8. Mac 目前從 [原生研究總覽](../research/macos-native/README.md) → [trial 2](../research/macos-native/DUALSENSE-TRIAL-2.json)及歷史 [trial 1](../research/macos-native/DUALSENSE-TRIAL-1.json) → [版本 manifest](../packaging/macos/version.json)與 [Mac 預設鍵位](MAC-DEFAULT-KEYS.md) → [USB neutral 收據](../research/macos-native/inspection/MAC-USB-DUALSENSE-NEUTRAL.json) → [VirtualHere guest 實驗](../research/macos-native/VIRTUALHERE-GUEST-SPIKE.md)接續，再讀 [V5 協定](../src/macos/DualSenseV5Protocol.swift)與 [Swift 核心契約](../research/macos-native/CONTROL-CORE.md)。[OJD audit](../research/macos-native/OJD-AUDIT.md)、[feed adapter](../research/macos-native/OJD-FEED.md)、首輪紀錄與 Windows 協定資料作歷史及行為參考，不取代本機最新試驗。

## Mac USB DualSense 歷史輸出證據

使用者沒有 Apple Developer 帳號，已授權繼續免費路線。本次 trial 2 成功回報時實體 USB DualSense 已拔除，只存在自有 USB DualSense；歷史 GameController neutral 盤點曾包含實體手把，不混用兩輪狀態。每輪先核對 Sony 連線與裝置，不為接手重啟 Sony。Computer Use 已在 ChatGPT 重啟後恢復，並成功讀取同一個測試 App。

- Linux guest 已完成 boot、USB/IP attach、`054c:0ce6` 枚舉與 64-byte neutral input 讀回。官方 VirtualHere Server **4.8.8** → Mac client **6.0.3** 已將該候選建立為 Mac Transport `USB`、usage `01:05` 的 DualSense。
- [Mac neutral 收據](../research/macos-native/inspection/MAC-USB-DUALSENSE-NEUTRAL.json)記錄試驗前 `[]`、試驗中裝置存在、試驗後 `[]`。`STOP USING` 回覆並非精確 `OK`，清理成功依據是另行觀測 guest `UNBOUND` 及 Mac 自有 HID entry 消失，不能只依指令回覆或程序退出判定。
- [GameController neutral 收據](../research/macos-native/inspection/GAMECONTROLLER-NEUTRAL.json)觀測 controller **1 → 2 → 1**：baseline 是已重新接回的實體 USB DualSense，加入自有虛擬裝置後兩者皆為 DualSense／extended gamepad，移除候選後實體手把保留。未讀 input、未要求無線探索或手動 open；這是系統辨識證據，不是 Sony 遊戲接受。
- 免費模式維持 `unlicensed max_devices=1`，Mac client 不裝成 service。最初 host relay `33757` 觸發 license 提示；改成官方預設 host TCP **7575** → guest **7575** 後接入成功。guest 固定識別及設定保留，沒有改 license key 或輪換識別。[實測與官方來源](../research/macos-native/VIRTUALHERE-GUEST-SPIKE.md)
- 新輸入路徑為 Mac V5 → relay → host loopback `33742` → guest VIIPER → VHCI → VirtualHere → Mac USB。`DualSenseV5Protocol.swift` 的 **22958** 項離線檢查通過；[relay smoke](../research/macos-native/inspection/DUALSENSE-RELAY-SMOKE.json)收到 125 幀 neutral，讀到 8 幀 feedback，frontend EOF 後以 `frontend_eof` 停止且 `cleanup=removed`，Mac 自有 entry 在 0.123 秒後消失。這不包含真鍵盤或 Sony 遊戲。
- 未編版號的 `DualSensePreviewApp` build 1 曾建置、strict verify 並從 `~/Applications/PSKeyboardMacDualSense.app` 啟動，本人確認允許 Accessibility；因 Documents FileProvider 在 open 時重加 FinderInfo才改用該固定位置。沒有移除 quarantine 或重簽第三方程式。專用 socket **57** 項檢查通過，但 [user trial 1](../research/macos-native/DUALSENSE-TRIAL-1.json)回報「沒反應」「鍵盤控制暫停，已按中斷」。**測試步驟最後原本就是 F8 暫停，不能因此推論先前從未啟用。**
- trial 1 記錄 **9982 input／14308 output／19 feedback** 幀，frontend EOF 後 `cleanup=removed`，另行盤點 Mac HID `[]`。舊版未計非中立幀，總數不能證明有效遊戲輸入，也不能定位唯一失敗層。這次清理已完成，不能再記成等待回報。
- [trial 2](../research/macos-native/DUALSENSE-TRIAL-2.json) 首個 owner 等待前端超過 600 秒，以 `frontend_wait_timeout` 結束，輸入 0 幀、`cleanup=removed`，另確認 Mac `[]`；這不是遊戲操作失敗。重新建立 owner 並完成 USB/IP／VirtualHere 接入後，使用者在同一份 alpha.1 確認遊戲可操作、W 放開會停止、Enter／Options 有效。首次成功回報時為 31 次收到／31 次接受按鍵、433 幀非中立輸出；自有 entry `4296402422` 存在，先前實體 entry `4296400524` 不存在。使用者結束遊戲後已按 App「中斷」；最終 App 為 150 次收到／144 次接受按鍵、1157 幀非中立輸出，`Accessibility=true`、`armed=false`、`connected=false`、`ended=true`、無錯誤。relay 最終 51066 input／59729 output／20 feedback，以 `frontend_eof` 停止，`cleanup=removed` 且確認成立；另行核對 Mac gamepad `[]`，VirtualHere `EXIT` 回覆 `OK`，guest stop／poweroff 正常 exit 0。
- 使用者要求固定版本、同一個 App 與減少重複授權；[Mac 版本 manifest](../packaging/macos/version.json)目前為 **0.1.3**、`0.1.3 / build 3`，固定 bundle identifier 與安裝路徑沿用。本人已明確授權 **PSKeyboard Local Development** 精確憑證的 **user-domain CodeSigning** 信任並完成兩次系統確認；初始額外 `-a`／`-u` 限制導致預查失敗，移除後最終 export 只有 CodeSigning entry，沒有 AllTrust／SSL／System-domain 信任。[最終 policy](../research/macos-native/inspection/LOCAL-SIGNING-TRUST-POLICY.json)
- [固定身分測試](../research/macos-native/inspection/LOCAL-SIGNING-SEARCH-ISOLATION.json)只暫時將專案 keychain 加入原 search list：不同 A／B binary 均簽章、strict verify、相同 designated requirement 及互驗通過，執行 exit `0`；完成後還原原 search list，default keychain 未變，測試前後已授權 trust export 一致。[setup 收據](../research/macos-native/inspection/LOCAL-SIGNING-SETUP.json)不是 TCC／Accessibility 跨 App 更新實測。alpha.1 的授權遷移及後續 0.1.2 的單次更新保留另有實際 App 證據；不能由 A／B 測試承諾後續免重授權或當成 Apple Developer ID／公證。私密材料只存 `~/Library/Application Support/PSKeyboard/Signing`，不入 repo。
- [Accessibility 遷移](../research/macos-native/inspection/ACCESSIBILITY-MIGRATION.json)：系統清單雖顯示允許，`tccd` 仍以舊 ad-hoc `cdhash` 核對新版固定憑證身分，明確回報 code requirement mismatch。單純開關及重開 App 未解決；移除該項目、重新選取同一路徑並由本人允許後，同一份 alpha.1 回報 `Accessibility=true`，當時未連接、未啟用輸出。沒有重新建置、換憑證或直接修改 TCC 資料庫；這是一次身分遷移，不是跨後續更新保留測試。
- [Mac 精簡鍵位](MAC-DEFAULT-KEYS.md)不依賴 Fn／F8，改用 **Esc** 啟用／暫停；已部署 UI 採明確「連接並啟用控制」，加彙總計數辨別輸入與非中立輸出，不存原始按鍵。新 core **459**、V5 **22958** 項純測試通過；trial 2 另有使用者確認遊戲操作、W 放開停止及 Enter／Options 成功，前景／Esc 與其餘動作仍待逐項確認。
- 本輪已刪舊 OJD App／CLI／DMG／Application Support、Generic preview 6、失敗 DualSense preview 1、final 重複 App／module cache、舊 VZ runner 1 執行檔及 VirtualHere DMG，共 **565,747,591 邏輯 bytes**，收據 `.local/MAC-CLEANUP-20261030.json`。保留目前 runtime、VM／guest／backend、VirtualHere App／設定、原始碼及收據；邏輯大小不等於實際磁碟回收。已刪除 Generic／OJD 舊 bundle 的 `tccutil reset` 因 LaunchServices 無 identifier 而失敗，**那些舊權限未確認移除**。目前 DualSense App 的過期 ad-hoc 項目已透過系統設定移除並重新加入，由本人允許；沒有直接修改 TCC 資料庫。

## 歷史：OJD 失敗試驗

以下是改走 USB DualSense 前的紀錄。使用者曾自行允許 OJD 輸入監控及舊研究前端 Accessibility；新 App 必須按其實際權限狀態核對，不能假設沿用。

- 自行 ad-hoc 加 `com.apple.developer.hid.virtual.device` 的最小無 HID app 在 main 前遭 AMFI `-424` 拒絕。免費替代候選為作者已簽章的 **OpenJoystickDriver 0.6.0-alpha.1**，固定 commit `3552be925d7dacef3fd895ac0c6cbf39e9d3cd70`；app 簽章、公證、HID entitlement 與 all-devices profile 已核對，沒有購買或引入付費依賴。DMG 包裝本身未附公證 ticket，與 app 的通過結果分開記錄。
- `.local/ojd-neutral-2/RESULT.json`（2026-10-30 台北 11:36:51）：試驗前 `[]`，試驗中觀測 `1209:4a4f`／`OpenJoystickDriver Generic HID Gamepad`／Virtual，EOF 後 child exit `0`，試驗後 `[]`，未強制終止 child。只送 neutral，沒有擷取鍵盤或操作 Sony；建立與移除成立，Sony 相容與遊戲操作仍待驗。
- Swift 動作核心 **426**、USB report 純編碼 **4294**、OJD feed adapter **205**、PreviewPipe 本地 pipe **32** 項檢查通過。這些各自有純測試邊界，不能算作鍵盤、Sony 或 PS5 實機驗收。
- 研究前端是固定鍵位、連點關閉；`TouchpadClick` 不支援，`Guide` 是否對應 Sony PS 鍵待驗。包含前景限制、F8／失焦清狀態與 `PreviewPipe` 獨立 watchdog（50 ms 檢查、250 ms 無主執行緒 pulse 時關 stdin）。stdin EOF 與 child 退出仍不能替代裝置移除核對，未確認清理時不重新接入。
- 使用者已授予前端 Accessibility；Generic 遊戲試驗回報「無反應」，已按中斷並核對裝置移除，OJD service 已停止。不再安排 Generic／Xbox 試驗。真實 Fn／F8、按下／放開與失焦尚未分層驗收；原生 DualSense encoder 為純碼研究，沒有因此變成 OJD 已支援的 DualSense 輸出。

## 現有證據與尚未完成的事

| 項目 | 已知結果 | 不可推論 |
| --- | --- | --- |
| Windows PS4 模擬 | alpha.5／6 有操作 PS5 成功回報，alpha.5 有一次重開機成功 | 不保證 Mac、所有機器或所有重連順序 |
| Windows DualSense | alpha.9 四種均有使用者 Sony／PS5 操作成功回報，指定第二種 `dualsensegamepadv5` | 不等於逐鍵、音訊、觸覺、長時間或其他版本實機通過 |
| Windows alpha.10 | 第二種模式、22 動作改鍵／連點整合、取消 30 分鐘期限、離線測試及本機交付 | 無本版使用者實機回報 |
| Windows alpha.11 | 固定來源 `6dbc154a6a887398796d442fd34b73455672aba3`；[Release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11) ID `427698167`，四附件公開下載雜湊通過 | 三個執行元件仍未簽章；沒有新 Sony 實機驗收；主畫面仍未完整九語 |
| Windows alpha.12 | 固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`，Release `427756025`；主介面與設定頁九語、2D／JRPG 定位，已發布及本機交付 | 代理未啟動交付版；三個執行元件仍未簽章，本版仍未取得實機逐項驗收 |
| Windows 0.1.0 | 固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`，Release `427775269`；歷史正式版，四附件匿名下載及本機交付通過 | 已由 0.1.1 接替 Latest；代理未啟動交付版，無本版實機逐項驗收 |
| Windows 0.1.1 | 固定來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`，Release `427807747`；正式版、當時為 Latest，國際品牌修正、四附件匿名下載及本機交付通過 | 代理未啟動交付版；三個執行元件仍未簽章，未取得本版實機逐項驗收 |
| 單款遊戲回報 | 使用者回報 Final Fantasy Resonance 可正常操作，但未指明當時工具 build | 不能歸屬為 alpha.12、0.1.0 或 0.1.1 驗收，也不代表其他 JRPG、3D 遊戲或所有操作情境適用 |
| 舊檔清理 | 0.1.1 收尾 Apply 完成：刪除 9 項、753,491,748 邏輯 bytes，失敗 0；當時版本與封存目錄均只剩 0.1.1，保護狀態核對通過 | 另保留設定、備份及小型驗證紀錄；邏輯檔案大小不是實際磁碟回收量 |
| Mac 首輪（歷史） | 2026-10-30 完成環境盤點、公開 API 研究；arm64 原 native 5743 項及另列的 124 項參考協定檢查通過 | 首輪當時未建立虛擬裝置；原 C#／Go 未在 Mac 執行 |
| Mac 原生／OJD（歷史） | Swift core 426、USB encoder 4294、OJD adapter 205、pipe 32 項通過；Generic neutral 建立與移除成立；其後使用者遊戲回報無反應，已清理並停止 OJD service | 不再安排 Generic／Xbox 試驗；舊研究前端不是正式 Mac 交付版 |
| Mac USB／協定證據 | guest neutral 讀回；Mac USB 建立／移除；core 459、V5 22958、socket 57 項；relay neutral／EOF 清理 | 各自的純測試／裝置層證據，不代替遊戲逐項驗收 |
| Mac DualSense trial 1（歷史） | 9982 input／14308 output／19 feedback；使用者回報無反應，清理後 Mac `[]` | 舊版未計非中立幀；最終暫停不能證明先前未啟用 |
| Mac DualSense trial 2（歷史遊戲成功） | alpha.1；使用者確認遊戲可操作、W 放開停止、Enter／Options 有效；首次成功時 31 次收到／接受按鍵、433 幀非中立輸出；僅自有 USB DualSense | 本輪已中斷、relay owner 移除、Mac gamepad `[]` 確認，client／guest 正常結束；全鍵、Esc／失焦、長時間／重開機與獨立封裝仍待驗 |
| Mac 0.1.2 本機介面更新 | 同一 App／路徑／憑證，更新後 AX=true 且未重授權；淺色 UI、品牌、面鍵菱形、完整鍵位及展開診斷實際檢視通過 | 僅本機一次更新保留；無新遊戲試驗，服務停止，supervisor／公開封裝未完成；[收據](../research/macos-native/inspection/MAC-0.1.2-UI.json) |
| Mac 系統辨識／固定簽章 | GameController neutral 1 → 2 → 1，候選是 DualSense／extended gamepad；本人授權 CodeSigning 後不同 A／B binary 同 DR、strict verify／互驗／exit 0 通過，原 search list 還原 | 沒有讀 input；不證明 Sony 接受或 TCC／Accessibility 跨 App 更新保留 |

0.1.1 最終乾淨建置通過 **6912 項 C#**（162+2890+149+276+101+36+167+3131）、**5743 項 native** 與 C# ABI；公開包 **39／39 項稽核**通過。後端沿用固定 r4 執行檔，從已核對的 0.1.0 原始碼及交付包恢復來源與授權材料，沒有重編或啟動；Go 4 個頂層測試及 2 個子情境是既有證據，本次未重跑。這些沒有擷取真實鍵盤、建立使用者電腦裝置或操作 Sony，不是 macOS 或 PS5 實機測試。

九語共 18 張離線 PNG：**9 張主畫面因品牌及版號變更新產，由主代理及獨立審查逐張目視**；**9 張設定頁與 0.1.0 的 SHA-256 相同，沿用已完成的目視結果**。本次開發階段另有 11 個排版情境，未發現檢查範圍內的問題。alpha.12 的 18 個主畫面、36 個設定頁情境仍為歷史排版證據，不記成 0.1.1 新跑的實機 DPI 驗證。

主介面 50 個資源、共用／設定頁 93 個資源均涵蓋繁中、簡中、英文、日文、韓文、德文、法文、西班牙文及巴西葡語；一般使用依 Windows 介面語言自動選擇，不支援的語言回退英文。**繁中／簡中品牌為「上班不要玩」；其餘七語的 UI、說明與回饋文案品牌均用 `PS Remote Play Keyboard`，不混入中文品牌。** 控制器名稱及技術診斷碼維持原文。長文可換行、內容可捲動；母語校閱、真實高 DPI、多螢幕、逐鍵／連點、重連、長時間與重開機仍待本版實機驗證。

目前產品定位為 **2D 遊戲，尤其以選單及方向操作為主的 JRPG**。沒有滑鼠瞄準或右搖桿方向映射，不適合需要靈活視角或精細類比控制的 3D 遊戲；不能以單款回報承諾所有 JRPG 適用。Mac 版應先保留此功能界線，再依實際輸出能力另訂擴充範圍。

## 不得改變的產品及操作限制

- **使用 Sony 官方 Remote Play。** 不以第三方 Remote Play 用戶端替代、不索取 PSN 密碼、不注入 Sony 程序或讀取其記憶體。不自行關閉、重啟、更新或重新安裝 Sony；難得建立的連線優先保護。
- 使用者已決定所有功能免費，原創碼 GPL-3.0-only，作者 xiunlee；不引入付費依賴、DRM 或付費解鎖。上游、編譯器和 runtime 的授權分別保留。
- 鍵盤與連點只在官方 Remote Play 位於前景時作用；切窗停止並清除按住／連點狀態，切回須重新按鍵。Windows／歷史 Mac 研究用 F8；目前 Mac 精簡配置改用 Esc，依 [Mac 鍵位](MAC-DEFAULT-KEYS.md)實測，不放寬前景限制。
- 不擅自改裝、降級或替換驅動；Windows 既有成套 usbip-win2 0.9.8.1 不動。Mac 若研究需要新增系統元件、權限或可能影響現有連線的動作，先提出具體可檢視方案及影響，再安排必要的本人操作。
- 不關閉 SIP／AMFI 或修改第三方簽章。本人已明確同意本專案精確憑證的 user-domain CodeSigning 信任；這不授權 AllTrust、SSL、System-domain 信任或修改其他憑證。
- 不記錄原始打字、帳號、裝置序號或付款資料。測試用合成事件；公開收據只保留必要的版本、來源、結果與限制。
- 保留使用者按鍵設定與備份，只有按儲存且成功才套用。不得將 Windows 整數鍵碼不加轉換地當成 Mac 鍵碼，也不得為遷移直接覆寫唯一原檔。
- 只清理本程式建立且完整核對過的裝置／bus／port；不全域 detach、不停止其他軟體裝置或程序。未確認清理成功時先停止新接入。
- 新版採新目錄、新版本與新證據；不覆寫已公開附件或移動舊 tag。使用者授權本機只留最新完成版，但清理必須在建置／交付驗證完成後逐項核查，保留個人設定、備份與 GitHub 歷史。
- Windows 產品後續版號依使用者已指定的 **0.1.0 → 0.1.1 → … → 0.1.9 → 0.2.0**，不再加 alpha 標記；歷史 tag 與附件照原樣保留。Mac 的 alpha.1 留作歷史試驗，目前 Mac 依根目錄 `VERSION` 與 Mac manifest 為 **0.1.3**，公開標籤使用 **`mac-v0.1.3`**；Windows `v0.1.3` 是另一平台已發布的版本，不重用或移動其標籤。Mac 首發不取代 Windows 最新及上一版的公開下載。版號命名不取代每版的實機與相容性驗收。

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

## 分階段工作（首輪與後續中立試驗已另存）

1. **唯讀確認來源與 Mac 環境。** 核對工作目錄、remote、分支、HEAD、未提交變更及最新 Windows 收尾證據；記錄 macOS 版本、CPU 架構、現有編譯工具與 Sony 官方 Remote Play 的實際版本／安裝位置／執行狀態。不要啟動、終止或更新 Sony，不讀帳號資料；本機序號及個人完整路徑不要放進公開報告。
2. **先寫輸出可行性問題清單。** 根據該 macOS 與 Sony 版本查 Apple／Sony 官方文件及候選原始碼：虛擬手把輸出是否有可用公開介面、所需權限與簽章條件、是否需額外系統元件、是否能讓官方 Remote Play 接受。將已知事實、推測與待測項分開；未解決的輸出問題不能用完成 UI 代替。
3. **建立不接觸 Sony 的隔離驗證。** 先用合成報告測動作編碼、neutral／release、資料長度、序列、失連及清理。僅證明協定與測試程序行為；記錄新目錄、依賴版本、來源雜湊與如何回收。
4. **再驗證 Mac 裝置層。** 只有在方案、權限與操作範圍已具體確定後，才安排自有虛擬 HID 的建立、輸入讀回與完全移除；不要求使用者先重啟／重裝 Sony。裝置列舉成功仍不能宣稱官方 Remote Play 已接受。
5. **最後安排官方 Remote Play 實機。** 由使用者保有連線控制，先做一個按鈕按下／放開，再測方向、其餘動作、前景／Esc、改鍵／連點、重連與長時間；分別記錄協定、Mac HID、Sony 程序與 PS5 實際反應。需要中斷連線的項目另行協調，不隱含在研究授權內。
6. **輸出路線有證據後才完整做 UI／封裝。** 沿用品牌與已確認語言需求，但先檢查文字在該平台的真實顯示。若免費且不干擾官方用戶端的免實體手把路線不可行，如實記錄阻礙與可選方向，不能改用付費依賴或第三方用戶端冒充完成。

上述階段保留作驗證順序：首輪環境／研究與純測試已完成，目前第四階段已有 guest USB neutral 讀回、Mac USB DualSense 建立／移除、GameController 辨識與 relay neutral／EOF 清理的證據。第五階段的歷史 trial 1 無反應並已清理；固定簽章 alpha.1 完成部署與舊授權遷移後，trial 2 首次取得使用者確認遊戲操作、W 放開停止及 Enter／Options 成功。本次停止與裝置移除亦已獨立核對；仍需逐項驗證 Esc／失焦、其餘鍵、重連及長時間／重開機，不把單次成功當成獨立 Mac 成品或 TCC／Accessibility 跨更新保留證據。既有授權內的唯讀與隔離工作直接推進；只有新出現、確實需要本人處理的系統權限或連線中斷才提出具體說明。

## 收尾紀錄與下一串仍待驗的項目

- 0.1.1 的建置、固定提交、交付、正式發布與四附件匿名下載核對已完成；0.1.0 及 alpha.12 作為歷史版本保留。Mac 下一串先核對最新版交接與各固定來源，不將 Windows 執行包拿到 Mac 當成原生程式。
- 逐鍵、設定、連點、失焦／F8、重連、長時間／重開機仍待相應版本的實機逐項回報。Final Fantasy Resonance 的單款正常操作回報因工具 build 未指明，獨立記錄，不補猜版本。
- **本機舊檔清理已完成。** 0.1.1 最後一輪確認刪除 9 項、753,491,748 邏輯 bytes，失敗 0；略過 101 項中，99 項本已不存在，2 項為刻意保留的小型 log。當時 `versions` 與 `versioned-archives` 均只剩 `0.1.1`，個人設定及備份、安裝中的 0.9.8.1 CLI／DLL、Sony 程序與舊 tag 前後核對一致；最新捷徑、GitHub 歷史、追蹤中的研究／PS4 原始碼及小型私人證據保留。先前中斷段已確認的 92 項／4,006,258,013 bytes，以及後續 0.1.0 收尾 26 項／1,583,433,983 bytes，分段留存收據；不加算未記錄的部分刪除，也不把邏輯檔案大小當成實際磁碟回收量。
- Mac 首輪歷史見 [首輪紀錄](../research/macos-handoff/README.md)，現況接續 [原生研究總覽](../research/macos-native/README.md)。OJD Generic 與 DualSense trial 1 的遊戲回報均為無反應，兩輪已清理；USB／relay neutral、GameController 辨識及純測試證據另列。舊 App／安裝材料本輪清除 565,747,591 邏輯 bytes；本人授權的 CodeSigning 身分已通過不同 binary 同 DR 驗證，alpha.1 的 trial 2 已取得遊戲操作、W 放開停止及 Enter／Options 成功回報，並完成停止與裝置移除。歷史 0.1.2 原位更新保留 Accessibility，未新增遊戲驗收；目前已接續 0.1.3 建置 9，完整 App 中立連接、中斷、再次連接及退出清理均已核對，淺深色主畫面與設定已檢視，取消草稿保留原設定且無需重授 AX。遊戲、全新 Mac、睡眠／崩潰仍待驗證。實際公開狀態以 `mac-v0.1.3` 發行頁為準；本次已授權在明示限制下公開，不因此宣稱新版遊戲或 Windows 驗收通過。
