# Mac 原生研究：免費 OJD 輸出路線

更新：2026-10-30（Asia/Taipei）。**OJD Generic 能建立／移除裝置，但使用者在遊戲內測試回報「無反應」。此路線停止，後續只研究真正的 DualSense 身分、descriptor 與 reports；不再安排 Generic／Xbox 試驗。** 固定鍵位研究前端已建置並在本機啟動，不是已驗收的 Mac 成品，也沒有建立新的公開 Mac 版本或移動 Windows 標籤。

## 最新使用者實測與清理

使用者授予研究 App 輔助使用後，依提示連接 Generic、切回遊戲、以 F8、W／S 與 Enter 試驗，回覆「無反應」，並明確要求沿用 Windows 已成功的 PS5／DualSense 方向。此為未成功的人工試驗；沒有收集真鍵盤／輸出 report trace，不能只由無反應定位唯一失敗層。已觀測 Generic HID 存在，不代表 Sony 接受。[試驗收據](GENERIC-TRIAL.json)

使用者按「中斷」後，唯讀盤點列表恢復 `[]`；已正常停止本輪自行啟動的 OJD service，CLI 回覆 `Stopped`。沒有停止 Sony。研究 App 的清理標記保留，不會再啟動 Generic 測試。自己建立的 build 1–5 及 build 6 module cache 已確認無程序使用後回收，共 1,340,304,206 logical bytes；保留最新小型 App、收據與原始碼。這是檔案邏輯大小，不是磁碟淨差。

## 目前路線與授權範圍

```text
Mac 鍵盤／前景限制
  → Swift ControlCore
  → OJD 全狀態 JSON encoder
  → PreviewPipe 非阻塞 stdin 與獨立 watchdog
  → 作者已簽章的 OpenJoystickDriver CLI／app
  → macOS 虛擬 gamepad
  → Sony 官方 Remote Play（接受與遊戲操作待驗）
```

使用者沒有 Apple Developer 帳號，已授權繼續免費方案，並自行允許 OJD 的輸入監控。原先供唯讀規格研究的 `054c:0ce6` USB DualSense 已由使用者拔除；Sony Remote Play 的既有連線持續保留。上述權限與實體手把狀態不代表新建研究前端已獲准 Accessibility，也不能以此自行操作系統權限對話框。

本專案原創碼維持 GPL-3.0-only；OJD 是 MIT 免費開源依賴。仍使用 Sony 官方用戶端，不引入付費 broker、不替換 Remote Play、不修改安全策略、不注入 Sony、不索取帳號資料，不停止或重啟 Sony。

## 已完成的證據

| 階段 | 已證實 | 邊界與來源 |
| --- | --- | --- |
| 原生自行簽章 | 同一份無 HID 呼叫 app：普通 ad-hoc 能進 main；加入 HID restricted entitlement 後 SIGKILL、AMFI `-424` | 本機目前不能靠自行加 entitlement plist 取得能力；不是 Accessibility 或 Sony 失敗。[研究](SIGNING-RESEARCH.md)、[收據](SIGNING-SMOKE.json) |
| 免費已簽章 helper | OJD `0.6.0-alpha.1`、commit `3552be925d7dacef3fd895ac0c6cbf39e9d3cd70`；下載 hash、app 簽章、公證、HID profile 核對通過 | app 本體通過；DMG 包裝有有效簽章但未附公證 ticket，兩者不可混寫。[audit](OJD-AUDIT.md)、[收據](OJD-AUDIT-RECEIPT.json) |
| 物理 DualSense 規格 | 唯讀取得 USB descriptor，與各 report 宣告長度一致 | 未讀取真實 input／feature payload，沒有 seize 或寫入。[盤點](inspection/README.md) |
| Swift 動作核心 | **426** 項合成事件／假時鐘檢查通過 | 無真鍵盤、event tap 或 Sony；[ControlCore](CONTROL-CORE.md) |
| USB report encoder | **4294** 項純編碼與既有 C oracle 對照通過 | 不是 OJD ABI，也未透過 Mac HID 傳送；[encoder 收據](inspection/USB-ENCODER-TEST.json) |
| OJD feed adapter | **205** 項純格式／狀態機檢查通過 | 三個 profile 的 JSON、neutral、stop／EOF、失敗邊界；[OJD-FEED](OJD-FEED.md) |
| PreviewPipe | **32** 項本地 POSIX pipe 檢查通過 | 含非阻塞 backpressure、EPIPE、全幀寫入、假心跳與主執行緒阻塞時獨立 timer；[原始碼](../../src/macos/PreviewPipe.swift)、[測試](../../src/macos/PreviewPipeTests.swift) |
| Mac 虛擬裝置層 | `generic` 全中立 feed：建立、EOF、程序正常退出與移除均有觀測 | 不含按鍵、不擷取鍵盤、不操作 Sony；詳見下節 |

這些測試數各自描述其範圍，不累加成實機相容性或遊戲操作驗收。

## 中立裝置試驗

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

## 固定鍵位研究前端

前端原始碼：[KeyboardPreviewApp.swift](../../src/macos/KeyboardPreviewApp.swift)。目前候選目標包為 `.local/mac-keyboard-preview-6/PSKeyboardMacResearch.app`；Swift 6 warnings-as-errors 編譯及 ad-hoc strict 簽章驗證通過，見 [BUILD 收據](PREVIEW-BUILD.json)。使用者已允許其輔助使用權限；遊戲實測結果另記。較早 build 1–5 的開發／編譯中間產物不列為交付；新建置腳本只處理自己新建 bundle 的 FinderInfo／ResourceFork，保留其他安全來源屬性並重新驗簽。

- 使用固定 Mac hardware keycode，核心支援的改鍵／連點尚未做成完整設定 UI；本研究前端連點關閉。沒有讀取、轉換或覆寫 Windows 個人設定。
- 只有官方 Remote Play 前景才輸出；F8 暫停，失焦清空按住／連點狀態，切回須重新按鍵。Fn+F8 與精簡鍵盤的導航鍵行為仍待真人實測。
- OJD 目前有 `generic`、Xbox `game-controller-compatible`、Xbox `sdl-compatible` 三種固定 ABI，沒有 DualSense persona。**Touchpad click 不支援**並保持未指定；**Guide 是否被 Sony 解讀為 PS 鍵待驗**。
- `PreviewPipe` 對每行限制在 512 bytes，以單次非阻塞 write 傳送；未完整寫入就關閉自己的 stdin，不重試舊狀態。每個 fd 設 SIGPIPE 抑制及 close-on-exec，不改全域 signal policy。
- 獨立 Dispatch timer 每 50 ms 檢查主執行緒心跳，距上次 pulse 達 250 ms 就關閉 stdin；它可在 AppKit 主 run loop 停滯時切斷 feed。**本機 stdin 關閉不是 kernel 裝置已移除的證據**，仍需核對 child 與 HID 移除；未確認清理時不新建下一輪。
- 開始時與每次 tick 檢查公開 `IsSecureEventInputEnabled()`；若 macOS 啟用安全鍵盤輸入便關閉本輪 feed，避免漏掉 key-up 時繼續輸出。沒有關閉或繞過系統安全輸入。
- 主執行緒的輸出取樣 timer 加入 common run-loop modes，避免一般拖動視窗時停止 tick 而被 watchdog 誤判；真實拖窗、停滯與異常清理仍待整合實測。

研究前端需要使用者授予它自己的 Accessibility 才能攔截遊戲鍵；其設定按鈕會提出 Apple 系統提示並開啟設定頁，授權仍由本人決定。OJD 的授權不能轉移給前端。完整 OJD app 啟動仍會初始化實體 HID／USB 偵測與 Bluetooth central，相關首次提示由本人處理；不能把 `--no-input` 誤認為關閉硬體掃描或 macOS 權限提示。

## DualSense 輸出缺口

已對 OJD 設定／CLI／feature 路徑與其他已發布候選核對；目前所查版本沒有能直接接上本專案的免費、已授權 DualSense helper。詳見 [DualSense helper 能力與簽章查核](DUALSENSE-HELPER-GAP.md)及 [Mac USB 接入端查證](USB-OUTPUT-RESEARCH.md)。標準 USB/IP 候選目前公開 CI 跳過簽章；VirtualHere 發布檔雖有授權，但未找到可接自製 DualSense 的公開接口或 VIIPER 互通證據。可重用 Swift 核心及 PS5 編碼；後續須解決真正 Mac 輸出端，不能再以 Generic、Xbox 或只改 VID/PID 代替。

## 接手順序

1. 先讀本文件、[OJD audit](OJD-AUDIT.md)、[feed 契約](OJD-FEED.md)與 [ControlCore 契約](CONTROL-CORE.md)，再確認本機最新 build／trial 收據和未提交變更。`.local` 不隨 GitHub 同步，沒有檔案時不可推測試驗結果以外的細節。
2. 核對 Sony 原連線、OJD 服務及本輪裝置狀態；不要為測試重啟 Sony。實體手把已拔除的紀錄是本輪狀態，未來接手須重新確認，不能當成永久保證。
3. 研究前端已建置並啟動，使用者已授予輔助使用；電腦操作工具未獲准操作此 App，沒有代理 UI 目視驗收。Generic 單鍵試驗已回報無反應且清理；未取得真正 DualSense 後端前，不再要求使用者重複這輪操作。
4. Generic 已回報無反應並結束；不再測 Xbox。後續需先取得能輸出真正 DualSense descriptor、input／feature report 的免費已授權後端，或能沿用既有 Windows PS5 協定的 Mac 虛擬 USB 路徑，再安排下一次遊戲試驗。
5. 未取得輸出與鍵盤實測前，不把研究 App 交付為正式 Mac 版，不改 Windows `0.1.1`／歷史 tags，也不宣稱能免實體手把玩遊戲。

首輪環境／5743 native／124 協定參考檢查另存於 [macos-handoff](../macos-handoff/README.md)；其中「尚未建立裝置」描述首輪當時狀態，已由本文件的中立裝置觀測更新。完整歷史入口為 [MAC-HANDOFF](../../docs/MAC-HANDOFF.md)。
