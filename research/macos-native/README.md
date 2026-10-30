# Mac 原生研究：USB DualSense 輸出路線

更新：2026-10-30（Asia/Taipei）。目前版本為 **Mac 0.1.3／build 4**，中文名稱「上班不要玩」、非中文名稱 **Three Cats Keybridge**；定位為鍵盤控制與模擬手把工具，實際支援 Sony 官方 PS Remote Play。同一 App 已加入 Windows 對應的改鍵、連點、持久化、九語與支持功能，並將 guest／relay 管理移入原生 helper。固定 App／路徑／簽章更新後，設定與輔助使用權限均保留。建置 9 已確認 App 中立連接、中斷、再次連接與退出清理，並檢視淺／深色主畫面及設定；本版遊戲、新機、睡眠／崩潰仍待驗；後續品牌調整另記核對，不將建置 9 的畫面當成新品牌證據。Mac 公開標籤為 `mac-v0.1.3`，不與 Windows `v0.1.3` 衝突；實際公開狀態以發行頁為準。請以 [功能對齊清單](../../docs/MAC-PARITY-CHECKLIST.md)及[發佈驗證](../../docs/MAC-RELEASE-READINESS.md)區分實作、離線檢查與實機結果。

本程式是獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係。以下歷史名稱、協定識別與試驗記錄保留原樣。

0.1.2 介面與該次權限更新證據保留於 [建置收據](../../packaging/build-records/macos/0.1.2.json)及 [UI 紀錄](inspection/MAC-0.1.2-UI.json)。最近的使用者遊戲證據仍屬 **0.1.0-alpha.1 的 [trial 2](DUALSENSE-TRIAL-2.json)**：遊戲可操作、W 放開停止、Enter／Options 有效，結束後裝置與相關程序已清理。這不是新版原生 supervisor 的遊戲驗收。

## 最新使用者試驗：trial 2 確認可操作

[DualSense trial 2](DUALSENSE-TRIAL-2.json)使用當時固定簽章的 **0.1.0-alpha.1**，不是後續 0.1.2 的遊戲驗收。首個 owner 等待前端超過 600 秒，以 `frontend_wait_timeout` 結束；收到 0 幀輸入，`cleanup=removed`，另行確認 Mac HID `[]`。這一段沒有實際遊戲輸入，不能當作遊戲失敗。

重新建立的 attempt 2 於 `2026-10-30T06:20:38.439032Z` 啟動（台北 14:20:38）。完成自有 bus 1／device 1 的 USB/IP attach，再以當輪 `LIST` 所見地址 `(none).11` 交由 VirtualHere `USE` 接入；每輪仍須重新取得地址，不把本次數值當成固定設定。使用者在接入尚未完成時曾回覆「不行」，接入完成且連接 App 後改回覆 **「喔 可以了」**，其後確認 **「放開會停止，Enter 也有效」**，涵蓋本次 W 放開停止及 Enter／Options。

首次成功回報時 App 彙總診斷為 `observedGamePresses=31`、`acceptedGamePresses=31`、`nonneutralFrames=433`，`accessibilityGranted=true`、`armed=true`、`connected=true`、`failure` 空白，且曾辨識 Sony 前景。Mac 僅列出本輪 USB `054c:0ce6`／entry `4296402422`；先前實體手把 entry `4296400524` 不存在。沒有保存原始按鍵內容。

這是首次使用者確認 Mac 鍵盤可透過官方 Remote Play 控制遊戲。尚未逐項確認所有鍵、Esc／失焦、長時間、重連／重開機或獨立封裝；本次 attempt 2 已停止並獨立確認裝置移除，結果另列如下，不沿用 attempt 1 或舊 neutral 試驗的清理結果。

使用者結束遊戲後，透過 App「中斷」結束本輪。**最終** App 彙總診斷為 `observedGamePresses=150`、`acceptedGamePresses=144`、`nonneutralFrames=1157`、`accessibilityGranted=true`、`armed=false`、`connected=false`、`ended=true`，`failure` 空白；與前述首次成功時的 31／31／433 分開記錄。relay 於 `2026-10-30T06:28:36.461481Z`（台北 14:28:36）以 `frontend_eof` 結束，最終 **51066 input／59729 output／20 feedback**，`cleanup=removed`、`cleanupConfirmed=true`。另行核對 Mac gamepad 列表 `[]`，再結束本輪 VirtualHere client（`EXIT` 回覆 `OK`），guest backend stop／poweroff 正常 exit 0。

## 歷史使用者試驗：trial 1 無反應，已清理

[DualSense trial 1](DUALSENSE-TRIAL-1.json)使用未編版號的研究 build 1，使用者已允許其 Accessibility，遊戲測試後回覆「沒反應」及「鍵盤控制暫停，已按中斷」。**原測試步驟最後就是 F8 暫停，因此最後顯示暫停不能證明先前從未啟用。** 舊 App／relay 沒有統計非中立幀，不能只由總幀數或最後狀態判定問題出在鍵盤、前景、編碼、Mac USB 或 Sony 哪一層。

該輪 relay 記錄 **9982** 幀 frontend input、**14308** 幀 output、**19** 幀 feedback；frontend EOF 後 `reason=frontend_eof`、`cleanup=removed`，另行盤點 Mac HID 為 `[]`。這證明該輪有資料傳遞且清理成立，沒有證明遊戲收到有效的非中立操作。收據中 relay 的 `sonyTested=false` 指自動程式沒有操作 Sony；使用者的人工無反應回報另列，不互相取代。

## 最新 USB neutral 結果

固定 `VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2` 在無外部網路的 Linux guest 內產生候選，經 guest VHCI／usbfs、官方 VirtualHere Server **4.8.8**、VSOCK 及已簽章的 Mac client **6.0.3** 接入。guest 已讀到 64-byte neutral input；Mac 盤點從 `[]` 變成 Transport `USB`、usage `01:05` 的 DualSense。`STOP USING` 回覆不是精確 `OK`，但另行核對 guest 已 `UNBOUND`、Mac entry 消失且列表回到 `[]`。[Mac neutral 收據](inspection/MAC-USB-DUALSENSE-NEUTRAL.json)

免費模式保持 `unlicensed max_devices=1`，Mac client 不裝為 service。早期 host relay `33757` 觸發 license 提示；使用預設 host TCP **7575** → guest **7575** 後 `USE` 成功。guest 的固定識別與設定保留，沒有改 license key 或輪換識別。另一次 neutral 收據觀測到 3920 bytes feedback 被讀取；這是回程資料觀測，不是 Sony 或遊戲相容性驗收。[guest 實驗與來源](VIRTUALHERE-GUEST-SPIKE.md)

後續 [relay smoke](inspection/DUALSENSE-RELAY-SMOKE.json)已完成合成 frontend → [guest-input-relay.py](guest-input-relay.py) → guest → Mac USB 的 neutral 測試：收到 **125** 幀輸入及 **8** 幀 feedback；frontend EOF 後 `reason=frontend_eof`、`cleanup=removed`，Mac 自有 entry 在 **0.123 秒**後消失，列表 `[]`。這是一次中立與 EOF 清理測試，不含真鍵盤、Sony 或所有異常路徑。

[GameController neutral 盤點](inspection/GAMECONTROLLER-NEUTRAL.json)另證實 macOS GameController 能辨識該虛擬 DualSense：實體 USB 手把已重新接回，baseline 為 **1** 個 controller；加入自有 neutral USB 後為 **2** 個，皆為 `DualSense` 且有 extended gamepad；移除自有裝置後回到 **1**，實體 USB entry 保留。此盤點沒有讀取 input 值、要求無線探索或手動 open 裝置；以數量與 USB ancestry 核對候選，不能只靠名稱區分，更不能推論 Sony 遊戲接受。

## 固定版本、部署與授權

使用者要求有固定版本、保留同一個 App、避免每次重做輔助使用授權，並適用 Mac 精簡鍵盤。[版本 manifest](../../packaging/macos/version.json)目前顯示版本 **0.1.3**、bundle version **0.1.3 / build 4**，沿用 bundle identifier `com.xiunlee.pskeyboard.mac-dualsense`；安裝位置固定為 **`~/Applications/PSKeyboardMacDualSense.app`**。這是與 Windows 分平台的 Mac 版本；顯示品牌更新不改此固定路徑或 identifier，亦不取代 Windows 已公開版本。

使用者已明確授權精確的 **PSKeyboard Local Development** 專案憑證加入 **user domain／CodeSigning policy** 信任，並親自完成兩次系統確認。初始設定曾額外限定 `codesign` application／sign key usage，預查回報 `no identity found`；移除額外 `-a`／`-u` 限制後，export 核對該憑證最終只有一個 `CodeSigning` entry，沒有 AllTrust、SSL 或 System-domain 信任。見 [首次信任與失敗](inspection/LOCAL-SIGNING-TRUST.json)及 [最終 policy](inspection/LOCAL-SIGNING-TRUST-POLICY.json)。

只在簽章測試期間將本專案 keychain 暫時加入原搜尋清單，兩個內容不同的 A／B binary 均簽章及 strict verify 通過、designated requirement 相同且互驗通過，執行皆 exit `0`。完成後移除臨時 binary、還原原 search list，default keychain 未變；此次臨時測試前後的已授權 trust export 一致。見 [隔離測試](inspection/LOCAL-SIGNING-SEARCH-ISOLATION.json)與 [setup 收據](inspection/LOCAL-SIGNING-SETUP.json)。這項 A／B 測試只證明不同 binary 的簽章身分一致，**本身不是 TCC／Accessibility 跨 App 更新實測**。alpha.1 的[建置](../../packaging/build-records/macos/0.1.0-alpha.1.json)、舊授權遷移與後續 [0.1.2 單次更新保留](inspection/MAC-0.1.2-UI.json)各有實際 App 證據，不以測試 binary 成功代替 App 或遊戲驗收。

系統清單的 Accessibility 開關曾顯示允許，但 `tccd` 明確記錄保存的舊 ad-hoc `cdhash` 與目前固定憑證 requirement 不符。切換開關及重開 App 後仍拒絕；透過系統設定移除這一個舊項目、重新選取同一 App，再由本人按「打開」並允許後，App 已回報 `Accessibility=true`，核對時 `armed=false`、`connected=false`。沒有重建、換簽章或直接修改 TCC 資料庫；[遷移收據](inspection/ACCESSIBILITY-MIGRATION.json)。Computer Use 的桌面控制亦已在 ChatGPT 重啟後恢復，成功讀取測試 App。

本機自簽不等於 Apple Developer ID／公證，沒有因此取得受限 HID／USB entitlement 或更動第三方簽章。私密簽章材料只留在 `~/Library/Application Support/PSKeyboard/Signing`，不進 repo；公開紀錄僅保留必要的結果與非秘密驗證資料。

[Mac 預設鍵位](../../docs/MAC-DEFAULT-KEYS.md)改用精簡鍵盤單鍵：WASD／方向鍵、IJKL 四面鍵、Tab／Return、P／T 等，啟用／暫停改成 **Esc**，不依賴 Fn 或 F8。已部署 UI 以明確的「連接並啟用控制」動作開始，並以彙總計數區分收到的輸入與非中立輸出，不保存原始按鍵。Swift 動作核心 **459** 項、V5 協定 **22958** 項及專用 [socket](inspection/DUALSENSE-RELAY-SOCKET-TESTS.json) **57** 項檢查通過；另有 trial 2 的真人操作、W 放開停止與 Enter／Options 成功回報，完整逐項流程仍待驗證。

trial 1 的歷史 [BUILD 收據](PREVIEW-DUALSENSE-BUILD.json)與 binary SHA-256 `d6fda81b262e6b469bf10ed303e4f42afc936709bef0b24823bcb1448ae484a1` 保留。當時使用 ad-hoc 簽章；因 Documents 的 FileProvider 在 open 時重加 FinderInfo，同一未修改 bundle 曾改放 `~/Applications`，strict verify 通過並啟動。該歷史結果不能當作新固定簽章版已部署的證據。

在 alpha.1 試驗收尾時，guest／relay／VirtualHere client 已停止，當時仍須研究流程協調，supervisor 與公開封裝尚未完成。此為歷史狀態；0.1.3 的原生管理與完整 App 中立連線／清理已完成，詳見文件開頭與同版驗證清單。trial 2 已取得首次真人操作、W 放開停止及 Enter／Options 成功回報；版本、固定簽章與本次 Accessibility 授權已核對，接著仍須逐項驗證其餘鍵及前景／Esc。本輪已停止並獨立確認裝置移除，VirtualHere client 與 guest 正常結束；重連及長時間另外驗證。

## 本輪舊版本與安裝材料清理

依使用者要求，已核對程序與開啟檔案後刪除舊 OJD App／CLI／DMG、Generic preview 6、失敗 DualSense preview 1、final build 的重複 App／module cache、舊 VZ runner 1 執行檔、VirtualHere DMG，以及本次產生的 OJD Application Support。共 **565,747,591 邏輯 bytes**，收據為 `.local/MAC-CLEANUP-20261030.json`；這不是實際磁碟淨回收量。保留目前 runtime、VZ runner 2、guest kernel／initrd／share／固定識別、backend、VirtualHere App／設定、原始碼及收據。

曾用 `tccutil reset` 清理已刪除 Generic／OJD 舊 bundle 的授權，但 LaunchServices 無法找到該 identifier，因此命令失敗。**不能宣稱那些舊權限已移除**。目前 DualSense App 的過期 ad-hoc 項目已透過系統設定移除並重新加入，由本人允許；沒有直接修改 TCC 資料庫。

## 歷史：OJD 遊戲無反應與清理

使用者授予研究 App 輔助使用後，依提示連接 Generic、切回遊戲、以 F8、W／S 與 Enter 試驗，回覆「無反應」，並明確要求沿用 Windows 已成功的 PS5／DualSense 方向。此為未成功的人工試驗；沒有收集真鍵盤／輸出 report trace，不能只由無反應定位唯一失敗層。已觀測 Generic HID 存在，不代表 Sony 接受。[試驗收據](GENERIC-TRIAL.json)

使用者按「中斷」後，唯讀盤點列表恢復 `[]`；已正常停止本輪自行啟動的 OJD service，CLI 回覆 `Stopped`。沒有停止 Sony。研究 App 的清理標記保留，不會再啟動 Generic 測試。較早曾回收 build 1–5 及 build 6 module cache，共 1,340,304,206 logical bytes，當時保留小型 App；後續本輪清理已移除該舊 App 與 OJD runtime，收據與原始碼保留。各段數字均為檔案邏輯大小，不是磁碟淨差。

## 目前路線與授權範圍

```text
Mac 鍵盤／前景限制
  → Swift ControlCore
  → DualSenseV5Protocol → host 127.0.0.1:33743 的 guest-input-relay.py
  → host 127.0.0.1:33742 → VSOCK 33642 → guest VIIPER API
  → guest USB/IP :33641 → vhci_hcd → usbfs
  → 官方 VirtualHere Server :7575 → VSOCK 7575
  → host 127.0.0.1:7575 → 官方已簽章 VirtualHere Mac client
  → macOS USB DualSense
  → Sony 官方 Remote Play（trial 2 首次使用者確認遊戲操作成功）
```

上圖從合成 frontend 到 Mac USB 的 neutral／EOF 清理及 GameController 辨識已通過；歷史 trial 1 無反應，trial 2 使用同一份 alpha.1 首次取得使用者確認遊戲操作成功。使用者沒有 Apple Developer 帳號，已授權繼續免費方案。本次成功回報時實體 USB 手把已拔除、只存在自有 USB DualSense；歷史 GameController neutral 盤點曾包含實體手把，不混用兩輪狀態。每輪需重新核對 Sony 連線與裝置狀態。固定簽章 alpha.1 的 Accessibility 已透過本次重新加入項目取得，依據是 App 自身的新診斷，不沿用 trial 1 的授權；仍不代表後續更新可永久保留。

本專案原創碼維持 GPL-3.0-only，Linux 等依賴保留各自授權。VirtualHere 是官方專有軟體，採 Generic Server 單裝置免費條件；免費使用不等於開源、改包或再散布授權。歷史 OJD 依賴為 MIT。仍使用 Sony 官方用戶端，不引入付費依賴、不替換 Remote Play，不關閉 SIP／AMFI 或修改第三方簽章；本人已同意專案憑證的 user-domain CodeSigning 信任。不注入 Sony、不索取帳號資料，不停止或重啟 Sony。

## 已完成的證據

| 階段 | 已證實 | 邊界與來源 |
| --- | --- | --- |
| 原生自行簽章 | 同一份無 HID 呼叫 app：普通 ad-hoc 能進 main；加入 HID restricted entitlement 後 SIGKILL、AMFI `-424` | 本機目前不能靠自行加 entitlement plist 取得能力；不是 Accessibility 或 Sony 失敗。[研究](SIGNING-RESEARCH.md)、[收據](SIGNING-SMOKE.json) |
| OJD helper（歷史） | OJD `0.6.0-alpha.1`、commit `3552be925d7dacef3fd895ac0c6cbf39e9d3cd70`；下載 hash、app 簽章、公證、HID profile 核對通過 | app 本體通過；DMG 包裝有有效簽章但未附公證 ticket，兩者不可混寫。[audit](OJD-AUDIT.md)、[收據](OJD-AUDIT-RECEIPT.json) |
| 物理 DualSense 規格 | 唯讀取得 USB descriptor，與各 report 宣告長度一致 | 未讀取真實 input／feature payload，沒有 seize 或寫入。[盤點](inspection/README.md) |
| Swift 動作核心 | 最新 **459** 項合成事件／假時鐘檢查通過，包含新的 Mac 精簡鍵位；舊版 426 項保留作歷史 | 無真鍵盤、event tap 或 Sony；[鍵位](../../docs/MAC-DEFAULT-KEYS.md)、[ControlCore](CONTROL-CORE.md) |
| USB report encoder | **4294** 項純編碼與既有 C oracle 對照通過 | 不是 OJD ABI，也未透過 Mac HID 傳送；[encoder 收據](inspection/USB-ENCODER-TEST.json) |
| OJD feed adapter | **205** 項純格式／狀態機檢查通過 | 三個 profile 的 JSON、neutral、stop／EOF、失敗邊界；[OJD-FEED](OJD-FEED.md) |
| PreviewPipe | **32** 項本地 POSIX pipe 檢查通過 | 含非阻塞 backpressure、EPIPE、全幀寫入、假心跳與主執行緒阻塞時獨立 timer；[原始碼](../../src/macos/PreviewPipe.swift)、[測試](../../src/macos/PreviewPipeTests.swift) |
| OJD 裝置層（歷史） | `generic` 全中立 feed：建立、EOF、程序正常退出與移除均有觀測 | 其後使用者遊戲回報無反應；路線停止，詳見歷史紀錄 |
| V5 協定 | **22958** 項離線檢查通過 | 純協定與既有格式對照，不是鍵盤或 Sony 驗收；[原始碼](../../src/macos/DualSenseV5Protocol.swift)、[測試](../../src/macos/DualSenseV5ProtocolTests.swift) |
| Mac USB DualSense | guest 64-byte neutral 讀回；Mac `054c:0ce6`／USB 建立，STOP USING 後 guest UNBOUND、Mac entry 消失 | 無鍵盤動作，Sony／PS5 遊戲未測；[收據](inspection/MAC-USB-DUALSENSE-NEUTRAL.json)、[實驗](VIRTUALHERE-GUEST-SPIKE.md) |
| Relay neutral smoke | 合成 frontend 125 幀 neutral、8 幀 feedback；EOF 後 cleanup removed、Mac entry 0.123 秒後消失 | 未擷取真鍵盤、未操作 Sony，不代表所有失連情境；[收據](inspection/DUALSENSE-RELAY-SMOKE.json) |
| DualSenseRelaySocket | **57** 項本地 socket 檢查通過 | 不等於真鍵盤或 Sony 操作；[收據](inspection/DUALSENSE-RELAY-SOCKET-TESTS.json) |
| DualSense user trial 1 | 使用者回報無反應；9982 input／14308 output／19 feedback；EOF removed 與 Mac `[]` 確認 | 結尾暫停不證明此前未啟用；舊版未計非中立幀，不能定位唯一失敗層；[收據](DUALSENSE-TRIAL-1.json) |
| DualSense user trial 2 | alpha.1；使用者確認遊戲可操作、W 放開停止、Enter／Options 有效；首次成功時 31 次收到／接受按鍵、433 幀非中立輸出；僅自有 USB DualSense | 本輪已中斷、relay owner 移除及 Mac gamepad `[]` 確認，client／guest 正常結束；全鍵、Esc／失焦、長時間／重開機與獨立封裝未驗；[收據](DUALSENSE-TRIAL-2.json) |
| GameController neutral | controller 數量 1 → 2 → 1；虛擬候選被辨識為 DualSense／extended gamepad，移除後實體手把保留 | 未讀 input、未證明 Sony 接受；[收據](inspection/GAMECONTROLLER-NEUTRAL.json) |
| 固定本機簽章 | 本人授權 user-domain CodeSigning；不同 A／B binary 同 DR、strict verify／互驗通過、exit 0；原 search list 還原；alpha.1 已固定簽章並部署 | 不等於 TCC／Accessibility 跨更新保留；[簽章收據](inspection/LOCAL-SIGNING-SEARCH-ISOLATION.json)、[App 建置](../../packaging/build-records/macos/0.1.0-alpha.1.json) |
| Mac 0.1.2 介面／更新權限 | 同一 App／路徑／憑證更新；啟動後 version 0.1.2、AX=true、未連接／未啟用，無重新授權；淺色介面及展開診斷實際檢視通過 | 僅本機這一次更新保留，沒有新遊戲試驗；服務停止，supervisor／公開封裝未完成；[收據](inspection/MAC-0.1.2-UI.json) |
| Accessibility 身分遷移 | `tccd` 確認舊 ad-hoc cdhash 與固定憑證 requirement 不符；移除舊項目、同一路徑重新加入並由本人允許後，同一份 alpha.1 回報 true | 當時未連接／未啟用輸出；不代表跨後續更新保留或遊戲成功；[收據](inspection/ACCESSIBILITY-MIGRATION.json) |

這些測試數各自描述其範圍，不累加成實機相容性或遊戲操作驗收。

## 歷史：OJD 中立裝置試驗

本機收據：`.local/ojd-neutral-2/RESULT.json`，時間 `2026-10-30T03:36:51.955859Z`（台北 11:36:51）。已直接讀取該收據，結果如下：

| 欄位 | 結果 |
| --- | --- |
| profile／輸入 | `generic`；`neutralOnly=true` |
| 試驗前 | 此試驗匹配的裝置列表 `[]` |
| 試驗中 | `OpenJoystickDriver Generic HID Gamepad`，VID/PID `1209:4a4f`，Transport `Virtual`，usage `01:05` |
| EOF 與退出 | child exit `0`；未強制終止自己的 child；stdout／stderr 空白 |
| 試驗後 | 匹配的裝置列表 `[]`；`creationObserved`、`removalObserved`、`absenceAfterTrialConfirmed` 均為 `true` |
| Sony／鍵盤 | `sonyOperated=false`、`keyboardCaptured=false` |

`--json` feed 本來沒有建立成功的 stdout 確認行；本次成立與移除的證據是前／中／後 HID 盤點，不是空白輸出。這份收據只涵蓋本輪 Generic feed，不能推論其他 profile、所有異常清理或 OJD 主服務已停止。

## 歷史：OJD 固定鍵位研究前端

舊前端原始碼：[KeyboardPreviewApp.swift](../../src/macos/KeyboardPreviewApp.swift)，當時包為 `.local/mac-keyboard-preview-6/PSKeyboardMacResearch.app`，現已清理；Swift 6 warnings-as-errors 編譯及 ad-hoc strict 簽章驗證的歷史證據見 [BUILD 收據](PREVIEW-BUILD.json)。使用者曾允許其輔助使用權限；遊戲實測結果另記。這份收據不涵蓋新的 DualSensePreviewApp。較早 build 1–5 的開發／編譯中間產物不列為交付；建置腳本只處理自己新建 bundle 的 FinderInfo／ResourceFork，保留其他安全來源屬性並重新驗簽。

- 使用固定 Mac hardware keycode，核心支援的改鍵／連點尚未做成完整設定 UI；本研究前端連點關閉。沒有讀取、轉換或覆寫 Windows 個人設定。
- 只有官方 Remote Play 前景才輸出；F8 暫停，失焦清空按住／連點狀態，切回須重新按鍵。Fn+F8 與精簡鍵盤的導航鍵行為仍待真人實測。
- OJD 目前有 `generic`、Xbox `game-controller-compatible`、Xbox `sdl-compatible` 三種固定 ABI，沒有 DualSense persona。**Touchpad click 不支援**並保持未指定；**Guide 是否被 Sony 解讀為 PS 鍵待驗**。
- `PreviewPipe` 對每行限制在 512 bytes，以單次非阻塞 write 傳送；未完整寫入就關閉自己的 stdin，不重試舊狀態。每個 fd 設 SIGPIPE 抑制及 close-on-exec，不改全域 signal policy。
- 獨立 Dispatch timer 每 50 ms 檢查主執行緒心跳，距上次 pulse 達 250 ms 就關閉 stdin；它可在 AppKit 主 run loop 停滯時切斷 feed。**本機 stdin 關閉不是 kernel 裝置已移除的證據**，仍需核對 child 與 HID 移除；未確認清理時不新建下一輪。
- 開始時與每次 tick 檢查公開 `IsSecureEventInputEnabled()`；若 macOS 啟用安全鍵盤輸入便關閉本輪 feed，避免漏掉 key-up 時繼續輸出。沒有關閉或繞過系統安全輸入。
- 主執行緒的輸出取樣 timer 加入 common run-loop modes，避免一般拖動視窗時停止 tick 而被 watchdog 誤判；真實拖窗、停滯與異常清理仍待整合實測。

研究前端需要使用者授予它自己的 Accessibility 才能攔截遊戲鍵；其設定按鈕會提出 Apple 系統提示並開啟設定頁，授權仍由本人決定。OJD 的授權不能轉移給前端。完整 OJD app 啟動仍會初始化實體 HID／USB 偵測與 Bluetooth central，相關首次提示由本人處理；不能把 `--no-input` 誤認為關閉硬體掃描或 macOS 權限提示。

## 先前輸出缺口與後續突破

初輪未找到能直接接上本專案的免費、已授權 DualSense helper，見 [當時能力查核](DUALSENSE-HELPER-GAP.md)及 [USB 接入端研究](USB-OUTPUT-RESEARCH.md)。標準 USB/IP → VirtualHere client 仍不是相容的直接連線；新的 guest VHCI → 官方 VirtualHere Server 轉接已補上 neutral 輸出的實測證據，取代「尚無可用接入路線」的初輪結論。它不依賴 Generic／Xbox 或單改 VID/PID，也沒有改 VirtualHere binary／簽章。完整 feature、Sony 接受度及遊戲操作仍不能由 neutral 成功推論。

## 接手順序

1. 先讀本文件、[trial 2](DUALSENSE-TRIAL-2.json)及歷史 [trial 1](DUALSENSE-TRIAL-1.json)、[Mac 版本 manifest](../../packaging/macos/version.json)、[Mac 鍵位](../../docs/MAC-DEFAULT-KEYS.md)，再讀 [USB neutral](inspection/MAC-USB-DUALSENSE-NEUTRAL.json)、[relay smoke](inspection/DUALSENSE-RELAY-SMOKE.json)、[guest 實驗](VIRTUALHERE-GUEST-SPIKE.md)及 [ControlCore 契約](CONTROL-CORE.md)。核對本機最新 build／trial 收據和未提交變更；`.local` 不隨 GitHub 同步，沒有檔案時不可推測試驗結果以外的細節。
2. 核對 Sony 原連線及本輪裝置狀態；不要為測試重啟 Sony。trial 2 成功回報時實體 USB 手把已拔除、只有本輪自有 entry；先區分實體與自有虛擬裝置，不沿用歷史 GameController neutral 的實體接回狀態。
3. trial 1 已回報無反應並清理；alpha.1 已完成舊 Accessibility 項目遷移，trial 2 已取得真人操作成功回報；歷史 0.1.2 原位更新未重授權且 AX=true；目前為 0.1.3，建置 9 已驗中立接入、停止、重連及退出清理，仍無本版遊戲驗收。接手仍核對實際執行版本及當下權限，不以舊版 hash／授權代替。保留同一安裝路徑，不新增多份使用者 App。Generic 已回報無反應且移除，不再安排 Generic／Xbox。
4. 使用已驗證的 guest 啟動順序、預設 host TCP 7575、固定 guest 識別及保留設定；只處理本 session 的裝置，維持單裝置免費限制。以新「連接並啟用」／Esc 流程測按下／放開與失焦，記錄彙總計數及使用者反應；STOP USING 後仍須獨立確認 guest UNBOUND 及 Mac entry 消失。
5. 本次遊戲操作成功不等於完整驗收或獨立封裝；本次停止移除已確認；0.1.3 的完整 App 中立重連已有獨立證據，其餘操作、長時間及重開機仍待驗。本次已授權依已揭露限制公開 Mac；使用 `mac-v0.1.3`，不改 Windows 版本／歷史 tags，也不把本機結果推廣成所有遊戲或機器的保證。

首輪環境／5743 native／124 協定參考檢查另存於 [macos-handoff](../macos-handoff/README.md)；其中「尚未建立裝置」描述首輪當時狀態，已由本文件的中立裝置觀測更新。完整歷史入口為 [MAC-HANDOFF](../../docs/MAC-HANDOFF.md)。
