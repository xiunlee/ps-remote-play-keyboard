# 版本控管

## 版本與來源

- 根目錄 `VERSION` 是建置版本號的唯一來源；採 `主版.次版.修訂-alpha.序號`，目前開發／下一交付為 `0.1.0-alpha.10`。alpha.9 四方案已有官方 Remote Play／PS5 操作的使用者成功回報，使用者指定第二種 `dualsensegamepadv5`；alpha.10 固定此模式、整合改鍵／連點並移除 30 分鐘限制。**本次文件更新時尚未完成 alpha.10 正式建置交付或實機驗收**，不能沿用 alpha.9 結果宣稱新版已驗收。
- 測試期間每次交付有程式變更的新版，就增加 alpha 序號並更新 [CHANGELOG.md](../CHANGELOG.md)。已標記版本不重用於另一份程式碼。
- `v<版本號>` 為附註 Git 標籤，固定經測試的原始碼提交。禁止強制移動、刪除後重建或強制覆寫遠端標籤。
- 編譯檔的 ProductVersion 包含版本與 Git 短碼；未提交的工作區另有 `.dirty`。視窗顯示產品版本，狀態與啟動紀錄保存完整來源提交及 dirty 狀態。
- `alpha.5` 是事後命名的成功基準：原執行檔沒有內嵌版本，不能事後改寫它並宣稱原檔已有版本。歷史校驗與來源紀錄保留；本機副本可依最新清理授權移除，事後重建不能宣稱就是當初原檔。
- 交付後的驗收文件、建置清單與研究工具可另作提交；不因此移動版本標籤或覆寫原執行檔。本次 alpha.10 完成後，重新交付有應用程式碼變更的版本從 **alpha.11** 起增加。

## 建置、驗證與交付

1. 更新 `VERSION` 與變更紀錄，完成程式碼審查及離線測試。
2. 提交程式碼，確認工作區乾淨。**alpha.10 現行 DualSense 主線**使用以下腳本；每個輸出目錄必須尚不存在：

   ```powershell
   .\research\dualsense-preview\backend\Build-Backend.ps1 `
     -SourceArchive <固定上游來源ZIP> -GoExecutable <已核對Go執行檔> `
     -OutputDirectory <全新後端建置目錄>

   .\research\dualsense-preview\Build-Candidate.ps1 `
     -BackendDirectory <上述後端建置目錄> `
     -MappingDirectory <含固定雜湊mapping DLL的保留版本目錄> `
     -OutputDirectory <全新玩家建置目錄> -RunTests -RequireClean
   ```

   `MappingDirectory` 必須明列，內含 `pskb-keyboard-merge.dll`，SHA-256 必須為 `DE1263A6FD34E98D5DAB8DAC5C9EF1B23FC9134ED89CF306A382C6ED83B159AE`。alpha.10 建置後仍攜帶相同 DLL；清理舊 alpha.8 目錄後，下一次建置可指向保留的最新版，不能依賴已刪除的路徑或替換成任意 DLL。固定 hbashton 來源與 ZIP 雜湊由後端建置腳本核對，不用原候選 EXE 的舊 native attach 路徑。

3. 建置自動使用全新的版本目錄，寫入 `BUILD.json` 與 `SHA256SUMS.txt`；不覆寫目前版本，不啟動 GUI、後端或 USB 裝置。既有 `v<版本號>` 若指向不同提交，建置拒絕以該號碼產出候選。
4. 使用下列唯讀工具驗證程式版本與檔案雜湊：

   ```powershell
   .\research\dualsense-preview\Verify-Candidate.ps1 -BuildDirectory <玩家建置目錄> -RequireClean
   ```

5. 測試通過後，以附註標籤固定該提交並同步開發分支及指定標籤。標籤不等於 GitHub Release；正式下載、收費與第三方散布另有驗收條件。

每份 `BUILD.json` 保存來源清單、各檔 SHA-256、版本、Git 提交、建置時間、執行入口、依賴與離線測試狀態。檔案雜湊用來辨識與檢查完整性，不代表發行者數位簽章。測試成功也不等於 Sony 接受輸入；實機結果另記日期、模式、版本與使用者回報。

alpha.10 的具體 commit、tag、交付位置、建置／封存清單與雜湊，須在實際完成並核對後補入。文件中的預定步驟不是完成證據。`research/controller-free-preview/Build.ps1` 與 `packaging/Verify-ControllerFreeBuild.ps1` 留給 alpha.8 PS4／舊 DualSense 歷史路線，不能用來交付 alpha.10。

## 更新、本機清理與回退

- 使用者於 2026-10-29 明確要求「電腦空間有限，舊版本在本地端都清除吧，就留下最新版本」。這項授權取代先前本機永久保留多版本的規則；**待 alpha.10 建置交付後清舊，本次文件更新不代表清理已完成**。
- 新版仍先建置到獨立全新資料夾並驗證，不能覆寫正在使用的版本。切換完成後，本機只保留最新版與其必要依賴、可追溯紀錄，以及個人設定／備份；舊執行檔、舊捷徑、重複建置與封存可依授權清理。
- 切換只處理「上班不要玩」及它自己建立的模擬裝置；不任意關閉或重啟 Remote Play，也不碰其他程式的裝置。
- 兩版本不能同時接管 F8 或同一組服務連接埠。執行中的 alpha.9 先正常退出，再開 alpha.10；未退出前不刪該程式／後端，不把清理磁碟授權當成停止 Sony、驅動或其他軟體的授權。
- 清理前逐項核對解析後的完整路徑、專案歸屬與程序占用，僅刪已確認的舊本地檔。不要整包遞迴刪除工具根目錄，不刪 `%LOCALAPPDATA%\PSKeyboard\bindings.json`、`profile-backups` 或最新版的必要檔案。完成後補記清理範圍、保留項目與驗證結果。
- **GitHub 歷史標籤、來源、研究、PS4 路線、建置與雜湊紀錄繼續保留**。本機刪除副本不授權刪改遠端歷史，也不移動舊 tag。清理後如需回退，依舊標籤及固定依賴另行重建；不要再指示開啟已移除的本地舊捷徑。
- 按鍵設定與程式分離；格式變更前另存備份，不用舊程式強行覆寫新格式。`alpha.5` 至 `alpha.8` 使用格式 2，alpha.9 唯讀載入，alpha.10 恢復儲存格式 2；開啟與取消不寫檔，僅使用者按儲存且成功後套用。這不代表將來所有版本都能互相讀取設定。

目前主線是 `research/dualsense-preview/`，USB/IP 埠 33641、API 埠 33642。`packaging/Build-Preview.ps1` 保留為歷史實體中繼 alpha.4 封裝工具，不用它建立目前版本。正式授權、公開二進位散布與販售另行研究；本次更新與清理不更改授權或儲存庫可見性。
