# 鍵盤手把 USB 測試版

目標是「鍵盤按下、按住、放開，等同手把按鈕」，不依賴遊戲的原生鍵盤支援。本版只做鍵盤，排除滑鼠。

本程式將鍵盤映射成 DS4 狀態，透過免費開源 VIIPER 及 usbip-win2 建立另一個 Windows USB 手把。它不是將資料寫回實體 PS4 手把，也尚未合併實體手把輸入。這是與先前 ViGEmBus、Sony 程序內載入不同的候選接法。

**目前仍是測試版，官方 Remote Play 9.5 尚未驗收。** 映射函式、傳輸錯誤處理及 USB/IP 報告回讀已通過；後續也實際建立 Windows USB DS4，從 Windows HID 介面讀回按下、長按、放開與複合按鍵，20 項檢查通過。這還不能當作真實鍵盤擷取及 Sony 遊戲控制成功。

## 操作

1. 先完成另行確認的 usbip-win2 驅動安裝。安裝會重新啟動 USB 3.0 集線器，可能短暫中斷鍵盤、滑鼠、手把及其他 USB 裝置；不要在仍需保持連線的遊戲中安裝。
2. 執行 `PSKeyboard-USB.exe`，按「建立手把」。程式本身不會自動安裝驅動。
3. 按 F8 啟用鍵盤。輸入只在本程式或標準安裝路徑的官方 Remote Play 視窗生效。
4. F8 暫停、切走視窗或關閉程式會釋放映射按鍵。停用時正常使用鍵盤。

| 鍵盤 | 手把 |
| --- | --- |
| WASD | 左搖桿 |
| 方向鍵 | 十字鍵 |
| I / J / L / K | △ / □ / ○ / × |
| Q / E | L1 / R1 |
| U / O | L2 / R2 |
| 1 / 2 | L3 / R3 |
| Enter / Backspace | Options / Share |
| T / P | 觸控板按下 / PS |

長按、放開、複合按鈕與方向抵消由狀態更新處理。兩個搖桿以外的資料保持中立；目前右搖桿固定中立。未實作觸控滑動、體感、震動回傳至實體手把、滑鼠及自訂鍵位。

## 元件及驗證

- [VIIPER v0.9.0](https://github.com/Alia5/VIIPER/releases/tag/v0.9.0)：獨立的本機服務，GPL-3.0；[對應原始碼](https://github.com/Alia5/VIIPER/tree/v0.9.0)。只監聽 `127.0.0.1:33241/33242`，關閉自動接入及更新通知，由我們的程式明確接入自己的裝置。
- 官方 x64 `viiper.exe` SHA-256：`142C144F6ABEE8B8A5F6A1ABBC7018683444DE71D2E45102CDE6AAAB9B2223E6`。建置時核對雜湊及 Windows 簽章。
- [usbip-win2 0.9.8.1](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1)：有簽章的 USB/IP 驅動。安裝檔 SHA-256：`38CAD6D4432B52D5BB9409D9AD03B72FDFFC4ADA4CD3A48FBECA1A2752A8518A`。
- 無付費工具依賴；不需要 PSN 或第三方帳號，也不會讀取 Sony 登入資料。
- 不使用 ViGEmBus、不改 Sony 安裝檔，也不載入任何 DLL 至 Sony 程序。

建置使用 Windows 內建 .NET Framework x64 編譯器及 Zig：

```powershell
.\src\keyboard-pad\Build.ps1 -Zig <zig.exe完整路徑> -BackendDirectory <含已驗證viiper.exe的資料夾>
```

建置不會安裝驅動、執行介面或連接裝置。重新散布 VIIPER 二進位檔時須同時履行其 GPL 授權及對應原始碼提供義務；公開專案目前只提供我們的程式碼、測試與上游下載連結。
