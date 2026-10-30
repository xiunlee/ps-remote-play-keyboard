# Mac 安裝、使用與更新

適用：**上班不要玩／Three Cats Keybridge Mac 0.1.3**。更新：2026-10-30（Asia/Taipei）。

「上班不要玩」（非中文名稱 Three Cats Keybridge）是鍵盤控制與模擬手把工具，目前支援下列 Sony 官方遠端遊玩環境，不宣稱適用所有遊戲或程式。

本程式是獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係。

這一版將按鍵設定、連點、九語介面與手把元件準備整合到同一個 App，並完成美術優化。**原生輔助程式及完整 App 已通過中立連接、中斷、再次連接與退出清理，淺／深色主畫面及設定已查看；本版遊戲、全新 Mac／新使用者帳號安裝、睡眠及崩潰情境仍未驗證。** 本版以這些已知限制提供，公開交付不代表所有系統與遊戲均相容。下載狀態、附件及發布時間以 [Mac 0.1.3 發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/mac-v0.1.3)為準。以下已完成的介面與中立連線證據屬建置 9；後續名稱與貓咪圖示調整的核對另記，不推算為新的遊戲結果。

先前 alpha.1 已有使用者確認遊戲操作、W 放開停止與 Enter／Options 有效；0.1.2 已完成本機介面更新及該次輔助使用權限保留。這些結果不代替 0.1.3 整合安裝與遊戲驗收。

## 使用前準備

- Apple Silicon Mac，macOS 15 或更新版本。Intel Mac 目前不在支援範圍。
- 另行安裝並設定 **Sony 官方 PS Remote Play**，先確認能正常連線至自己的 PS5；本工具不代替 Sony 的登入或串流程式。
- 首次取得必要元件時需要網路。你不需要安裝 Python、Go、Swift、Docker 或 Parallels，也不需要 Apple 開發者帳號。

App 已包含原生輔助程式、精簡的無磁碟 Alpine 執行環境與手把後端，使用 macOS 內建虛擬化功能。首次連接時會從 VirtualHere 官方取得未修改的 **Mac Client 6.0.3** 與 **Generic ARM64 Server 4.8.8**，核對固定版本、雜湊與適用簽章後保存在本程式資料夾。

這條免費路線只使用一個 USB 裝置；VirtualHere Client 以一般 App 執行，不安裝為需要付費 server 的 service。VirtualHere 保留自己的授權，免費使用不代表它是開源軟體。[Server 免費條件](https://www.virtualhere.com/embedded_server_license)、[Client 授權](https://www.virtualhere.com/node/316)、[service 條件](https://www.virtualhere.com/client_service)。

## 安裝與第一次開啟

1. 若已有舊版，先按「中斷」，等待停止與清理完成，再正常結束 App。保留 Sony Remote Play 原本的連線。
2. 取得此版本提供的完整 App；若收到 ZIP，先完整解壓縮；若收到 DMG，先打開磁碟映像。
3. 在 Finder 按 **Shift＋⌘＋G**，輸入 `~/Applications`。將 `PSKeyboardMacDualSense.app` 放在這個資料夾；資料夾不存在時可在自己的使用者資料夾建立 `Applications`。
4. 後續都開啟這一份 App，不從 DMG、Downloads 或不同名稱的副本執行。更新也替換同一路徑，不為每個版本另裝一個 App。

固定位置為 `~/Applications/PSKeyboardMacDualSense.app`，程式身分為 `com.xiunlee.pskeyboard.mac-dualsense`。畫面名稱是「上班不要玩」，非中文語系顯示「Three Cats Keybridge」。檔名、程式身分及資料路徑沿用原值，以保留設定與既有權限，不因顯示名稱更新另裝一份 App。

本 App 使用專案自簽憑證，**不是 Apple Developer ID 簽章，也未經 Apple 公證**。如果 macOS 阻擋開啟，且你確認來源與附件完整性，可以在嘗試開啟後，前往「系統設定 → 隱私權與安全性」，於系統有提供時按「仍要打開」，再由本人確認。這是 macOS 對該 App 的正常開啟流程；不同系統或受管理的 Mac 不一定提供相同選項。[Apple 官方說明](https://support.apple.com/en-us/102445)。

不需要安裝作者的私有憑證、匯入作者鑰匙圈或執行簽章設定腳本。若系統仍無法開啟，請保留提示文字回報；不要關閉 SIP、刪除 quarantine、修改 TCC 資料庫或降低全域安全設定。

## 權限與連接

1. 開啟 App，按「開啟輔助使用設定」，在 macOS 的「輔助使用」清單允許這一份 App。若未出現在清單，可按「＋」並選取上述固定位置的 `PSKeyboardMacDualSense.app`。
2. 系統驗證由你在 macOS 視窗完成。回到 App，確認狀態顯示「輔助使用：已允許」。不需要安裝或授權 ChatGPT／Computer Use。
3. 保持 Sony Remote Play 開啟，按 **「連接並啟用控制」**。第一次可能需要下載元件，請等待準備、建立手把與連接階段完成；可按「中斷」取消。
4. 手把接入後切回 Remote Play 操作。不要在「正在準備」時把沒有反應當成連接完成。
5. 結束時按「中斷」，等待清理完成；要再玩時可重新連接。正常結束 App 也會先停止自己的輸出並清理自有程序。

「元件與授權」可查看使用元件與官方授權頁。元件下載或驗證失敗時，App 會停止該次準備並顯示錯誤；重試會重新檢查。已安裝的自有元件若損壞，會先驗證替換檔，再替換原件；不會改動你另行安裝的其他 VirtualHere。

**本版已在本機實際完成 App 連接、中斷、再次連接及退出；每次停止後都確認自有手把與程序已移除。這不等於本版遊戲或全新 Mac 安裝驗收。** 出現「清理未確認」時，請保留錯誤與版本回報，不反覆接入或刪除狀態資料來解除限制。

## 鍵盤與連點

預設不需要 Fn 或功能鍵。主畫面顯示的是目前設定；自訂鍵位後請以畫面為準。

| PS5 動作 | Mac 預設鍵 |
| --- | --- |
| 左搖桿上／下／左／右 | W／S／A／D |
| 十字鍵上／下／左／右 | ↑／↓／←／→ |
| △／□／×／○ | I／J／K／L |
| L1／R1 | Q／E |
| L2／R2 | Z／C |
| Create／Options | Tab／Return（Enter） |
| L3／R3 | F／空白鍵 |
| PS／觸控板按下 | P／T |
| 啟用／暫停 | Esc，固定保留 |

**I 在上、J 在左、K 在下、L 在右，對應手把四個面鍵的菱形位置。** 22 個遊戲動作可改成單鍵、清除或還原；Esc 保留為控制切換鍵，不能綁成遊戲動作。

在「按鍵與連點設定」中，可為每個動作選擇關閉、按住連點或按一下切換連點，間隔為 **20–5000 毫秒**，預設關閉、100 毫秒。過短脈衝不一定會被每款遊戲接收。只有按「儲存設定」並成功寫入才套用；取消、關閉視窗或儲存失敗會保留原設定。進入設定會暫停控制，離開後也保持暫停。

鍵盤只在官方 Remote Play 位於前景時作用。切換到其他視窗，或按住 Command／Control／Option／Shift，會清除按住狀態與連點；切回後重新按鍵，不會恢復先前的切換連點。**Esc 在其他 App 中保留原用途**，也可在本程式按啟用／暫停按鈕。

介面跟隨 macOS 偏好語言，支援繁中、簡中、英文、日文、韓文、德文、法文、西班牙文與巴西葡語；未支援語言回退英文。更多鍵位說明見 [Mac 預設鍵位](MAC-DEFAULT-KEYS.md)。本版定位為 2D／方向與選單操作，沒有滑鼠視角、右搖桿方向或觸控滑動。

美術優化後可在 App 選單的「外觀」選擇跟隨系統、淺色或深色；只影響本程式，不更動 macOS 外觀。本機已查看淺／深色主畫面及設定；其他語系布局與顯示環境仍待持續核對。

## 更新與資料保留

更新前先中斷、等待清理並結束 App，再用新版替換 `~/Applications/PSKeyboardMacDualSense.app`。不要在程式或它的自有元件仍執行時搬移、刪除或覆蓋。重新開啟後確認版本、鍵位與輔助使用狀態；固定身分更新以減少重複授權，但不保證每次 macOS 都免確認。

資料位於 `~/Library/Application Support/PSKeyboard`，更新 App 時保留整個資料夾：

| 名稱 | 用途 |
| --- | --- |
| `mac-bindings.json` | Mac 鍵位與每鍵連點設定，不與 Windows 設定混用。 |
| `mac-bindings.json.backup` | 上次儲存前的設定內容；不是多版本備份管理器。 |
| `RuntimeState` | 此次安裝的固定手把環境識別、元件設定與必要清理記錄；更新必須保留。 |
| `Dependencies` | 本程式取得的官方 VirtualHere 原件；已通過核對的元件不需每次重下。 |
| `Diagnostics` | 本機彙總診斷，不是原始按鍵錄製。 |

設定無法讀取時，App 會提示並暫用預設值，原檔保留不變。請先保留主檔與備份再處理；不要直接拿 Windows 的 `bindings.json` 覆蓋。`RuntimeState` 也不應為了解決連接問題或重新取得免費使用而刪除。

## 解除安裝

1. 按「中斷」，等待清理完成，再正常結束 App。若清理未確認，先保留狀態資料與錯誤，待處理後再移除。
2. 在 Finder 將 `~/Applications/PSKeyboardMacDualSense.app` 移到垃圾桶。
3. 若只是暫時移除或之後可能重裝，可保留 `~/Library/Application Support/PSKeyboard`。若確定完全移除，可在備份需要的鍵位後刪除本程式資料夾；想保留設定時，至少保留 `mac-bindings.json` 與 `.backup`，重裝則建議一併保存 `RuntimeState` 的固定識別。
4. 可在 macOS「輔助使用」清單移除本程式的授權項目。

只清理上述本程式持有的 App 與資料；不要刪除 Sony Remote Play、其他位置的 VirtualHere 或其他程式的裝置／服務。本程式不要求解除安裝全域驅動，也不會替你關閉 Sony。

## 回饋、贊助與對應來源

全部功能免費。回饋按鈕只開啟郵件草稿，填入 [xiun1019@gmail.com](mailto:xiun1019@gmail.com) 與版本主旨，不自動寄出或附加個人設定／診斷。回報時提供版本、錯誤文字與重現步驟即可。

[PayPal 自願贊助 US$1](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW) 用於幫三隻貓咪買罐頭；僅點擊才開啟瀏覽器，沒有付費解鎖或訂閱，程式不處理付款資料。

原創程式採 GPL-3.0-only，第三方元件保留各自授權。[專案原始碼入口](https://github.com/xiunlee/ps-remote-play-keyboard/tree/mac-v0.1.3)與 [GitHub 發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases)可查看實際已交付項目；請選取標示 Mac／arm64 的附件，Windows 程式包不能在 macOS 使用。

Mac 0.1.3 的安裝附件為 `ThreeCatsKeybridge-Mac-0.1.3-arm64.dmg` 或 `ThreeCatsKeybridge-Mac-0.1.3-arm64.zip`；兩者包含同一份 App，擇一下載即可。另附 SHA-256 清單、App 對應來源包、runtime 對應來源包及選配的 `ThreeCatsKeybridge-Mac-0.1.3-build-inputs.tar.gz`。來源包命名為 `ThreeCatsKeybridge-Mac-<版本>-source.zip` 及 `ThreeCatsKeybridge-Mac-<版本>-runtime-sources.tar.gz`，**實際檔名與可用附件以該次發行清單為準**。runtime 來源包含實際 guest／後端使用的版本、修改、授權、建置與安裝材料；VirtualHere 專有二進位由其官方來源另行取得。使用者安裝不需下載來源包或自行編譯。

本專案亦與 VirtualHere 或 PayPal 無隸屬或背書關係。完整準備與驗收進度見 [Mac 發佈準備清單](MAC-RELEASE-READINESS.md)及 [功能對齊清單](MAC-PARITY-CHECKLIST.md)。
