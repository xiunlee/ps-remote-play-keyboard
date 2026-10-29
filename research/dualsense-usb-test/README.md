# DualSense 本機後端驗證

本工具驗證自製鍵盤映射經 VIIPER v0.9.0 轉成模擬 DualSense USB 報告。只連接另行啟動的隔離本機後端，透過使用者空間 USB/IP 讀回資料，**不將裝置匯入 Windows、不攔截鍵盤、不操作 Sony Remote Play，也不需要實體 PS5 手把**。

建置：

```powershell
.\research\dualsense-usb-test\Build.ps1 -MappingDirectory '<已建置程式的目錄>'
```

操作者須先核對獨立 VIIPER 執行檔的固定版本、雜湊及簽章，確認 API 僅監聽 `127.0.0.1:33342`、USB/IP 僅監聽 `127.0.0.1:33341`，並明確停用自動匯入。工具不啟動或停止該後端；只建立與移除自己建立的虛擬匯流排。

```text
DualSenseUsbReadbackTest.exe --isolated-loopback-server 33342 33341 --auto-attach-disabled
```

只有以上精確參數才執行。這個確認參數是操作者對隔離後端設定的聲明，不是工具已能證明伺服器設定；工具另核對 API 回覆版本、自己的匯流排識別與 USB `054C:0CE6`。正常試驗只需要數秒；每次網路作業限時三秒，主要流程總計最多九十秒，最後仍嘗試清除自己的匯流排。

測試沿用實際 `KeyboardMapping.Frame`、原生映射 DLL 與 `ViiperTransport` 的 DualSense 轉換，讀回 64-byte `01` USB 報告後，用獨立的預期 USB 欄位比對：22 個動作各自按下及放開、持續按住、搖桿斜向與相反方向、方向鍵組合、PS／觸控板位元、Create／Options、雙扳機、停用回中、右搖桿保持中立、觸控未按住與靜止感測值。另核對校準 `05`、配對 `09`、韌體 `20` 的回覆長度與報告 ID，不輸出原始內容。

結果中的 `windows_attached=false sony_tested=false` 是必要界線：即使全部通過，也只證實本機模擬與轉換，不能宣稱 Windows 或官方 Remote Play 已接受 DualSense。真正 Sony 相容性仍需另外安排實機測試；保留既有正常 DS4 連線。

## 2026-10-29 實際結果

已在固定雜湊及有效簽章的 VIIPER v0.9.0 上執行。操作者建立獨立後端，清除繼承的 VIIPER 環境設定、停用自動匯入，並確認兩個 loopback 監聽端均屬於當次新程序；完成後只結束該程序。

```text
RESULT passed=62 success=true elapsed_ms=1157 windows_attached=false sony_tested=false keyboard_captured=false
```

22 個動作的按下與放開、組合與中立報告、三種 feature 長度／ID 及自己的匯流排清理全部通過。既有 alpha.5 與 Sony 程序保持相同啟動時間及程序識別。這次未把 DualSense 接到 Windows，也未要求使用者在遊戲按鍵；下一步才是 Windows 與 Sony 相容性試驗。

## 協定來源

- [VIIPER v0.9.0 DualSense 文件](https://github.com/Alia5/VIIPER/blob/v0.9.0/docs/devices/dualsense.md)：裝置類型及輸入／回饋格式。
- [state.go](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualsense/state.go)、[const.go](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualsense/const.go)：33-byte 輸入、PID、按鈕位元及中立值。
- [device.go](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualsense/device.go)：USB 報告欄位及 feature 回覆。
- [VIIPER 授權](https://github.com/Alia5/VIIPER/blob/v0.9.0/LICENSE.txt)：GPL-3.0；此目錄不包含第三方執行檔或複製的實作，正式散布仍須完成相依授權審查。

固定的 v0.9.0 DualSense 後端只有 HID 介面，不能把按鍵模擬成功宣稱為完整音訊、自適應扳機或進階觸覺功能已完成。
