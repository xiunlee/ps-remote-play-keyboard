# 0.1.3 發布與驗證紀錄

2026-10-30，Windows 版 [v0.1.3](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.3) 發布；來源提交 `8f1918e48bfadc1b8d7120b1991b497d060fc764`，Release ID `428835423`。此版本更新：名稱與圖示更新。

- 乾淨建置通過 6984 項 C# 合成檢查、5743 項 native 檢查及 ABI 檢查；公開包通過 41 項稽核。原後端沿用先前已核對的建置，未重新執行 Go 測試。
- 九語主畫面與設定畫面完成離線繪製檢查；另有 11 個排版測試情境通過。這些不代表母語校閱、實際高 DPI 或多螢幕驗收。
- 四個公開附件匿名下載後的 SHA-256 均與發行清單一致。Windows 執行包為 `87EC6A6E212234BC36B8E06530C077CE16C59C91BADB2E3C0B67021119AD61C8`，對應原始碼包為 `3F7E5907C0CB240F85E360C1B51E1CBC78B6B7CAEEDB26336B65E85B2A54F303`。執行元件未簽章。
- 本機另建 0.1.3 程式、封存及捷徑，未啟動正式程式；不改動驅動、個人設定或 Remote Play。此版尚未取得逐項實機操作回報，不以離線測試或裝置狀態推定遊戲操作成功。
- [建置紀錄](../../packaging/build-records/0.1.3.json) · [公開附件清單](../../packaging/build-records/0.1.3-archives.json) · [九語離線畫面](../../assets/previews/0.1.3)。
