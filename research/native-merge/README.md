# 鍵盤輸入合併核心（尚未接入 Remote Play）

這個純 C 模組將呼叫端提供的按住狀態，合併進一份 USB DUALSHOCK 4 報告。它不讀取鍵盤、不開啟裝置、不載入其他程序，也不傳送資料。**測試通過不代表官方 Remote Play 或 PS5 已收到操作。**

依最新範圍只處理鍵盤；右搖桿的兩個位元組一律保留實體手把原值，沒有滑鼠功能。R3 按鈕仍可映射，與右搖桿方向不同。

## 合併規則

- WASD 轉左搖桿，斜向使用約 0.707 的各軸力度，避免兩軸都推到底。相反方向互相抵消；若沒有剩餘方向，保留實體左搖桿。仍有鍵盤方向時，該方向優先於整組實體左搖桿。
- 方向鍵採同樣的相反方向抵消規則。非中立鍵盤方向優先於實體方向鍵；中立時保留實體方向鍵。
- 按鈕與實體按鈕以 OR 合併，鍵盤 L2／R2 同時設定數位按下及 255 的類比力度。未按下時保留實體扳機。
- 每次都必須使用該次未修改的實體資料。釋放按鍵或停用時回到實體狀態，不把上一個合併結果當下一次來源。
- 接受恰好 64 bytes、ID `0x01`、有效方向鍵值的報告；拒絕未知按鍵旗標、其他格式及錯誤長度。拒絕時目的緩衝區不變。無操作時逐 byte 保留來源。
- 所有右搖桿、感測器、觸控資料、狀態及按鈕之外的計數器位元保留。格式與實際來源裝置的驗證仍由未完成的接入層負責。

`keyboard_merge.h` 的旗標是動作，不是 Windows 按鍵代碼。呼叫端將 K 等按鍵轉為 `PSKB_CROSS`，再呼叫 `pskb_merge_keyboard`。此核心不包含背景按鍵擷取。

資料位置參照 Linux 核心的 [DS4 USB input report 定義與解析](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-playstation.c)。這是獨立實作，沒有複製 Linux 驅動程式。藍牙不支援。

## 建置與驗證

```powershell
& .\research\native-merge\BuildAndTest.ps1 -Zig '<zig.exe 絕對路徑>' -OutputDirectory '<測試輸出絕對路徑>'
```

測試使用合成報告，涵蓋單獨按鈕、全部 WASD／方向鍵組合、實體輸入保留、停用／放開、重疊緩衝區，以及無效輸入不修改輸出。這些測試不碰正在遊玩的 Remote Play。第一個實機驗收仍是「一個鍵盤鍵 → 手把 × → 遊戲實際反應」；接入尚未完成。

## C# 呼叫

建置同時輸出 x64 `pskb-keyboard-merge.dll`。匯出名稱是 `pskb_merge_keyboard`，使用 C ABI／cdecl。結構為連續的 `uint32_t held`、`int32_t enabled`（合計 8 bytes），`size_t` 在 x64 是 8 bytes。參考 `KeyboardMergeInteropTest.cs` 的 P/Invoke 宣告；建置腳本會另跑 C# 到原生 DLL 的 × 按下／放開測試。DLL 本身沒有捕捉鍵盤、傳輸或裝置接入功能。
