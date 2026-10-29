# 版本控管

## 版本與來源

- 根目錄 `VERSION` 是建置版本號的唯一來源；採 `主版.次版.修訂-alpha.序號`。目前準備 **0.1.0-alpha.11 免費開源公開測試版**，加入自願贊助、回饋及公開封裝；尚未完成該版公開發布或使用者實機驗收。
- alpha.10 已完成乾淨建置及本機交付，來源／本地標籤固定於 `8359c1ded5cf3310ad85ed3b42e0f47fd55da586`；本次文件更新時尚未推送該標籤至遠端。見 [交付紀錄](../research/dualsense-preview/ALPHA10-TRIAL.md)，不得覆寫成 alpha.11。
- 每次交付有應用程式碼變更的新版，就增加 alpha 序號並更新 [CHANGELOG.md](../CHANGELOG.md)。alpha.11 完成後，下一個有程式變更的交付從 **alpha.12** 起增加。
- 尚未建立版本標籤、交付或公開發布的候選可在同一版號下提交修正，但每次仍建置到全新目錄，保留各自來源提交與雜湊；不得覆寫舊候選產物或把未發布建置說成已交付。
- `v<版本號>` 為附註 Git 標籤，固定經測試的原始碼提交。禁止強制移動、刪除重建或強制覆寫遠端標籤。
- 編譯檔的 ProductVersion 包含版本與 Git 短碼；未提交工作區另有 `.dirty`。視窗顯示版本，狀態／啟動紀錄保存來源提交及 dirty 狀態。
- 驗收文件與建置清單可於交付後另作提交，不因此移動版本標籤。alpha.9 的 PS5 操作成功回報不等於 alpha.10／11 每個新 session 已獲 Sony 接受。
- `alpha.5` 是事後命名的成功基準，原檔沒有內嵌版本。歷史校驗與來源保留；本機副本可依清理授權移除，事後重建不能宣稱就是當初原檔。

## 建置與驗證

1. 更新 `VERSION` 與變更紀錄，完成來源審查及離線測試；提交程式碼並保持工作區乾淨。
2. **alpha.11 現行 DualSense 主線**使用以下流程，每個輸出目錄必須尚不存在：

   ```powershell
   .\research\dualsense-preview\backend\Build-Backend.ps1 `
     -SourceArchive <固定上游來源ZIP> -GoExecutable <已核對Go執行檔> `
     -OutputDirectory <全新後端建置目錄>

   .\research\dualsense-preview\Build-Candidate.ps1 `
     -BackendDirectory <上述後端建置目錄> -Zig <Zig執行檔完整路徑> `
     -OutputDirectory <全新玩家建置目錄> -RunTests -RequireClean

   .\research\dualsense-preview\Verify-Candidate.ps1 `
     -BuildDirectory <玩家建置目錄> -RequireClean
   ```

   `-Zig` 必填，不再使用 alpha.10 的 `-MappingDirectory`。建置從未變更的 `research/native-merge/keyboard_merge.c`／`.h` 編譯 mapping DLL，不依賴舊版二進位檔。固定 Zig 0.17.0，套件／執行檔雜湊及 native 測試由建置流程核對；保存 `NATIVE-BUILD.json`、`NATIVE-MATERIALS.json` 與適用 notices。

3. 後端固定 hbashton/VIIPER commit `b78e31e93e84b4cb7c4b4c15b8aace10749106d2`，以受限入口建置，不使用原候選的舊 native attach。MATERIALS 公開輸出須移除本機個人路徑；這不表示控制協定已更換。
4. `BUILD.json`／`SHA256SUMS.txt` 記錄版本、提交、來源與產物雜湊、工具、依賴及測試狀態。來源在建置中變動、版本標籤指向別份提交或輸出目錄已存在時，必須停止，不覆寫舊產物。
5. 建置與離線驗證不安裝驅動、不接入 Windows 裝置、不啟停 Sony。離線渲染只使用合成設定，不開啟瀏覽器／郵件程式或擷取真實按鍵。

雜湊只證明檔案身分與完整性，不等於數位簽章或 Sony 實機驗收。alpha.11 的具體來源提交、最終測試、產物雜湊與交付位置，完成核對後另補紀錄，不預填成功。

`research/controller-free-preview/Build.ps1` 與 `packaging/Verify-ControllerFreeBuild.ps1` 僅供 alpha.8 PS4／舊 DualSense 歷史路線。`packaging/Build-Preview.ps1` 是 alpha.4 實體中繼封裝工具；不能用上述舊工具交付目前主線。

## 免費公開發布

- 原始碼使用 [GPL-3.0-only](../LICENSE)，作者 **xiunlee**；詳見 [COPYRIGHT.md](../COPYRIGHT.md)。儲存庫維持公開，歷史分支名稱 `codex/commercial-preview` 不代表收費計畫。
- 測試通過後，以附註標籤固定來源並推送指定分支／標籤，再核對遠端；不移動既有 tag。alpha 測試版使用 GitHub **pre-release**，不宣稱穩定版。
- 公開執行包與完整對應原始碼包須在同一 Release 提供。來源包含本專案精確來源、固定 VIIPER 與實際 Go 依賴來源、native 來源、建置腳本及重建說明；執行包保留各元件 notices、材料清單與 SHA-256。
- 發布前檢查來源包是否足以重建所交付元件，並核對第三方授權、公開檔案白名單及個人路徑排除；不要只附本專案 source ZIP 就宣稱後端對應原始碼完整。
- 本包不含 USBip 驅動，也不會自動安裝或更換。下載頁清楚列出 0.9.8.1 成套先決條件與未驗收項目。完整核對見 [發行工作表](../packaging/RELEASE-CHECKLIST.md)。
- 使用者最後指定[PayPal 贊助頁](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)，公開頁設定已唯讀核對為啟用、USD 1.00，以自願贊助 US$1 幫三隻貓咪買罐頭為文案；程式僅在點擊後開啟瀏覽器，不接收付款資料。最終來源、文件及包內只保留此入口，排除先前已撤回的連結。HTTP GET 或頁面核對不等於實付驗收入帳，也不阻擋完成核對的免費版公開發布。
- [alpha.11 預定發布頁（待發布）](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11)須於實際上傳及下載核對後才改為已發布。

## 更新、本機清理與回退

- 使用者要求本機舊版本清除，只留最新版本。待 alpha.11 建置交付及正常切換後，最終只留 alpha.11、必要依賴及可追溯紀錄；個人設定／備份另行保留。文件中的授權不等於清理已完成。
- 新版先建置到全新資料夾並驗證，不能覆寫運行中的版本。舊版正常退出後才切換／清除，不讓兩版爭用 F8 或同一組服務埠。
- 清理前核對解析後的完整路徑、專案歸屬與程序占用，只刪確認的舊執行檔、捷徑、重複建置與封存。不對工具根目錄整包遞迴刪除，不停止 Sony、驅動或其他軟體。
- 不刪 `%LOCALAPPDATA%\PSKeyboard\bindings.json`、`profile-backups` 或最新版必要檔案。清理完成後補記範圍、保留項目與驗證結果。
- GitHub 歷史標籤、PS4 原始碼、研究、建置與雜湊紀錄繼續保留。清舊後如需回退，依舊標籤與固定依賴另行重建，不能再指示開已移除的捷徑。
- 設定與程式分離；alpha.9 唯讀載入，alpha.10／11 僅使用者按儲存且成功後更新格式 2。開啟或取消不寫檔；格式變更前另存備份，不用舊程式強行覆寫新格式。
