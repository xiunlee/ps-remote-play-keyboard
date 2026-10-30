# Mac 首輪離線驗證

日期：2026-10-30（Asia/Taipei）。研究名稱 `mac-offline.1`，產品 `VERSION` 仍是 `0.1.1`；沒有新成品、發行或標籤。來源基準為 `1e46bd0bf5774a724910621ec4d648df27c146ae`（`codex/commercial-preview`）。

**原有 native 映射核心已在本機 arm64 Mac 編譯並通過既有 5743 項檢查。** 另有 124 項 native 呼叫／Python 參考模型檢查通過，產生 22 個合成動作向量。這是記憶體資料的結果，沒有建立 HID、開啟網路 listener、擷取鍵盤或存取 Sony。

## 本輪實際執行與限制

| 範圍 | 實際結果 | 證據界線 |
| --- | --- | --- |
| 原 `keyboard_merge.c`／`.h`／`keyboard_merge_test.c` | Apple clang 原生 arm64 編譯，5743 checks、0 failures | 執行現有 C 核心及既有測試，沒有修改 Windows 原始碼 |
| 原 C 核心的動態函式呼叫 | 編譯研究用 dylib，由 Python ctypes 呼叫 `pskb_merge_keyboard` | 只驗證這組 C／ctypes ABI；沒有執行既有 Windows C# ABI 測試 |
| 22 動作、放開及停用 | 每個動作均由新建中立樣本呼叫 C 核心，核對明列預期欄位；放開／停用回到中立 | 動作 bitmask 不是 macOS key code，沒有載入個人鍵位 |
| V5 參考轉換／framing | 33-byte state、49-byte frame、inactive touch `80`／`80`、CRC、little-endian 序號，與既有 C# neutral／Cross golden frame 一致 | 執行新 Python 參考實作，**不是原 `V5Protocol.cs` 的重跑結果** |
| 記憶體序列／損壞／EOF | Python input-frame reader 拒絕重播、序號缺口、截斷及 CRC 損壞，關閉自己持有的 BytesIO，關閉後不可繼續讀取 | 只驗證此參考 reader；不是正式 feedback reader、socket、bus 或 HID 清理 |
| 暫存回收 | 本輪唯一暫存建置目錄已由 context manager 移除 | 沒有裝置、後端、服務或設定需要移除；收據保留於研究目錄 |
| 原 C# transport／Go 後端 | 本輪未執行 | 本機目前 PATH 無 .NET／Go，不沿用 Windows 歷史結果宣稱 Mac 通過 |

124 項是本輪 native 呼叫與 Python 參考模型的合計，不併入原 C suite 的 5743 項。沒有執行原 `V5FeedbackReader` 的整體 deadline 測試、`CandidateTransport` 的失連／bus 清理，或 Go `shutdownServers` 的 watchdog。Python BytesIO 關閉不能證明這些實作安全，也不能證明 macOS 會將報告接入 Sony。

## 環境與可重現方式

| 工具／平台 | 本輪版本 |
| --- | --- |
| macOS | 26.6.2，build 25G83 |
| CPU 架構 | arm64 |
| Apple clang | 21.0.0（clang-2100.1.1.101） |
| Command Line Tools SDK | 26.5 |
| Python | 3.9.6，僅標準函式庫 |

從 repository 根目錄執行，輸出目錄必須尚不存在：

```sh
python3 research/macos-handoff/run_offline.py \
  --output research/macos-handoff/records/mac-offline.2
```

Runner 透過 `xcrun --find clang` 使用既有編譯器。C 選項為 `-std=c11 -Wall -Wextra -Werror -O2`；研究 dylib 另加 `-dynamiclib`。沒有下載或安裝依賴。編譯與執行檔只存在 `tempfile.TemporaryDirectory` 建立的唯一 `pskb-mac-offline-*` 目錄，正常完成或 Python 例外退出會回收；測試輸出只保留 JSON 收據與合成向量。

本輪輸出：

```text
KEYBOARD_MERGE_TEST checks=5743 failures=0; synthetic reports only; no device or Remote Play access
PYTHON_REFERENCE checks=124 synthetic_vectors=22 original_csharp=false original_go=false
TEMPORARY_BUILD_REMOVED=true devices_created=false sony_accessed=false
```

- [RESULT.json](records/mac-offline.1/RESULT.json)：時間、精確 HEAD、版本、原始檔及研究 runner SHA-256、暫存產物 SHA-256、結果與限制；來源在執行前後雜湊相同。
- [SYNTHETIC-VECTORS.json](records/mac-offline.1/SYNTHETIC-VECTORS.json)：22 個動作的 state／frame hex；全部由合成資料產生，沒有真實按鍵或控制器擷取。
- [run_offline.py](run_offline.py)：可重現 runner、ctypes 呼叫、參考轉換／封包及記憶體 reader。程式頂端與輸出均標示參考模型的範圍。

本輪產物已自動回收，無需 detach、停止程序或修改系統設定。若不保留某輪收據，僅刪除自己明確指定的該輪 `records/mac-offline.N` 目錄；不要用全域暫存目錄清除或依程序名稱終止其他程式。強制終止 Python／機器斷電不保證 context manager 能執行；這種情況只能核對該次自建目錄後回收，不能宣稱已完成自動清理。

## 接續驗證

既有 C 核心可作 Mac adapter 的離線基準；下一階段應把實際 Mac 輸出 adapter 接到這些固定合成向量，再分別驗證自有裝置建立、按下／放開讀回與完整移除。這個步驟的 API、權限及本人操作範圍須先依[輸出可行性研究](OUTPUT-FEASIBILITY.md)具體化。

未來若沿用原 V5／Go 路徑，仍須用已核對來源的相應 runtime 實跑原 C#／Go 測試，並確認 macOS 可接入的裝置層；不因本輪參考模型通過就視為後端已移植。前景限制、F8、連點、實機鍵碼、設定遷移與 Sony／PS5 操作均未在本輪驗收。
