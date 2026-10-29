此目錄是候選預覽的獨立後端入口，並非上游 Windows CLI。固定使用 `hbashton/VIIPER` 的 `b78e31e93e84b4cb7c4b4c15b8aace10749106d2`，不修改上游來源或正式 CLI 的驅動版本檢查。

只有 `--candidate-preview` 可啟動；API 與 USB/IP 僅綁定 `127.0.0.1:33642`、`127.0.0.1:33641`。只允許四個已通過隔離 Windows 測試的 V5 persona，自動掛載與 native attach 停用。GUI 負責使用現有成套 USBip 0.9.8.1 掛載和清理。標準輸入收到 `stop`、EOF 或讀取錯誤即關閉；單次工作階段最多 30 分鐘，另有 15 秒硬退出餘裕。

`Build-Backend.ps1` 必須傳入已固定雜湊的來源 ZIP、Go 1.27.3 Windows AMD64 執行檔與全新輸出目錄。它編譯後端並執行三個純函式單元測試；測試不呼叫後端入口、不開 socket、不使用裝置或 Remote Play。所有 Go cache、相依套件及 telemetry 設定皆限制在新輸出目錄。

建置物料包括：

- `BUILD.json`：來源、測試、建置程式與二進位 SHA256。
- `MODULES.jsonl`：固定模組圖；`MATERIALS.json`：實際連結模組及授權檔 SHA256。
- `notices/`：固定上游 `LICENSE.txt`、連結之 Go 擴充模組及 Go 標準程式庫授權原文。
- `upstream/`：未修改的固定上游來源；`module/`：本入口、純單元測試、go.mod 與 go.sum。
- `PURE-TESTS.jsonl`：純參數、四 persona 白名單與 stdin EOF/stop 測試結果。

上游根授權為 GPL v3；本目錄不提供重新授權的聲明。此處的 notices 與來源紀錄供本機候選包保留來源及授權材料。候選包沒有證明 Sony Remote Play 或 PS5 操作成功，也沒有把 CI 後端的 hosted-only 限制移除。
