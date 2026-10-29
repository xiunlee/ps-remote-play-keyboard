# 上班不要玩：DualSense 四方案測試版

此版本供本機相容性試驗。四種方案已在獨立 Windows Server 測試機，以相同 **usbip-win2 0.9.8.1** 通過按鍵讀回與清理；**尚未確認官方 Remote Play 可以操作 PS5**。

## 怎麼試

1. 保持 Sony Remote Play 開啟。若 alpha.8 也開著，先正常退出「上班不要玩」alpha.8；兩版不能同時接管 F8。
2. 開啟 `PSKeyboard-DualSensePreview.exe`。它一開始不接入手把，鍵盤控制也先暫停。
3. 從下拉選單選一個方案，再按連接。切回官方 Remote Play，按 F8 啟用後試操作。
4. 先試十字方向與確認鍵。實際鍵位沿用你的個人設定；若未更動 alpha.7／8 的預設，十字上下是 **Home／End**、叉鍵是 **↓**，不是按鍵盤上下鍵來移動選單。
5. 換方案前按 F8 暫停，回測試視窗正常中斷，等移除完成後再選下一個。若顯示移除未確認，停止切換並回報該訊息，不重開 Remote Play、不改驅動。
6. 回退時正常退出測試版，開原本保留的 **alpha.8** 捷徑使用 PS4 模式。

| 選項 | 差異 |
| --- | --- |
| 一般 DualSense，含音訊介面 | 模擬一般手把及其音訊介面 |
| 一般 DualSense，僅手把 | 不建立音訊介面 |
| DualSense Edge，含音訊介面 | 模擬 Edge 身份及其音訊介面 |
| DualSense Edge，僅手把 | 模擬 Edge 身份，不建立音訊介面 |

請回報「哪個方案、是否能操作 PS5」。畫面顯示手把已接入，只代表 Windows 已接入，不能當成遊戲操作成功。

## 保留與限制

- 不安裝、更換或降級驅動，不自行停止、重啟或更新 Sony Remote Play，不要求 PSN 帳密。
- 個人鍵位與連點設定只讀；這個測試版不改寫既有設定。需要改鍵時先退出測試版，再使用原版設定頁。
- 鍵盤與連點只在官方 Remote Play 前景生效；切窗清除連點，F8 暫停。連線或輸出失敗即停用控制。
- 每次只接一個本程式建立的裝置；只移除自己核對過的 bus／port。使用獨立 loopback `33641/33642`，不使用 alpha.8 的通訊埠。
- 每次候選後端連線最多 30 分鐘，屬短期相容性測試；尚未驗收長時間、音訊、觸覺、自適應扳機或 Edge 額外背鍵。
- 保留 alpha.8、舊版本與標籤。本包不是正式公開 Release，也不啟用付費服務；第三方元件未完成正式再散布審查。

## 建置者

先讀 `../dualsense-driver-test/RESULTS-20261029.md`。`backend/Build-Backend.ps1` 以固定候選來源建置受限後端；`Build-Candidate.ps1` 從根目錄 `VERSION` 產生版本與雜湊，使用全新目錄，嵌入後端 SHA-256，並執行離線傳輸與保護測試。交付建置必須 `-RunTests -RequireClean`，再由 `Verify-Candidate.ps1` 唯讀核對。

不要啟用或偽造 hosted runner 環境來執行上一階段的測試 fixture；玩家後端是本目錄另一個明確入口，保留原始 hosted 限制。固定上游來源為 `hbashton/VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2`，來源與授權檔保留在本機開發資料中；正式散布前仍須另行檢查所有依賴與授權。
