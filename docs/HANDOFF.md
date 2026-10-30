# 上班不要玩：總進度與換串交接

更新日期：2026-10-30（Asia/Taipei）。這份文件是下一串的入口，將已實測、離線驗證與尚未完成的工作分開記錄。閱讀後可直接繼續已授權的開發與隔離研究，不必重新詢問已決定的產品方向。

## 1. 目前結論與接續目標

- **公開下載保留政策（2026-10-30）：**依使用者要求，對外僅保留最新版本 v0.1.2 與上一版 v0.1.1。v0.1.0、alpha.12、alpha.11 的 GitHub Release 已轉為草稿，原附件與草稿保留；舊版本標籤未刪除或移動，仍可公開瀏覽原始碼歷史。其歷史發行紀錄中的公開下載狀態是當時結果，舊附件連結現不對外。以後完成新版本後，保留新版與緊接的上一個正式版公開，其餘依此方式保留為草稿。
- **發行頁格式：**每版沿用[中英文發布說明範本](RELEASE-NOTES-TEMPLATE.md)的功能、注意事項與系統需求；更新「此版本更新」的內容和發布時間。0.1.2 更新內容僅寫「美術優化」／「Visual improvements」，不提採用的工具或設計標準。固定內容只有在功能或需求實際變動時才修正，避免資訊過時。
- **2026-10-30 最新 Windows 版本：v0.1.2，更新說明為「美術優化」。** [正式發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.2)，來源 `da12240649df74dc4e2846404317e8a91b231a05`，四附件匿名下載核對完成。6912 項 C#、5743 項 native、ABI 與 40／40 公開包稽核通過；九語主畫面已目視，本版逐項 PS5 實機仍待驗。Windows 原始碼包排除不相關 Mac 研究；Mac 工作仍依 [最新研究](../research/macos-native/README.md)接續。九語畫面及實際細節見 [v0.1.2 紀錄](../research/dualsense-preview/RELEASE-0.1.2.md)。
- 本輪移除 11 項舊版及建置暫存，共 848,558,583 logical bytes、失敗 0；`versions` 與 `versioned-archives` 僅保留 0.1.2，個人設定／備份、驅動、Sony 與舊標籤前後核對一致。下文的 0.1.1 交付與清理數據屬當時歷史。本版「此版本更新」只寫「美術優化」；以後依該版實際變動更新此段，不標示採用的工具或設計標準。
- 產品主線是 **Windows、免實體手把、鍵盤映射、Sony 官方 PS Remote Play**。中文名稱是「上班不要玩」，非中文語系統一為 **PS Remote Play Keyboard**。定位 2D 遊戲，尤其選單與方向操作為主的 JRPG；不適合需要靈活視角或精細類比控制的 3D 遊戲，不保證所有 JRPG 都適合。
- **最新發布方向：維持公開、免費 GPL-3.0-only 開源，作者 xiunlee。** 所有功能免費，原商售、付費解鎖與 DRM 規劃已被此指示取代；根目錄 [LICENSE](../LICENSE)與 [COPYRIGHT.md](../COPYRIGHT.md)已新增。
- **模擬 PS4 DUALSHOCK 4 已能控制 PS5**：alpha.5 有拔除實體手把與一次 Windows 重開機後的使用者成功回報；alpha.6 PS4 模式亦由使用者確認正常。不是只能操控 PS4 主機。
- **alpha.9 DualSense 已有官方 Remote Play 控制 PS5 的使用者成功回報**：使用者表示「四種都可以 保留第二種就好了」，指定一般 DualSense／純手把 `dualsensegamepadv5`。見 [alpha.9 實機補記](../research/dualsense-preview/ALPHA9-TRIAL.md)。這不是由 Windows 辨識或 CI 推論，亦不代表逐鍵、重開機、長時間或音訊／觸覺全部驗收。
- alpha.6 原版 DualSense 在 Windows 可建立裝置但 Sony 無反應，屬於**歷史失敗**；切回同版 PS4 可操作。alpha.9 成功仍不足以定位原版失敗的唯一根因。alpha.8 是先前 PS4 品牌介面版，完成白色幾何 PS 圖示與 `#D62355` 配色。
- 使用者接續要求「把我們的按鍵設定 連點 那些功能都放進來吧，就用這版本」。**0.1.0-alpha.10 已完成乾淨建置、本機交付及離線驗證**，固定第二種模式、恢復完整按鍵／連點設定並取消 30 分鐘自動結束限制；[交付紀錄](../research/dualsense-preview/ALPHA10-TRIAL.md)。alpha.10／11 固定標籤均已推送並核對；沒有把 alpha.10 覆寫成下一版。
- **0.1.0-alpha.11 已免費公開發布並本機交付**：標籤 `v0.1.0-alpha.11` 固定來源 `6dbc154a6a887398796d442fd34b73455672aba3`，[GitHub pre-release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11) ID `427698167`，四個附件公開下載後雜湊全數相同。801 項 C#、5743 項 native、ABI／Go 純測試及 27 項公開包稽核通過；兩張離線 UI 已目視。三個執行元件均為 NotSigned，**本版仍未取得使用者實機驗收**，沒有代使用者啟動一般模式。完整證據見 [alpha.11 發布紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)。
- **0.1.0-alpha.12 九語介面版已公開並本機交付**：固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`，[GitHub pre-release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.12) ID `427756025`。主介面 50 鍵與共用設定頁 93 鍵均支援九語；5816 項 C#、5743 項 native 及 ABI 檢查、33／33 項公開包稽核通過，18 張最終離線圖片全部目視。四附件匿名下載雜湊全數相同；桌面入口已交付，沒有代使用者啟動一般模式。後端沿用 r4 與先前 Go 4 個頂層測試／2 個子情境證據；三元件仍為 NotSigned。詳見 [alpha.12 發布紀錄](../research/dualsense-preview/ALPHA12-TRIAL.md)。
- **v0.1.0 首個正式發行版已公開並本機交付**：固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`，[正式發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0) ID `427775269`，`prerelease=false`，發布時以 `make_latest=true` 設為最新，當時 latest API 與四附件匿名下載雜湊均已核對。5834 項 C#、5743 項 native／ABI、67 項版號純邏輯檢查及 35／35 項公開包稽核通過；九張新版主畫面已目視，九張設定頁與已審閱 alpha.12 圖片 SHA 相同。三元件仍未簽章；本機程式、封存及桌面入口已交付，沒有代使用者啟動。詳見 [正式發布紀錄](../research/dualsense-preview/RELEASE-0.1.0.md)。
- 使用者要求對外只保留一組版號，之後依 **V0.1.0 → V0.1.1 → … → V0.1.9 → V0.2.0** 遞增，不再附 alpha；Git 標籤使用小寫 `v`。下一個變更版本為 **0.1.3**；去除 alpha 不代表逐項實機、長時間或新電腦已驗收，歷史附件／標籤保持不變。
- **歷史 v0.1.1 國際化品牌修正版已發布、交付並完成清理**：固定來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`，[正式發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.1) ID `427807747`，`prerelease=false` 發布時為 GitHub latest。6912 項 C#、5743 項 native／ABI、39／39 項公開包稽核通過，四附件匿名下載雜湊一致。九張最終主畫面目視完成；九張設定頁與已審閱 v0.1.0 圖片 SHA 相同，開發階段另有 11 個排版情境、0 項問題。後端沿用 r4 及先前 Go 證據、未重跑 Go；三元件仍未簽章。本輪沒有新增使用者實機回報。詳見 [RELEASE-0.1.1.md](../research/dualsense-preview/RELEASE-0.1.1.md)。
- 中文語系保留「上班不要玩」，其餘七個語系統一為 **PS Remote Play Keyboard**，涵蓋主畫面、設定頁、說明、回饋主旨及英文文件；不覆寫 v0.1.0。
- **單款遊戲回報**：2026-10-29，使用者確認《Final Fantasy Resonance》可正常操作；未指定本工具 build，不視為 v0.1.1 逐鍵、改鍵或連點驗收，不擴張為所有 JRPG 相容性保證。
- 最新唯一贊助入口為 [PayPal](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)，自願 US$1 幫三隻貓咪買罐頭；僅點擊後開瀏覽器，本程式不接收付款資料。公開頁設定已唯讀核對為啟用、USD 1.00，未實付驗收入帳。回饋固定 `xiun1019@gmail.com`，不自動寄信。
- **Windows v0.1.1 收尾完成；Mac 免費 OJD 路線已有中立裝置證據（2026-10-30）**：首輪 macOS 26.6.2 arm64／官方 Remote Play 9.5.0 唯讀盤點、native 5743 項及 124 項參考協定檢查保留於 [首輪紀錄](../research/macos-handoff/README.md)。後續 Swift core 426、USB encoder 4294、OJD adapter 205、pipe 32 項通過；免費已簽章 OJD Generic 全中立裝置建立與 EOF 後移除已確認。固定鍵位研究前端已建置，使用者 Generic 遊戲試驗回報無反應；已清理裝置並停止 OJD，後續只查真正 DualSense／虛擬 USB 免費輸出；見 [Mac 最新研究](../research/macos-native/README.md)與 [交接](MAC-HANDOFF.md)。Windows 實機待驗獨立保留。
- 使用者因空間有限授權「舊版本在本地端都清除吧，就留下最新版本」，取代先前本機保留多版本的規則。歷史 v0.1.0 清理累計 118 項、5,589,691,996 logical bytes；本輪 **v0.1.1 另完成 9 項、753,491,748 logical bytes，失敗 0 項**。這些包含重建暫存，均不是淨磁碟差；本輪 101 項略過中，99 項原已不存在、2 份小型 log 刻意保留。`versions` 與 `versioned-archives` 都只剩 0.1.1，個人設定／備份、驅動、Sony 及 Git 標籤前後比對通過。
- **隔離研究已完成兩階段**：[V5 使用者空間驗證](../research/dualsense-v5-test/RESULTS-20261029.md) 通過並修正觸控 inactive 差異；後續[四方案的 0.9.8.1 hosted Windows HID 驗證](../research/dualsense-driver-test/RESULTS-20261029.md) 亦全部通過。原候選正式 EXE 的啟動限制與舊 native ABI 未改，候選程式使用另寫的受限入口及匹配 CLI。**Windows Server HID 成功不等於本機 Windows 11／Sony 操作成功。**
- 使用者進一步明確要求：**沿用目前 0.9.8.1，不降級，不影響能用的 PS4 與 Remote Play**。新增 [獨立 Windows 驅動研究](../research/dualsense-driver-test/README.md)，把接入測試安排在一次性的 GitHub hosted Windows VM；不得把該安裝／接入流程搬到使用者電腦執行。

## 2. GitHub、版本與哪些東西沒有公開

儲存庫：<https://github.com/xiunlee/ps-remote-play-keyboard>，已確認**維持公開**。GitHub 預設及目前工作分支均為 **`codex/commercial-preview`**，名稱沿用歷史，不代表收費計畫。使用者指出公開首頁仍顯示舊資訊後，已調整預設分支並 GET 核對；`main` 保留歷史，未合併或覆寫，舊標籤未移動。

| 基準 | 原始碼提交 | 證據／用途 |
| --- | --- | --- |
| `v0.1.0-alpha.5` | `4e4b8a3` | 首次免手把成功；事後命名，原執行檔未內嵌版本號 |
| `v0.1.0-alpha.6` | `15fc0d35ae93737317684fc33920f6f8c7dc5a1c` | 加入版本控管與 DualSense 實驗；PS4 通過、PS5 操作未通過 |
| `v0.1.0-alpha.7` | `aaef2da98e6c1e8844a0561124f26480e3f68b81` | 人體工學預設鍵位、保留使用者設定 |
| `v0.1.0-alpha.8` | `a6c5c2a99ed32709287000319ec0f44f95254847` | 圖示與 UI；後端及控制規則未改 |
| `v0.1.0-alpha.9` | `18c370cb528bbc2107827bebb7799bdb095e133b` | 四方案 hosted HID 通過；使用者回報四種均能操作 Sony／PS5，指定第二種 |
| `v0.1.0-alpha.10` | `8359c1ded5cf3310ad85ed3b42e0f47fd55da586` | 固定純手把一般版、整合改鍵／連點、移除 30 分鐘限制；乾淨建置及離線驗證通過，實機待驗 |
| `v0.1.0-alpha.11` | `6dbc154a6a887398796d442fd34b73455672aba3` | 免費公開 alpha；乾淨建置、本機交付、公開包稽核與下載雜湊通過；本版實機待驗 |
| `v0.1.0-alpha.12` | `f2c0b1759610e27da0d3bf835b4faeef07030d7a` | 九語主介面與設定頁；乾淨建置、公開包稽核、四附件匿名下載及本機交付完成；逐項實機待驗 |
| `v0.1.0` | `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe` | 正式首發；發布時為 GitHub latest，現由 0.1.1 接替。5834 項 C#、35 項公開包稽核、四附件匿名下載及本機交付完成；逐項實機待驗 |
| `v0.1.1` | `3eab9678afe6ea1eddcd35080f2c7e15c1051629` | 國際品牌修正、發布時為 GitHub latest；6912 項 C#、39 項公開包稽核、匿名下載、本機交付及清理完成；逐項實機待驗 |

alpha.8 建置／封存紀錄與最終畫面另於 `cf51de1` 提交；本交接文件再作文件提交。**文件提交超前版本標籤是正常的，不要移動舊標籤來追齊分支。**

GitHub 保存自製原始碼、研究／實機紀錄、設計素材、版本標籤、建置清單與 SHA-256；本機清舊不刪除這些歷史。自製原始碼採 GPL-3.0-only，第三方保留各自授權。v0.1.1 的[歷史發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.1)提供 Windows ZIP、完整對應原始碼 ZIP、RELEASE.json 與 SHA256SUMS.txt，四附件匿名下載的 bytes 及 SHA-256 全數一致；v0.1.0 及歷史 alpha 附件保持不變。正式發布與下載核對完成，不代表數位簽章、長時間或使用者逐項實機驗收。

## 3. 不可遺失的操作規則

1. **不得自行關閉、重啟或更新 Sony Remote Play**。使用者反覆強調連線不容易；早先安裝／測試的同意不是永久中斷連線的許可。若新步驟真的需要中斷當前使用，先做好可檢視的候選並說明具體原因，再協調測試時段。
2. 依最新授權，**新版建置交付與正常切換後，本機只留下最新版**；alpha.12 已交付，清理前仍須逐項核對路徑與程序占用，執行中的舊版待正常退出，不刪執行中的後端。保留個人設定及其備份、GitHub 歷史標籤、研究與 PS4 原始碼，不同時啟動兩版爭用 F8 或通訊埠。此授權不包含停止 Sony、驅動或其他軟體。
3. 不重新安裝 ViGEmBus、不依賴 reWASD 等付費軟體、不改用第三方 Remote Play 用戶端、不索取 PSN 密碼。**目前要求保留本機 0.9.8.1，不更換、不降級、不重開機**；候選要求舊版是其第三方程式的限制，不是 PS5 或 Sony 要求使用較舊驅動。
4. **只在官方 Remote Play 前景接受遊戲按鍵**。切到其他視窗停止並清除全部連點，切回不自動恢復；F8 可啟用／暫停。使用者最終選擇了這個規則，背景掛網不是目前規格。
5. 現有自訂鍵與連點不可自動覆寫。「恢復預設」是使用者主動動作，且會將連點改為關閉、間隔 100 毫秒。
6. 不注入 Sony 程序、不讀取其記憶體、不保存帳號資料或原始按鍵歷史。既有探針僅讀取裝置 handle 中繼資料；查到 handle 不等於遊戲接受輸入。
7. 清理僅針對當下核對過、由本程式建立的裝置／完整本機 URL，不用全裝置 detach，不停止其他軟體的裝置或服務。
8. 使用繁體中文、盡量直接完成工作，不反覆詢問已確認偏好。所有功能免費；贊助自願、不解鎖功能、不建立訂閱。收款帳號身分與金融資料由本人處理；只使用最後指定的 PayPal 公開收款連結，不猜測收款帳號或把回饋信箱當作收款網址，不公開收款者的私人身分與金融資料。回饋僅由點擊開啟郵件草稿，不自動寄出或附加個人資料。

## 4. 功能完成度

| 項目 | 現況 | 還缺什麼 |
| --- | --- | --- |
| 免手把 DS4 → PS5 | alpha.5／alpha.6 使用者實機通過，一次重開機成功 | 多種啟動順序、反覆重連、全按鍵、長時間及第二台電腦 |
| 免手把 DualSense | alpha.9 四方案 hosted HID／清理通過，使用者回報四種 Sony／PS5 操作成功；現行固定第二種 | v0.1.1 新版逐項實機回歸、重連、重開機與長時間；音訊／觸覺另驗 |
| 22 動作自訂鍵 | alpha.10 已交付共用設定頁；僅使用者儲存成功才套用，開啟／取消不覆寫 | 新版儲存／載入完整實機回歸 |
| 每鍵連點 | alpha.10 接回關閉／按住／按一下啟停；20–5000 毫秒；預設 100 | 新版逐項操作、切窗／F8／編輯／斷線停止與長時間可靠性 |
| UI／圖示 | alpha.8 已交付；主色 `#D62355`、白色圖示、狀態燈 | 使用者視覺確認、150%／200% DPI、多螢幕 |
| 多語言 | v0.1.1 已發布；主介面 50 鍵、共用設定頁 93 鍵九語，非中文品牌統一，主畫面已目視、設定頁與已審閱 v0.1.0 圖片 SHA 相同 | 母語校閱、實際高 DPI／多螢幕及使用者實機 |
| 錄製／重播 | 僅有設計，尚未實作 | 動作序列、計時、儲存、單次／循環播放、停止測試 |
| 滑鼠控制右搖桿 | 本主線未實作，依使用者要求暫緩 | 日後另排，不從舊 ViGEm 原型推論已支援 |
| 其他實體手把 | 暫緩；舊有線第一代 DS4 中繼保留備援 | 不作首版必要條件；任意手把尚未支援 |
| macOS | 免費已簽章 OJD Generic 中立裝置建立與 EOF 後移除通過；固定鍵位研究前端已建置並啟動 | 見 [最新研究](../research/macos-native/README.md)；Sony 接受、鍵盤／PS5 反應、前景／F8 與異常清理尚待驗，非正式 Mac 成品 |
| 免費開源發布 | GPL-3.0-only、作者 xiunlee；v0.1.1 曾正式公開且當時為 latest，完整來源／notices／雜湊及匿名下載核對完成 | 三個 runtime 二進位未簽章；乾淨安裝、長期相容性及本版逐項實機另驗 |
| 贊助與回饋 | PayPal 自願贊助 US$1 幫三隻貓咪買罐頭；mailto 固定回饋信箱，僅版本主旨 | 唯一現行公開連結 GET 回應 200、設定啟用、金額 USD 1.00；HTTP GET 不等於實付款，尚未驗收入帳，不使用已撤回連結 |

九語：繁中、簡中、英文、日文、韓文、德文、法文、西班牙文、巴西葡語。alpha.11 只有設定頁使用此語系機制；alpha.12 主畫面跟隨同一 `UiText.LanguageTag`、未支援語言回退英文。文件仍以繁中／英文提供；翻譯存在不代表母語校閱已通過。

連點間隔指一次按下開始到下次開始；每週期約半數時間按下、半數放開。USB／遊戲取樣可能忽略很短脈衝，不能保證設定 20 毫秒就被所有遊戲逐次接受。

## 5. 鍵位與使用者設定

alpha.7 起的新建或「恢復預設」排列如下，現行 v0.1.0 沿用；已有格式 1／2 設定保持原樣。

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
| `research/dualsense-preview/DualSensePreview.cs` | **現行 DualSense 主程式**，一般版純手把、按鍵／連點；alpha.11 新增免費與回饋區塊，alpha.12 完成九語介面與 2D／JRPG 定位 |
| `research/dualsense-preview/SupportLinks.cs` | 固定 mailto／PayPal 公開收款連結許可規則，僅使用者點擊可開外部程式；不在本程式收集付款資料 |
| `research/dualsense-preview/PreviewText.cs`、`PreviewTextTests.cs` | alpha.12 主介面 50 鍵九語資源、英文回退、格式參數與回饋主旨純測試；不取代共用設定頁 UiText |
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
| `research/dualsense-preview/` | alpha.9 四方案歷史實測、alpha.10 起單模式玩家主線；USB/IP 33641、API 33642 |
| `research/native-probe/` | Sony handle 唯讀檢查，`--dualsense` 限定 `054C:0CE6` |
| `packaging/Verify-ControllerFreeBuild.ps1` | alpha.8 舊路線的來源／版本／雜湊驗證，不用於現行主線 |

alpha.11 起必須使用 `research/dualsense-preview/Build-Candidate.ps1`，先建置同目錄 `backend`，再明列 `-Zig` 從未變更 native 來源編譯 DLL，最後用 `Verify-Candidate.ps1` 核對；不再使用 `-MappingDirectory`，實際參數見 [版本規則](VERSIONING.md)。原 `controller-free-preview/Build.ps1` 不能交付新主線。不要誤用 `packaging/Build-Preview.ps1` 或 `src/physical-keyboard/Build.ps1`；它們是歷史路線。`docs/architecture.md` 原有內容亦屬較早研究。

現行後端固定 **hbashton/VIIPER `b78e31e93e84b4cb7c4b4c15b8aace10749106d2`** 的底層套件，由自製受限入口啟動，並以既有 **usbip-win2 0.9.8.1** 成套 CLI／DLL 明確匯入自有裝置。僅 loopback：API **33642**、USB/IP **33641**；只允許 `dualsensegamepadv5`，不自動接入。alpha.11 不隨包散布驅動，不自動安裝或更換。最終後端 **r4** 使用相對 go.mod replace，MATERIALS 與二進位掃描不再帶本機個人來源路徑；這項隱私修正不代表控制協定變更。Alia5 VIIPER 0.9.0 與 33241／33242 是 alpha.8 舊路線。DualSense VID/PID `054C:0CE6`；識別碼或 Windows 接入均不是該次 Sony 操作成功證明。

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

1. 唯讀核對目前版本、分支、程序及設定；先保護正在使用的程式與 Sony 連線，不按歷史 PID 操作，也不重做四方案選型或原版 62 項試驗。
2. alpha.10 乾淨建置、774 項前端／傳輸／設定／連點／UI 檢查及後端純函式測試已通過，兩張最終離線畫面已檢視；先讀 [交付紀錄](../research/dualsense-preview/ALPHA10-TRIAL.md)，不要將其當成遊戲驗收。
3. v0.1.1 發布、來源／notices／39 項公開材料稽核及四附件匿名下載已完成，現行先讀 [RELEASE-0.1.1.md](../research/dualsense-preview/RELEASE-0.1.1.md)，歷史再讀 [RELEASE-0.1.0.md](../research/dualsense-preview/RELEASE-0.1.0.md)及各 alpha 紀錄。不要重建或替換同版附件，也不要把收尾文件提交追寫到固定標籤。
4. v0.1.1 的最終乾淨建置、6912 項 C#、5743 項 native／ABI、九語離線畫面、公開封裝及交付已完成。尚須驗收語系、既有設定沿用、22 動作改鍵、儲存／取消／重新載入、三種連點及不同間隔；不得因切換自行重啟 Sony 或更動驅動。
5. 驗收切窗清除連點、切回不自動恢復、F8 暫停、編輯期間暫停、斷線停止與自有裝置清理；另外安排長時間及重開機驗收，不把移除 30 分鐘限制說成長時間已通過。
6. v0.1.1 已發布、交付並完成舊版清理，下一串可依 [Mac 交接](MAC-HANDOFF.md)在實體 Mac 唯讀核對並隔離研究。Windows 後續功能修正使用 0.1.3，不把本次封裝、清理與國際品牌成果當成新增實機驗收。

後端回讀、Windows 裝置、Sony handle 與使用者 PS5 操作仍須分開記錄。alpha.9 的使用者回報可以記為該版成功，不能讓新版連線燈或每次新 session 自動宣稱 Sony 已接受輸入；《Final Fantasy Resonance》回報也須保留工具 build 未指定的限制。

## 8. 本機交付、回退與版本規則

公開文件用環境變數表示路徑；下串在使用者原工作目錄繼續即可。不要把個人設定、程序收據、原始 USB／按鍵資料或裝置序號推送到公開 GitHub。

alpha.9 曾交付至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.9`，入口 `PSKeyboard-DualSensePreview.exe`，桌面捷徑「上班不要玩 0.1.0-alpha.9 四方案測試」。開啟不自動接入、鍵盤暫停、設定唯讀；使用者逐一操作後回報四種成功。完整 [交付與實機紀錄](../research/dualsense-preview/ALPHA9-TRIAL.md)、[建置](../packaging/build-records/0.1.0-alpha.9.json) 與[封存](../packaging/build-records/0.1.0-alpha.9-archives.json)保留於 GitHub。

**alpha.10 已交付**至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.10`，桌面捷徑「上班不要玩 0.1.0-alpha.10」，入口 `PSKeyboard-DualSensePreview.exe`。來源為 `8359c1ded5cf3310ad85ed3b42e0f47fd55da586`；[建置清單](../packaging/build-records/0.1.0-alpha.10.json)、[封存雜湊](../packaging/build-records/0.1.0-alpha.10-archives.json)及[完整交付紀錄](../research/dualsense-preview/ALPHA10-TRIAL.md)已保存。只執行離線繪圖，沒有代使用者啟動或接入新版，尚無新版實機回報。

**alpha.11 已交付**至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.11`，桌面捷徑「上班不要玩 0.1.0-alpha.11」，另有同版 `versioned-archives` 封存。來源為 `6dbc154a6a887398796d442fd34b73455672aba3`；[建置清單](../packaging/build-records/0.1.0-alpha.11.json)、[公開附件雜湊](../packaging/build-records/0.1.0-alpha.11-archives.json)及[發布紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)可追溯。已完成公開發布／下載核對，但沒有代使用者啟動一般模式或操作 Sony。上述均為 Windows 路徑，Mac 不直接套用。

**alpha.12 曾交付**至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0-alpha.12`，沒有代使用者啟動一般模式。來源為 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`；[建置清單](../packaging/build-records/0.1.0-alpha.12.json)、[公開附件雜湊](../packaging/build-records/0.1.0-alpha.12-archives.json)與[發布紀錄](../research/dualsense-preview/ALPHA12-TRIAL.md)保留。本機清理結果另記，不能依歷史交付路徑推論舊檔案仍在或已全部刪除。

**v0.1.0 曾交付**至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.0`，來源為 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`；[建置清單](../packaging/build-records/0.1.0.json)、[公開附件雜湊](../packaging/build-records/0.1.0-archives.json)與[正式發布紀錄](../research/dualsense-preview/RELEASE-0.1.0.md)可追溯。當版沒有代使用者啟動一般模式；舊本機副本已納入後續 v0.1.1 清理，公開歷史保留。

**歷史 v0.1.1 曾交付**至 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.1`，同版封存與桌面入口均已更新；固定來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`，四附件匿名下載通過。[建置清單](../packaging/build-records/0.1.1.json)、[附件雜湊](../packaging/build-records/0.1.1-archives.json)與[發布紀錄](../research/dualsense-preview/RELEASE-0.1.1.md)保存實際證據。當時清理後 `versions`／`versioned-archives` 均僅 0.1.1；沒有新增本版 PS5 實機回報。

最新本機保存規則：

- 新版先放獨立全新目錄，驗證完成再切換；不得覆寫執行中的舊版。正常退出舊版後才移除其檔案／捷徑，不強制終止 Sony 或驅動。
- v0.1.1 清理已按授權完成；日後新版交付及正常切換後，舊 `versions`、重複建置目錄、`versioned-archives` 舊封存與 `versioned-baselines` 舊基準可再次核對清理。具體目錄先核對解析後的完整路徑、專案歸屬及程序占用，不能對工具根目錄整包遞迴刪除，也不把前次數字重複計入。
- 保留 `%LOCALAPPDATA%\PSKeyboard\bindings.json`、`profile-backups`、最新版執行所需檔案及可追溯紀錄。alpha.11 的 `pskb-keyboard-merge.dll` 改由固定工具從來源建置，以新清單記錄實際雜湊，不冒稱其二進位必與舊編譯器輸出相同。
- 用系統 Desktop known folder 找舊專案捷徑，只移除已核對的舊捷徑，不假定桌面路徑，也不清除無關檔案。
- GitHub 的舊標籤、原始碼、PS4 路線、研究、建置與封存雜湊紀錄不刪、不改。清理後舊本機執行檔可能已不存在，回退需依標籤與固定來源另行重建，不能再指示使用者直接開已移除的 alpha.8／9 捷徑。

遵循 [版本規則](VERSIONING.md)：**v0.1.2 已發布，下一個變更版本為 0.1.3；依 V0.1.0 → V0.1.1 → … → V0.1.9 → V0.2.0 遞增，不再使用 alpha 或改成 0.1.10**。UI 顯示 `PS Remote Play Keyboard V 0.1.2` 或中文 `上班不要玩 V 0.1.2`，Git tag 使用小寫 `v0.1.2`；尚未指定的更高位進位不自行決定。新程式不覆寫已發布附件或重用／移動既有 tag，清理舊本地副本不等於可以覆寫版本歷史。文件／驗收補記不用升程式版本。

建置前提交程式碼、保持工作區乾淨；使用 `research/dualsense-preview/backend/Build-Backend.ps1`、`Build-Candidate.ps1 -BackendDirectory <新後端目錄> -Zig <Zig執行檔> -OutputDirectory <全新目錄> -RunTests -RequireClean` 與 `Verify-Candidate.ps1 -BuildDirectory <新建置目錄> -RequireClean`。alpha.11 的 `-Zig` 必填，固定 Zig 0.17.0，從未變更的 `keyboard_merge.c`／`.h` 編譯。完整後端參數與公開來源規則見 [版本控管](VERSIONING.md)。精確重現舊版需使用該標籤的隔離 checkout 及當版流程，不移動標籤來追齊文件。

alpha.8：36 項控制保護、106 項假傳輸／轉換、17 項建置完整性與 122 項設定頁互動檢查通過；九語設定頁逐張檢視。alpha.7 設定／連點／流程／設定頁共 535 項離線檢查通過。先前原版 DualSense 使用者空間 USB/IP 62 項通過。這些測試各有範圍，不是遊戲、長時間操作、真正高 DPI 或新候選的驗收。

## 9. UI 素材與後續收尾

- 設計及預覽：[VISUAL-DESIGN.md](VISUAL-DESIGN.md)；素材在 `assets/branding`，離線畫面在 `assets/previews/alpha.8`。
- 圖示由內建 imagegen 製作，PNG／ICO 已嵌入執行檔，包含 16–256 像素圖示。UI 常數精準使用 `#D62355`，**生成圖示底色有微小色差**；詳見 [PROMPT.md](../assets/branding/PROMPT.md)。
- 狀態綠燈表示本機裝置／輸出通道已接入，不代表 PS5 已連線或遊戲接受輸入。預覽圖片是離線示意，非即時遊戲畫面。
- 主畫面常用鍵位摘要來自當前設定，不是寫死新預設；後續調整不能破壞此點。
- alpha.11 最終[主畫面](../assets/previews/alpha.11/main-preview.png)與[設定頁](../assets/previews/alpha.11/editor-preview.png)保留歷史。alpha.12 九語主介面與設定頁共 18 張最終離線圖片已全部目視，存於 `assets/previews/alpha.12/main-<lang>.png` 與 `editor-<lang>.png`；主畫面按翻譯長度調整排版。使用者逐項實機、實際高 DPI、多螢幕及母語校閱不可由離線圖片推論。
- v0.1.0 的九張新版主畫面已目視；九張設定頁與已審閱 alpha.12 圖片的 SHA-256 相同，沿用該視覺證據。正式版圖片存於 `assets/previews/0.1.0/main-<lang>.png` 與 `editor-<lang>.png`。
- v0.1.1 的九張最終主畫面已目視，九張設定頁 SHA-256 與已審閱 v0.1.0 相同；開發階段 11 個排版情境均無問題。當時圖片存於 `assets/previews/0.1.1/main-<lang>.png` 與 `editor-<lang>.png`；新版見 `assets/previews/0.1.2`，僅為離線示意。

## 10. 其餘待辦與閱讀順序

1. 本文件 → [版本紀錄](../CHANGELOG.md) → [版本控管](VERSIONING.md) → [v0.1.2 正式發布紀錄](../research/dualsense-preview/RELEASE-0.1.2.md)。Mac 下一串再完整讀 [MAC-HANDOFF.md](MAC-HANDOFF.md)，依該文件順序讀協定、平台相依程式與歷史證據。
2. [alpha.6 歷史實機結果](../research/controller-free-preview/ALPHA6-TRIAL.md) → [DualSense 候選](../research/dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md) → [V5 隔離結果](../research/dualsense-v5-test/RESULTS-20261029.md) → [既有驅動相容性研究](../research/dualsense-driver-test/README.md) → [alpha.9 成功回報](../research/dualsense-preview/ALPHA9-TRIAL.md) → [現行 DualSense 功能說明](../research/dualsense-preview/README.md) → 相關程式與測試。
3. 需要確認早期成功證據時讀 [首次免手把紀錄](../research/controller-free-preview/LIVE-TRIAL.md)；歷史失敗仍保留，不能當成現況或假定其根因已修正。
4. [支援範圍](PRODUCT-SCOPE.md)、[連點／錄製設計](AUTOMATION-AND-CONTROLLER-TESTS.md)、[發行工作表](../packaging/RELEASE-CHECKLIST.md) 是後續待辦。錄製只記錄已映射的手把動作與時間，不記錄全部 Windows 打字、不執行任意程式碼；失焦／F8／斷線停止。
5. v0.1.1 完整對應原始碼、第三方 notices、39 項公開產物稽核、雜湊與匿名下載已完成；參照 [LICENSE](../LICENSE)、[COPYRIGHT.md](../COPYRIGHT.md)與發行工作表。來源包內容已核對，但未在全新電腦獨立重建；乾淨安裝／移除、恢復、長時間及簽章後續改善另列，不當成已通過。沒有定價、付費解鎖、DRM 或銷售交付計畫；不要宣稱首創、Sony 官方授權或所有遊戲／電腦通用。

完成下一個階段後，更新本文件與對應證據、提交到工作分支並核對遠端。交付的是可追溯的進度與成果，不只在聊天中宣告完成。
