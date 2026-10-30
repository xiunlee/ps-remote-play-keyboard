# Mac Codex 協作說明：名稱與圖示調整

日期：2026-10-30。這是使用者授權的品牌與文字調整。**現況修正：Mac alpha.1 已取得遊戲、W 放開停止與 Enter／Options 成功回報；0.1.3 建置 9 已完成完整 App 中立連接、停止、重連與退出清理，但未完成本版遊戲、新機、睡眠／崩潰驗證。** 品牌調整不擴大這些證據。先確認最新遠端分支與本機未提交工作，再閱讀 [Mac 接手交接](MAC-HANDOFF.md)和 [Mac 最新研究](../research/macos-native/README.md)；不要以舊 `main` 覆蓋 `codex/commercial-preview`，也不要覆寫另一台電腦的提交。

## 使用者這次希望改什麼

1. 中文名稱維持 **「上班不要玩」**；非中文語系改用 **Three Cats Keybridge**，不以 PS 或 Sony 名稱作為產品品牌。
2. 使用新設計的貓咪與三個按鍵圖示：[圖示 PNG](../assets/branding/three-cats-mark.png)、[Windows ICO](../assets/branding/three-cats.ico)。圖形由專案的 [Build-ThreeCatsMark.ps1](../assets/branding/Build-ThreeCatsMark.ps1)以幾何圖形產生；Mac 可參考同一圖形或為 Mac 格式另行匯出，不能假定 Windows ICO 是 Mac App icon 成品。
3. 應用程式內使用「模擬手把」／「Virtual controller」或各語言的自然翻譯；不要將它描述成 Sony 實體手把，也不要在產品標題或圖示上使用 Sony 手把型號或 PlayStation 標誌。內部協定識別與實測記錄應維持真實，不為改名而偽造、改寫或刪除證據。
4. 對外簡介採中性描述：鍵盤控制與模擬手把工具，適用特定相容的遠端遊玩環境；**不能宣稱適用所有遊戲或所有程式**。實際使用手冊需保留足以讓使用者辨識支援環境及必要設定的資訊，避免暗示已支援未驗證的客戶端或平台。
5. 中英文說明清楚標示「獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係」；其他已提供語系的介面也應自然地顯示相同事實。不把這項說明、換名或換圖示宣稱為法律保證或 Sony 授權。
6. 發行頁沿用[中英文固定說明範本](RELEASE-NOTES-TEMPLATE.md)，未來只按該版實際內容更新「此版本更新」及日期，功能／需求變更時同步更正固定文字；不用提所用的工具或設計標準。

## 平台與協作邊界

- Windows **v0.1.3 已於 2026-10-30T09:15:32Z（台北 17:15:32）公開發布**，固定來源 `8f1918e48bfadc1b8d7120b1991b497d060fc764`，名稱為 Three Cats Keybridge；[發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.3)已核對。共享分支的後續文件來源為 `6007809`。Mac 保留已審閱的 `71aca9c` 基底與獨立發行分支，不將新 Windows 程式碼合入已凍結的 Mac 建置，也不移動既有標籤。
- Mac 的 Generic 遊戲無反應是已停止的歷史路線；後續 alpha.1 的 [trial 2](../research/macos-native/DUALSENSE-TRIAL-2.json) 已由使用者確認成功。0.1.3 原生整合與中立連線／清理結果見 [發布準備清單](MAC-RELEASE-READINESS.md)。不得把 alpha.1 或 Windows 的結果及本次改名當成 0.1.3 遊戲驗收。
- Mac 本次公開標籤使用 **`mac-v0.1.3`**，已發布的 Windows `v0.1.3` 保持不變。公開附件使用 `ThreeCatsKeybridge-Mac-0.1.3-…`，既有 App 路徑／bundle identifier／簽章身分不改。Windows 最新及上一版的公開保留政策不變；Mac 首次發布不歸檔任何 Windows Release。
- 不自行關閉、重啟或更新 Sony Remote Play，不降級或更換驅動，不導入付費依賴或第三方 Remote Play 用戶端。輸入與連點只在受支援的官方前景視窗作用，切窗停止；Mac 使用 **Esc** 啟用／暫停，Windows 保留 F8；既有個人設定與備份不可覆寫。
- 本次不要求啟動虛擬裝置、捕捉真實按鍵、重啟服務、登入帳號或測試遊戲。先做不接觸裝置的文字、資源與離線畫面檢查；與既有 Mac 的中立連線成功及尚未完成的遊戲／新機驗證分開記錄。使用者另已授權依已揭露限制公開 Mac 版本，不因本次品牌調整重新要求發布許可。
- 先確認使用者的 Mac Codex 工作樹是否有未提交變更；不要強制推送、重寫歷史、刪除舊標籤，或用 Windows 檔案覆蓋 Mac 原生程式。完成後回報修改檔案、已測內容、未驗限制與來源提交。
