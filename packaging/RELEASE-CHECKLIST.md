# 商業版發行工作表

目前：alpha.9 四種 DualSense 候選均取得使用者透過 Sony 官方 Remote Play 操作 PS5 的成功回報；選定第二種 `dualsensegamepadv5`。alpha.10 正整合按鍵設定與每鍵連點，尚待交付及新版實機驗收。尚未開始收費或完成正式／商業 Release。

## 已決定的工作原則

- 主線為免手把 DualSense 一般版、純手把模式搭配 Sony 官方 Remote Play；PS4 與實體中繼保留歷史紀錄及備份，本階段不加入滑鼠。
- 不為開發、測試或更新中斷使用者正在玩的 Remote Play／中繼連線。
- GitHub 目前公開，作為程式碼、問題與版本的總控；尚未決定改為私有。後續準備在 `codex/commercial-preview` 分支進行。
- 使用者已要求本機執行版本只保留 alpha.10；整理時保留個人設定與備份，不移動或刪除 GitHub 既有標籤與歷史，也不覆寫舊版建置紀錄。
- 由開發代理處理工程、英文文件及上架準備；遇到付款、收款、身分與稅務資料，再請使用者本人於平台填寫。
- 不將任何金融資料、私密授權金鑰或個人裝置記錄放入公開儲存庫。
- 不宣稱首創、Sony 官方授權、全機型支援或所有遊戲通用；已存在 reWASD 等同類產品。

## 發行前必要工作

- [x] 保留已成功試用版本的 Git 標籤、歷史紀錄與使用者回報。
- [x] 建立隔離輸出的封裝腳本，使用檔案白名單、版本與原始碼雜湊。
- [x] 加入唯讀支援狀態檢查，無原始按鍵、USB 資料或帳號內容。
- [x] 區分自製元件、執行時依賴與未完成的散布授權審查。
- [x] 免手把版統一 VERSION、檔案版本、Git 標籤規則、來源／產物雜湊與不可覆寫建置；見 [版本控管](../docs/VERSIONING.md)。
- [ ] 新電腦設定精靈：偵測環境與必要模擬元件，避免依賴本機研究設定。
- [ ] 統一啟動與停止流程；正常退出、崩潰、USB 拔除及重開機後均能恢復。
- [ ] 停止／移除時只還原自己接管的裝置，保留其他軟體與裝置設定。
- [ ] 在可中斷的測試環境驗證以上還原，不用正在遊玩的連線測試。
- [x] 實作 22 動作自訂鍵、每鍵連點與離線設定測試；alpha.8 完成品牌介面、狀態燈與常用鍵位摘要。
- [ ] 補齊一般使用者錯誤引導與新電腦設定流程，降低使用者接觸技術設定的需求。
- [ ] 新版介面與自訂按鍵實機驗收：儲存後重開仍保留、失焦／暫停不黏鍵、修改設定不斷開手把。
- [ ] 每動作連點實機驗收：按住／切換、各自毫秒間隔、F8 全停、切窗停止且回來不自動恢復；舊設定升級與回退備份。
- [ ] 錄製／重播需求另行實作驗收；目前只有功能設計，不把連點宣稱為腳本錄製。
- [x] 共用設定頁九種語言自動選擇與 alpha.8 離線畫面檢視。
- [ ] 免手把主畫面完整本地化、各語言翻譯校閱與實際高 DPI 驗收；維護腳本仍以操作者工具呈現。
- [ ] 按 [支援範圍](../docs/PRODUCT-SCOPE.md) 公布分版本證據：免手把 DS4 的早期單機／重開機回報、alpha.9 四種 DualSense 候選的 PS5 操作成功回報、alpha.10 待交付及整合驗收，以及未完成的實體手把／macOS 支援。
- [ ] 第二台電腦的全新安裝／移除，以及長時間操作、睡眠恢復與版本相容性。
- [ ] 決定自製程式的授權；候選後端已核對為 GPLv3，商用包、買家再散布與各元件的授權界線仍待審查，逐項確認 notices、對應原始碼及再散布要求。尚未選定授權驗證或 DRM 方案。
- [ ] 發行簽章與安裝包校驗；費用、申請人資料須由使用者確認。
- [ ] 定價、授權範圍、退款與支援範圍，整理成中文供使用者確認。
- [ ] 選擇支援使用者所在地與收款身分的支付平台，再請本人完成帳號與金融資料。
- [ ] 測試付款、授權交付、退款與升級；最後才啟用正式銷售。

## 本次測試包的界線

目前主線使用 [research/dualsense-preview/Build-Candidate.ps1](../research/dualsense-preview/Build-Candidate.ps1)，產生獨立版本目錄、來源／產物雜湊與測試紀錄。alpha.10 使用選定的 `dualsensegamepadv5`，改鍵與連點整合仍待交付及實機驗收。候選後端固定來源與第三方材料紀錄不等於整個商用包的散布審查已完成。

建置與離線測試不安裝驅動、不建立 Windows 虛擬／匯入裝置、不啟動或停止 Sony，也不覆蓋目前運行版本。`Build-Preview.ps1` 與 `research/controller-free-preview/Build.ps1` 保留為早期實體中繼／PS4 模擬工具；維護工具不能當作一般使用者的安裝體驗。

付款平台、正式售價與自製程式授權尚未選定；這份工作表不會自動建立訂閱、購買服務、更改儲存庫可見性或授權，也不對買家再散布作出尚未審查的法律結論。

## 已核對的主要來源

- [reWASD 實體手把映射](https://help.rewasd.com/basic-functions/how-to-apply-config-to-physical-device.html)
- [GitHub Releases：有讀取權限即可存取版本](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)
- [usbipd-win 5.3.0 GPL 授權](https://github.com/dorssel/usbipd-win/blob/v5.3.0/COPYING.md)
- [VirtualBox 驅動散布說明](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Drivers/README.md)
- [usbip-win2 0.9.8.1 授權](https://github.com/vadimgrn/usbip-win2/blob/v.0.9.8.1/LICENSE.txt)
