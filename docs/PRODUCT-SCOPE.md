# 發布前功能與支援範圍

更新：2026-10-29。使用者要求先完善功能，再發布或收費。原本正在使用的中繼與官方 Remote Play 保持運作；新版本只在另一個資料夾建置與離線驗證。

## 本輪實作

1. **鍵盤自訂與連點**：22 個手把動作可指定單一按鍵、清除、還原預設並儲存，並各自選擇關閉連點、按住連點或按一下切換。間隔 20–5000 毫秒，預設 100。禁止重複指派，F8 保留為暫停鍵，Ctrl／Alt／Windows／Shift 不可用作遊戲映射。只在官方 Remote Play 前景接受遊戲按鍵；失焦停止全部連點且回來不自動恢復。編輯期間暫停鍵盤，不停止實體中繼。錄製／重播與滑鼠尚未實作，詳見 [連點與驗證方式](AUTOMATION-AND-CONTROLLER-TESTS.md)。
2. **介面與狀態燈**：一般畫面只保留必要狀態、啟用／暫停與按鍵設定；技術設定收進進階區。綠燈只代表實體通道確認且最近兩秒內收到資料，不表示 PS5 已連線或遊戲已接受操作。手把與鍵盤各自有文字狀態，不能只靠顏色辨識。
3. **系統語言**：啟動時依 `CurrentUICulture` 選擇繁體中文、簡體中文、英文、日文、韓文、德文、法文、西班牙文、巴西葡萄牙文。台灣／香港／澳門使用繁中，中國／新加坡使用簡中；葡語地區先使用巴西葡語。其他語言回退英文。這是介面的初版翻譯，仍需母語使用者校閱；維護腳本與研究文件尚未全部翻譯。

設定只保存於目前 Windows 使用者的 `%LOCALAPPDATA%\PSKeyboard\bindings.json`，不含按鍵歷史、PSN 帳密或 USB 原始資料。錯誤／不支援版本的設定回退預設並提示，不自動覆蓋原始檔案。

## 手把支援

| 類型 | 本專案目前狀態 | 發布前所需證據 |
| --- | --- | --- |
| 有線 DS4 第一代，054C:05C4 | alpha.3 已有本機正常操作回報；新連點版尚未切換實測 | 自訂鍵、連點、失焦停止與重新啟動驗證 |
| 有線 DS4 第二代，054C:09CC | 中繼允許此型號，未在本機實測 | 真實裝置描述、報告、控制往返及官方 Remote Play 操作 |
| DualSense／DualSense Edge | 尚未實作本專案支援 | 新格式解析、通道驗證、合併測試與實機操作 |
| Xbox、Switch、其他任意手把 | 尚未支援 | 個別輸入格式及 Sony 相容輸出路徑研究 |
| 完全不插手把 | 尚未達成 | 獨立裝置與 Sony 接受輸入的證據；先前候選無操作效果 |

Sony 官方 Windows 用戶端列出 DualSense、DualSense Edge 與 DUALSHOCK 4，但這不表示本中繼已支援全部型號。任意手把也不能僅改一組識別碼就當作已相容。現在成功的方式保留真正 DS4 的控制與功能回覆；免手把是另一條需要驗證的路線。[Sony Windows 說明](https://remoteplay.dl.playstation.net/remoteplay/lang/en/1100001.html)

## Mac

**目前不支援，不能直接把 Windows 程式改成 Mac 安裝包。** Sony 有官方 Mac Remote Play，也列出 DS4、DualSense 與 DualSense Edge。[Sony Mac 說明](https://remoteplay.dl.playstation.net/remoteplay/lang/en/1100002.html)

本專案使用 Windows Forms／.NET Framework、Windows 鍵盤攔截與 Windows USB/IP 驅動，這些平台部分不能直接在 macOS 運作。[Microsoft Windows Forms 說明](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/)；[usbip-win2 專案](https://github.com/vadimgrn/usbip-win2)

未來 Mac 研究應先在實體 Mac 上確認可讀取／中繼指定手把、保留必要回覆，並讓 Sony 官方程式接受輸入；通過後再移植鍵盤擷取與介面。按鍵設定概念與部分原生合併邏輯可以沿用，但現有 Windows 虛擬 USB 後端不可直接沿用。若需 DriverKit，還要核對 Apple 的驅動權限與發行要求；這是工程評估，尚不是可行性實證。[Apple DriverKit 權限說明](https://developer.apple.com/documentation/driverkit/requesting-entitlements-for-driverkit-development)

首版候選維持 **Windows + 已驗證有線 DS4 + Sony 官方 Remote Play**。其他手把、免手把與 macOS 都必須個別通過後才加入支援清單，不用尚未達成的功能對外收費。
