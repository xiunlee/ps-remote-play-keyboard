# 上班不要玩

**繁體中文** · [English](README.en.md)

**專為 Windows 上 Sony 官方 PS Remote Play 製作的鍵盤映射工具。** 先用官方 Remote Play 連上 PS5，再讓「上班不要玩」把鍵盤輸入轉成模擬 DualSense 手把輸入。本程式不是替代 Remote Play 用戶端，也不直接連線主機。

**本版以 2D 遊戲為定位，尤其適合以選單與方向操作為主的 JRPG。** 目前沒有滑鼠瞄準或右搖桿方向映射，不適合需要靈活視角或精細類比控制的 3D 遊戲。是否適用仍取決於個別遊戲的操作需求，不保證所有 JRPG 都適合。

免費開源 · 作者 **xiunlee** · **GPL-3.0-only**。所有功能免費使用，贊助不影響功能。

## 下載與目前狀態

**v0.1.2 已正式發布：美術優化。** [下載 Windows x64 程式包](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.2/PSKeyboard-0.1.2-windows-x64.zip)，或前往 [v0.1.2 發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.2)取得對應 Windows 原始碼與 SHA-256 清單。四附件匿名下載雜湊均已核對，歷史標籤與附件保留。本版逐項實機、長時間及新電腦安裝仍待驗收。

中文介面保留「上班不要玩」；英文、日文、韓文、德文、法文、西班牙文與巴西葡語統一使用 **PS Remote Play Keyboard**，包含主畫面、設定頁、說明及回饋主旨。

三個執行元件目前**沒有數位簽章**；雜湊只供核對檔案完整性，不代表簽章或通用相容性。詳見[發布與驗證紀錄](research/dualsense-preview/RELEASE-0.1.2.md)。Mac 版尚未提供；[Mac 研究](research/macos-native/README.md)仍在進行，官方 Remote Play 相容性尚未通過。後續接手見 [Mac 交接文件](docs/MAC-HANDOFF.md)。

alpha.9 的四種 DualSense 方案已有使用者透過官方 Remote Play 操作 PS5 的[成功回報](research/dualsense-preview/ALPHA9-TRIAL.md)。目前固定其中第二種「一般 DualSense／純手把」模式；alpha.10 已完成設定與連點整合、本機交付，alpha.11 接續加入免費開源說明、回饋與公開封裝。上述結果不代表所有電腦、遊戲或新版本均已驗收。

**介面語言：** 支援繁中、簡中、英文、日文、韓文、德文、法文、西班牙文與巴西葡語，依 Windows 介面語言選擇，未支援語言回退英文。母語校閱、實際高 DPI、多螢幕及逐項實機仍待驗。[主畫面預覽](assets/previews/0.1.2/main-zh-Hant.png) · [按鍵設定預覽](assets/previews/0.1.2/editor-zh-Hant.png)（均為離線示意）。

**使用者遊戲回報：**2026-10-29，使用者回報《Final Fantasy Resonance》可正常操作。回報未指定本工具的建置版本，不視為 v0.1.2 逐鍵或連點驗收，也不延伸為所有 JRPG 的相容性保證。

## 使用需求

- Windows x64 桌面環境與 .NET Framework。
- **Sony 官方 PS Remote Play 已安裝，並先連線到 PS5。** 本程式不代管 PSN 帳號、不取代官方用戶端。
- 已安裝完整、相互匹配的 **usbip-win2 0.9.8.1 驅動、CLI 與 DLL**。本包不含驅動，不會自動安裝、更換、降級或重新啟動驅動；版本不符時停止接入。

本模式不需插入實體手把，但**不是免驅動或一鍵安裝程式**。驅動原始套件與說明見 [usbip-win2 0.9.8.1 官方版本頁](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1)。尚未備妥相依環境時，請先閱讀上游說明；不要為本程式的測試中斷目前的 Remote Play 連線。

## 開始使用

1. 下載執行包並完整解壓縮，保留 EXE、DLL、後端與其他隨附檔案，不要只取出單一 EXE。
2. 用官方 PS Remote Play 連上 PS5，保持該程式開啟；正常退出其他版本的「上班不要玩」。兩版不能同時接管 F8。
3. 開啟 `PSKeyboard-DualSensePreview.exe`，按「連接手把」。啟動不自動接入，鍵盤控制預設暫停。
4. 切回官方 Remote Play，按 **F8** 啟用鍵盤；再按 F8 暫停。只有官方 Remote Play 在前景時，遊戲按鍵與連點才會送出。
5. 要改鍵或連點，按「按鍵與連點設定」。只有按「儲存」且成功時才會套用；取消保留原設定。編輯後保持暫停，切回 Remote Play 再按 F8。
6. 結束時按「中斷手把」或正常關閉本程式。若清理未確認而鎖住重新接入，保留訊息回報，不反覆建立裝置或重啟 Sony。

## 按鍵、連點與設定保存

22 個手把動作可各自指定單一按鍵，並選擇關閉連點、按住連點或按一下啟停；每鍵間隔 20–5000 毫秒，預設 100 毫秒。F8 保留為控制開關，不能指派成遊戲按鍵。遊戲可能忽略很短的連點脈衝。

下表適用於新設定或儲存「恢復預設」之後；既有自訂設定會沿用。

| 手把動作 | 預設鍵盤 |
| --- | --- |
| 左搖桿上／下／左／右 | W／S／A／D |
| △／□／×／○ | ↑／←／↓／→ |
| L1／L2 | E／Q |
| R1／R2 | Delete／Page Down |
| 十字方向上／下／左／右 | Home／End／Insert／Page Up |
| L3／R3 按下 | F／空白鍵 |
| Create／Options | Tab／Enter |
| 觸控板按下／PS | R／F1 |
| 啟用／暫停 | F8 |

方向鍵是面板四鍵，**選單的十字方向上／下預設為 Home／End**。R3 只代表按下右搖桿，不包含視角移動。完整說明見 [預設鍵位](docs/DEFAULT-KEYS.md)。

個人設定在 `%LOCALAPPDATA%\PSKeyboard\bindings.json`，與程式版本分開。更新、開啟或取消編輯都不重設設定；儲存失敗也不套用新草稿。「恢復預設」會先修改草稿，連點全關、間隔回到 100 毫秒，儲存後才生效。

## 常見問題

**顯示手把已接入，但 PS5 沒反應？** 連線燈只表示 Windows 輸出通道，不是 Sony 接受輸入的證明。先確認官方 Remote Play 已連上 PS5、視窗在前景、F8 已啟用，以及使用的按鍵符合目前設定。若有驅動或清理錯誤，先保留訊息回報，不要反覆接入、更換驅動或為此重啟 Sony。

**取消改鍵會不會覆蓋原設定？** 不會。只有按「儲存」且寫入成功才套用，取消、關閉編輯器或儲存失敗都保留原設定；編輯後鍵盤仍維持暫停。

**切到其他視窗，連點會繼續嗎？** 不會。切窗會停止並清除連點，切回不自動恢復，需重新按鍵觸發。F8、編輯、斷線或退出也會停止輸出。

**能用滑鼠轉視角、錄製腳本，或在 Mac 上使用嗎？** 目前不支援滑鼠右搖桿、錄製／重播或 macOS，也不承諾完整音訊、觸覺與自適應扳機功能。

長時間、重開機、反覆重連、第二台電腦安裝與新版逐鍵操作仍需驗收。詳見 [支援範圍](docs/PRODUCT-SCOPE.md)及 [DualSense 使用說明](research/dualsense-preview/README.md)。本專案不是 Sony 官方軟體。

## 贊助與意見回饋

覺得好用的話，歡迎[透過 PayPal 自願贊助 US$1，幫三隻貓咪買罐頭](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)。贊助不解鎖功能，也不建立訂閱。點擊程式的「贊助貓咪 US$1」才會開啟瀏覽器，由 PayPal 處理收款；本程式不接收或保存付款資料。

[意見回饋：xiun1019@gmail.com](mailto:xiun1019@gmail.com)。程式只在你點擊「意見回饋」後開啟郵件程式，預填收件人與版本主旨；不自動寄出、不附帶個人設定或診斷檔。

## 原始碼與授權

本專案原始碼採 [GNU GPL 第 3 版，僅此版本](LICENSE)（`GPL-3.0-only`），作者 **xiunlee**；第三方元件保留各自授權，見 [著作權與授權說明](COPYRIGHT.md)。公開執行包須與完整對應原始碼、固定上游及依賴來源、重建說明、第三方 notices 與雜湊一同提供。

建置方式見 [版本控管](docs/VERSIONING.md)。最新開發分支仍叫 `codex/commercial-preview`，名稱沿用歷史，不代表收費計畫；`main` 可能落後。維護者先讀 [交接文件](docs/HANDOFF.md)、[版本紀錄](CHANGELOG.md)與 [發行工作表](packaging/RELEASE-CHECKLIST.md)。

## 歷史研究

PS4 模擬、實體 DS4 中繼與早期候選保留於 Git 歷史及研究文件，不能當作目前版本的安裝流程。

- [免手把 PS4 首次成功與重開機回報](research/controller-free-preview/LIVE-TRIAL.md)
- [alpha.6 舊 DualSense 失敗紀錄](research/controller-free-preview/ALPHA6-TRIAL.md)
- [V5 隔離協定驗證](research/dualsense-v5-test/RESULTS-20261029.md)與 [0.9.8.1 hosted 驅動驗證](research/dualsense-driver-test/RESULTS-20261029.md)
- [實體 DS4 中繼](research/physical-usb-relay/README.md)與 [原生映射核心](research/native-merge/README.md)
