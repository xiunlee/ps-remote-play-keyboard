# 上班不要玩：總進度與換串交接

更新日期：2026-10-29（Asia/Taipei）。這份文件是下一串的入口，將已實測、離線驗證與尚未完成的工作分開記錄。閱讀後可直接繼續已授權的開發與隔離研究，不必重新詢問已決定的產品方向。

## 1. 目前結論與接續目標

- 產品主線是 **Windows、免實體手把、鍵盤映射、Sony 官方 PS Remote Play**。名稱是「上班不要玩」。
- **模擬 PS4 DUALSHOCK 4 已能控制 PS5**：alpha.5 有拔除實體手把與一次 Windows 重開機後的使用者成功回報；alpha.6 PS4 模式亦由使用者確認正常。不是只能操控 PS4 主機。
- **alpha.9 DualSense 已有官方 Remote Play 控制 PS5 的使用者成功回報**：使用者表示「四種都可以 保留第二種就好了」，指定一般 DualSense／純手把 `dualsensegamepadv5`。見 [alpha.9 實機補記](../research/dualsense-preview/ALPHA9-TRIAL.md)。這不是由 Windows 辨識或 CI 推論，亦不代表逐鍵、重開機、長時間或音訊／觸覺全部驗收。
- alpha.6 原版 DualSense 在 Windows 可建立裝置但 Sony 無反應，屬於**歷史失敗**；切回同版 PS4 可操作。alpha.9 成功仍不足以定位原版失敗的唯一根因。alpha.8 是先前 PS4 品牌介面版，完成白色幾何 PS 圖示與 `#D62355` 配色。
- 使用者接續要求「把我們的按鍵設定 連點 那些功能都放進來吧，就用這版本」。目前開發版本 **0.1.0-alpha.10** 固定第二種模式、恢復完整按鍵／連點設定並取消 alpha.9 的 30 分鐘自動結束限制；**本次文件更新時尚未完成正式建置交付，也未取得 alpha.10 實機驗收**。下一步是交付後驗收設定、連點及停止行為，不再重跑四方案選型。
- 使用者因空間有限，進一步授權「舊版本在本地端都清除吧，就留下最新版本」。此指示取代先前本機保留多版本的規則：待 alpha.10 建置交付後清理舊本地檔；執行中的 alpha.9 先正常退出，個人設定／備份、GitHub 歷史標籤、研究與 PS4 原始碼仍保留。**本段記錄授權，尚不代表清理完成**。
- **隔離研究已完成兩階段**：[V5 使用者空間驗證](../research/dualsense-v5-test/RESULTS-20261029.md) 通過並修正觸控 inactive 差異；後續[四方案的 0.9.8.1 hosted Windows HID 驗證](../research/dualsense-driver-test/RESULTS-20261029.md) 亦全部通過。原候選正式 EXE 的啟動限制與舊 native ABI 未改，候選程式使用另寫的受限入口及匹配 CLI。**Windows Server HID 成功不等於本機 Windows 11／Sony 操作成功。**
- 使用者進一步明確要求：**沿用目前 0.9.8.1，不降級，不影響能用的 PS4 與 Remote Play**。新增 [獨立 Windows 驅動研究](../research/dualsense-driver-test/README.md)，把接入測試安排在一次性的 GitHub hosted Windows VM；不得把該安裝／接入流程搬到使用者電腦執行。

## 2. GitHub、版本與哪些東西沒有公開

儲存庫：<https://github.com/xiunlee/ps-remote-play-keyboard>（公開）。目前工作分支是 **`codex/commercial-preview`**。GitHub 預設分支仍是 `main`；閱讀或接手務必指定工作分支，不要誤讀 `main` 的舊內容，也不要自行合併或切換預設分支。

| 基準 | 原始碼提交 | 證據／用途 |
| --- | --- | --- |
| `v0.1.0-alpha.5` | `4e4b8a3` | 首次免手把成功；事後命名，原執行檔未內嵌版本號 |
| `v0.1.0-alpha.6` | `15fc0d35ae93737317684fc33920f6f8c7dc5a1c` | 加入版本控管與 DualSense 實驗；PS4 通過、PS5 操作未通過 |
| `v0.1.0-alpha.7` | `aaef2da98e6c1e8844a0561124f26480e3f68b81` | 人體工學預設鍵位、保留使用者設定 |
| `v0.1.0-alpha.8` | `a6c5c2a99ed32709287000319ec0f44f95254847` | 圖示與 UI；後端及控制規則未改 |
| `v0.1.0-alpha.9` | `18c370cb528bbc2107827bebb7799bdb095e133b` | 四方案 hosted HID 通過；使用者回報四種均能操作 Sony／PS5，指定第二種 |
| `0.1.0-alpha.10`（開發中） | 尚待乾淨建置與交付紀錄補入 | 固定純手把一般版、整合改鍵／連點、移除 30 分鐘限制；未宣稱實機通過 |

alpha.8 建置／封存紀錄與最終畫面另於 `cf51de1` 提交；本交接文件再作文件提交。**文件提交超前版本標籤是正常的，不要移動舊標籤來追齊分支。**

GitHub 保存：自製原始碼、研究與實機紀錄、設計素材／預覽、版本標籤、建置清單與檔案 SHA-256。本機清舊授權不包含刪除這些歷史。沒有公開第三方執行檔或本機測試 ZIP，也沒有建立正式二進位 Release、啟用收費或支付平台。自製程式的正式授權與第三方再散布審查仍未完成；使用者另詢問 GitHub 與販售後散布問題，尚在獨立研究，不能據此自行更改授權或儲存庫可見性。

## 3. 不可遺失的操作規則

1. **不得自行關閉、重啟或更新 Sony Remote Play**。使用者反覆強調連線不容易；早先安裝／測試的同意不是永久中斷連線的許可。若新步驟真的需要中斷當前使用，先做好可檢視的候選並說明具體原因，再協調測試時段。
2. 依最新授權，**alpha.10 建置交付後，本機只留下最新版**；清理前逐項核對路徑與程序占用，執行中的 alpha.9 待正常退出，不刪執行中的後端。保留個人設定及其備份、GitHub 歷史標籤、研究與 PS4 原始碼，不同時啟動兩版爭用 F8 或通訊埠。此授權不包含停止 Sony、驅動或其他軟體。
3. 不重新安裝 ViGEmBus、不依賴 reWASD 等付費軟體、不改用第三方 Remote Play 用戶端、不索取 PSN 密碼。**目前要求保留本機 0.9.8.1，不更換、不降級、不重開機**；候選要求舊版是其第三方程式的限制，不是 PS5 或 Sony 要求使用較舊驅動。
4. **只在官方 Remote Play 前景接受遊戲按鍵**。切到其他視窗停止並清除全部連點，切回不自動恢復；F8 可啟用／暫停。使用者最終選擇了這個規則，背景掛網不是目前規格。
5. 現有自訂鍵與連點不可自動覆寫。「恢復預設」是使用者主動動作，且會將連點改為關閉、間隔 100 毫秒。
6. 不注入 Sony 程序、不讀取其記憶體、不保存帳號資料或原始按鍵歷史。既有探針僅讀取裝置 handle 中繼資料；查到 handle 不等於遊戲接受輸入。
7. 清理僅針對當下核對過、由本程式建立的裝置／完整本機 URL，不用全裝置 detach，不停止其他軟體的裝置或服務。
8. 使用繁體中文、盡量直接完成工作，不要反覆詢問已確認的偏好。需要收款、身分、付款、稅務資料時再由使用者本人填寫。功能先完善，正式發布與銷售尚未開始。

## 4. 功能完成度

| 項目 | 現況 | 還缺什麼 |
| --- | --- | --- |
| 免手把 DS4 → PS5 | alpha.5／alpha.6 使用者實機通過，一次重開機成功 | 多種啟動順序、反覆重連、全按鍵、長時間及第二台電腦 |
| 免手把 DualSense | alpha.9 四方案 hosted HID／清理通過，使用者回報四種 Sony／PS5 操作成功；alpha.10 固定第二種 | alpha.10 新版實機回歸、重連、重開機與長時間；音訊／觸覺另驗 |
| 22 動作自訂鍵 | alpha.10 接回共用設定頁；僅使用者儲存成功才套用，開啟／取消不覆寫 | 正式建置交付及新版儲存／載入完整實機回歸 |
| 每鍵連點 | alpha.10 接回關閉／按住／按一下啟停；20–5000 毫秒；預設 100 | 新版逐項操作、切窗／F8／編輯／斷線停止與長時間可靠性 |
| UI／圖示 | alpha.8 已交付；主色 `#D62355`、白色圖示、狀態燈 | 使用者視覺確認、150%／200% DPI、多螢幕 |
| 多語言 | 共用設定頁九語、系統語言選擇及九語離線畫面檢視 | **免手把主畫面仍大量繁中文字，尚未完整本地化**；母語校閱 |
| 錄製／重播 | 僅有設計，尚未實作 | 動作序列、計時、儲存、單次／循環播放、停止測試 |
| 滑鼠控制右搖桿 | 本主線未實作，依使用者要求暫緩 | 日後另排，不從舊 ViGEm 原型推論已支援 |
| 其他實體手把 | 暫緩；舊有線第一代 DS4 中繼保留備援 | 不作首版必要條件；任意手把尚未支援 |
| macOS | 不支援 | 須實體 Mac 先驗證輸出，再研究平台移植 |
| 安裝與商業化 | 版本封存、雜湊、發行工作表已有 | 乾淨安裝／移除、授權、簽章、支付、退款及發行驗收 |

設定頁九語：繁中、簡中、英文、日文、韓文、德文、法文、西班牙文、巴西葡語。不要將設定頁完成說成整個免手把程式已九語。

連點間隔指一次按下開始到下次開始；每週期約半數時間按下、半數放開。USB／遊戲取樣可能忽略很短脈衝，不能保證設定 20 毫秒就被所有遊戲逐次接受。

## 5. 鍵位與使用者設定

alpha.7 起的新建或「恢復預設」排列如下，alpha.10 沿用；已有格式 1／2 設定保持原樣。

| 手把動作 | 鍵盤 |
| --- | --- |
| 左搖桿上下左右 | W / S / A / D |
| △ / □ / × / ○ | ↑ / ← / ↓ / → |
| L1 / L2 | E / Q |
| R1 / R2 | Delete / Page Down |
| 十字鍵上／下／左／右 | Home / End / Insert / Page Up |
| L3 / R3 按下 | F / 空白鍵 |
| Create（Share）/ Options | Tab / Enter |
| 觸控板按下 / PS | R / F1 |
| 控制啟用／暫停 | F8（保留，不能改成遊戲動作） |

**最新版測方向選單不能繼續一律叫使用者按鍵盤上下鍵**：新預設方向鍵是面板四鍵，十字方向上／下是 Home／End；先核對其實際設定。R3 在此只代表按下，沒有右搖桿視角移動。

設定檔：`%LOCALAPPDATA%\PSKeyboard\bindings.json`。不提交或隨舊程式清除此個人檔案。格式 1 載入後保留鍵位、連點預設關閉；儲存才寫格式 2。alpha.5–alpha.8 使用格式 2，alpha.9 只讀，alpha.10 恢復由使用者主動儲存格式 2；不能推論更舊的程式均可讀取。既有 `profile-backups` 同樣保留。

## 6. 現行架構與程式入口

目前資料路徑：鍵盤擷取 → 前景／F8 狀態 → BindingProfile／RapidFireEngine／KeyboardControlPipeline → 33-byte 手把內容轉 V5 framed 協定 → 受限 hbashton 後端 → 既有成套 usbip-win2 0.9.8.1 CLI／DLL／驅動 → Windows HID → Sony 官方 Remote Play → PS5。

| 路徑 | 用途 |
| --- | --- |
| `research/dualsense-preview/DualSensePreview.cs` | **現行 DualSense 主程式**，alpha.10 固定一般版純手把，接回按鍵／連點編輯 |
| `research/dualsense-preview/CandidateTransport.cs` | 單一 persona 的 V5 傳輸、回饋與自有 bus 清理 |
| `research/dualsense-preview/backend/` | 固定 hbashton 來源的受限玩家後端與獨立建置；不走舊 native attach |
| `research/dualsense-preview/Build-Candidate.ps1`、`Verify-Candidate.ps1` | 現行建置與唯讀完整性驗證；非安裝／啟動工具 |
| `research/controller-free-preview/ControllerFreeKeyboard.cs`、`Build.ps1` | alpha.8 PS4／舊 DualSense 歷史路線，保留來源供重建及回退研究 |
| `src/physical-keyboard/BindingProfile.cs`、`RapidFireEngine.cs`、`KeyboardControlPipeline.cs` | 主線沿用的設定／連點／按鍵核心；目錄名不代表只供實體中繼 |
| `src/physical-keyboard/BindingEditor.cs`、`KeyboardUi.cs`、`UiText.cs` | 設定頁、品牌樣式與共用翻譯 |
| `src/keyboard-pad/ViiperTransport.cs`、`KeyboardInputState.cs`、`KeyboardMapping.cs` | 沿用鍵盤狀態與舊格式報告轉換；現行連線由 CandidateTransport 負責 |
| `research/native-merge/keyboard_merge.c`、`.h` | 22 動作的原生報告處理；順序是 ABI，不可只改一端 |
| `research/dualsense-usb-test/` | 現有 DualSense 使用者空間 USB/IP 測試、後續候選研究 |
| `research/dualsense-v5-test/` | 固定 hbashton 候選 V5 轉接、離線測試、純使用者空間 lab server；不可作玩家後端 |
| `research/dualsense-driver-test/` | 僅供 GitHub hosted Windows VM 的 0.9.8.1 接入／HID 讀回研究；禁止本機啟動 |
| `research/dualsense-preview/` | alpha.9 四方案歷史實測、alpha.10 單模式玩家主線；USB/IP 33641、API 33642 |
| `research/native-probe/` | Sony handle 唯讀檢查，`--dualsense` 限定 `054C:0CE6` |
| `packaging/Verify-ControllerFreeBuild.ps1` | alpha.8 舊路線的來源／版本／雜湊驗證，不用於 alpha.10 |

alpha.10 必須使用 `research/dualsense-preview/Build-Candidate.ps1`，先建置同目錄 `backend`，再用 `Verify-Candidate.ps1` 核對；實際參數見 [版本規則](VERSIONING.md)。原 `controller-free-preview/Build.ps1` 不能交付新主線。不要誤用 `packaging/Build-Preview.ps1` 或 `src/physical-keyboard/Build.ps1`；它們是歷史路線。`docs/architecture.md` 原有內容亦屬較早研究。

現行後端固定 **hbashton/VIIPER `b78e31e93e84b4cb7c4b4c15b8aace10749106d2`** 的底層套件，由自製受限入口啟動，並以既有 **usbip-win2 0.9.8.1** 成套 CLI／DLL 明確匯入自有裝置。僅 loopback：API **33642**、USB/IP **33641**；alpha.10 只允許 `dualsensegamepadv5`，不自動接入。Alia5 VIIPER 0.9.0 與 33241／33242 是 alpha.8 舊路線。DualSense VID/PID `054C:0CE6`；VID/PID 或 Windows 接入狀態均不是該次 Sony 操作成功證明。

## 7. DualSense 研究證據與下一步驗收

先讀 [固定來源研究](../research/dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md) 與 [alpha.6 實機紀錄](../research/controller-free-preview/ALPHA6-TRIAL.md)。2026-10-29 查核結果：

- 原版 Alia5 最新正式版仍為 v0.9.0；當日 main 比正式版新增的四次提交未改 DualSense 裝置實作。這是當日快照，下串若需「最新」須重新核對上游。
- hbashton/VIIPER 的 `v0.1.9-rc4.6.6` 有實質不同的 DualSense 複合裝置：音訊介面 0–2、HID 介面 3；原版是單一 HID 介面 0。**差異是研究線索，尚未證明就是 Sony 拒絕原版的根因。**
- 候選使用 V5 裝置名稱（如 `dualsensecombinedaudioduplexv5`），需要 16-byte VPCM 標頭、種類、長度、序號、CRC32 與 framed feedback；原始碼另有文件表未列的 `0x84` 回饋。**33-byte 內容也不完全相容**：原版觸控 bool `00` 表示未接觸，候選 packed 狀態必須用 `80`。獨立 adapter 已修正，沒有修改現行 PS4／原版 DualSense 傳輸。
- 候選正式 Windows CLI 不只文件要求，而是在開啟 server 前**無條件檢查** usbip-win2 必須為 0.9.7.7，關閉 auto-attach 也不例外。本機為 0.9.8.1；後續已用受限入口與匹配 CLI 驗證另一條接入路徑，**不是移除原 EXE 的檢查，亦不可降級或混用不同版 CLI／DLL**。
- 固定候選 ZIP 已下載核對 GitHub digest／BUILD-INFO，`viiper.exe` 實際為 **NotSigned**；正式候選執行檔未啟動。早期以固定來源底層套件自建 lab server，在獨立 loopback `33441/33442` 完成使用者空間測試，該階段沒有 Windows attach、驅動或 Sony 操作；後來的 hosted HID 與 alpha.9 使用者成功回報分別補足不同層級證據，不能回寫成 lab server 已經驗過 Sony。
- 後續來源核對確認候選內建 native attach 結構確實不合 0.9.8.1 ABI，不只是版本文字檢查；USB/IP 網路欄位則保有可測基礎。新研究使用固定來源 fixture、關閉 native attach，讓完整 0.9.8.1 CLI／DLL 對接同版驅動。詳細差異與已知限制見 [相容性研究](../research/dualsense-driver-test/COMPATIBILITY.md)；不宣稱原候選 EXE 可直接使用。
- DS5Dongle 需要實體 DualSense 與 Pico 硬體，僅作協定參考，不是目前免手把方向的直接解法。使用者手邊沒有 PS5 手把。

前述 V5／使用者空間部分已有[結果](../research/dualsense-v5-test/RESULTS-20261029.md)：104 項自製離線檢查、70 項 USB/IP 檢查與 13 個上游精準測試通過。後續 [CI 40838199781](https://github.com/xiunlee/ps-remote-play-keyboard/actions/runs/40838199781) 四個獨立 Windows VM 各通過 63 項 HID 檢查與自有裝置移除；alpha.9 再取得四方案官方 Remote Play／PS5 操作成功回報。證據共同支持本次環境沿用 0.9.8.1，不外推所有機器、遊戲或原候選 EXE。

接續工作：

1. 唯讀核對目前版本、分支、程序及設定；先保護正在使用的 alpha.9 與 Sony 連線，不重做四方案選型或原版 62 項試驗。
2. 完成 alpha.10 的乾淨建置、離線驗證與主畫面／設定頁渲染檢查；補入具體來源提交、建置清單、雜湊及交付位置。本次文件更新不預填這些證據。
3. 由舊程式正常退出後切換 alpha.10；驗收既有設定沿用、22 動作改鍵、儲存／取消／重新載入、三種連點及不同間隔。不得因切換自行重啟 Sony 或更動驅動。
4. 驗收切窗清除連點、切回不自動恢復、F8 暫停、編輯期間暫停、斷線停止與自有裝置清理；另外安排長時間及重開機驗收，不把移除 30 分鐘限制說成長時間已通過。
5. 依最新授權清理舊本地版本與重複建置／封存，保留最新版、個人設定／備份及 GitHub 歷史；完成後記錄清理範圍與仍保留項目。只處理已核對且未使用中的專案檔案，不按歷史 PID 操作。

後端回讀、Windows 裝置、Sony handle 與使用者 PS5 操作仍須分開記錄。alpha.9 的使用者回報可以記為該版成功，不能讓 alpha.10 的連線燈或每次新 session 自動宣稱 Sony 已接受輸入。

## 8. 本機交付、回退與版本規則

公開文件用環境變數表示路徑；下串在使用者原工作目錄繼續即可。不要把個人設定、程序收據、原始 USB／按鍵資料或裝置序號推送到公開 GitHub。

alpha.9 曾交付至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.9`，入口 `PSKeyboard-DualSensePreview.exe`，桌面捷徑「上班不要玩 0.1.0-alpha.9 四方案測試」。開啟不自動接入、鍵盤暫停、設定唯讀；使用者逐一操作後回報四種成功。完整 [交付與實機紀錄](../research/dualsense-preview/ALPHA9-TRIAL.md)、[建置](../packaging/build-records/0.1.0-alpha.9.json) 與[封存](../packaging/build-records/0.1.0-alpha.9-archives.json)保留於 GitHub。

**alpha.10 正式建置交付與本機清理尚待補記**：此時不填未驗證的 commit、tag、安裝路徑、產物或雜湊。交付後更新本段與對應紀錄，清楚區分「建置／已放置／已啟動／使用者實測」。

最新本機保存規則：

- 新版先放獨立全新目錄，驗證完成再切換；不得覆寫執行中的 alpha.9。正常退出舊版後才移除其檔案／捷徑，不強制終止 Sony 或驅動。
- alpha.10 交付後，舊 `versions`、重複建置目錄、`versioned-archives` 舊封存與 `versioned-baselines` 舊基準可依使用者授權清理；具體目錄先核對解析後的完整路徑、專案歸屬及程序占用，不能對工具根目錄整包遞迴刪除。
- 保留 `%LOCALAPPDATA%\PSKeyboard\bindings.json`、`profile-backups`、最新版執行所需檔案及最新版的可追溯紀錄。最新包仍需相同雜湊的 `pskb-keyboard-merge.dll`，清理不能誤刪其唯一可用副本。
- 用系統 Desktop known folder 找舊專案捷徑，只移除已核對的舊捷徑，不假定桌面路徑，也不清除無關檔案。
- GitHub 的舊標籤、原始碼、PS4 路線、研究、建置與封存雜湊紀錄不刪、不改。清理後舊本機執行檔可能已不存在，回退需依標籤與固定來源另行重建，不能再指示使用者直接開已移除的 alpha.8／9 捷徑。

遵循 [版本規則](VERSIONING.md)：本次為 alpha.10，之後有應用程式碼變更的交付從 **alpha.11** 起增加；新程式不覆寫舊版或重用／移動既有 tag。清理舊本地副本不等於可以覆寫版本歷史。文件／驗收補記不用升程式版本。

建置前提交程式碼、保持工作區乾淨；使用 `research/dualsense-preview/backend/Build-Backend.ps1`、`Build-Candidate.ps1 -BackendDirectory <新後端目錄> -MappingDirectory <固定雜湊DLL所在目錄> -OutputDirectory <全新目錄> -RunTests -RequireClean` 與 `Verify-Candidate.ps1 -BuildDirectory <新建置目錄> -RequireClean`。`MappingDirectory` 必填；歷史版本目錄清除後，改指保留最新版中符合固定雜湊的 mapping DLL 目錄，不可暗中更換依賴。完整後端參數見 [版本規則](VERSIONING.md)。精確重現舊版需使用該標籤的隔離 checkout，不移動標籤來追齊文件。

alpha.8：36 項控制保護、106 項假傳輸／轉換、17 項建置完整性與 122 項設定頁互動檢查通過；九語設定頁逐張檢視。alpha.7 設定／連點／流程／設定頁共 535 項離線檢查通過。先前原版 DualSense 使用者空間 USB/IP 62 項通過。這些測試各有範圍，不是遊戲、長時間操作、真正高 DPI 或新候選的驗收。

## 9. UI 素材與後續收尾

- 設計及預覽：[VISUAL-DESIGN.md](VISUAL-DESIGN.md)；素材在 `assets/branding`，離線畫面在 `assets/previews/alpha.8`。
- 圖示由內建 imagegen 製作，PNG／ICO 已嵌入執行檔，包含 16–256 像素圖示。UI 常數精準使用 `#D62355`，**生成圖示底色有微小色差**；詳見 [PROMPT.md](../assets/branding/PROMPT.md)。
- 狀態綠燈表示本機裝置／輸出通道已接入，不代表 PS5 已連線或遊戲接受輸入。預覽圖片是離線示意，非即時遊戲畫面。
- 主畫面常用鍵位摘要來自當前設定，不是寫死新預設；後續調整不能破壞此點。
- 待做：alpha.10 主畫面與設定流程驗收、實際高 DPI、多螢幕、主介面完整九語、翻譯校閱。先完成選定 DualSense 模式的功能整合，不擅自擴大成整套重寫。

## 10. 其餘待辦與閱讀順序

1. 本文件 → [版本紀錄](../CHANGELOG.md) → [版本控管](VERSIONING.md)。
2. [alpha.6 歷史實機結果](../research/controller-free-preview/ALPHA6-TRIAL.md) → [DualSense 候選](../research/dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md) → [V5 隔離結果](../research/dualsense-v5-test/RESULTS-20261029.md) → [既有驅動相容性研究](../research/dualsense-driver-test/README.md) → [alpha.9 成功回報](../research/dualsense-preview/ALPHA9-TRIAL.md) → [現行 DualSense 功能說明](../research/dualsense-preview/README.md) → 相關程式與測試。
3. 需要確認早期成功證據時讀 [首次免手把紀錄](../research/controller-free-preview/LIVE-TRIAL.md)；歷史失敗仍保留，不能當成現況或假定其根因已修正。
4. [支援範圍](PRODUCT-SCOPE.md)、[連點／錄製設計](AUTOMATION-AND-CONTROLLER-TESTS.md)、[發行工作表](../packaging/RELEASE-CHECKLIST.md) 是後續待辦。錄製只記錄已映射的手把動作與時間，不記錄全部 Windows 打字、不執行任意程式碼；失焦／F8／斷線停止。
5. 正式發布前補足乾淨電腦安裝／移除、恢復與長時間測試、第三方 notices／對應原始碼、發行簽章與授權、退款支援及支付平台。不要宣稱首創、Sony 官方授權或所有遊戲／電腦通用。

完成下一個階段後，更新本文件與對應證據、提交到工作分支並核對遠端。交付的是可追溯的進度與成果，不只在聊天中宣告完成。
