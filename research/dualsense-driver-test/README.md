# DualSense 與既有 0.9.8.1 驅動的隔離驗證

這是獨立研究工具，不是新版「上班不要玩」，也不供玩家啟動。成品維持 alpha.8；不得用本工具替換現行 PS4 後端。

目的：在可丟棄的 GitHub 標準 Windows 測試機，安裝與本機相同的官方 usbip-win2 **0.9.8.1**，測試固定 hbashton 候選的複合 DualSense 接入、實際 HID 按鍵讀回及移除。**不在使用者電腦安裝或切換驅動，不連線 Sony、不擷取鍵盤，也不將 Windows 通過稱作 Remote Play 成功。**

使用者希望能逐一比較不同方案，因此隔離測試涵蓋四個固定來源已實作的 persona，各自在獨立 VM 執行；沒有任意偽造型號，也不把同時接入多個手把當比較方式。

| 選項 | 固定 persona | PID |
| --- | --- | --- |
| 一般 DualSense，含音訊介面 | `dualsensecombinedaudioduplexv5` | `0CE6` |
| 一般 DualSense，僅手把介面 | `dualsensegamepadv5` | `0CE6` |
| DualSense Edge，含音訊介面 | `dualsenseedgecombinedaudioduplexv5` | `0DF2` |
| DualSense Edge，僅手把介面 | `dualsenseedgegamepadv5` | `0DF2` |

VID 均為 `054C`。本輪驗證共同的 22 個動作，不宣稱 Edge 額外背鍵、音訊串流或觸覺功能通過。通過隔離門檻後才可製作獨立玩家測試版本。

## 為何另外寫接入工具

候選正式 CLI 要求 0.9.7.7，而且內建 native attach 使用舊版 IOCTL 結構；只拿掉版本檢查仍不相容。詳細來源見 [COMPATIBILITY.md](COMPATIBILITY.md)。研究 fixture 僅取固定來源的網路後端，關閉 native／自動接入，由 **0.9.8.1 成套安裝內的 CLI 與 DLL** 呼叫其自己的驅動。

原本 `dualsense-v5-test` 的使用者空間測試保留不變。這個 fixture 使用另一組 loopback 埠 `33541/33542`，其 ping 明確標示由外部測試工具負責 Windows 接入，不沿用 `windowsAttached=false` 的舊標記。

## 執行範圍

- [工作流程](../../.github/workflows/dualsense-driver-0981.yml) 只在指定公開儲存庫、`codex/commercial-preview` 分支的相關程式變更時執行，使用標準 `windows-2025`；沒有付費大型 runner、快取或 artifact 上傳。
- `Run-HostedDriverTest.ps1`、Go fixture 與 C# 測試均檢查 GitHub hosted Windows 環境及明確測試引數。本機只可編譯／檢視，**不可偽造環境變數在本機執行**。環境檢查用於防誤啟動，不是密碼學證明。
- 下載檔固定 SHA-256；安裝只發生在一次性的遠端 VM，使用 `/NORESTART`，不重開測試機。安裝器 `AlwaysRestart=yes` 會要求重啟，因此記錄退出碼 3010，並以實際驅動載入和讀回結果判定，不能將 3010 單獨當成功或失敗。
- 只接入自己建立的 bus，驗證完整 `usbip://127.0.0.1:33541/<bus>`；只讀取本次新增、且位於 USB/IP UDE 下的 DualSense HID。清理只處理自己的 port、bus 及子程序。
- 以合成的 22 個動作逐一按下／放開，測量 Windows `ReadFile` 收到的報告。它不是從 Windows 裝置名稱推論按鍵成功。
- 固定輸出來源與結果留在 Actions logs／job summary；不公開第三方執行檔、不動既有版本或標籤。

GitHub 標準公開儲存庫 runner 的免費政策見 [官方說明](https://docs.github.com/en/billing/concepts/product-billing/github-actions)。Windows hosted runner 是獨立新 VM，見 [官方 runner 說明](https://docs.github.com/en/actions/concepts/runners/github-hosted-runners)。

## 證據界線

`windows-2025` 是 Windows Server 測試環境，與使用者 Windows 11 不相同。通過可支持這組固定候選／0.9.8.1 的 Windows 驅動與 HID 路徑；失敗必須先區分測試工具、Server 環境、驅動載入及協定。兩者都不能直接判定使用者的 Sony Remote Play 可用與否。完整音訊、觸覺、長時間穩定性和官方 Remote Play 操作仍需各自驗證。
