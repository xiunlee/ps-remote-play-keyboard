# 上班不要玩：PS Remote Play 鍵盤控制

繁體中文使用手冊 · [English](README.en.md)

用鍵盤操作 **Sony 官方 Windows PS Remote Play 裡的 PS5**，提供 22 個動作改鍵、每鍵連點與 F8 暫停。程式建立虛擬 DualSense，不需插入實體手把；PS5 連線、帳號登入與串流仍由 Sony 官方 Remote Play 處理。

**免費開源 · 作者 xiunlee · GPL-3.0-only。** 本手冊對應 **0.1.0-alpha.11 測試版**，不是穩定版。所有功能免費，贊助不影響功能。[下載、更新與對應原始碼](https://github.com/xiunlee/ps-remote-play-keyboard/releases)。

## 使用前準備

- Windows x64、.NET Framework 4.x，以及可正常連線 PS5 的 Sony 官方 PS Remote Play。
- 已安裝完整、相互匹配的 **usbip-win2 0.9.8.1 驅動、CLI 與 DLL**；程式使用 `C:\Program Files\USBip\usbip.exe` 與同目錄的 `libusbip.dll`。元件驗證失敗會停止接入。
- 完整解壓縮本程式包，保留 EXE、DLL、後端與其他隨附檔案，不要只取出單一 EXE。

本包不附帶驅動，也不會自動安裝、更換、降級或重啟驅動；新電腦需要先備妥上述環境。驅動說明見 [usbip-win2 0.9.8.1 上游版本頁](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1)。請保留已建立的 Remote Play 連線，不要為本程式關閉、重啟或更新 Sony Remote Play。

## 開始操作

1. 保持 Sony Remote Play 開啟，先正常退出其他版本的「上班不要玩」，避免同時接管 F8 或手把通道。
2. 開啟 `PSKeyboard-DualSensePreview.exe`，按 **「連接手把」**。程式不會在啟動時自動接入，鍵盤控制預設暫停。
3. 接入完成後切回官方 Remote Play，按 **F8** 啟用鍵盤；再按 F8 暫停。
4. 鍵盤與連點只在官方 Remote Play 位於前景時作用。切到其他視窗會清除按住狀態與連點；切回後重新按鍵才會操作，先前的切換連點不會自動恢復。若程式已暫停，先按 F8 啟用。
5. 要調整鍵位，按 **「按鍵與連點設定」**。編輯期間會暫停；關閉設定視窗後也保持暫停，切回 Remote Play 再按 F8。
6. 結束時使用程式的中斷按鈕或正常關閉視窗。若顯示清理未完全確認，請保留錯誤訊息並回報，不反覆建立裝置。

主畫面目前以繁體中文為主；按鍵設定視窗會依 Windows 顯示語言選擇支援的翻譯，未涵蓋的語言回退英文。這份英文手冊不代表整個程式介面已英文化。

## 預設鍵位

已有個人設定時會沿用設定，以下只適用於初次使用或按「恢復預設」並儲存後。**鍵盤方向鍵對應四個圖形按鈕；十字方向上／下是 Home／End。**

| PS5 手把動作 | 預設鍵 |
| --- | --- |
| 左搖桿上 | W |
| 左搖桿下 | S |
| 左搖桿左 | A |
| 左搖桿右 | D |
| 十字方向上 | Home |
| 十字方向下 | End |
| 十字方向左 | Insert |
| 十字方向右 | Page Up |
| × 叉鍵 | ↓ |
| ○ 圈鍵 | → |
| □ 方塊 | ← |
| △ 三角 | ↑ |
| L1 | E |
| R1 | Delete |
| L2 | Q |
| R2 | Page Down |
| Create（設定頁顯示 Share） | Tab |
| Options | Enter |
| L3（左搖桿按下） | F |
| R3（右搖桿按下） | 空白鍵 |
| PS | F1 |
| 觸控板按下 | R |

**F8 固定用於啟用／暫停，不能改綁遊戲動作。** 此版本沒有右搖桿方向或滑鼠瞄準映射；R3 是右搖桿按下，不是移動右搖桿。

## 改鍵、連點與設定保存

在設定視窗選取動作，按「按下新按鍵」再按想使用的鍵；也可雙擊該列開始設定。「清除按鍵」讓該動作不綁鍵。每個動作綁一個鍵，重複鍵及 F8 等保留鍵會被拒絕。

各動作可獨立選擇連點模式與間隔：

| 模式 | 行為 |
| --- | --- |
| 關閉（Off／0） | 一般按住與放開，不自動連點 |
| 按住連發（Hold） | 按住該鍵時連點，放開停止 |
| 按一下切換（Toggle） | 按一次開始，再按一次停止；切窗或 F8 暫停也會清除 |

間隔為 **20–5000 毫秒**，預設 **100 毫秒**。遊戲可能忽略過短的按下／放開脈衝，請依實際遊戲調整。「恢復預設」會在編輯草稿中恢復上表鍵位、關閉所有連點並把間隔設為 100 毫秒。

只有按 **「儲存設定」且儲存成功**才寫入並套用；取消或儲存失敗都保留原設定。設定檔位於 `%LOCALAPPDATA%\PSKeyboard\bindings.json`，更新程式時會沿用，不必隨程式搬移。請保留這個設定檔與自己的備份。若既有設定無法讀取，程式會提示並暫用預設值，不會自動覆寫原檔。

## 常見狀況

| 狀況 | 處理方式 |
| --- | --- |
| F8 或鍵盤擷取無法啟用 | 先正常退出其他版本的「上班不要玩」，再開本版；保留 Sony Remote Play。 |
| 找不到 USBip／0.9.8.1 元件驗證失敗 | 核對外部驅動、CLI、DLL 是否完整匹配；不要混用版本或為了測試降級現有驅動。程式不會代為安裝。 |
| 已接入但遊戲沒有反應 | 確認官方 Remote Play 在前景、鍵盤已啟用，並查看實際個人鍵位。綠燈只代表本機手把通道，不保證 PS5 已收到操作。 |
| 十字方向鍵不符預期 | 預設上／下為 Home／End、左／右為 Insert／Page Up；鍵盤方向鍵是 ×／○／□／△。 |
| 手把元件遺失或驗證失敗 | 從同一版本的下載包重新完整解壓，不混用不同版本的 EXE、DLL 或後端。 |
| 設定無法儲存 | 保留原檔與備份，檢查設定資料夾是否可寫或檔案是否被其他程式佔用；儲存失敗不會套用半套設定。 |
| 輸出中斷、通道占用或清理未確認 | 控制會停止；先查看是否仍開著其他版本。若重新接入被鎖住，保留畫面錯誤與版本資訊回報，不強制重試、刪裝置或重啟 Sony。 |

## 測試範圍

alpha.9 的四種候選已有使用者透過官方 Remote Play 操作 PS5 的成功回報，目前採用一般 DualSense 的純手把模式。**alpha.11 尚未取得該版使用者實機驗收**；Windows 認出裝置、綠燈或離線測試通過，都不等於遊戲已接受操作。長時間使用、各種重連、重新開機與新電腦安裝仍需更多驗證。

此版本沒有 30 分鐘使用期限。音訊、觸覺、自適應扳機、滑鼠、錄製／重播及 macOS 不列為本版已完成的功能。更多資訊見 [支援範圍](https://github.com/xiunlee/ps-remote-play-keyboard/blob/codex/commercial-preview/docs/PRODUCT-SCOPE.md)與 [alpha.9 操作紀錄](https://github.com/xiunlee/ps-remote-play-keyboard/blob/codex/commercial-preview/research/dualsense-preview/ALPHA9-TRIAL.md)。

## 回饋、贊助與授權

回饋請寄至 [xiun1019@gmail.com](mailto:xiun1019@gmail.com)，附上程式版本、畫面錯誤與重現步驟即可。程式的回饋按鈕只開啟郵件程式，預填收件人和版本主旨，不會自動寄信或附加個人設定、診斷資料；不需要提供 PSN 密碼或付款資料。

覺得好用的話，歡迎[透過 PayPal 自願贊助 US$1，幫三隻貓咪買罐頭](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)。點擊「贊助貓咪 US$1」才會開啟瀏覽器，由 PayPal 處理收款；本程式不接收或保存付款資料。全部功能仍可免費使用，沒有付費解鎖或訂閱。

原創程式作者為 **xiunlee**，採 [GPL 第 3 版，僅此版本](LICENSE)；第三方元件保留各自授權，見 [著作權說明](COPYRIGHT.md)與隨附 `notices` 目錄。[公開儲存庫](https://github.com/xiunlee/ps-remote-play-keyboard/tree/codex/commercial-preview)提供程式說明；對應原始碼與重建資料隨公開版本放在[同一發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases)。本專案與 Sony、PlayStation 或 PayPal 無隸屬或背書關係。
