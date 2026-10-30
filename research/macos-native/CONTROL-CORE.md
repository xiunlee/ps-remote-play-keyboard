# Mac 鍵盤與連點純核心

2026-10-30（Asia/Taipei）。[ControlCore.swift](../../src/macos/ControlCore.swift)已換成適合精簡 Mac 鍵盤的預設配置，以 Swift 6 原生編譯，通過 **459 項合成事件／假時鐘檢查**。本輪未擷取真鍵盤、建立裝置、存取個人設定或操作 Sony；這不是前景識別、Esc 硬體事件或 PS5 實機驗收。操作用鍵位表見 [Mac 預設鍵位](../../docs/MAC-DEFAULT-KEYS.md)。

## 範圍與格式

- `MacAction` 的 22 個 bit 順序與既有 `keyboard_merge.h` 完全一致。輸出為 `UInt32` action mask，尚不含 DualSense／HID report 編碼。
- `MacBindingProfile` 是不可變設定值；`withBinding`／`withRepeat` 回傳新值。每個動作可用一個 Mac hardware key code，`nil` 表示未指定；**Mac 的 A 是 0，不以 0 表示未指定**。
- 格式標記是 `platform="macOS-hardware-keycode"`、`schemaVersion=1`。輸入初始化會拒絕其他平台、版本、重複鍵、保留鍵及範圍外連點間隔。未提供檔案載入／儲存，未讀取或轉換 Windows 格式 1／2；未來序列化必須保存並驗證平台與格式兩項欄位。
- 連點模式為 `.off`、`.hold`、`.toggle`，每動作 20–5000 毫秒、預設 100。取樣當下半週期，延遲不補送一串脈衝；奇數間隔以 `phase * 2 < interval` 保留原語義。
- Esc（`0x35`）是保留控制鍵，不可綁到遊戲動作。只有官方 Remote Play 前景、輸出已連線、無禁止修飾鍵且未編輯時，新的實體 Esc down 可切換啟用；重複 keydown 與注入事件不會切換。原 F8 不再切換預設控制狀態；F8 仍屬可供未來自訂綁定的有效硬體鍵碼。

預設鍵碼來源是本機 SDK 26.5 的 Carbon／HIToolbox `Events.h`，鍵碼依實體 ANSI 位置，不是字元翻譯或 Windows VK。新預設為 W/S/A/D 左搖桿、方向鍵十字鍵、I/J/K/L 四圖形鍵（△／□／×／○）、Q/E 肩鍵、Z/C 扳機、Tab/Return、F/Space、P/T。22 個動作加 Esc 共 23 個不同的單鍵，沒有 Fn、功能鍵、Home、End、Insert 或 Page 鍵需求。

原先依賴 Home／End 等導航鍵的配置不再是 Mac 預設。核心仍允許未來自訂功能／導航鍵，但不猜測 Fn 組合會產生哪個鍵碼。`modifiersDown` 的契約仍不包含 Fn；新預設本身不需要它。

## 整合契約

`MacControlCore` 是 `Sendable` value type，沒有鎖或全域狀態。呼叫端必須在同一 actor／序列 queue 持有並修改同一份核心，不在不同 callback 中各複製一份狀態。App 編譯只加入 `ControlCore.swift`；`ControlCoreTests.swift` 有測試專用 `@main`。

```swift
var core = MacControlCore(profile: .default)
core.updateEnvironment(foreground: sonyIsFrontmost, connected: outputIsReady,
                       modifiersDown: commandControlOptionOrShift, editing: isEditing)
// UI 按鈕或 Esc 改變啟用狀態；不要在每個取樣重新 setEnabled。
core.setEnabled(true)
let consume = core.process(keyCode: keyCode, isDown: isDown,
                           isRepeat: isRepeat, injected: isInjected,
                           atMilliseconds: monotonicMilliseconds)
let actions = core.evaluate(atMilliseconds: monotonicMilliseconds)
```

1. `updateEnvironment` 的前景判斷、連線與修飾鍵來源由 app 實作；核心不自行信任程序名稱或取用 OS。必須在前景／修飾鍵／連線變化時，以及每次輸出前更新。`modifiersDown` 表示 Command／Control／Option／Shift 等禁止遊戲輸入的修飾鍵，不含必要的 Fn。
2. `setEnabled(true)` 要求輸出已連線且不在編輯；可以由設定視窗的按鈕預先啟用，但只有 Sony 前景且重新按下按鍵才有輸出。斷線與進入編輯會停用，重連或離開編輯不自動啟用。
3. 傳入全部真實 down/up 事件，包括 Sony 以外視窗，以追蹤既有按住狀態。核心只記憶必要的按住／歸屬，不寫按鍵歷史。注入事件完全忽略；不得拿注入事件的旗標當作真實系統修飾鍵狀態。
4. `process` 回傳該事件是否應攔截：被遊戲接收的 down，其 repeats/up 即使切窗仍保持配對消耗；原本交給其他視窗的 down，進入 Sony 或改鍵後不會突然變成遊戲按鍵。首次觀測到的是 OS repeat 時，也不假定先前 down 已由本程式接收。
5. 失焦、修飾鍵、Esc、`reset()`、輸出失敗或編輯清除連點；已按住的鍵必須先看到 release，再以新的 down 重啟。切回 Sony 不恢復原連點。`applyProfile` 清狀態並暫停，呼叫端只應在使用者儲存成功後發布；儲存失敗不呼叫。
6. `atMilliseconds` 是 `UInt64` 單調毫秒值，例如由 uptime 取得。較舊樣本會夾到上一個時間，不會倒轉脈衝；核心不自己讀時鐘。`evaluate` 在不符合 active 條件時一律回 0。

如果 event tap 曾停止而漏掉 key-up，必須重新確認鍵盤狀態或等待該鍵真實 release；不可為了恢復輸入就無條件清空歸屬並把 OS repeat 當成新按下。

## 本輪驗證與重跑

在 repo 根目錄，以全新輸出目錄編譯；下列指令不開啟 AppKit／HID 或安裝任何權限：

```sh
mkdir .local/mac-core-new-build
swiftc -swift-version 6 -warnings-as-errors -parse-as-library \
  -module-cache-path .local/mac-core-new-build/module-cache \
  src/macos/ControlCore.swift src/macos/ControlCoreTests.swift \
  -o .local/mac-core-new-build/ControlCoreTests
.local/mac-core-new-build/ControlCoreTests
```

結果：

```text
MAC_CONTROL_CORE_PASS checks=459 actions=22 synthetic_only=true keyboard_captured=false device_created=false sony_accessed=false
```

涵蓋 22 個新預設鍵位及反向查找、23 鍵唯一性與精簡鍵盤鍵碼集合、Esc 保留／F8 不再切換、UI 按鈕預先待命後切入 Sony 的新按鍵語義，以及所有 22 動作的正常按下／放開、hold/toggle、最短及最長與奇數間隔、不同動作相位、延遲與亂序時鐘、最大 `UInt64`、原事件配對、失焦、修飾鍵、注入事件、首次 OS repeat、斷線、編輯、改鍵時已按住與未指定鍵語義。不同 App 執行緒／actor 的整合排程與真實鍵盤、event tap 及 HID 尚未由本輪驗收。

| 項目 | 本輪收據 |
| --- | --- |
| 基準 commit | `0d31270ac24edce808891833c75632fb87ebe718`；本輪變更在工作樹中建置 |
| 工具 | Apple Swift 6.3.3（swiftlang-6.3.3.1.3）、Swift language 6、arm64 |
| ControlCore.swift SHA-256 | `51a9690e62fc40a2c2f503a7adc86f7d18bc11dfa5774234eeb90cc4a4e8eb5b` |
| ControlCoreTests.swift SHA-256 | `312a9b366d47a98bc53b2a8a50e5cacaafa1148fc3805c6e4c445d9fded80cb5` |
| 測試 executable SHA-256 | `3139df32667650a2511f27393e0c6358ac0d165fbfb35218bc9f79ee8622ef35` |

前兩輪原配置分別通過 418／426 項；本輪更新精簡鍵位及 Esc 後通過 459 項。最新 executable 與 JSON 收據留在 `.local/mac-core-build-3`；來源與測試收據使用新的 hash，不沿用舊預設的雜湊。原有 Windows 來源、設定與產品版本檔皆未更動。

另以新預設重跑 V5 協定邊界驗證，22,958 項通過，包含新 K 鍵經核心輸出 Cross 的 golden frame；收據在 `.local/mac-v5-protocol-2/RESULT.json`。本輪自己的 core module cache 已回收 16,213,876 logical bytes，V5 測試程式也回收其自建 cache；最終 executable、來源與收據保留。
