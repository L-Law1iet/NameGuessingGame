# 猜名字遊戲 (Name Guessing Game)

這是一個基於 ASP.NET Core 和 Vue.js 的即時互動猜名字遊戲平台。

## 專案結構

- **NameGuessingGame.Api**: 後端 API 和 SignalR 服務，提供實時通訊和遊戲邏輯
- **NameGuessingGame.Web**: 前端 Vue.js 專案，提供用戶界面

## 功能特點

- 多人房間制遊戲
- 即時通訊和通知
- 提問和猜測機制
- 遊戲進程追蹤

## 技術棧

- **後端**: ASP.NET Core 8.0, SignalR, Entity Framework Core
- **前端**: Vue.js 3, Vite, Tailwind CSS
- **數據存儲**: In-Memory Database

## 開發環境設置

### 後端設置

```bash
cd NameGuessingGame.Api
dotnet restore
dotnet run
```

### 前端設置

```bash
cd NameGuessingGame.Web
npm install
npm run dev
```

## 遊戲規則

1. 每個玩家提交一個名字
2. 系統隨機分配名字給其他玩家猜測
3. 玩家輪流提問，其他人回答「是」或「否」
4. 根據回答，玩家嘗試猜測分配給自己的名字
5. 猜對的玩家完成遊戲，其他人繼續猜測
6. 當只剩下一名玩家未猜對時，遊戲結束

## 部署

### 後端部署
```bash
cd NameGuessingGame.Api
dotnet publish -c Release
```

### 前端部署
```bash
cd NameGuessingGame.Web
npm run build
``` 