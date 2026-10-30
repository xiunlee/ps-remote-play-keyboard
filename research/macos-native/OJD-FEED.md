# OpenJoystickDriver stdin feed adapter

2026-10-30（Asia/Taipei）。[OJDFeed.swift](../../src/macos/OJDFeed.swift) 是獨立撰寫的純 Swift adapter，將 `MacControlCore` 的 action mask 轉成 OJD 的完整 JSON 行。不建立程序、pipe、裝置或 event tap，不讀時鐘、鍵盤、設定檔，不操作 Sony。來源固定為 OpenJoystickDriver **0.6.0-alpha.1 / `3552be925d7dacef3fd895ac0c6cbf39e9d3cd70`**；可序列化與傳輸格式相容，不代表官方 PS Remote Play 已接受該虛擬手把。

## 固定來源與能力

實作依下列固定 commit 的原始碼查核，未複製第三方實作。上游 MIT 與本專案 GPL-3.0-only 相容。

- [VirtualFeedCommand.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Commands/Virtual/VirtualFeedCommand.swift)：`virtual feed --as` 讀 stdin，依序處理 JSON；EOF 排空已接受佇列後關閉 feed。CLI 在讀取 stdin 時發送 session heartbeat，caller 不自行呼叫私人 service RPC。
- [VirtualFeedPayloads.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/ApplicationService/Payloads/VirtualFeedPayloads.swift)：接受 `buttons`、`dpad`、`axes`、可選 `holdMilliseconds`；未指定的控制項為 neutral。最小 frame 8 ms、最大佇列 256、idle timeout 2 秒、heartbeat 0.5 秒。adapter 不設定 hold、不排積壓佇列。
- [ControllerDomain.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Device/Controller/ControllerDomain.swift) 與 [Controls.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Remapping/Profile/Controls/Controls.swift)：raw 字串區分大小寫；dpad 必須使用 `DpadUp`／`DpadDown`／`DpadLeft`／`DpadRight`，不是 wiki 範例中的小寫 `up`。
- [VirtualHIDProfile.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Output/Profiles/VirtualHIDProfile.swift) 與 [VirtualDevice.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Output/Profiles/VirtualDevice.swift)：固定來源包含三個 profile：`generic`、`sdl-compatible`、`game-controller-compatible`，皆非 DualSense。
- [VirtualGamepadReportMapping.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Output/Backends/VirtualGamepadReportMapping.swift)、[PersonaControlMappingTests.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Tests/OpenJoystickDriverKitTests/Output/Backends/PersonaControlMappingTests.swift)：`TouchpadClick` 雖能 decode，report mapping 不輸出它；不能只因 JSON 合法便宣稱觸控板按鍵可用。
- [GlobalOptions.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Root/GlobalOptions.swift)：`--no-input` 禁止互動提示、`--json` 使用機器可讀結果、`--timeout 5` 是 RPC timeout 秒數，並非每行持續時間或五秒測試時限。

| Action | JSON / 能力 |
| --- | --- |
| 左搖桿上下左右 | `LeftStickX/Y`，範圍 −1…1，Y 正值向下；對向抵消後，斜角乘 √½ |
| 十字鍵上下左右 | `dpad` 陣列使用上述四個 raw 值；對向抵消、斜向保留兩個值 |
| Cross / Circle / Square / Triangle | `South` / `East` / `West` / `North` |
| L1 / R1 | `LeftShoulder` / `RightShoulder` |
| L2 / R2 | `LeftTrigger` / `RightTrigger` buttons，對應軸同時為 1；放開軸為 0 |
| Create / Options | `Back` / `Start`；Create 不映到 Xbox `Capture` |
| L3 / R3 | `LeftStickClick` / `RightStickClick` |
| PS | `Guide`；代表 Generic／Xbox Guide 能力，Sony 是否解讀成 PS 鍵仍須實測 |
| Touch click | **拒絕**：拋 `unsupportedActions`，不送出偽成功 JSON；此輸出模式必須將此綁定設為 `nil`，UI 顯示不支援 |

`OJDFeedEncoder.supportedActions` 包含 21 個 action bits；`unsupportedActions` 為 touchClick。未知 action bit 另拋 `unknownActions`。三個 profile 皆使用同一 JSON 控制項集合，實際 HID persona 不同；不得將任一 profile 描述成 DualSense。

## 整合 API 與 I/O 邊界

純 adapter 整合使用 `ControlCore.swift`、`OJDFeed.swift`；研究 App 另編入 `PreviewPipe.swift` 與 `KeyboardPreviewApp.swift`。各個 `*Tests.swift` 有測試入口，不能混入 app target。

```swift
let profile = OJDFeedProfile.gameControllerCompatible
// 呼叫端以已核對的 OJD executable 路徑與下列 arguments 建立程序。
let arguments = profile.cliArguments
// ["--no-input", "--json", "--timeout", "5", "virtual", "feed",
//  "--as", "game-controller-compatible"]
let data = try OJDFeedEncoder.line(actions: mask, profile: profile)
// Data 含一個末尾 LF，可直接交給 caller 的非阻塞寫入器。
```

每行包含三個欄位與完整六軸：左搖桿、右搖桿、兩個 triggers。未使用的右搖桿永遠是零。buttons/dpad 無重複且排序固定，JSON key 排序固定；沒有 envelope、額外鍵或 `holdMilliseconds`。例如 neutral：

```json
{"axes":{"LeftStickX":0,"LeftStickY":0,"LeftTrigger":0,"RightStickX":0,"RightStickY":0,"RightTrigger":0},"buttons":[],"dpad":[]}
```

Caller 可使用 encoder 搭配自己的排程器，也可使用純狀態機 `OJDFeedSession(profile:frameIntervalMilliseconds:)`。間隔必須 ≥8 ms。狀態機以傳入的 `UInt64` 單調毫秒值運作，較舊時間會夾到前值。

1. `next(actions:atMilliseconds:)` 首次固定回 `.writeLine(neutralData)`。初始 neutral 不採用傳入 mask；啟用前仍須單獨驗證設定能力。
2. Caller 完整成功寫入該行後呼叫 `writeSucceeded(atMilliseconds:)`；等待完成期間 `next` 不產生下一行。從完成時間起至少間隔 8 ms；延遲只送最新取樣，沒有 catch-up burst 或舊狀態佇列。最短於輸出取樣間隔的普通短按不保證送達。
3. 正常輸出時 `next` 若遇 unsupported/unknown mask 會拋錯並進入 stopping；caller 應停用核心並繼續完成 neutral／EOF，不可吞錯後繼續控制。
4. F8 停用或失焦時，核心立即清空狀態，下一合法時槽輸出 neutral；需要關閉 feed 時呼叫 `requestStop()`。狀態機會在 pending write 完成及最小間隔滿足後送 final neutral；收到成功 ack 才產生 `.closeStdin`。
5. Caller 關閉 stdin 後呼叫 `acknowledgeStdinClosed()`。`.stdinClosed` 只代表 caller 已承認本地 FD 關閉；**不是 OJD 程序已結束、服務已處理 final neutral、虛擬裝置已刪除或 Sony 已釋放**。這些需 parent 的程序／裝置層核實。正常 EOF 的上游行為是處理完已接受 frames 後關閉 feed。
6. 寫入失敗、partial write 後放棄或 timeout 呼叫 `writeFailed()`。狀態機保留 `ioFailed=true` 並要求關閉 stdin，不在可能含半行 JSON 的 pipe 上補送；此時 neutral 送達與清理尚未確認，caller 需執行自己的失敗清理。

實際 I/O 層應在單一序列 queue／actor 持有狀態機，處理 partial writes 與 EAGAIN；只有整行完成才 ack。不要在 timer 每 tick 無界排隊；讀取 stdout/stderr 也要避免子程序因 pipe 滿而阻塞。此 adapter 沒有 timeout timer、process termination、裝置清理或 Remote Play 前景識別。CLI 的 `--no-input` 指提示互動，不是 macOS 鍵盤權限或輸入攔截開關。

## 合成驗證

測試不啟動 OJD、不呼叫 service、不建立裝置、不擷取鍵盤、不切換 Sony。涵蓋所有 22 action、三個 profile、觸控板／未知 bits 拒絕、全六軸 neutral、方向與斜角、對向抵消、trigger 雙欄位、Guide／Back、精確 JSON raw 字串、至少 8 ms、pending write、延遲不補送、回退／極大時鐘、stop 中寫入、final neutral 後 EOF、partial-write failure、重複停止／失敗，以及真正 `MacControlCore` toggle／失焦輸入的 adapter 邊界。

```sh
mkdir .local/ojd-feed-new-build
swiftc -swift-version 6 -warnings-as-errors \
  -module-cache-path .local/ojd-feed-new-build/module-cache \
  src/macos/ControlCore.swift src/macos/OJDFeed.swift src/macos/OJDFeedTests.swift \
  -o .local/ojd-feed-new-build/OJDFeedTests
.local/ojd-feed-new-build/OJDFeedTests
```

最終結果：`PASS: 205 OJD feed pure checks (no subprocess, devices, keyboard or Sony access)`。工具為 Apple Swift 6.3.3（swiftlang-6.3.3.1.3）、Swift language 6、warnings-as-errors、arm64；基準 commit 為 `7e198f7c0f1408de97cdedb10009e0da9e24c5d3`，新來源在工作樹中建置。

| 項目 | SHA-256 |
| --- | --- |
| ControlCore.swift | `39863f3efec47506866fc533496a93a38cc3805853f64079d039c06be3747614` |
| OJDFeed.swift | `6e8e827bf463c02fdfa73230a681032f886d8e58ce42892814e835d2eec5ea8d` |
| OJDFeedTests.swift | `c4159480fce5da736ed9162cb81dc59bde4a3d25868619536e347729b8490025` |
| 最終測試 executable | `2c5e3afbef5eeaf6b26fa2c7bf8326ea3e2c16a70bdf2f2b8f76f249350864fb` |

最新測試 executable 與 `RESULT.json` 留在 `.local/ojd-feed-build-2`。只回收自己建立的 `.local/ojd-feed-build-1` 與 `.local/ojd-feed-build-2/module-cache`，共 187,052,832 logical bytes；這不是磁碟淨增加量。裝置與前端實機結果由 parent 的驗證文件另行記錄。
