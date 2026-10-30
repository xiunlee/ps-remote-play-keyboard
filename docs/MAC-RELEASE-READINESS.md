# Mac 發佈準備清單

更新：2026-10-30（Asia/Taipei）。**本文件記錄「上班不要玩／Three Cats Keybridge」Mac 0.1.3 的公開交付範圍：原生 helper 及完整 App 建置 9 的連接、停止、再次連接與退出清理已通過，淺深色主畫面／設定已實際查看；本版遊戲、全新 Mac、睡眠及崩潰情境仍未驗證。** 本次公開保留這些已知限制，不以發布取代驗收。實際發布狀態與時間以 [GitHub 發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/mac-v0.1.3)為準；Windows 既有產物不覆寫。

本文件的勾選表示該列明確範圍內的完成狀態；程式存在、離線測試通過、既有研究流程的遊戲成功，不代表最終整合包已通過驗收。Windows 功能逐項對照見 [功能對齊清單](MAC-PARITY-CHECKLIST.md)；建置 9 的固定證據見 [BUILD](../packaging/build-records/macos/0.1.3/BUILD.json)及 [INTEGRATION](../packaging/build-records/macos/0.1.3/INTEGRATION.json)。

## 最終品牌建置的離線覆核

Mac manifest 為 **0.1.3／build 4**，根目錄 Windows `VERSION` 維持 **0.1.4**；兩平台版本獨立。Mac 保持 **Esc**，Windows 保持 **F8**。最終主程式、貓咪 `.icns`、九語資源、兩個原生 helper 與 runtime 材料已重新建置並驗簽；[FINAL-BUILD](../packaging/build-records/macos/0.1.3/FINAL-BUILD.json) 與舊 [BRANDING-BUILD](../packaging/build-records/macos/0.1.3/BRANDING-BUILD.json)、[PACKAGE](../packaging/build-records/macos/0.1.3/PACKAGE.json) 分開保留。

[FINAL-VERIFICATION](../packaging/build-records/macos/0.1.3/FINAL-VERIFICATION.json) 記錄本輪實際重跑：控制核心 459、profile 79、V5 22958、relay socket 57、runtime 231 項全部通過；另通過依賴修復／回復／取消／自有程序清理檢查，以及平台版號 3 個測試方法、9 種情境。九語共 197 keys、94 個介面字串引用、各語品牌及非官方聲明均通過。來源白名單由 69 檔增為 70 檔，新增的是平台版本隔離測試；所有建置輸入雜湊與目前來源一致。Windows 端先前回報的 20 項研究檢查保持為外部既有證據，未計入本輪重跑。

本輪未安裝或啟動 App，未建立虛擬裝置、擷取按鍵、修改個人設定或操作 Sony。最終 ZIP／DMG 必須以這份新建置重新產生並重新驗證；建置 9 的 PACKAGE 不是這份最終包的證據。公開附件及匿名下載只在實際完成後記錄。

## 範圍與現有證據

首版以 **Apple Silicon、macOS 15 以上、Sony 官方 Remote Play、單一模擬手把輸出**為範圍，延續免費開源與 2D／選單方向操作 JRPG 的定位。本程式是鍵盤控制與模擬手把工具、獨立專案，非 Sony 官方產品，與 Sony 無隸屬或背書關係。最低系統版本是建置條件，並非所有系統與 Remote Play 版本的相容性證明。

固定 App 為 `~/Applications/PSKeyboardMacDualSense.app`，bundle identifier 為 `com.xiunlee.pskeyboard.mac-dualsense`；以版號迭代，不另裝不同名稱的試驗 App。使用者不需 Apple 開發者帳號，但目前作者自簽產物並非 Developer ID 簽章，也未經 Apple 公證；全新環境的正常 Gatekeeper 開啟流程仍待驗證。

| 範圍 | 已取得的證據 | 不代表的結果 |
| --- | --- | --- |
| 0.1.0-alpha.1 遊戲 | 使用者確認無實體手把時遊戲可動、W 放開停止、Enter／Options 有效，收尾 Mac gamepad 為 `[]`；詳見 [trial 2](../research/macos-native/DUALSENSE-TRIAL-2.json)。 | 不是 0.1.3 自動啟動、連點、改鍵或長時間遊戲驗收。 |
| alpha.1 → 0.1.2 本機更新 | 同一憑證／路徑更新，App 診斷 `AX=true`，未新增授權；[建置收據](../packaging/build-records/macos/0.1.2.json)、[UI 證據](../research/macos-native/inspection/MAC-0.1.2-UI.json)。 | 不代替最終包更新或全新 Mac 權限測試。 |
| 0.1.3 設定與介面 | 實際 W→U 後取消，沒有寫出設定檔；W 的 Hold 設定儲存／讀回一致並跨一次建置更新保留。全部還原修正後，實際回到預設鍵位、Off、100 ms。建置 9 已查看淺／深色主畫面及設定；W→U 與 Hold 草稿取消後，儲存設定的 SHA-256 完全不變；外觀恢復跟隨系統，AX 仍允許且未重授權。 | 未在 0.1.3 遊戲中驗證改鍵、連點或設定期間停止輸出；不代替全新 Mac 權限測試。 |
| 0.1.3 依賴與原生 runtime | 首次連接已成功從官方新下載 VH Client 並驗簽。helper 8b 的中立啟動與立即再次連接均到達 1 個自有 HID；兩次停止後均為 0 HID、0 自有程序、無 journal、exit 0。 | 這是獨立 helper 的中立輸出驗證；完整 App 結果另列，兩者都不代替遊戲或全新 Mac 安裝驗收。 |
| 0.1.3 完整 App 建置 9 | 從 App 連接至「等待輸入」時 1 HID；中斷後回到可連接狀態，0 HID、0 自有程序、無 journal。再次連接為 1 HID；Cmd+Q 退出後再次獨立確認 0 HID、0 自有程序、無 journal。 | 未送出遊戲操作，尚無本版 PS5 接收／改鍵／連點或睡眠／崩潰實測結論。 |

## 已實作與已核對的功能

- [x] 同一 App 納入原生 supervisor、Swift relay、VZ guest helper 與固定 guest 資源；執行路徑不再使用研究 Python relay，使用者不需 Python／Go／Swift／Docker／Parallels。自啟實機結果仍依下節判定。
- [x] guest 初始化／停止、階段期限、取消、單一 session、擁有權 journal、子程序及 HID 清理已實作；不接管其他 VirtualHere 或重啟 Sony。**231 項 runtime 離線檢查通過**，production runtime／guest 編譯與 App controller typecheck 通過；實機成功範圍另依 helper 8b 紀錄。
- [x] 原生 relay 移除研究用 600 秒等待／7200 秒 session 上限；保留獨立 250 ms 失聯停止、frame／CRC／sequence 驗證及 neutral／EOF 清理。離線測試確認無總 session 上限。
- [x] 官方 Client 6.0.3／Generic ARM64 Server 4.8.8 的固定下載、SHA-256、Client 簽章驗證、取消與有期限的修復流程已接入；Client 新下載／驗簽實測成功。修復只處理本程式 `Dependencies`，先驗證替換檔再更換，失敗可回復原件。
- [x] 同一 App 保留固定 runtime identity／vendor 設定；檔案驗證失敗不輪換識別或改第三方授權。免費單裝置、一般使用者 Client、預設 7575 埠的邊界保持不變。
- [x] 22 動作自訂單鍵、清除、單項／全部還原、儲存／取消及衝突提示已接入。鍵位與每鍵連點原子儲存、上一份備份、壞檔保留與平台／版本檢查已實作，**79 項 profile 測試通過**。
- [x] Off／Hold／Toggle 與 20–5000 ms 間隔接入設定；預設 Off／100 ms。編輯、暫停、切窗及失聯清空輸出的規則已接入；遊戲結果仍待整合驗收。
- [x] 主畫面讀取目前 profile、同步鍵位指南與 Circle／Options 提示；Esc 固定保留。狀態及錯誤有文字，詳細彙總診斷可收合，不將本機通道狀態宣稱為 PS5 已接收。
- [x] 最終品牌版九語共 197 keys 的缺鍵、插值及語系選擇檢查通過；主畫面、設定、進度、權限、錯誤、關於、支持、外觀選項與元件用途說明已接入。建置 9 已查看繁中淺／深色主畫面及設定；其他語系與顯示環境仍待核對。
- [x] 美術優化已實作：共用淺深色與字型、平面鍵位指南、原生設定表格及選取動作細節、膠囊按鈕與細分隔線；外觀可跟隨系統或選擇淺色／深色。程式 typecheck 已通過，建置 9 的淺／深色主畫面與設定已實際檢視。最終品牌版使用「上班不要玩／Three Cats Keybridge」及貓咪圖示；最終建置的簽章、資源及來源雜湊另存 [FINAL-BUILD](../packaging/build-records/macos/0.1.3/FINAL-BUILD.json)，不沿用建置 9 的舊品牌畫面作證。
- [x] 關於／版本、元件授權、回饋及既有贊助入口已接入；實際 PayPal 頁確認 US$1。mailto 僅做結構檢查，未寄出郵件；不自動附加個人設定、按鍵或診斷。

實作入口：[主 App](../src/macos/DualSensePreviewApp.swift)、[執行管理](../src/macos/MacRuntimeController.swift)、[runtime 說明](../src/macos/runtime/README.md)、[依賴準備](../src/macos/MacDependencyInstaller.swift)、[設定儲存](../src/macos/MacProfileStore.swift)。

## 已備妥的封裝、來源與文件

- [x] 完整對應來源、第三方 notices、固定版本／patch／設定及重建腳本已整理；runtime source archive 為 450,919,582 bytes，另有 115,775,397 bytes 的固定 build-input archive。詳細內容與 SHA-256 見 [runtime 發佈材料](MAC-RUNTIME-MATERIALS.md)。這些是分開交付的開發材料，不重複放入 App。
- [x] 公開 runtime 材料取得腳本已提供固定官方輸入下載或固定 archive 重用；不依賴作者原本的 cache。固定 rootfs／APK 到 Image／initrd 的重現與來源完整性已核對，並非宣稱全部 Alpine 套件均已重新編譯。
- [x] [封裝程式](../packaging/macos/package_release.py) 已建立 ZIP／DMG、白名單專案來源 ZIP、獨立 runtime source archive、選配 build-input archive、SHA256SUMS 與 RELEASE.json 的流程，包含重新取出 ZIP 與實際唯讀掛載 DMG 後的 App 簽章／內容比對。**已以建置 9 完成封裝驗證；ZIP 解壓及唯讀掛載 DMG 後的 App 簽章與全部檔案雜湊均通過。** 來源 ZIP、runtime／build-input archive、版本與白名單也已核對；各交付檔的大小與 SHA-256 以同包 `RELEASE.json`／`SHA256SUMS` 為準。後續品牌建置與最終附件須重新核對簽章、來源對應及雜湊，公開下載狀態另由發行頁記錄。
- [x] [繁中安裝手冊](MAC-INSTALL.md)與[英文安裝手冊](MAC-INSTALL.en.md)涵蓋固定路徑、權限、使用、更新與移除，標示未公證、已驗證範圍與未完成新機／遊戲驗收的限制。使用者流程不要求匯入作者憑證或執行作者簽章設定。
- [x] 封裝白名單排除作者私鑰、keychain、私人簽章設定、個人 runtime 識別、vendor 設定與原始研究紀錄；VirtualHere 二進位不內附，仍向官方取得。輸出已依白名單核對；不將個人設定、簽章私鑰或元件設定加入來源／App 封裝。

## 驗證追蹤與發布工作

以下未勾選的遊戲、新機與異常情境是已揭露的後續驗證項目，不再作為本次公開的前置條件；仍不得標為通過。封裝、來源對應與公開附件完整性則按實際執行結果記錄。

### 自動啟動與控制的已驗證範圍及限制

- [x] 原生 helper 已自行完成 guest、backend、VirtualHere 與 Mac HID 接入；helper 8b 中立啟動及立即再次連接均確認 1 個自有 HID，停止後 0 HID、0 自有程序、無 journal、exit 0。
- [x] 完整 App 建置 9 由連接按鈕自行完成通路，顯示「等待輸入」且有 1 個自有 HID；中斷後回到可連接狀態並清理完成。本輪沒有送出遊戲操作。
- [x] 完整 App 建置 9 中斷後再次連接成功；隨後 Cmd+Q 退出，另行確認 HID 及自有程序均為 0、無 journal，未重啟 Sony。
- [ ] 封裝後從交付包正常重開 App，覆核首次連接及完整退出流程。
- [ ] 在裝置已接入的情況驗證退出、取消、helper 失敗與睡眠的停止／清理；核對自有 HID 消失、子程序結束、journal 清除且其他裝置不變。先前啟動失敗清理、helper 正常停止，以及完整 App 中斷／退出均已驗；取消啟動、崩潰與睡眠仍須分別確認。
- [ ] 以 0.1.3 最終整合版本做基本遊戲、放開停止、Options、一次改鍵及 Hold／Toggle 連點確認；核對設定、切窗、Esc 與失連會清空輸出。

### 安裝、更新與封裝的已驗證範圍及限制

- [ ] 在全新 Mac 或新使用者環境驗證最終包首次開啟與正常 Gatekeeper 流程、AX 權限引導、官方元件取得及首次連線；不要求關閉 SIP／AMFI、刪除 quarantine 或修改 TCC。
- [ ] 核對缺件、下載／驗證失敗、權限未授予與元件損壞時的可恢復指引；隔離測試通過不代替一般使用者的安裝流程。
- [ ] 以最終交付包覆核固定身分更新、AX、profile／備份與 RuntimeState 保留。歷史 AX 更新和目前 profile 更新證據分開保留，不合併宣稱已完成最終包驗收。
- [x] 已完成建置 9 封裝驗證，核對 App／ZIP／唯讀掛載 DMG 的簽章、內容、版本、大小、SHA-256 與來源材料對應；不含私密資料或 Sony／VH 二進位，來源 tar 獨立交付。
- [ ] 用實際最終產物驗證安裝／更新／移除手冊，關閉並等清理完成才替換 App；移除僅處理自有 App／資料，設定保留有清楚選擇。

### 介面與公開資料

- [x] 建置 9 已實際查看淺／深色主畫面及設定；改鍵與 Hold 草稿取消保留原設定，外觀已恢復跟隨系統。
- [ ] 核對 Dock／Finder 圖示、長文案與九語關鍵操作布局。其他 Retina／字體／顯示設定及母語校閱須保留未驗範圍，不把翻譯缺鍵檢查當作視覺驗收。
- [ ] 產生最終主畫面與設定頁截圖，定稿中英文 README、支援系統／Remote Play 版本、限制、依賴與發行說明，移除過時的 Mac 尚未開發描述。App 內手冊按鈕不是既有 Windows 功能，不阻擋本輪對齊；安裝包必須附手冊。
- [ ] 使用獨立標籤 `mac-v0.1.3` 與 `ThreeCatsKeybridge-Mac-0.1.3-…` 附件名稱封版；建立本機版號不等於已公開發佈。**不得重用或移動 Windows 已有 `v0.1.0`／`v0.1.1`／`v0.1.2` 標籤，也不得覆寫其附件，或與 Windows 已發布的 `v0.1.4` 衝突。** Mac 與 Windows 分平台保留版本，Mac 首次發布不將 Windows Release 轉為草稿。
- [ ] 來源與產物固定、已知限制明示後建立 Release、上傳附件，並以公開下載重新核對大小及 SHA-256；只有實際完成才標記已發布。

## 後續項目與下一步

右搖桿／滑鼠視角、觸控滑動、音訊、震動／自適應扳機、錄製／重播、其他手把中繼、Intel Mac 與較舊 macOS 不是本次首版前提，也不能列入已支援功能。更多遊戲、多螢幕、長時間壓力及更多系統版本可分批驗證，公開說明須保持實際範圍。

建置 9 的封裝與簽章核對已完成；本次公開須固定後續品牌建置與對應附件，明示新機、遊戲及異常情境未驗，並核對最終公開附件。交付包首次開啟、本版遊戲及乾淨環境驗證繼續追蹤，不作為本次公開的前置條件。已完成的設定、原生 helper、翻譯與來源材料保留；完整 App 的中立連接及清理成功也不代替遊戲及安裝驗收。
