# 上班不要玩：總進度與換串交接

更新日期：2026-10-29（Asia/Taipei）。這份文件是下一串的入口，將已實測、離線驗證與尚未完成的工作分開記錄。閱讀後可直接繼續已授權的開發與隔離研究，不必重新詢問已決定的產品方向。

## 1. 目前結論與接續目標

- 產品主線是 **Windows、免實體手把、鍵盤映射、Sony 官方 PS Remote Play**。名稱是「上班不要玩」。
- **模擬 PS4 DUALSHOCK 4 已能控制 PS5**：alpha.5 有拔除實體手把與一次 Windows 重開機後的使用者成功回報；alpha.6 PS4 模式亦由使用者確認正常。不是只能操控 PS4 主機。
- **模擬 PS5 DualSense 尚未成功**：alpha.6 在 Windows 可建立裝置，使用者在官方 Remote Play 回報無反應；切回同版 PS4 模式後可操作。失敗根因尚未定位。
- 保留的 PS4 成品 **0.1.0-alpha.8** 完成白色幾何 PS 圖示、莓紅介面與設定頁，保留 alpha.7 鍵位及原後端。新 **0.1.0-alpha.9** 是另一個 [DualSense 四方案測試程式](../research/dualsense-preview/README.md)，不能拿它替換已可用的 alpha.8 PS4 執行檔；官方 Remote Play 操作仍待使用者驗證。
- 本次交接唯讀檢查發現 alpha.8 已在執行，官方 Remote Play 仍是原先程序；**尚未收到使用者對 alpha.8 的遊戲操作及視覺驗收回報**。程序狀態會變動，下串先唯讀確認，不按歷史 PID 操作。
- 使用者最新順序是：**完成 UI／圖示後，接著研究如何讓 DualSense 模擬被官方 Remote Play 接受**。可先進行不影響現有連線的原始碼研究與隔離協定測試。
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
| `v0.1.0-alpha.9` | `18c370cb528bbc2107827bebb7799bdb095e133b` | 獨立 DualSense 四方案候選；hosted HID 通過，Sony 操作待使用者驗收 |

alpha.8 建置／封存紀錄與最終畫面另於 `cf51de1` 提交；本交接文件再作文件提交。**文件提交超前版本標籤是正常的，不要移動舊標籤來追齊分支。**

GitHub 保存：自製原始碼、研究與實機紀錄、設計素材／預覽、版本標籤、建置清單與檔案 SHA-256。沒有公開第三方執行檔或本機測試 ZIP，也沒有建立正式二進位 Release、啟用收費或支付平台。自製程式的正式授權與第三方再散布審查仍未完成。

## 3. 不可遺失的操作規則

1. **不得自行關閉、重啟或更新 Sony Remote Play**。使用者反覆強調連線不容易；早先安裝／測試的同意不是永久中斷連線的許可。若新步驟真的需要中斷當前使用，先做好可檢視的候選並說明具體原因，再協調測試時段。
2. 保留已正常的 DS4 模式、舊版執行檔與設定備份。新研究使用獨立目錄／連接埠；不覆蓋運作中的後端，不同時啟動兩版來爭用 F8 或通訊埠。
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
| 免手把 DualSense | 原版 Sony 操作失敗；新候選四方案的 0.9.8.1 hosted Windows HID／清理通過 | alpha.9 使用者本機與真正 Sony 操作驗收；音訊／長時間另驗 |
| 22 動作自訂鍵 | 實作、儲存、載入、清除、重複鍵檢查及離線測試完成 | 最新版完整實機回歸 |
| 每鍵連點 | 關閉／按住／按一下啟停；20–5000 毫秒；預設 100 | 實機逐項驗收及長時間停止可靠性 |
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

alpha.7／alpha.8 新建或「恢復預設」的排列如下；已有格式 1／2 設定保持原樣。

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

設定檔：`%LOCALAPPDATA%\PSKeyboard\bindings.json`。不提交此個人檔案。格式 1 載入後保留鍵位、連點預設關閉；儲存才寫格式 2。alpha.5–alpha.8 使用格式 2，不能推論更舊的程式均可讀取。

## 6. 現行架構與程式入口

目前資料路徑：鍵盤擷取 → 前景／F8 狀態 → BindingProfile／RapidFireEngine／KeyboardControlPipeline → 手把報告轉換 → 本機 VIIPER → 既有 usbip-win2 → Windows HID → Sony 官方 Remote Play → PS5。

| 路徑 | 用途 |
| --- | --- |
| `research/controller-free-preview/ControllerFreeKeyboard.cs` | **現行免手把主程式**、視窗、狀態、擷取與裝置生命週期 |
| `research/controller-free-preview/Build.ps1` | 主線建置與離線檢查，非安裝／啟動工具 |
| `src/physical-keyboard/BindingProfile.cs`、`RapidFireEngine.cs`、`KeyboardControlPipeline.cs` | 主線沿用的設定／連點／按鍵核心；目錄名不代表只供實體中繼 |
| `src/physical-keyboard/BindingEditor.cs`、`KeyboardUi.cs`、`UiText.cs` | 設定頁、品牌樣式與共用翻譯 |
| `src/keyboard-pad/ViiperTransport.cs`、`KeyboardInputState.cs`、`KeyboardMapping.cs` | 主線沿用的傳輸與鍵盤處理 |
| `research/native-merge/keyboard_merge.c`、`.h` | 22 動作的原生報告處理；順序是 ABI，不可只改一端 |
| `research/dualsense-usb-test/` | 現有 DualSense 使用者空間 USB/IP 測試、後續候選研究 |
| `research/dualsense-v5-test/` | 固定 hbashton 候選 V5 轉接、離線測試、純使用者空間 lab server；不可作玩家後端 |
| `research/dualsense-driver-test/` | 僅供 GitHub hosted Windows VM 的 0.9.8.1 接入／HID 讀回研究；禁止本機啟動 |
| `research/dualsense-preview/` | alpha.9 四方案玩家候選、獨立傳輸／受限後端／建置；使用 33641/33642，設定唯讀 |
| `research/native-probe/` | Sony handle 唯讀檢查，`--dualsense` 限定 `054C:0CE6` |
| `packaging/Verify-ControllerFreeBuild.ps1` | 來源／檔案版本、雜湊、品牌資源驗證 |

alpha.9 必須使用 `research/dualsense-preview/Build-Candidate.ps1`；原 `controller-free-preview/Build.ps1` 仍是 alpha.8 所用 PS4／舊 DualSense 主程式，不能拿它交付新的四方案。不要誤用 `packaging/Build-Preview.ps1` 或 `src/physical-keyboard/Build.ps1`；它們是歷史路線。`docs/architecture.md` 原有內容亦屬較早研究。

目前後端是已固定雜湊並驗證簽章的 **Alia5 VIIPER 0.9.0**；接收端是已安裝的 **usbip-win2 0.9.8.1**。本機通訊僅 loopback，主程式 API 埠 33242、USB/IP 埠 33241，明確匯入自己建立的裝置。DS4 VID/PID `054C:09CC`；DualSense `054C:0CE6`。不要將 VID/PID 相同當成完整實作或 Sony 相容性的證明。

## 7. DualSense 下一步：已做的研究與未做的實驗

先讀 [固定來源研究](../research/dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md) 與 [alpha.6 實機紀錄](../research/controller-free-preview/ALPHA6-TRIAL.md)。2026-10-29 查核結果：

- 原版 Alia5 最新正式版仍為 v0.9.0；當日 main 比正式版新增的四次提交未改 DualSense 裝置實作。這是當日快照，下串若需「最新」須重新核對上游。
- hbashton/VIIPER 的 `v0.1.9-rc4.6.6` 有實質不同的 DualSense 複合裝置：音訊介面 0–2、HID 介面 3；原版是單一 HID 介面 0。**差異是研究線索，尚未證明就是 Sony 拒絕原版的根因。**
- 候選使用 V5 裝置名稱（如 `dualsensecombinedaudioduplexv5`），需要 16-byte VPCM 標頭、種類、長度、序號、CRC32 與 framed feedback；原始碼另有文件表未列的 `0x84` 回饋。**33-byte 內容也不完全相容**：原版觸控 bool `00` 表示未接觸，候選 packed 狀態必須用 `80`。獨立 adapter 已修正，沒有修改現行 PS4／原版 DualSense 傳輸。
- 候選正式 Windows CLI 不只文件要求，而是在開啟 server 前**無條件檢查** usbip-win2 必須為 0.9.7.7，關閉 auto-attach 也不例外。本機唯讀確認為 0.9.8.1。候選網路後端與現有成套驅動的實際相容性仍未驗證；**不可降級、混用不同版 CLI/DLL，或移除檢查便宣稱相容**。
- 固定候選 ZIP 已下載核對 GitHub digest／BUILD-INFO，`viiper.exe` 實際為 **NotSigned**；正式候選執行檔未啟動。另以固定來源底層套件自建 lab server，在獨立 loopback `33441/33442` 完成使用者空間測試，沒有 Windows attach、驅動操作或 Sony 操作。尚無此候選已通過 Sony Remote Play 9.5 的明確實測證據。
- 後續來源核對確認候選內建 native attach 結構確實不合 0.9.8.1 ABI，不只是版本文字檢查；USB/IP 網路欄位則保有可測基礎。新研究使用固定來源 fixture、關閉 native attach，讓完整 0.9.8.1 CLI／DLL 對接同版驅動。詳細差異與已知限制見 [相容性研究](../research/dualsense-driver-test/COMPATIBILITY.md)；不宣稱原候選 EXE 可直接使用。
- DS5Dongle 需要實體 DualSense 與 Pico 硬體，僅作協定參考，不是目前免手把方向的直接解法。使用者手邊沒有 PS5 手把。

接續順序：

前述 V5／使用者空間部分已有[結果](../research/dualsense-v5-test/RESULTS-20261029.md)：104 項自製離線檢查、70 項 USB/IP 檢查與 13 個上游精準測試通過。後續 [CI 40838199781](https://github.com/xiunlee/ps-remote-play-keyboard/actions/runs/40838199781) 四個獨立 Windows VM 各通過 63 項 HID 檢查與自有裝置移除，現有 0.9.8.1 成套驅動不需降級。下一步是 alpha.9 本機四方案操作驗證，不重做原版 62 項試驗；**Sony 實際操作、音訊串流及長時間仍未通過**。以下保留整體驗收順序。

1. 唯讀核對目前版本、分支、程序及既有資料；先保護正常的 DS4 連線，不重做已完成研究。
2. 以固定 tag／commit 比較複合描述、feature/control 回覆及 V5 協定；在獨立研究目錄實作候選 adapter 與離線測試。預設 PS4 不變。
3. 完成不匯入 Windows 的使用者空間 USB/IP 驗證，再評估既有驅動相容性與候選簽章／依賴。使用獨立埠，不占用目前服務。
4. 只有 Windows 測試條件具體可檢視、可回退時才安排實機測試。若會中斷當前控制或更動驅動，先說明影響並協調；不能為驗證自行關閉 Sony。
5. 分別記錄後端回讀、Windows 裝置、Sony handle、使用者 PS5 實際操作。只有最後一項成功才可稱 Sony 操作通過。若仍失敗，記錄具體失敗階段，保留正常 PS4 方案。

## 8. 本機交付、回退與版本規則

公開文件用環境變數表示路徑；下串在使用者原工作目錄繼續即可。不要把個人設定、程序收據、原始 USB／按鍵資料或裝置序號推送到公開 GitHub。

alpha.9 已交付至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.9`，入口 `PSKeyboard-DualSensePreview.exe`，桌面捷徑「上班不要玩 0.1.0-alpha.9 四方案測試」。開啟不自動接入，鍵盤暫停；只讀既有設定，四方案由使用者逐一連接。完整 [交付／待驗收紀錄](../research/dualsense-preview/ALPHA9-TRIAL.md)、[建置](../packaging/build-records/0.1.0-alpha.9.json) 與[封存](../packaging/build-records/0.1.0-alpha.9-archives.json)已保存。**尚未在使用者本機接入或驗證 Sony 操作；不能把 hosted 成功外推為已可玩。** 下列 alpha.8 資料作可用 PS4 回退保留。

- alpha.8 安裝目錄：`%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.8`。
- 執行檔：`PSKeyboard-ControllerFree.exe`；同目錄保留 `pskb-keyboard-merge.dll`、`viiper.exe`、`BUILD.json`、`SHA256SUMS.txt`。
- 桌面捷徑名稱：`上班不要玩 0.1.0-alpha.8`。用系統 Desktop known folder 尋找，不假設桌面固定在 `%USERPROFILE%\Desktop`。
- 捷徑預設參數為 `--connect --controller dualshock4 --status-file <該版本目錄的 status.json>`；鍵盤預設暫停，F8 啟用。
- 本機封存：`%USERPROFILE%\.pskeyboard-tools\versioned-archives\0.1.0-alpha.8`，含 runtime ZIP、source ZIP 與 `ARCHIVE.json`；雜湊另存 [公開封存紀錄](../packaging/build-records/0.1.0-alpha.8-archives.json)。
- 原建置目錄：`%USERPROFILE%\.pskeyboard-tools\controller-free-builds\0.1.0-alpha.8-a6c5c2a99ed3-e69f4d4480b04c329699b5ca37d73324`。
- 其他版本保留於 `versions`；alpha.5 原始成功基準另在 `versioned-baselines\0.1.0-alpha.5`。alpha.7 前的個人設定備份位於 `profile-backups`，不公開內容。
- 回退先正常退出我們的程式，再開保留版；不要強殺 Sony，也不要同時開兩版。

遵循 [版本規則](VERSIONING.md)：本次四方案候選使用 alpha.9，之後有應用程式碼變更的交付從 alpha.10 起增加，不覆寫 alpha.8／9、不重用或移動既有 tag。文件／驗收補記不用升程式版本。

建置前提交程式碼、保持工作區乾淨；使用 `research/controller-free-preview/Build.ps1 -RunTests -RequireClean`，再用 `packaging/Verify-ControllerFreeBuild.ps1 -BuildDirectory <新建置目錄> -RequireClean` 驗證。**在已標記的 alpha.8 上新增文件後，不應為重新編譯而移動標籤；若要精確重現，使用該標籤的隔離 checkout，若交付新程式則升版本。**

alpha.8：36 項控制保護、106 項假傳輸／轉換、17 項建置完整性與 122 項設定頁互動檢查通過；九語設定頁逐張檢視。alpha.7 設定／連點／流程／設定頁共 535 項離線檢查通過。先前原版 DualSense 使用者空間 USB/IP 62 項通過。這些測試各有範圍，不是遊戲、長時間操作、真正高 DPI 或新候選的驗收。

## 9. UI 素材與後續收尾

- 設計及預覽：[VISUAL-DESIGN.md](VISUAL-DESIGN.md)；素材在 `assets/branding`，離線畫面在 `assets/previews/alpha.8`。
- 圖示由內建 imagegen 製作，PNG／ICO 已嵌入執行檔，包含 16–256 像素圖示。UI 常數精準使用 `#D62355`，**生成圖示底色有微小色差**；詳見 [PROMPT.md](../assets/branding/PROMPT.md)。
- 狀態綠燈表示本機裝置／輸出通道已接入，不代表 PS5 已連線或遊戲接受輸入。預覽圖片是離線示意，非即時遊戲畫面。
- 主畫面常用鍵位摘要來自當前設定，不是寫死新預設；後續調整不能破壞此點。
- 待做：使用者視覺確認、實際高 DPI、多螢幕、主介面完整九語、翻譯校閱。PS5 研究優先，不擅自擴大成整套重寫。

## 10. 其餘待辦與閱讀順序

1. 本文件 → [版本紀錄](../CHANGELOG.md) → [版本控管](VERSIONING.md)。
2. [alpha.6 實機結果](../research/controller-free-preview/ALPHA6-TRIAL.md) → [DualSense 候選](../research/dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md) → [V5 隔離結果](../research/dualsense-v5-test/RESULTS-20261029.md) → [既有驅動相容性研究](../research/dualsense-driver-test/README.md) → 相關程式與測試。
3. 需要確認早期成功證據時讀 [首次免手把紀錄](../research/controller-free-preview/LIVE-TRIAL.md)；歷史失敗仍保留，不能當成現況或假定其根因已修正。
4. [支援範圍](PRODUCT-SCOPE.md)、[連點／錄製設計](AUTOMATION-AND-CONTROLLER-TESTS.md)、[發行工作表](../packaging/RELEASE-CHECKLIST.md) 是後續待辦。錄製只記錄已映射的手把動作與時間，不記錄全部 Windows 打字、不執行任意程式碼；失焦／F8／斷線停止。
5. 正式發布前補足乾淨電腦安裝／移除、恢復與長時間測試、第三方 notices／對應原始碼、發行簽章與授權、退款支援及支付平台。不要宣稱首創、Sony 官方授權或所有遊戲／電腦通用。

完成下一個階段後，更新本文件與對應證據、提交到工作分支並核對遠端。交付的是可追溯的進度與成果，不只在聊天中宣告完成。
