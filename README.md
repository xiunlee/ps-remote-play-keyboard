# 上班不要玩（開發中）

將鍵盤轉成手把輸入，搭配 Sony 官方 PS Remote Play 使用。主要開發方向為**免實體手把**；專案原名為 PS Remote Play 鍵盤手把。

> **專案狀態：2026-10-29 免手把 PS4 模式實機成功。** `0.1.0-alpha.5` 已有重開機後正常回報；新版 `0.1.0-alpha.6` 的 PS4 模式亦由使用者確認可操作。alpha.6 的 PS5 DualSense 模式能接入 Windows，但官方 Remote Play 操作無反應，保留為實驗選項。主捷徑預設可用的 PS4 模式；[實機紀錄](research/controller-free-preview/ALPHA6-TRIAL.md)。長時間使用、各種重連與乾淨電腦安裝仍待驗證，尚未正式發布。

版本總控：[總進度與換串交接](docs/HANDOFF.md) · [目前版本](VERSION) · [變更紀錄](CHANGELOG.md) · [版本控管與回退](docs/VERSIONING.md)。最新開發內容在 `codex/commercial-preview`，GitHub 預設 `main` 可能落後；請以工作分支的交接文件接續。

`0.1.0-alpha.8` 新增白色 PS 圖示與莓紅介面，統一主畫面、按鍵設定及狀態燈；[查看設計與畫面預覽](docs/VISUAL-DESIGN.md)。保留已使用的按鍵配置與模擬後端，PS5 相容性研究未在本輪變更。

`0.1.0-alpha.7` 依使用者要求調整[預設鍵位](docs/DEFAULT-KEYS.md)：方向鍵對應 △／□／×／○，E／Q 對應 L1／L2，Delete／Page Down 對應 R1／R2。已有設定會保留，須按「恢復預設」才套用新排列。

## 主要方向：免手把鍵盤控制

[免手把測試程式](research/controller-free-preview/README.md) 將鍵盤輸入交給免費開源 VIIPER 與既有 usbip-win2 驅動，讓官方 Remote Play 讀取模擬 DS4。使用者不需插入實體手把，也不需交出 PSN 帳號；仍使用 Sony 官方用戶端。完整依賴、證據與限制見[首次成功紀錄](research/controller-free-preview/LIVE-TRIAL.md)。

沿用自訂按鍵、每鍵連點（按住／按一次啟停、可設定毫秒間隔）及 F8 暫停。鍵盤控制只在官方 Remote Play 為前景視窗時生效；切換視窗會停止並清除連點，回來後須重新觸發。滑鼠與腳本錄製尚未實作。

依使用者選定方向，後續優先完善免手把版的穩定性、操作介面與安裝流程；其他實體手把相容性暫不擴充。既有實體中繼程式保留作為備份與研究紀錄，不要求使用者購買或插入手把。

**操作約束：保留使用者目前的官方 Remote Play 連線，不得為檢查或套用更新自行關閉、重啟它。** 若後續操作確實需要中斷連線，先說明原因並取得使用者同意。

- [發布前功能與支援範圍](docs/PRODUCT-SCOPE.md)：目前功能、語言、平台與發布條件。
- [連點與控制驗證](docs/AUTOMATION-AND-CONTROLLER-TESTS.md)：自動化界線、首次成功證據與待測項目。

## 保留的備用方案：合併實體手把的輸入

實體手把把資料交給 Windows，官方 Remote Play 再讀取它。新方向是在這條通訊路徑中保留真手把的 USB 識別、控制與 feature report 往返，只把鍵盤狀態合併進已確認格式的按鍵報告。這需要實際轉接手把通訊，一般 HID 寫入不能直接變成其他程式讀到的按鍵。

目前採用 usbipd-win 匯出實體裝置、自製本機 USB/IP 中繼和已安裝的 usbip-win2 接收端；仍會重新枚舉 USB 裝置，並非保持原來的 Windows 裝置路徑不變。經使用者明確授權，已安裝簽章驅動並切換指定的實體 DS4，Windows 經中繼連續回讀兩分鐘通過。翌日使用者回報測試正常，唯讀核對亦確認鍵盤合併輸出；畫面操作結果以使用者回報為據，未直接讀取 Sony 畫面。實體手把通訊限制在本機，不需第三方帳號。

- [實體中繼程式](src/physical-keyboard/README.md)：鍵盤擷取、F8 暫停及實體報告合併；單獨啟動不會接管手把。
- [USB/IP 中繼與驗證](research/physical-usb-relay/README.md)：限定本機、單一裝置，核對實際 HID 輸入通道後才合併按鍵。
- [實機測試準備](research/physical-usb-relay/FEASIBILITY.md)：免費簽章驅動、單一實體裝置切換、網路限制及還原步驟。
- [本機實測紀錄](research/physical-usb-relay/LIVE-TRIAL.md)：區分使用者的正常測試回報、本機輸出核對與尚未逐項確認的功能；原始手把驅動還原仍未實測。
- [發布前功能與支援範圍](docs/PRODUCT-SCOPE.md)：自訂鍵盤、狀態燈、系統語言，以及其他手把／免手把／Mac 的目前界線；先完善再發布。

## 歷史紀錄：2026-10-28 未成功的獨立 USB 手把候選

當時的程式在 [src/keyboard-pad](src/keyboard-pad/README.md)。我們的程式處理鍵盤與按鍵狀態，免費開源 VIIPER 提供 USB 手把模擬，usbip-win2 提供有數位簽章的 Windows 驅動。這條路不需要第三方帳號、不讀取 PSN 資料、不把程式載入 Sony 程序，也不重新安裝 ViGEmBus。以下保留當次未成功的結果；翌日獨立免手把測試已成功，先前失敗原因仍未定位。

此候選建立另一個 USB 手把，沒有把實體 PS4 手把合併為同一個裝置。2026-10-28 使用者實測，Windows 可讀到真實鍵盤映射的按下、長按、放開；切回 Sony 後 PS5 選項不動，暫拔實體手把後仍無反應。依使用者指定方向停止擴充此候選，並移除當次建立的虛擬裝置與背景服務。這不能推論所有虛擬手把都不可行，也尚未定位 Sony 拒絕或未採用輸入的原因。

- [鍵盤映射核心](research/native-merge/README.md)：方向、複合按鈕、扳機及放開回中。
- [按鍵生命週期](research/keyboard-input-tests/README.md)：暫停、失焦、快捷鍵及不完整按鍵事件的處理。
- [本機傳輸](research/viiper-client-tests/README.md)：斷線、回中、限定只清除自己建立的裝置。
- [USB/IP 回讀](research/viiper-usb-test/README.md)：實際映射程式送進 VIIPER 後，讀回 64-byte DS4 報告；這不等於 Windows 或 Sony 已驗收。
- [Windows HID 實測](research/windows-hid-test/README.md)：實際建立 Windows USB DS4，透過 Windows 手把介面讀回按下、長按、放開及移動狀態；20 項檢查通過。
- [真實鍵盤觀測](research/windows-hid-observer/README.md)：唯讀觀測既有虛擬裝置，確認真實鍵盤輸入已進入 Windows；使用者同時回報 Sony 無操作效果。

安裝 USBip 會短暫重新連接 USB 裝置，且本次安裝要求稍後重開機。程式不會自行安裝驅動或重開機。建置、映射表及操作方式見 [測試版說明](src/keyboard-pad/README.md)。

## 先前實體 PS4 手把研究

已透過[只讀檢查](research/native-probe/README.md)找到官方程式開啟的實體 DS4。後續的[程序內觀測實驗](research/native-bridge/README.md)未能在 Sony 成功載入，使用者回報程式自行退出；未定位根因，未實現鍵盤控制。自製測試程序通過不代表 Sony 相容。

## 已排除：reWASD 商用依賴

使用者於 2026-10-28 明確選擇「不要依賴付費軟體，維持自製方案」。因此不安裝、不啟用 reWASD，不將它列為產品依賴。以下資料僅保留研究紀錄。

[reWASD 官方文件](https://help.rewasd.com/basic-functions/how-to-apply-config-to-physical-device.html)明確支援將鍵盤、滑鼠和 DUALSHOCK 4 組成群組，選用實體手把輸出；這條路保留 Sony 官方 Remote Play。文件提及不支援的 relative sticks 是組合動作的搖桿偏移功能，不能據此推論一般滑鼠轉右搖桿不支援。

這是第三方商用映射工具的候選整合方案，並非本專案已完成的自製驅動。需要另外[安裝 reWASD 驅動並重開機](https://help.rewasd.com/installation-notes/installation.html)，以及[登入 reWASD 帳號](https://help.rewasd.com/first-launch.html)啟用試用或授權；不需要把 PSN 帳號交給它。官方目前提供 [7 天試用](https://www.rewasd.com/advanced-controller-mapping)，試用後繼續使用需付費。

2026-10-28 已下載 reWASD 9.5.0.11346 官方安裝檔，Windows 驗證簽章有效；尚未安裝或套用映射。已從內附範本製作實體 DS4 輸出的本機候選配置，移除不相容的 relative-stick 額外動作，保留 WASD、滑鼠右搖桿和解除鍵。JSON 參照檢查通過，尚待實際匯入、確認輸出裝置和 PS5 操作。官方本機 API 可管理群組與套用設定；詳細路徑、限制和驗收步驟見 [整合研究](research/rewasd-api/API-INTEGRATION-LIMITS.md)。

## 保留的 ViGEm 原型

這個 Windows 原型將鍵盤輸入轉成虛擬 DUALSHOCK 4，供 PS Remote Play 測試。它不是 Sony 官方軟體，也不是完整的 DualSense 模擬器。

**相容性尚未確認：** 此電腦安裝的是 PS Remote Play 9.5；ViGEm 的虛擬手把在 Remote Play 6 以後有相容性問題的回報。即使 Windows 看見手把，Remote Play 或 PS5 也可能不接受它。需要實際連上 PS5 才能確認。ViGEmBus 已停止維護。

### 舊原型的重現步驟（不作為目前建議安裝流程）

下列步驟只記錄舊原型如何運行。本機測試未通過，請勿將它當成已相容 Remote Play 9.5 的成品。

1. 在 Windows 10／11 x64 上，從下方官方連結安裝 ViGEmBus 1.22.0。它是已退役的第三方系統驅動，安裝可能需要系統管理員權限。
2. 保持 `PS鍵盤.exe` 和 `ViGEmClient.dll` 在同一個資料夾，執行 `PS鍵盤.exe`。
3. 點擊「連接虛擬手把」，再點「啟用控制」，切換至已連線 PS5 的 PS Remote Play 視窗。
4. 滑鼠控制右搖桿預設開啟，可在程式視窗調整靈敏度或關閉。此功能尚待實機測試；啟用控制並切到 Remote Play 後，游標會隱藏並限制在視窗範圍。
5. 按 F8 可暫停或繼續。切換到其他視窗、暫停或關閉程式時，手把輸入會回中並釋放游標。

如要從原始碼重建，在 Windows 雙擊 `建置程式.cmd`，使用系統內建 .NET Framework C# 編譯器。程式不會自行安裝驅動。

首次測試時，先確認程式顯示「手把已建立」，接著在 Remote Play 中測試移動、按鈕、L2／R2 與觸控板按下。若手把已建立但 Remote Play 沒有反應，不能據此認定 PS5 已支援：這可能是上述版本相容性問題。

## 舊 ViGEm 原型按鍵（非目前免手把版配置）

| PS 手把 | 鍵盤 |
| --- | --- |
| 左搖桿 | W、A、S、D |
| 右搖桿 | 滑鼠移動或方向鍵 |
| △、□、○、× | I、J、L、K |
| L1、R1、L2、R2 | Q、E、U、O |
| 方向鍵 | 數字鍵盤 8、4、2、6（開啟 Num Lock） |
| L3、R3 | 1、2 |
| Options、Share | Enter、Backspace |
| 觸控板按下、PS | T、P |
| 暫停／繼續 | F8 |

按鍵為固定配置；鍵盤方向是全幅輸入，滑鼠則依移動量與靈敏度轉換搖桿輸入。觸控板滑動、體感、震動與自適應扳機不在此原型範圍內。

## 相容性資料

- [Sony：Windows 上的 PS Remote Play](https://remoteplay.dl.playstation.net/remoteplay/lang/gb/ps5_win.html)
- [ViGEmBus 官方最終版 1.22.0](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0)；[停止維護說明](https://docs.nefarius.at/projects/ViGEm/End-of-Life/)
- [XI2DS：Remote Play 6 以後的相容性回報](https://github.com/micro-artwork/xi2ds)
