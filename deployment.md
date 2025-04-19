# 猜名字遊戲外網部署指南

## 前置要求
- 已安裝 .NET 8.0 SDK
- 已安裝 Node.js (建議 v18+)
- 已安裝 Nginx
- 已配置好 TPLink Deco 路由器
- 已設定 TPLink DDNS (guressname.tplindns.com)

## 1. 部署後端 (ASP.NET Core API)

### 編譯發布
```bash
cd NameGuessingGame.Api
dotnet publish -c Release
```

### 運行後端
```bash
cd bin/Release/net8.0/publish
dotnet NameGuessingGame.Api.dll --urls=http://localhost:5000
```

或創建一個 Windows 服務來運行 API，確保持續運行。

## 2. 部署前端 (Vue.js)

### 構建前端
```bash
cd NameGuessingGame.Web
npm install
npm run build
```

這將在 `dist` 目錄中生成靜態文件。

### 配置 Nginx

1. 將 `nginx.conf` 文件複製到 Nginx 配置目錄，例如：
```bash
cp nginx.conf C:\nginx\conf\sites-enabled\nameguessinggame.conf
```

2. 修改配置文件中的路徑：
```nginx
location / {
    root C:/path/to/NameGuessingGame.Web/dist;
    ...
}
```

3. 重啟 Nginx
```bash
nginx -s reload
```

## 3. 配置 TPLink Deco 路由器

1. 登入 TPLink Deco 管理頁面
2. 設置端口轉發：
   - 外部端口: 80
   - 內部 IP: [您的伺服器 IP 地址]
   - 內部端口: 80
   - 協議: TCP+UDP

3. 確認 DDNS 設置（guressname.tplindns.com）已正確配置

## 4. 安全考量

- 考慮添加 HTTPS，為此需要申請 SSL 證書
- 添加 IP 限制或訪問驗證以保護遊戲伺服器
- 定期更新系統和所有依賴項

## 5. 故障排除

- 如果無法連接 SignalR，檢查防火牆是否允許 WebSocket 連接
- 如果部分頁面無法加載，確認 Nginx 配置中的 `try_files` 指令正確設置
- 如果使用防火牆，確保開放了必要的端口（80、443） 