# 按鍵生命週期測試

這些純事件測試驗證 `KeyboardInputState`，沒有安裝鍵盤鉤子、不呼叫 Windows 輸入 API，也不接觸 Remote Play。涵蓋停用時已按住再啟用、失焦／暫停後的重複按下、快捷鍵修飾鍵、注入事件、按鍵放開與多鍵同時操作，以及重設和事件處理並行。

呼叫端在停用、暫停、失焦、連線中斷時呼叫 `Reset()`，清除遊戲動作。已被擷取的實體按壓仍保留標記，後續重複及放開持續被抑制，直到確實放開；這可避免 Windows 收到不成對的按下／放開事件。若原先按下已交給 Windows，途中啟用也不會接管其重複或放開。重新擷取必須先放開，再重新按下。

```powershell
& .\research\keyboard-input-tests\BuildAndTest.ps1 -OutputDirectory '<測試輸出絕對路徑>'
```
