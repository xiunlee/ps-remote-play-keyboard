# 功能與支援範圍

更新：2026-10-29。使用者已決定維持公開儲存庫，以 **GPL-3.0-only 免費開源**，作者 **xiunlee**；所有功能免費，原收費規劃不再適用。alpha.9 四種 DualSense 候選均取得使用者透過官方 Remote Play 操作 PS5 的成功回報，選定第二種 `dualsensegamepadv5`。alpha.10 整合設定與連點；**alpha.11 已公開發布、本機交付並完成公開下載雜湊核對，本版實機仍待驗**。目前優先補齊 **alpha.12 主介面九語**，尚待最終建置、封裝、發布及交付，再進行 Windows 最終收尾與 Mac 接手。

## 目前產品方向

依使用者最新決定，**「上班不要玩」以 Windows x64 免手把 DualSense 一般版、純手把模式搭配 Sony 官方 Remote Play 為主線**，建置入口為 [Build-Candidate.ps1](../research/dualsense-preview/Build-Candidate.ps1)。已公開的 alpha.11 明示日常啟動、重連、長時間與新電腦安裝等待驗項目，不宣稱穩定版；alpha.12 也保留這些界線。使用這條路線不需另外購買或插入實體手把；仍需既有完整匹配的 usbip-win2 0.9.8.1 驅動／CLI／DLL。本包不含驅動，不自動安裝、更換、降級或重啟，亦不是免驅動或一鍵安裝的成品。

其他實體手把的中繼相容性暫緩，不列為公開 alpha 前提。已成功使用的 PS4 模擬與有線 DS4 中繼保留版本歷史與測試紀錄，不與免手把版本同時接管輸入。alpha.12 最終交付與正常切換後再核對本機只留最新版的清理；個人設定與備份保留，不移動或刪除 GitHub 既有標籤與歷史。整理授權不代表清理已完成，選定主線也不代表通用相容性已驗收。

## 本輪實作

1. **鍵盤自訂與連點**：沿用已實作的共用設定與輸入核心，alpha.10 已接回日常介面並交付，待新版實機驗收。22 個手把動作可指定單一按鍵、清除、還原預設並儲存，並各自選擇關閉連點、按住連點或按一下切換。間隔 20–5000 毫秒，預設 100。禁止重複指派，F8 保留為暫停鍵，Ctrl／Alt／Windows／Shift 不可用作遊戲映射。只在官方 Remote Play 前景接受遊戲按鍵；失焦停止全部連點且回來不自動恢復。編輯期間暫停鍵盤；儲存成功後才套用新設定，失敗保留原設定。錄製／重播與滑鼠尚未實作，詳見 [連點與驗證方式](AUTOMATION-AND-CONTROLLER-TESTS.md)。
2. **介面與狀態燈**：既有實體版的一般畫面只保留必要狀態、啟用／暫停與按鍵設定；技術設定收進進階區。其綠燈只代表實體通道確認且最近兩秒內收到資料，不表示 PS5 已連線或遊戲已接受操作。免手把主線也須清楚區分裝置接入、鍵盤啟用與 PS5 實際接受輸入，不能把裝置建立成功顯示成遊戲已連線。各狀態保留文字，不能只靠顏色辨識。
3. **系統語言**：依 `CurrentUICulture` 選擇繁體中文、簡體中文、英文、日文、韓文、德文、法文、西班牙文、巴西葡萄牙文。台灣／香港／澳門使用繁中，中國／新加坡使用簡中；葡語地區先使用巴西葡語，其他語言回退英文。**已發布 alpha.11 的設定頁九語、主畫面繁中；開發中的 alpha.12 把同一選擇機制延伸到主畫面、連線狀態、錯誤、贊助／回饋及郵件主旨**，新增 49 個文字鍵、每鍵九語。主畫面隨文字長度調整行高與按鈕換行；最終乾淨建置及九語排版仍須依本版產物核對。母語校閱、實際高 DPI 與多螢幕另驗；文件目前提供繁中／英文，不宣稱九語文件全部完成。
4. **免費、贊助與回饋**：全部功能免費，不設付費解鎖或 DRM。自願贊助文案是「覺得好用的話，歡迎贊助 US$1，幫三隻貓咪買罐頭。」使用者最後指定[PayPal 公開贊助頁](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)；只有點擊「贊助貓咪 US$1」才會開啟瀏覽器，由 PayPal 處理收款，本程式不接收或保存付款資料，不建立訂閱。唯讀 GET 已確認公開頁設定啟用、金額 USD 1.00；尚未實付驗收入帳，HTTP GET 或頁面可開啟都不等於付款成功。意見回饋 [xiun1019@gmail.com](mailto:xiun1019@gmail.com)僅由使用者點擊開啟郵件程式，預填收件人與版本主旨，不自動寄信或附加資料。

設定只保存於目前 Windows 使用者的 `%LOCALAPPDATA%\PSKeyboard\bindings.json`，不含按鍵歷史、PSN 帳密或 USB 原始資料。錯誤／不支援版本的設定回退預設並提示，不自動覆蓋原始檔案。

## 主線與備援範圍

| 類型 | 本專案目前狀態 | 尚待驗證 |
| --- | --- | --- |
| 免手把 DualSense 一般版、純手把 | 目前選定主線 `dualsensegamepadv5`；alpha.9 使用者回報官方 Remote Play 可操作 PS5；alpha.11 已公開，alpha.12 九語版準備中 | 新版語系、改鍵／連點、重開機、啟動順序、重連、長時間操作與新電腦安裝／移除 |
| alpha.9 其餘三種 DualSense／Edge 候選 | 四種候選皆有使用者 PS5 操作成功回報；其餘三種保留研究，不列入現行日常模式 | 若日後重新納入，再逐項驗收該版本；不將音訊介面存在視為音訊功能已通過 |
| 免手把 PS4 模擬 | 歷史可用路線；alpha.5 已有單機與重開機回報，alpha.6 PS4 操作亦成功 | 不把舊版回報代替新版 DualSense 的整合驗收 |
| 有線 DS4 第一代，054C:05C4 | 歷史備援；alpha.3 已有本機正常操作回報，保留既有程式與紀錄 | 若日後納入正式支援，再驗收新版自訂鍵、連點、停止與還原 |
| 有線 DS4 第二代，054C:09CC | 暫緩；中繼允許此型號，但未在本機實測 | 若重新排入計畫，再驗證真實裝置與官方 Remote Play 操作 |
| 實體 DualSense／DualSense Edge 中繼 | 暫緩；尚未實作實體中繼支援，與已取得 PS5 操作回報的免手把模擬不同 | 實體中繼不阻擋免手把首版；模擬路線見 [交接](HANDOFF.md) |
| Xbox、Switch、其他任意手把 | 暫緩；尚未支援 | 日後研究輸入轉換，不阻擋免手把首版 |

Sony 官方 Windows 用戶端列出 DualSense、DualSense Edge 與 DUALSHOCK 4，但這不表示本中繼已支援全部型號。任意手把也不能僅改一組識別碼就當作已相容。實體中繼路線保留真正 DS4 的控制與功能回覆；免手把路線使用獨立模擬裝置。早期 PS4 模擬採用 `054C:09CC`，選定的 DualSense 模擬採用 `054C:0CE6`；這些識別碼不等於對應實體型號已完成中繼測試。[Sony Windows 說明](https://remoteplay.dl.playstation.net/remoteplay/lang/en/1100001.html)

### 歷史紀錄：免手把 PS4 的首次單機結果

2026-10-29，使用者拔除實體 DS4 後，測試前未發現實體 Sony 手把，原手把的 bus 亦不存在。獨立候選使用已核對簽章與固定版本的 VIIPER v0.9.0，經既有 usbip-win2 接入自己的虛擬 `054C:09CC`。同一個官方 Remote Play 9.5 程序在測試前沒有 DS4 handle，接入後出現一個 `09CC` handle；Sony 未重啟。

使用者隨後確認「有反應，可以用鍵盤操作」，提供了官方 Remote Play 內 PS5 操作成功的直接回報。同期候選記錄鍵盤已啟用、送出 5,127 幀，其中 2,021 幀為非中立輸出，未回報失敗；這些輸出統計與 handle 只作輔助證據，不能單獨證明遊戲接受輸入。

隨後唯讀 Windows HID 觀察完成 120,001 毫秒（約兩分鐘）：`success=true`，收到 56,755 份報告、171 次狀態變化，其中 24,594 份非中立；閒置逾時為 0，裝置未變更。這項結果支持當次虛擬裝置持續輸出正常，並不代替使用者的 PS5 操作確認，也不等於長時間掛網通過。

以上是首次單台電腦、既有 Sony 連線下的成功紀錄；同日稍後使用者重開機亦確認功能正常。其他啟動順序、多次重新連線、完整按鍵清單、連點與長時間掛網或其他電腦仍待驗收。先前候選在 Windows 有輸出、PS5 無反應的失敗紀錄仍保留；兩次結果差異的原因尚未定位。完整實驗與限制見 [免手把實驗版](../research/controller-free-preview/README.md)。

## 免手把主線的下一步驗收

歷史上，alpha.5 的 DS4 模式已有一次重開機後操作確認；alpha.6 當時的 DualSense 模式在 Sony 無反應、切回 PS4 可操作，完整[驗收紀錄](../research/controller-free-preview/ALPHA6-TRIAL.md)保留不變。同日後續 alpha.9 四種新 DualSense 候選均取得使用者官方 Remote Play 操作 PS5 成功回報，並選定第二種純手把模式。這些證據分屬不同版本，不互相覆蓋。

alpha.11 公開版仍有下列待驗項目，alpha.12 九語版也必須以自己的最終產物驗證；alpha.9 操作成功及早期 DS4 重開機成功都不能代替新版驗收。

1. **重開機與啟動**：實體手把保持拔除，驗證 Windows 重開後啟動，以及 Sony／本程式不同開啟順序；不得依賴前次留下的裝置或既有 Sony 連線。
2. **重新連線與停止**：驗證 PS5 斷線後重連、候選重開、F8、切窗、退出與失敗清理；僅處理本程式建立的裝置，避免殘留按鍵或重複裝置。
3. **長時間使用**：完成全部按鍵、自訂鍵、連點與長時間掛網測試，確認停止可靠，並記錄遊戲、網路與取樣限制。
4. **乾淨安裝與移除**：在另一台 Windows 電腦驗證相依元件、簽章、首次設定、必要重啟、更新與移除，不沿用目前電腦的研究環境。

alpha.11 已完成執行包、完整對應原始碼、第三方 notices、版本／雜湊、27 項公開包稽核與四附件公開下載核對，見[發布紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)；三個 runtime 二進位均為 NotSigned。alpha.12 須重新核對自己的最終產物，不沿用前版驗證作交付證據。自製來源採 [GPL-3.0-only](../LICENSE)，第三方保留各自授權，見 [COPYRIGHT.md](../COPYRIGHT.md)；同一 Release 提供固定上游、實際依賴來源與重建說明。贊助實付不作為免費功能發布前提，不安排商售或 DRM。詳見[發行工作表](../packaging/RELEASE-CHECKLIST.md)。

## Mac

**目前不支援，不能直接把 Windows 程式改成 Mac 安裝包。** Sony 有官方 Mac Remote Play，也列出 DS4、DualSense 與 DualSense Edge。[Sony Mac 說明](https://remoteplay.dl.playstation.net/remoteplay/lang/en/1100002.html)

本專案使用 Windows Forms／.NET Framework、Windows 鍵盤攔截與 Windows USB/IP 驅動，這些平台部分不能直接在 macOS 運作。[Microsoft Windows Forms 說明](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/)；[usbip-win2 專案](https://github.com/vadimgrn/usbip-win2)

Mac 暫不列為免手把首版前提。未來研究應先在實體 Mac 上確認可建立相容的手把輸出，並讓 Sony 官方程式接受輸入；通過後再移植鍵盤擷取與介面。按鍵設定概念與部分輸出邏輯可以沿用，但現有 Windows 虛擬 USB 後端不可直接沿用。若需 DriverKit，還要核對 Apple 的驅動權限與發行要求；這是工程評估，尚不是可行性實證。[Apple DriverKit 權限說明](https://developer.apple.com/documentation/driverkit/requesting-entitlements-for-driverkit-development)

目前先完成 **Windows alpha.12 九語介面與最終交付**，再依使用者安排到實體 Mac 接手。已準備 [Mac 交接文件](MAC-HANDOFF.md)，尚未進行 Mac 開發或建立相容性證據；Windows 最終收尾與本機清理也尚不能宣告完成。已知可用的實體 DS4 中繼保留歷史備援，不將其他手把或 macOS 列為公開 alpha 的既有功能。
