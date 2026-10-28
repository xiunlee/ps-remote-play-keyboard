# PS Remote Play 鍵盤手把（測試原型）

> **專案狀態：尚無可用成品。** ViGEmBus 曾能在 Windows 建立虛擬手把，但測試時官方 PS Remote Play 9.5 發生當機；該驅動已移除。使用者確認實體 PS4 手把可在官方 Remote Play 操作 PS5，但鍵鼠模擬手把尚未實現。這次未找到能驗證完整需求的實作方式。詳見 [架構評估](docs/architecture.md)。

這個 Windows 原型將鍵盤輸入轉成虛擬 DUALSHOCK 4，供 PS Remote Play 測試。它不是 Sony 官方軟體，也不是完整的 DualSense 模擬器。

**相容性尚未確認：** 此電腦安裝的是 PS Remote Play 9.5；ViGEm 的虛擬手把在 Remote Play 6 以後有相容性問題的回報。即使 Windows 看見手把，Remote Play 或 PS5 也可能不接受它。需要實際連上 PS5 才能確認。ViGEmBus 已停止維護。

## 開始使用

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
