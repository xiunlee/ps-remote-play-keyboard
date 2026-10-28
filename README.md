# PS Remote Play 鍵盤手把（測試原型）

> **專案狀態：尚無可用成品。** ViGEmBus 曾能在 Windows 建立虛擬手把，但測試時官方 PS Remote Play 9.5 發生當機；該驅動已移除。使用者確認實體 PS4 手把可在官方 Remote Play 操作 PS5，但鍵鼠模擬手把尚未實現。現正評估 reWASD 的實體手把輸出模式；廠商有文件支援，但尚未在此電腦實測。詳見 [架構評估](docs/architecture.md)。

## 目前候選：透過已插上的實體 PS4 手把

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

## 按鍵

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
