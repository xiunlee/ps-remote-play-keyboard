# CoreHID 最小裝置試驗候選

日期：2026-10-30（Asia/Taipei）。**僅完成編譯與純資料 dry-run，未建立裝置。** 這不是 Mac 版產品，也不證明 Sony 相容。Windows 成品／版本與已發布附件不變。

## 可檢視的試驗範圍

[GenericGamepadProbe.swift](GenericGamepadProbe.swift) 使用 macOS 15+ 公開 CoreHID，準備在本人完成必要權限安排後，驗證一個本程式自有的 generic gamepad：

1. 預設執行、`--dry-run`、`--self-test` 僅核對記憶體中的 descriptor 與三個合成報告。不列舉裝置、不建立 HID、不讀取鍵盤，也不操作 Sony。
2. 只有精確指定 `--execute-generic-probe` 才進入裝置候選路徑。先唯讀檢查自己的 `com.apple.developer.hid.virtual.device` entitlement 與 Sony 程序；欠缺 entitlement 或 Sony 正在執行就停止，不請求權限、不啟停 Sony。entitlement 存在不代表 Apple provisioning 已核准，真正授權仍由系統判定。
3. 建立實驗用、非 Sony 身分的單鍵＋X/Y gamepad，`vendorID=0`、`productID=0x5053`、transport virtual、合成 UUID uniqueID；VID 不是正式分配值，僅供本機候選研究，不可直接用於產品。X/Y 永遠為零。
4. 只讀取本次 `deviceReference` 的 report 1，發送 `01 00 00 00` → `01 01 00 00` → `01 00 00 00`，間隔 150 ms。預期同一 observer 讀回完全相同的三筆；若實際 API 的 report-ID bytes 慣例不同，會失敗，須解釋差異後再改測試，不能先放寬驗證。
5. 釋放本程式持有的 `HIDVirtualDevice`，只接受以本次 uniqueID 篩選的 `deviceRemoved` 通知。CoreHID 沒有公開 cancel 方法；**作用域釋放能否及時移除仍未實測**。讀回與移除都確認才宣告通過。

這個候選沒有 UI、真鍵盤、F8、前景輸入、改鍵／連點、Sony VID/PID、PS5 操作、網路、安裝器或背景服務。generic HID 成功不代表官方 Remote Play 會接受，下一階段仍需獨立證據。

## 編譯與本輪結果

從儲存庫根目錄呼叫：

```sh
research/macos-handoff/probe/build-dry-run.sh .local/mac-probe-new-build
```

輸出目錄必須尚不存在。腳本只使用既有 Swift/SDK，編譯 arm64/macOS 15 target，執行 self-test 及 dry-run。沒有加 entitlement、找憑證、執行自訂簽章、請求 Accessibility、安裝 extension 或呼叫 live mode。

本輪確切工具、來源與執行檔 SHA-256 及結果見 [BUILD-DRY-RUN.json](BUILD-DRY-RUN.json)。Swift 的預設建置簽章只滿足本機 executable 格式，不是 Apple 核准的 HID capability 或發行簽章。第一輪編譯因 HIDReportID／SecStaticCode 型別未正確轉換而失敗，修正後第二輪編譯通過；獨立審查再修正 pending latch 的實際 executable 路徑解析，最終以第三輪全新目錄重編通過。失敗未列為測試通過。

## 下一步本人需要處理的條件

先核對 Apple 對目前開發者身分的 HID virtual device capability／provisioning 要求及可用憑證，再選定可執行且不削弱安全設定的簽章方案。**不要只把受限制 entitlement 加到 ad-hoc 簽章就當成已獲授權**，也不以 sudo、關閉 SIP、重啟、改裝驅動或偽造環境補救。授權條件見[候選研究](../OUTPUT-FEASIBILITY.md)。本輪未檢查私人憑證，也未請求任何權限。

只有在使用者檢視並同意上述短暫 generic HID 試驗與所需本人權限操作後，才準備對已核對的可執行檔使用 live flag。Sony 保持未執行的試驗時段由使用者安排；程式只拒絕，不替使用者關閉。程式在每筆報告前重新檢查，但此檢查不能原子阻止其他程序同時啟動 Sony，因此實跑期間使用者仍須維持隔離條件。

## 退出與回收

Live mode 只有一輪；一般錯誤先嘗試 neutral 再釋放引用，8 秒 watchdog 僅終止此試驗程序。SIGINT／外部終止或 watchdog 退出均**不代表裝置已移除**，不能以退出碼取代 observer 證據。

Live mode 建裝置之前，會在 binary 同目錄以 exclusive create 建立 `pending-probe.txt`，內容只有本輪 synthetic uniqueID；讀回與移除均成功且檔案身分相同才刪除此檔。失敗／中斷則保留，下一次同一 binary 目錄拒絕建立新裝置。此 latch 不能阻止另外複製一份 binary，因此未解決前也不可另建目錄規避它。需要後續以該精確 uniqueID 確認裝置不存在，才可人工核查並回收 latch；不要全域移除 HID 或停止其他程序。

本輪沒有 pending latch 或裝置。本輪已精確移除自己建立的失敗 `mac-probe-build-1`、中間成功 `mac-probe-build-2` 與最終 `mac-probe-build-3/module-cache`；`.local/mac-probe-build-3/GenericGamepadProbe` 保留。清理範圍與邏輯 bytes 見 JSON 收據，沒有刪除其他 `.local` 內容。研究來源與 JSON 證據保留。後續若回收最終 binary，先確認此程式未執行且沒有未核對的 pending latch。

## 依據

- [Apple：Creating virtual devices](https://developer.apple.com/documentation/corehid/creatingvirtualdevices)：公開 CoreHID 建立、delegate 與報告 API。
- 本機 macOS 26.5 SDK 的 CoreHID.swiftinterface：`HIDVirtualDevice`、`HIDDeviceClient`、`HIDDeviceManager` 宣告。
- 本機 SDK `IOKit/hidsystem/IOHIDUserDevice.h`：要求 `com.apple.developer.hid.virtual.device`。
- [Apple DTS：虛擬 HID entitlement 說明](https://developer.apple.com/forums/thread/843327)：不要沿用已無功能的 DriverKit virtual device entitlement。
