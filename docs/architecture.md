# 架構評估

## 產品目標

使用者希望保留 Sony 官方 PS Remote Play，並以我們的 Windows 程式將鍵盤與滑鼠轉換成手把操作；滑鼠移動控制右搖桿視角。因帳號安全顧慮，使用者不希望改用第三方 Remote Play 客戶端。

## 目前原型的缺口

目前程式透過第三方 ViGEmBus 建立虛擬 DUALSHOCK 4，必須另行安裝驅動；ViGEm 已停止維護，且有 Remote Play 6 以後不相容的回報。曾在本機確認 ViGEmBus 1.22.0 能建立 Windows 辨識的虛擬 DS4。測試期間 Remote Play 9.5 兩次當機；關閉測試程式及虛擬手把後，Remote Play 可再次啟動。這些觀察提示可能有相容性問題，但不足以確認當機的根本原因。依使用者要求已移除 ViGEmBus；PS5 操作仍未驗收。

## 候選方向

1. **自製虛擬手把驅動並整合安裝流程**：保留官方 PS Remote Play，開發我們的虛擬裝置驅動和鍵鼠映射，透過單一安裝程式安裝。這仍需在 Windows 安裝驅動；系統權限與驅動簽章需求不能省略。必須先驗證官方 Remote Play 9.5 是否能辨識並使用該虛擬裝置，不能假設普通 HID 回報就能工作。
2. **我們的程式直接連接 PS5**：將影像串流及控制功能整合到自己的 Remote Play 用戶端，鍵鼠直接轉成控制資料，不需要在 Windows 建立虛擬手把。這會取代 Sony 官方 Remote Play 視窗，不符合原先「開啟官方程式」的前提，需另行確認需求。採用現成程式碼時須核對授權條件。
3. **介入官方程式的輸入流程**：可能避免系統層虛擬裝置，但依賴官方程式的內部行為，更新後容易失效，並且目前沒有證據顯示它支援本機的 9.5 版。不能當作已驗證方案。

## 驗收條件

- 一套安裝與操作流程，使用者不需另外下載第三方手把工具。
- 如選擇驅動方案，清楚說明 Windows 需要安裝及簽章，且實際在 Remote Play 9.5 與 PS5 測試通過。
- 驗證左搖桿、滑鼠視角、按鈕、扳機、觸控板按下與 PS 鍵；暫停或失焦時必須釋放輸入與游標。
- 若改用自建串流客戶端，必須明示不再使用 Sony 官方視窗，並實測連線及操作。

## 參考

- [Microsoft：Virtual HID Framework](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-)
- [Microsoft：虛擬驅動的簽章要求](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-requirements--windows-vista-and-later-)
- [XI2DS：Remote Play 6 與 ViGEm 的相容性回報](https://github.com/micro-artwork/xi2ds)
- [PS4 Keyboard and Mouse Adapter：新版 Remote Play 相容性說明](https://github.com/starshinata/PS4-Keyboard-and-Mouse-Adapter)
