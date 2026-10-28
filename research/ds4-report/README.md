# DUALSHOCK 4 USB 報告離線研究

這是獨立的資料轉換實驗，**不連接手把、不中介或修改 Remote Play、不產生虛擬裝置、不接觸 PSN 資料**。它只接受記憶體中的 DUALSHOCK 4 USB input report（64 bytes，report ID `0x01`），把合成的軸與按鈕合併後回傳新陣列。這不是已可使用的 Remote Play 功能。

格式參考 Linux 核心 [`dualshock4_input_report_usb` 與 common 結構](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-playstation.c)。實際裝置與 Remote Play 9.5 使用的讀取方式、report 長度和相容性仍須另行確認；藍牙封包不支援。

- 以未修改的實體報告為每次合成來源；未映射的動作、計數器、感測器、觸控資料及剩餘位元保留。
- 人工輸入為空時傳回實體報告的複本。有人工作動時，搖桿及方向鍵以人工輸入優先，其他按鈕以 OR 合併；扳機力度取兩者較大值。
- 報告長度、report ID 和方向鍵值不符合假設時拒絕處理，避免把其他裝置封包誤當成 DS4 USB 資料。

在 Windows PowerShell 使用系統內建 .NET Framework 編譯並執行測試：

```powershell
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testExe = Join-Path $env:TEMP 'pskeyboard-ds4-offline-tests.exe'
& $compiler /nologo /out:$testExe 'research\ds4-report\Ds4UsbReport.cs' 'research\ds4-report\Ds4UsbReportTests.cs'
if ($LASTEXITCODE -eq 0) { & $testExe }
```
