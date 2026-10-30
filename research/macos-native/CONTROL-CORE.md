# Mac 鍵盤與連點純核心

2026-10-30（Asia/Taipei）。[ControlCore.swift](../../src/macos/ControlCore.swift)已以 Swift 6 原生編譯，通過 **426 項合成事件／假時鐘檢查**。本輪未擷取真鍵盤、建立裝置、存取個人設定或操作 Sony；這不是前景識別、F8 硬體事件或 PS5 實機驗收。

## 範圍與格式

- `MacAction` 的 22 個 bit 順序與既有 `keyboard_merge.h` 完全一致。輸出為 `UInt32` action mask，尚不含 DualSense／HID report 編碼。
- `MacBindingProfile` 是不可變設定值；`withBinding`／`withRepeat` 回傳新值。每個動作可用一個 Mac hardware key code，`nil` 表示未指定；**Mac 的 A 是 0，不以 0 表示未指定**。
- 格式標記是 `platform="macOS-hardware-keycode"`、`schemaVersion=1`。輸入初始化會拒絕其他平台、版本、重複鍵、保留鍵及範圍外連點間隔。未提供檔案載入／儲存，未讀取或轉換 Windows 格式 1／2；未來序列化必須保存並驗證平台與格式兩項欄位。
- 連點模式為 `.off`、`.hold`、`.toggle`，每動作 20–5000 毫秒、預設 100。取樣當下半週期，延遲不補送一串脈衝；奇數間隔以 `phase * 2 < interval` 保留原語義。
- F8 是保留控制鍵，不可綁到遊戲動作。只有官方 Remote Play 前景、輸出已連線、無禁止修飾鍵且未編輯時，新的實體 F8 down 可切換啟用；重複 keydown 與注入事件不會切換。

預設鍵碼來源是本機 SDK 26.5 的 Carbon／HIToolbox `Events.h`，鍵碼依實體 ANSI 位置，不是字元翻譯或 Windows VK。預設沿用 W/S/A/D 左搖桿、方向鍵四圖形鍵、Home/End/Help（Insert）/PageUp 十字鍵、E/ForwardDelete/Q/PageDown 肩鍵、Tab/Return、F/Space、F1/R。

精簡 Mac 鍵盤通常需要 Fn 或額外按鍵配置才能送出 F8、Home/End、PageUp/PageDown、ForwardDelete；Help／Insert 未必有對應實體鍵。**Fn+方向／Delete 實際發出的 key code 與旗標尚未擷取驗證**。核心不自行猜測 Fn 組合等同某導航鍵，不能把本輪純測試當成所有精簡鍵盤可用。輸入 adapter 需要實測或明確的改鍵 UI；不要把 Fn 與 Command／Control／Option／Shift 一起全面封鎖，否則 Fn+F8 可能無法使用。

## 整合契約

`MacControlCore` 是 `Sendable` value type，沒有鎖或全域狀態。呼叫端必須在同一 actor／序列 queue 持有並修改同一份核心，不在不同 callback 中各複製一份狀態。App 編譯只加入 `ControlCore.swift`；`ControlCoreTests.swift` 有測試專用 `@main`。

```swift
var core = MacControlCore(profile: .default)
core.updateEnvironment(foreground: sonyIsFrontmost, connected: outputIsReady,
                       modifiersDown: commandControlOptionOrShift, editing: isEditing)
// UI 按鈕或 F8 改變啟用狀態；不要在每個取樣重新 setEnabled。
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
5. 失焦、修飾鍵、F8、`reset()`、輸出失敗或編輯清除連點；已按住的鍵必須先看到 release，再以新的 down 重啟。切回 Sony 不恢復原連點。`applyProfile` 清狀態並暫停，呼叫端只應在使用者儲存成功後發布；儲存失敗不呼叫。
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
MAC_CONTROL_CORE_PASS checks=426 actions=22 synthetic_only=true keyboard_captured=false device_created=false sony_accessed=false
```

涵蓋所有 22 動作的正常按下／放開、hold/toggle、最短及最長與奇數間隔、短於輸出週期的兩次切換、不同動作相位、延遲與亂序時鐘、最大 `UInt64`、原事件配對、失焦、修飾鍵、F8、注入事件、首次 OS repeat、斷線、編輯、改鍵時已按住與未指定鍵語義。不同 App 執行緒／actor 的整合排程與真實鍵盤、Fn 及 HID 尚未驗收。

| 項目 | 本輪收據 |
| --- | --- |
| 基準 commit | `7e198f7c0f1408de97cdedb10009e0da9e24c5d3`；新檔在工作樹中建置 |
| 工具 | Apple Swift 6.3.3（swiftlang-6.3.3.1.3）、Swift language 6、arm64 |
| ControlCore.swift SHA-256 | `39863f3efec47506866fc533496a93a38cc3805853f64079d039c06be3747614` |
| ControlCoreTests.swift SHA-256 | `550ef30fc43af785bd5dd61a237d4555e0163278cde6d6e6acdf571e0780e6f0` |
| 最終測試 executable SHA-256 | `2d043f0f48a827c8a70eaed29c4881e9e046eee49095ed21a872340cd760eae1` |

第一輪 418 項通過後，增加修飾／注入 F8 與改鍵遇到既有外部按住的案例；第二輪 426 項通過。最終 executable 與 JSON 收據留在 `.local/mac-core-build-2`。自己建立的中間 `.local/mac-core-build-1` 及最終 `module-cache` 已逐項回收，合計 32,569,384 logical bytes；這不是磁碟淨增加量。沒有移除其他目錄或處理任何裝置／程序。
