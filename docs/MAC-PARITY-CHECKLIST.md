# Mac 功能對齊與整合安裝清單

更新：2026-10-30（Asia/Taipei）。「上班不要玩／Three Cats Keybridge」是鍵盤控制與模擬手把工具；目標是將 Windows 現行使用者功能帶到 Mac，並交付可自行完成準備、連接、停止與重連的單一 App。**Mac 0.1.3 的原生 helper 與完整 App 建置 9 已通過連接／停止／重連／退出清理，淺深色主畫面與設定已查看；本版遊戲、新機、睡眠及崩潰仍未驗證。公開交付明示這些限制，發行狀態以 GitHub 實際頁面為準。** 首版範圍為 Apple Silicon／macOS 15 以上、Sony 官方 Remote Play、單一模擬手把、免費使用。下表分開記錄程式、離線測試與實機驗收，不能互相替代。

Windows 比對基準為 [0.1.4 使用手冊](../packaging/README.zh-TW.md)、[現行主程式](../research/dualsense-preview/DualSensePreview.cs)、[設定編輯器](../src/physical-keyboard/BindingEditor.cs)、[BindingProfile](../src/physical-keyboard/BindingProfile.cs) 與九語 [UiText](../src/physical-keyboard/UiText.cs)／[PreviewText](../research/dualsense-preview/PreviewText.cs)。不以舊 physical relay 內部測試包或早期 prototype 當作目前產品。

本程式是獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係。新名稱及貓咪圖示的核對與建置 9 的既有證據分開記錄；不改寫歷史協定識別或試驗結果。

## Windows → Mac 功能對照

| 使用者功能 | Windows 現況 | Mac 對應與完成條件 |
| --- | --- | --- |
| 官方 Remote Play／免實體手把 | 自有虛擬 DualSense；Sony 負責登入與串流 | alpha.1 已有遊戲、W 放開停止、Options 成功回報；0.1.3 已驗原生 helper 與完整 App 的中立接入及清理，尚無遊戲驗收。保留官方 Sony 路線，不改成通用／Xbox 手把。 |
| 22 個動作 | 左搖桿四向、十字鍵四向、四面鍵、L1/R1/L2/R2、Create/Options、L3/R3、PS/觸控板按下 | `MacAction` 已有相同 22 動作。介面、設定與輸出維持同一動作順序；Mac 預設採 [精簡鍵位](MAC-DEFAULT-KEYS.md)，不複製 Windows 的 Home/End/Page 鍵需求。 |
| 單鍵改綁 | 點選或雙擊動作後擷取新鍵；每動作一鍵 | 原生編輯器、擷取、鍵名、清除及衝突提示已接入。較早實際擷取 W→U 後取消，確認沒有寫出設定檔；建置 9 另核對改鍵與 Hold 草稿取消後原設定 SHA-256 不變。使用 macOS 實體鍵碼，拒絕保留鍵及修飾鍵組合；`nil` 才是未指派，鍵碼 `0` 是 A。改鍵後遊戲輸出仍待整合驗收。 |
| 清除／恢復預設 | 清除單一動作；恢復預設只改編輯草稿 | 已有單項及全部還原；全部還原的介面同步問題已修正，實際核對 Mac 預設鍵位、連點 Off、100 ms。未儲存不套用草稿。 |
| 儲存／取消 | 草稿獨立；儲存成功才套用。取消、關閉及儲存失敗保留原設定 | `MacProfileStore` 與編輯器已接入，先原子儲存成功才套用；79 項 profile 測試通過。實際取消未寫檔，儲存 W 的 Hold 設定後讀回一致，且跨一次本機 App 建置更新保留。 |
| 編輯時暫停 | 開啟設定即清空輸出；儲存或取消後仍暫停 | 已接入編輯視窗生命週期與核心停止規則；擷取鍵不觸發視窗預設按鈕。實際連線遊戲中開啟／關閉設定的停止結果仍待驗。 |
| 每鍵連點 | Off／Hold／Toggle；20–5000 ms，預設 100 ms | 三模式、每動作間隔與持久化已接入；Hold 設定已實際儲存／讀回，全部還原回到 Off／100 ms。Hold 放開、Toggle 再按及切窗清空已有核心規則；0.1.3 遊戲內效果仍待驗，不保證每款遊戲接收最短脈衝。 |
| 暫停／切窗／快捷鍵 | F8 保留；失焦、編輯、斷線與系統快捷鍵會清除連點 | Mac 使用已選定的 Esc，僅 Remote Play 前景處理，其他 App 保留原用途。Command／Control／Option／Shift 清空輸出；切回後不恢復舊連點或仍按住的動作。Fn 不一律禁止，預設鍵位不需要 Fn。 |
| 設定載入／更新保留 | 使用者資料與程式分離；壞檔警告後暫用預設，不覆寫原檔；Windows 格式 1 可讀、儲存才升為格式 2 | 已實作 Mac 專用 `macOS-hardware-keycode`／schema 1 嚴格格式。缺檔使用預設；壞檔、未知版本、不合法數值保留原檔並警告。79 項測試包含非法輸入與保留原件；本機建置更新已確認設定保留。最終交付包更新仍待驗。 |
| 備份與復原 | 文件要求保留 `bindings.json`／既有 `profile-backups`；**現行 UI 沒有匯入、匯出或多 profile 切換** | 已有 `mac-bindings.json.backup`，保存上次儲存前的原始內容；測試核對備份及失敗時不覆寫。手冊列明更新／移除時保留方式；沒有宣稱自動復原或新增匯入／匯出、多 profile UI。 |
| 主畫面鍵位與狀態 | 按目前 profile 更新摘要；手把連接與鍵盤啟用分開顯示 | 已接上目前 profile 的完整鍵位、面鍵菱形、動態 Circle／Options 提示與可展開診斷；準備、暫停、錯誤及下一步有文字。就緒、前景與控制中狀態已接入程式，建置 9 已確認連接後「等待輸入」、中斷後可再次連接；未將此狀態當作 PS5 已接收。 |
| 連接／中斷／退出／再連接 | App 管理自有 backend 與 attach；清理不確定時禁止建立下一裝置 | 原生 supervisor、relay、guest helper、單一 session、取消與 journal 已實作，231 項 runtime 離線檢查與正式 helper 編譯通過。helper 8b 中立啟動及立即再次連接均成功到達 1 HID；停止後均為 0 HID、0 自有程序、無 journal、exit 0。完整 App 建置 9 也完成 1 HID 就緒→中斷後 0 HID／0 自有程序／無 journal→重連 1 HID→退出後再清理為 0。遊戲、睡眠與崩潰仍待驗。 |
| 使用時間 | 一般版沒有 30 分鐘試用期限 | 原生 relay 已移除 600 秒等待／7200 秒 session 上限並有離線檢查；保留階段期限、可取消等待及獨立 250 ms 失聯停止保護。這不代表已做長時間遊戲測試。 |
| 九語 | 繁中、簡中、英、日、韓、德、法、西、巴西葡語；跟隨系統，未支援語言回退英文 | 主畫面、設定、進度、錯誤、權限與支持區共 197 keys × 9 語，缺鍵與插值檢查通過；跟隨 macOS 第一偏好語言，未支援則英文。包含外觀選項及元件用途說明；建置 9 已實際檢視繁中淺／深色主畫面及設定，其他語系排版仍待核對。目前中文品牌「上班不要玩」，其餘「Three Cats Keybridge」；不另增 Windows 原本沒有的手動語言選單。 |
| 說明／限制／版本 | 主畫面操作說明及中英手冊；標示 2D/JRPG 定位、版本及真實驗證範圍 | 已有操作提示、關於／版本、元件授權及收合診斷；[繁中](MAC-INSTALL.md)／[英文](MAC-INSTALL.en.md)手冊涵蓋安裝、權限、更新與移除，並標示本版已驗證與未驗證範圍。App 尚無手冊按鈕；Windows 也沒有此按鈕，不列為功能對齊缺項。發行說明沿用固定格式，驗證範圍由安裝與驗收文件記錄。 |
| 免費／贊助／回饋 | 全部功能免費；固定 PayPal 自願 US$1；mailto 只含收件人及版本主旨 | 已接入九語介面；實際開啟 PayPal 頁核對 US$1。mailto 已結構檢查，未寄信；沒有自動付款、寄送或附加按鍵／設定／診斷。 |
| 品牌與圖示 | 既有品牌 PNG／ICO、主畫面與設定頁 | 美術優化已實作：共用淺深色與字型、平面鍵位指南、原生表格與動作細節、膠囊按鈕及外觀選單；新品牌採貓咪與三個按鍵圖示，已匯出 Mac `.icns` 並納入最終建置雜湊核對。建置 9 的淺／深色主畫面及設定已實際檢視，外觀恢復跟隨系統。Dock/Finder、其他 Retina／顯示環境與九語長文案仍待核對。 |

Windows 沒有的右搖桿方向／滑鼠視角、觸控滑動、音訊、震動、自適應扳機及錄製／重播，不因「功能對齊」而列為已支援或本輪必要開發。Windows 的 usbip-win2／.NET 也不是 Mac 安裝需求。

## 整合安裝採用的元件邊界

| 元件 | 執行時用途 | 交付方式 |
| --- | --- | --- |
| 原生 Swift App／supervisor／relay | UI、鍵盤、狀態機、VZ 控制、frame 轉送與停止保護 | 已納入同一固定 App 及簽章身分，以編譯好的 Swift 模組／helper 取代研究 Python relay；使用者不安裝 Python。獨立 helper 與完整 App 建置 9 的中立啟動／停止／再次連接已成功，App 退出清理亦已確認；遊戲及新機仍待驗收。 |
| macOS Virtualization framework | 啟動我們準備的 Linux guest、VSOCK 與唯讀資源分享 | macOS 內建；由隨 App 編譯的 `GuestRunner.swift` helper 管理，不需 Docker、Parallels、另裝 VM 管理軟體或另一台電腦。 |
| Linux guest／USB-IP／socat／VIIPER backend | 在 guest 中建立 DualSense 與 USB 通路 | 已以固定版本納入 App 資源；對應來源 archive、notices、固定建置輸入 archive 與公開重建腳本已備妥。最終包仍需逐項核對並同時交付，不把原始 `.local` 研究目錄直接打包。 |
| VirtualHere Generic ARM64 Server | 在 guest 中轉送自有 USB 裝置 | 官方原件取得，放進 App 管理的 runtime 資料區供 guest 使用；不改 binary、授權檢查或 notices。 |
| VirtualHere Mac Client | 將該裝置接到 macOS | 管理目錄缺件時下載官方完整已簽章 App；本機首次新下載與驗簽已成功。保持原始簽章與名稱，不自行重簽；免費路線以正常使用者 App 執行，不安裝為 service，也不接管其他位置的安裝。 |
| Sony PS Remote Play | PS5 登入、連線與串流 | 使用者既有官方 App；本工具不打包 Sony、不儲存帳號資料，也不為連接／重連重啟 Sony。 |
| Swift／Go／建置用 Python／Xcode 工具 | 建置我們的程式與 guest 資源 | 僅開發端使用。整合 App 的執行路徑已不呼叫 Python relay 或編譯器；最終包仍須在無開發目錄的環境驗收。 |

「單一 App」指玩家由一個入口準備和操作；VirtualHere 仍是有獨立授權的第三方元件。首次準備可能下載元件並需要本人完成 macOS 權限，後續啟動不可再要求終端指令、ChatGPT 或開發目錄。受管理的官方原件放在 `~/Library/Application Support/PSKeyboard/Dependencies`，更新保留可驗證的元件，不為每個版本新增一份 App。

## VirtualHere 原件取得與再散布決策

以下於 2026-10-30 核對官方現行頁面；版本與條款須在封版時再核對實際下載物。

- **Mac Client 可免費使用，但不是開源元件。** 官方 [Client License v1](https://www.virtualhere.com/node/316) 允許不限電腦數安裝與使用、不收費；限制修改、衍生作品、移除 notices，並明列不得 “resell, rent, lease, transfer rights to the Software”。頁面下方 LZ4／OpenSSL 的再散布條款屬各該元件，不能當成整個 VirtualHere Client 的再散布授權。
- **Generic Server 可單裝置免費使用。** 官方 [Server License v2](https://www.virtualhere.com/embedded_server_license) 允許每 server 分享一個 USB 裝置、不收費；同樣列有修改、衍生作品與轉售／轉讓等限制。[下載頁](https://www.virtualhere.com/usb_server_software) 說明 Generic 單裝置沒有時間限制；多裝置、CPU optimized build 或轉售用途另有條件。維持固定 guest 識別與設定，不藉重建 guest 重設授權。
- **選擇首啟官方下載，暫不內附 VH 二進位。** 上述公開條款未明確授予本專案重新散布整個 VH 產品的權利；這不等於斷言任何免費散布一律被禁止。為避免把免費使用推論成改包／再散布授權，首版採官方原件下載，重用本程式管理目錄中通過核對的既有元件；沒有選取其他安裝的 UI。若日後要直接隨 App／DMG 內附，須先取得適用的明確授權。現階段不承諾完全離線、所有元件內附的安裝包。
- **免費條件影響 supervisor 設計。** [官方 client service 說明](https://www.virtualhere.com/client_service) 明確要求付費 server 才能使用 service 模式；本產品以登入中的正常 App 管理生命週期。依 [官方設定文件](https://www.virtualhere.com/configuration_faq)，更改 `TCPPort` 屬付費進階設定，因此保留 7575，衝突時提供可處理錯誤，不改埠繞過條件。
- **官方取得及本機完整 App 連線已核對。** App 的元件說明列出用途與授權，按連接才開始準備；固定 Client 6.0.3／Generic Server 4.8.8 與 SHA-256，Mac 原件另驗簽。Client 新下載及驗簽已實測成功。不同內容不靜默升級；已損壞的自有元件先下載、驗證替換檔，再以可回復方式更換，不修改無關安裝。重試、取消、逾時與 bounded repair 已實作及隔離測試；完整下載失敗／修復流程的乾淨環境體驗仍待驗。來源為 [Mac Client](https://www.virtualhere.com/usb_client_software) 與 [Generic ARM64 Server](https://www.virtualhere.com/usb_server_software)。

我們的 GPL 程式與可再散布的 guest 材料可隨包提供，前提是依實際元件授權附 notices 與完整對應來源、patch、設定和建置／安裝腳本；僅放上游首頁或 binary hash 不夠。Linux kernel／BusyBox／socat／VIIPER 及連結依賴逐項盤點，不能統稱全部適用同一授權。依據：[GPLv2 第 3 節](https://www.gnu.org/licenses/old-licenses/gpl-2.0.en.html)、[GPLv3 第 6 節](https://www.gnu.org/licenses/gpl.en.html#section6)。專案私鑰、keychain、作者信任設定及個人 runtime 識別不進公開包。

## 公開交付與後續驗證

本次公開不以補完遊戲、新機及異常情境為前置條件；以下保留實際未驗項目，不因公開而改寫為通過。

1. **固定公開產物與封裝證據。** 建置 9 的 App 連接、中斷、重連、退出清理及淺深色介面已核對；最終品牌包的 ZIP／DMG 簽章、內容及來源對應已另驗通過；交付包啟動流程仍待實測。維持擁有權界線，不把中立連線成功當作本版遊戲驗收。
2. **持續補實機驗證。** 同一 App 正常退出後重開、連接、中斷再連接；基本遊戲、放開停止、Options、改鍵及連點；在有裝置的情況核對退出／失敗／睡眠清理，不重啟 Sony。
3. **追蹤新機安裝與更新。** 新 Mac 或新使用者環境的首次開啟、Gatekeeper、AX、官方元件取得及無開發目錄自啟；以最終包覆核 AX、profile／備份與固定 RuntimeState 保留。目前只有本機設定更新保留及歷史 AX 更新證據。
4. **定稿文件與視覺。** 核對圖示、深色及九語長文字，補最終截圖與真實驗證範圍；對外 README、手冊與版本資訊一致。母語校閱與更多顯示環境可持續追蹤，不把尚未查看的畫面標為已驗。
5. **固定產物再交付。** 最終品牌版 0.1.3／build 4 已重新產生 App ZIP／DMG、70 檔白名單專案來源、runtime 對應來源與建置輸入，簽章、版本、完整內容及 SHA-256 已核對；ZIP 已重解、DMG 已唯讀掛載比對內部 App，另有 151 項獨立稽核通過。最終來源固定為 `28084359384b11d43272ad17f4207ae58b27f933`，建置 9 收據保留為歷史。已於 **2026-10-30 18:52（台灣時間）**透過獨立 [mac-v0.1.3 發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/mac-v0.1.3)交付八個附件，Windows 0.1.4 及既有標籤／附件保持不變；八個公開附件已匿名重新下載，大小與 SHA-256 均符合最終包及 GitHub digest；[下載驗證收據](../packaging/build-records/macos/0.1.3/PUBLIC-DOWNLOADS.json)。細項見 [Mac 發佈準備清單](MAC-RELEASE-READINESS.md)。
