# DualSense V5 隔離研究

研究版本 `dualsense-v5-research.1`，2026-10-29（Asia/Taipei）。本目錄是協定工具，不是 alpha.9，也不接入主程式。交付版仍為 alpha.8，預設 PS4、個人鍵位、前景限制與 F8 規則未改。

固定候選為 [hbashton/VIIPER v0.1.9-rc4.6.6](https://github.com/hbashton/VIIPER/releases/tag/v0.1.9-rc4.6.6)，提交 `b78e31e93e84b4cb7c4b4c15b8aace10749106d2`；接續[上輪候選研究](../dualsense-usb-test/UPSTREAM-CANDIDATES-20261029.md)。實際結果、下載檔驗證、失敗與限制見 [RESULTS-20261029.md](RESULTS-20261029.md)。

## 已實作的範圍

- `V5Protocol.cs`：33-byte 候選狀態、16-byte VPCM V5 header、IEEE CRC32、序號與長度驗證。回饋解析 `81`、`83`、`84`，僅明確協商時允許 `85`；本次 composite alias 不啟用 `85`。
- `ConvertLegacyKeyboardState`：保留既有鍵盤轉換結果，將兩個觸控狀態從原版 bool inactive `00` 轉成候選 packed inactive `80`。嚴格拒絕不屬於鍵盤中立觸控／體感的資料，不猜測 raw／實體報告格式。
- `V5ProtocolTests.cs`：固定 CRC 範例、分段與合併讀取、格式錯誤、長度、序號、EOF、跨 header/payload 的總期限；不啟動後端。
- `V5UsbReadbackTest.cs`：由既有原生鍵盤映射 DLL 產生合成的 22 動作，經 V5 送入固定來源後端，再由使用者空間 USB/IP 讀回。驗證四個介面、端點、HID 描述、部分 feature/control、按下／放開、組合與輸出回饋；不攔截真實鍵盤。
- `labserver/`：直接使用固定上游的底層使用者空間套件，完全不經 Windows driver。它不是候選正式 CLI，不改寫或移除正式 CLI 的版本檢查。詳見[獨立 server 說明](labserver/README.md)。

本工具不提供 Windows attach 指令，不以視窗／裝置名稱判定 Sony 相容，也不保存裝置序號、原始輸入報告或個人設定內容。

## 重現方式

先依 labserver 文件準備固定來源 ZIP、已核對的 Go 可攜式編譯器，建置到全新 `.local` 目錄。再建置 C# 工具：

```powershell
.\research\dualsense-v5-test\Build.ps1 `
  -MappingDirectory "$env:USERPROFILE\.pskeyboard-tools\versions\0.1.0-alpha.8" `
  -OutputDirectory '<全新的 .local 測試工具目錄>' -RunTests
```

建置先以 alpha.8 公開紀錄核對原生 DLL，複製來源快照，兩個工具均只編譯此快照；建置期間若工作區來源有變則拒絕寫成功清單。每次輸出保留 `BUILD.json` 與來源／執行檔 SHA-256，不覆寫舊輸出。

唯讀檢查目前程序、研究埠與建置清單後，使用以下入口執行使用者空間測試：

```powershell
.\research\dualsense-v5-test\Run-Isolated.ps1 `
  -LabDirectory '<本次 lab server 建置目錄>' `
  -TestDirectory '<本次 C# 工具建置目錄>' `
  -ResultDirectory '<儲存庫 .local 下全新的結果目錄>'
```

此入口會啟動自己的隱藏 helper，僅容許 API `127.0.0.1:33442` 與 USB/IP `127.0.0.1:33441`，檢查兩個 listener 都屬於本次程序；埠已占用就停止，不移除占用者。現行產品 `33241/33242` 不使用。測試最多 90 秒主流程，另有 bounded cleanup；外部程序等待上限 100 秒。server 自身上限 180 秒正常結束、195 秒硬性退出。

清理先移除測試自己建立的記憶體 bus，再向本次 helper 的 API 停止；必要時僅終止本次 `Start-Process` 得到的 helper。強制終止、server 非零結束或清理錯誤均不得列為完整成功。不使用全域 detach、服務停止或按名稱殺程序。

結果目錄限 `.local`，保留私有的前後程序與設定雜湊收據；公開僅使用經挑選的摘要，不能直接提交整個資料夾。第三方來源、Go／候選執行檔與個人收據均不提交 GitHub。

## 驗收界線與下一步

本次能證實的是 **固定來源的 V5／USB/IP 往返**。沒有驗證音訊 ISO 串流品質、真實體感／觸覺、長時間排程、Windows driver 接入或 Sony PS5 操作。

正式候選 Windows CLI 要求 usbip-win2 `0.9.7.7`；現有 `0.9.8.1` 的成套 CLI／driver 保持不變。在有可回退的隔離 Windows 環境完成現有版本相容性評估前，不把此 lab fixture 改成日常後端、不安排在目前遊玩連線上匯入候選。若後續測試會影響現有控制，須先交付可檢視的測試方案，再與使用者協調時段；不得自行重啟 Sony。

將來必須分別記錄：後端回讀 → Windows 裝置 → Sony handle → 使用者在官方 Remote Play 內的 PS5 實際操作。最後一項才是操作成功證據。
