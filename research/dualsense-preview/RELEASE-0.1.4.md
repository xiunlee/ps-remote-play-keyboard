# 0.1.4 發布與驗證紀錄

2026-10-30，Windows 版 [v0.1.4](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.4) 發布；來源提交 `c0de54e964ae85b50c4fb0fdb1d4b89de7fc7699`，Release ID `428848090`。此版本更新：檔案整理。

- 乾淨建置通過 6984 項 C# 合成檢查、5743 項 native 檢查及 ABI 檢查；公開包通過 42 項稽核。原後端沿用先前已核對的建置，未重新執行 Go 測試。
- 九語主畫面與設定畫面完成離線繪製檢查；另有 11 個排版測試情境通過。這些不代表母語校閱、實際高 DPI 或多螢幕驗收。
- 四個公開附件匿名下載後的 SHA-256 均與發行清單一致。Windows 執行包為 `0D3E82133307FCD4D81585A7E10728CDAEDF0DE91CA0F81068730344CDB07CD3`，對應原始碼包為 `D536C6B97CFAD99FFEA2D397A98F0AFF7E880E0AA0DED75B47C3F0A297A46C70`。執行元件未簽章。
- 本機另建 0.1.4 程式、封存及捷徑，未啟動正式程式；不改動驅動、個人設定或 Remote Play。此版尚未取得逐項實機操作回報，不以離線測試或裝置狀態推定遊戲操作成功。
- [建置紀錄](../../packaging/build-records/0.1.4.json) · [公開附件清單](../../packaging/build-records/0.1.4-archives.json) · [九語離線畫面](../../assets/previews/0.1.4)。
