# Mac 原生輸出：簽章與權限研究

日期：2026-10-30（Asia/Taipei）。本文件只記錄官方文件、固定候選原始碼與主代理提供的本機盤點，不代替裝置實測。使用者已要求開始建置 Mac 版並允許研究目前連接的 PS5 手把；官方 Remote Play 保持開啟，不能自行停止、更新或重啟。

**公開 CoreHID／IOHIDUserDevice 路線存在，但需要區分簽章授權、使用者隱私權限，以及 Sony 是否接受三件事。本機無 HID 呼叫的對照實驗已證實：普通 ad-hoc app 能執行，同程式加入 virtual HID restricted entitlement 後在進入 main 前遭 AMFI 終止。這阻擋目前自行 ad-hoc 簽章的原生輸出，未證明 Mac 產品不可行。另找到免費開源、作者提供簽章 DMG 且可接收外部 JSON 輸入的 OpenJoystickDriver；實際 DMG 簽章與 Sony 相容性仍須驗證。**

## Apple 官方證據

| 證據 | 對本專案的含意 |
| --- | --- |
| 本機 SDK `IOHIDUserDevice.h` 的 create API 明載要求 `com.apple.developer.hid.virtual.device` | 改用低階 IOKit 不消除 entitlement 要求 |
| [Apple DTS HID Entitlement Configuration Guide](https://developer.apple.com/forums/thread/843327) | 此 key 控制 CoreHID 虛擬裝置 API，屬於應用程式；不是 DEXT entitlement。名稱相似的 `com.apple.developer.driverkit.family.hid.virtual.device` 已無功能 |
| [Apple DTS virtual gamepad 回覆](https://developer.apple.com/forums/thread/845599) | HID Virtual Device 的 capability 申請仍在處理；沒有提供任意個人開發的 entitlement 豁免。CoreHID 與 DEXT 的裝置發布架構相同，GameController 配對仍有未知細節 |
| [Apple：Request access to managed capabilities](https://developer.apple.com/help/account/capabilities/capability-requests) | managed capability 先由 Apple 指派到帳號，之後才能啟用；申請由 Account Holder 處理，不能單靠本地 plist 取得核准 |
| [Apple TN3125：Inside Code Signing: Provisioning Profiles](https://developer.apple.com/documentation/technotes/tn3125-inside-code-signing-provisioning-profiles) | macOS 的 unrestricted entitlement 不需 profile，但 restricted entitlement 必須由 profile 授權；簽章內宣告與 profile allowlist 是兩個獨立部分 |
| [Apple：Signing a daemon with a restricted entitlement](https://developer.apple.com/documentation/xcode/signing-a-daemon-with-a-restricted-entitlement) | 獨立 CLI 沒有嵌入 profile 的位置。若 helper 需要 restricted entitlement，應包成 app-like bundle，profile 放在 `Contents/embedded.provisionprofile`；這不表示要安裝 daemon |

HID key 的 Apple entitlement 簡介本身很短，且仍把它置於 DriverKit 相關清單；本研究以較新的 DTS 指引釐清應用程式／DEXT 差別，不照抄其他產品的所有 entitlement。DriverKit 的某些 development capability 可自助取得，不應推論 CoreHID virtual HID 的 managed capability 也有同樣規則。

## Ad-hoc、本機測試與正式發行

- 本機編譯、普通 ad-hoc executable、純合成測試不等於 virtual HID 授權；現有 dry-run 成功只證明該測試程式能執行。
- `codesign --sign -` 搭配 entitlement plist 可以在檔案上宣告 key，但不產生 Apple 簽發的 provisioning profile。不能把 `codesign -d --entitlements` 能看到 key 當成核准證據。
- 本輪主代理唯讀執行 `security find-identity -v -p codesigning` 的摘要為 **0 valid identities found**，未讀取私人金鑰。其後使用者明確回覆沒有 Apple Developer 帳號；此事與 Keychain 盤點是兩項分開的證據。
- 主代理已以[同一份不呼叫 HID 的最小 C 程式](signing-smoke.c)建立兩個 app-like bundle，結果記錄於 [SIGNING-SMOKE.json](SIGNING-SMOKE.json)：普通 ad-hoc 對照 exit 0 且到達 main；加入 `com.apple.developer.hid.virtual.device=true` 的 ad-hoc app return code -9，沒有到達 main。主代理另過濾該次 amfid 診斷，得到 `AppleMobileFileIntegrityError Code=-424`，原因為 `The file is adhoc signed but contains restricted entitlements`。這是執行前簽章拒絕，還不是 HID API、Accessibility 或 Sony 的測試結果。
- 該實驗沒有 HID 呼叫、權限請求、Sony 操作或系統安全設定變更。不能改 SIP／AMFI 或系統策略讓候選通過。
- 真正裝置階段應使用符合帳號能力與 profile 的 app bundle；本機 development profile 與對外 Developer ID／distribution profile 的適用條件分開核對。未取得 Apple 核准前，不承諾申請時間、費用或成功。

## Accessibility 不是替代簽章

[Apple 論壇 thread 820676](https://developer.apple.com/forums/thread/820676)是一位 CoreHID 使用者的第一手回報：其程式具有 HID entitlement 後仍遇到 Accessibility 提示。該帖沒有 Apple 技術回覆，因此僅能作為會遇到第二層使用者權限的線索，不能當作本機已確認的需求或 App Store 一律不准的判決。

簽章／profile 由系統驗證程式身分及 capability；Accessibility／Device Control and Data Access 是使用者授予執行中程式的另一層權限。只在系統設定開 Accessibility，不能補上一份缺少的 provisioning profile。若本機實測出現權限要求，先清楚說明對應程式與用途，再由本人操作；不讀寫 TCC 資料庫，不偽造其他程式的身分。

## 候選來源交叉核對

以下只引用作者自身文件，沒有下載或啟動其 driver／broker，也不將它們加入產品依賴。

| 固定來源 | 實際描述 | 採用界線 |
| --- | --- | --- |
| [zeromiyi/x360w `a8ac63058f29e071bbf2a7c051845db5b8f67ca1`](https://github.com/zeromiyi/x360w/blob/a8ac63058f29e071bbf2a7c051845db5b8f67ca1/README.md) | 系統手把需 managed HID key；其個人 ad-hoc 使用路線要求放寬 AMFI | 不能當作原安全設定下 ad-hoc 即可用的成功證據；本專案不採其安全設定降級路線 |
| [madeye/swpasskey `59f08259202028888934597513515fc3cb8ca8c9`](https://github.com/madeye/swpasskey/blob/59f08259202028888934597513515fc3cb8ca8c9/README.md) | 已簽章 app／profile 承載 HID key；作者明列無授權 restricted key 會在 exec 被終止，不支援 ad-hoc 簽章 | 支持 app bundle／profile 設計；它是 FIDO 工具，不證明 gamepad 或 Sony 接受 |
| [LizardByte/libvirtualhid `dd0ff885fb0d146fe3a21b3fd80b4b50e7386377`](https://github.com/LizardByte/libvirtualhid/blob/dd0ff885fb0d146fe3a21b3fd80b4b50e7386377/docs/macos-gamepad.md) | broker 的 debug build 也需 HID restricted entitlement；已簽章安裝需適用 profile，另有使用者隱私權限 | 該方案有 machine license／付款啟用，與本專案免費依賴要求不符，只作來源研究 |

## 免費現成 helper：值得驗證的候選

**OpenJoystickDriver 是本次找到最接近需求的候選，但仍不是已確認的 Sony 輸出後端。** GitHub 的 [0.6.0-alpha.1 release](https://github.com/xsyetopz/OpenJoystickDriver/releases/tag/0.6.0-alpha.1) 於 2026-10-23 提供 DMG；tag 固定為 `3552be925d7dacef3fd895ac0c6cbf39e9d3cd70`。其 MIT 原始碼及以下文件已透過 GitHub connector 唯讀核對，本研究未下載或執行 DMG。

- [來源建置說明](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/wiki/Building-from-Source.md)明載 tester DMG 由維護者簽章與公證，使用者不需 Apple Developer 帳號，SIP／AMFI 維持啟用。[簽章設計](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/docs/development/signing.md)將 virtual HID entitlement 放在 host app 的 profile；Xbox USB DEXT 是另外的能力，建立虛擬 gamepad 不靠該 DEXT。這些是作者文件聲明，尚非本機驗簽結果。
- [自動化介面](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/wiki/Automating-OpenJoystickDriver.md)及 [VirtualFeedCommand.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverCLI/Commands/Virtual/VirtualFeedCommand.swift)提供 `ojd virtual feed --as PROFILE`：外部程式將完整按鍵、方向與軸狀態以 JSON lines 寫入 stdin，已簽章 app/service 負責產生 HID。自建 client 不可直接使用同簽章限制的內部 RPC；呼叫發布的 CLI 是作者明訂路線，不需重新簽章 broker，也可先不開公開 endpoint。
- Feed 最小幀時間 8 ms，EOF／取消會清理，service 收不到更新 2 秒會移除裝置。但 CLI 在等待 stdin 時會傳 heartbeat，所以**我方鍵盤輸入失焦、停止、例外或 GUI 中斷時，仍需主動送 neutral 並關閉 feed**；不能拿 service watchdog 代替前端防卡鍵。長時間 `holdMilliseconds` 與過長佇列也會延遲清理，測試只用短幀。
- [VirtualHIDProfile.swift](https://github.com/xsyetopz/OpenJoystickDriver/blob/3552be925d7dacef3fd895ac0c6cbf39e9d3cd70/Sources/OpenJoystickDriverKit/Output/Profiles/VirtualHIDProfile.swift)只有三個固定 ABI：Xbox 的 SDLCompatible、Xbox 的 GameControllerCompatible 與 Generic，**沒有 DualShock／DualSense report profile**。資料檔的 identity 自訂不能自動補出 Sony descriptor/report 行為。作者也明列 feed 的遊戲相容性與延遲尚未驗證。
- 因此合適下一步是：只讀下載並檢查 release DMG／app 的簽章、notarization／profile 和 CLI；核對 app/service 初次啟動、登入項目及自動讀取實體手把的副作用後，再安排使用者授權範圍內的中立裝置測試。Sony 已在執行，不使用 `ojd virtual launch` 啟動或重啟 Sony，不啟用 Xbox DEXT，不改現有手把的 ownership。若 Generic／Xbox 無法被 Sony 接受，此候選也不能直接成為完成版。

另核對兩項較接近但不足以直接採用的來源：

| 候選 | 查得能力與限制 |
| --- | --- |
| [WaveBird v0.9.0](https://github.com/murphyjt/wavebird/tree/v0.9.0) | MIT；作者提供已簽章公證 DMG，包含 DualSense 等輸出 profile，但產品是 Switch 2 實體手把的 BLE bridge。未在已讀文件找到外部鍵盤／report feed API；修改 source 後仍回到我方簽章問題，不能直接當已驗證 helper |
| [Karabiner-DriverKit-VirtualHIDDevice v8.7.0](https://github.com/pqrs-org/Karabiner-DriverKit-VirtualHIDDevice/tree/v8.7.0) | 免費開源且有已簽章發布，但公開能力是虛擬鍵盤與滑鼠，非 gamepad；不為本案安裝系統 driver |

目前合理的兩條後續路線是驗證已授權、免費可散佈的外部 helper，或在未來有合適 Apple team 時申請 HID managed capability、取得匹配 identity/profile 後開發自己的 helper。未要求使用者購買帳號或付費依賴，也沒有證據能宣稱所有免費路線都不存在。

## 生命週期補記

CoreHID 的 [HIDVirtualDevice.hidDevice](https://developer.apple.com/documentation/corehid/hidvirtualdevice/hiddevice)在 macOS 26.0 起公開底層 `IOHIDUserDevice?`。這比首輪只考慮 macOS 15+ actor 作用域釋放的資訊更完整，但官方頁面只列 property，沒有說明直接更動 CoreHID 內部 cancel handler 的所有權規則。

若原生輸出改用 `IOHIDUserDevice`，本機 SDK 明訂 dispatch queue、activate、cancel 與 cancel handler 完成後 release 的順序，可對自有裝置實作明確清理。若透過 CoreHID 暴露的底層物件操作，不能覆寫 framework 正在使用的 callback 後假定安全；須驗證所有權、移除通知與錯誤路徑。兩條實作仍共用同一 entitlement 門檻。

本研究代理只查文件與原始碼；簽章 smoke test 由主代理執行並另有收據。尚無本研究的虛擬裝置或 Sony／PS5 操作成功結果，不將來源聲明或編譯通過改寫為實測成功。
