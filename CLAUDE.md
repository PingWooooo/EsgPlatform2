# CLAUDE.md — ESG 永續數據與文件管理平台 開發規範

## 一、技術框架

| 項目 | 規格 |
|------|------|
| 後端框架 | .NET 8.0 MVC（Repository + Service Pattern） |
| ORM / 資料存取 | Dapper（嚴禁 EF Core，所有查詢必須使用參數化 SQL） |
| 資料庫 | Microsoft SQL Server |
| 認證機制 | Cookie Authentication（BCrypt 密碼雜湊） |
| Excel 處理 | MiniExcel（導入 / 導出） |
| 前端圖表 | Chart.js 4.x |
| CSS 框架 | Bootstrap 5.3 + Bootstrap Icons 1.11 |

## 二、語言規範（強制）

- **UI 文字**：全部使用**繁體中文**
- **程式碼註解**：全部使用**繁體中文**
- **README / 文件**：全部使用**繁體中文**
- **日誌訊息（Logger）**：全部使用**繁體中文**
- 資料庫欄位名稱、C# 類別屬性名稱、URL 路由保持英文

## 三、架構規範

### 3.1 目錄結構
```
EsgPlatform/
├── Controllers/          # MVC 控制器（薄層，僅路由協調）
├── Models/               # 資料庫對映模型
├── ViewModels/           # 頁面專用 ViewModel
├── Views/                # Razor 視圖
│   └── Shared/
│       ├── Components/Nav/   # 動態導覽 ViewComponent
│       └── _Layout.cshtml
├── Repositories/
│   └── Interfaces/       # Repository 介面
├── Services/
│   └── Interfaces/       # Service 介面
├── ViewComponents/       # ViewComponent 實作
└── SQL/                  # 資料庫腳本
```

### 3.2 Repository Pattern
- 所有資料庫操作必須透過 Repository 介面進行
- Repository 直接使用 `SqlConnection` + Dapper
- 所有 SQL 查詢必須使用具名參數（`@ParamName`），嚴禁字串拼接
- 交易（Transaction）操作必須使用 `using var tx = conn.BeginTransaction()`

### 3.3 Service Pattern
- Controller 不得直接存取 Repository，必須透過 Service 層
- 商業邏輯（計算、驗證、狀態推算）統一放在 Service
- Service 可跨 Repository 協調操作

### 3.4 依賴注入
- Repository / Service 一律以 `Scoped` 生命週期注入
- 介面與實作分離，方便單元測試

## 四、圖表規範（強制）

- **嚴禁圓餅圖（Pie / Doughnut）**
- 展現各「範疇佔比」必須使用**堆疊橫向條形圖（Horizontal Stacked Bar Chart）**
- 趨勢資料使用折線圖（Line Chart）
- 多項目比較使用分組條形圖（Grouped Bar Chart）

```javascript
// 正確範例：堆疊橫向條形圖
{
  type: 'bar',
  options: {
    indexAxis: 'y',          // 橫向
    scales: { x: { stacked: true }, y: { stacked: true } }
  }
}
```

## 五、動態導覽列規範

- 導覽列內容**必須從** `NavItems` **資料表讀取**，不得硬編碼在 Layout
- 支援父子兩層結構（`ParentId` 為 NULL = 一級選單）
- `IsAdminOnly = 1` 的項目僅對 `Admin` 角色顯示
- 使用 ASP.NET Core **ViewComponent**（`NavViewComponent`）渲染

## 六、範疇三（Scope 3）開發規範

### 6.1 資料庫連動
- 類別下拉選單從 `Scope3Categories` 讀取
- 計算方法下拉從 `Scope3CalculationMethods`（依 CategoryId 篩選）讀取
- 兩個下拉皆透過 AJAX 動態連動，禁止硬編碼

### 6.2 動態欄位渲染
- 前端必須解析 `RequiredFieldsJson`，動態生成表單欄位
- JSON 欄位格式：
```json
[
  { "fieldName": "amount",   "label": "支出金額", "type": "number",
    "unit": "元", "required": true },
  { "fieldName": "currency", "label": "幣別",     "type": "select",
    "options": ["TWD","USD","EUR"], "required": true }
]
```
- 支援欄位型別：`number`、`text`、`select`、`date`

### 6.3 計算公式類型（`CalculationFormula`）
| 方法代碼 | 說明 | 公式 |
|----------|------|------|
| `spend` | 支出法 | CO₂e(kg) = amount / 1000 × emissionFactor |
| `supplier` | 供應商特有法 | CO₂e(kg) = quantity × supplierFactor |
| `average` | 平均資料法 | CO₂e(kg) = weight × emissionFactor |
| `activity_transport` | 活動數據法（運輸） | CO₂e(kg) = weight × distance × emissionFactor |
| `direct` | 直接測量法 | CO₂e(kg) = activityAmount × emissionFactor |

## 七、ESG 文件紅綠燈規範

### 7.1 燈號演算法
```
若 今日 > 截止日：
    若 有在截止日前上傳記錄 → 綠燈
    否則                    → 紅燈
否則（截止日尚未到）：
    若 已上傳             → 綠燈
    若 剩餘天數 ≤ 預警天數 → 黃燈
    否則                  → 綠燈（期限充裕）
```

### 7.2 預設預警天數
- 月報（Monthly）：預設 7 天
- 年報（Yearly）：預設 90 天

### 7.3 自動推算下次截止日
- 上傳成功後，系統自動更新 `EsgDocumentSchedules.NextDueDate`
- Monthly：`NextDueDate = 上傳當月最後一天 + 1個月`
- Yearly：`NextDueDate = 上傳當年最後一天 + 1年`
- 第一欄必須顯示紅綠燈圖示

## 八、安全規範

- 所有 POST 必須加 `[ValidateAntiForgeryToken]`
- 敏感操作（排程維護、係數設定）必須加 `[Authorize(Roles = "Admin")]`
- 檔案上傳必須驗證：副檔名白名單、最大 10MB、不得執行副檔名（.exe/.bat 等）
- 儲存路徑禁止使用使用者輸入的檔名（使用 GUID 重新命名）

## 九、日誌規範

```csharp
// 使用繁體中文記錄
_logger.LogInformation("使用者 {UserId} 上傳文件，排程 {ScheduleId}，檔案大小 {Size} bytes", userId, scheduleId, size);
_logger.LogWarning("排程 {ScheduleId} 截止日已過，尚未上傳文件", scheduleId);
_logger.LogError(ex, "處理 ESG 文件上傳時發生錯誤，ScheduleId={ScheduleId}", scheduleId);
```

## 十、禁止事項

- 禁止在 SQL 查詢中使用字串拼接（SQL injection 風險）
- 禁止使用圓餅圖（Pie / Doughnut）
- 禁止在 Controller 直接操作 SqlConnection
- 禁止將使用者輸入的檔名直接用於磁碟路徑
- 禁止硬編碼導覽列選單
- 禁止硬編碼資料庫連線字串（必須從 appsettings.json 讀取）
