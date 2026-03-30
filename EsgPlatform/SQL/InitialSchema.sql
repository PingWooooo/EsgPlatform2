-- ============================================================
-- ESG 企業永續數據與文件管理平台 - 資料庫初始化腳本
-- 資料庫：Microsoft SQL Server
-- 版本：3.0.0
-- 說明：請依序執行本腳本，所有資料表將依照外鍵關聯順序建立
-- ============================================================

USE master;
GO

-- 若資料庫已存在則先卸離，重新建立（生產環境請移除此段）
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'EsgPlatform')
BEGIN
    ALTER DATABASE EsgPlatform SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE EsgPlatform;
END
GO

CREATE DATABASE EsgPlatform
    COLLATE Chinese_Taiwan_Stroke_CI_AS;
GO

USE EsgPlatform;
GO

-- ============================================================
-- 1. Roles 角色資料表（先建立，供 Users 參照）
-- ============================================================
CREATE TABLE Roles (
    Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(50)    NOT NULL UNIQUE  -- Admin / User
);
GO

INSERT INTO Roles (RoleName) VALUES (N'Admin');
INSERT INTO Roles (RoleName) VALUES (N'User');
GO

-- ============================================================
-- 2. Users 會員主資料表
-- ============================================================
CREATE TABLE Users (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Username        NVARCHAR(100)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(256)   NOT NULL,   -- BCrypt 雜湊
    Email           NVARCHAR(200)   NOT NULL UNIQUE,
    RoleId          INT             NOT NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,  -- 帳號啟用狀態
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);
GO

CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_Email    ON Users(Email);
GO

-- ============================================================
-- 3. NavItems 二維導覽列結構表
-- ============================================================
CREATE TABLE NavItems (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Title           NVARCHAR(100)   NOT NULL,               -- 選單顯示名稱（繁體中文）
    Controller      NVARCHAR(100)   NULL,                   -- MVC Controller（一級群組可為 NULL）
    Action          NVARCHAR(100)   NULL,                   -- MVC Action
    Icon            NVARCHAR(100)   NOT NULL DEFAULT N'bi bi-circle', -- Bootstrap Icons 類別
    ParentId        INT             NULL,                   -- 父選單 Id（NULL = 一級選單）
    DisplayOrder    INT             NOT NULL DEFAULT 0,     -- 同層排序
    IsAdminOnly     BIT             NOT NULL DEFAULT 0,     -- 1 = 僅管理員可見
    CONSTRAINT FK_NavItems_Parent FOREIGN KEY (ParentId) REFERENCES NavItems(Id)
);
GO

CREATE INDEX IX_NavItems_ParentId ON NavItems(ParentId);
GO

-- NavItems 種子資料（使用 IDENTITY_INSERT 以固定 ParentId 參照）
SET IDENTITY_INSERT NavItems ON;

-- 一級選單（ParentId = NULL）
INSERT INTO NavItems (Id, Title, Controller, Action, Icon, ParentId, DisplayOrder, IsAdminOnly) VALUES
(1,  N'儀表板',         N'Dashboard',  N'Index', N'bi bi-speedometer2',       NULL, 1, 0),
(2,  N'溫室氣體盤查',   NULL,          NULL,     N'bi bi-bar-chart-steps',    NULL, 2, 0),
(3,  N'ESG 文件監控',   NULL,          NULL,     N'bi bi-file-earmark-check', NULL, 3, 0),
(4,  N'系統管理',       NULL,          NULL,     N'bi bi-gear-fill',          NULL, 4, 1);

-- 二級選單：溫室氣體盤查（ParentId = 2）
INSERT INTO NavItems (Id, Title, Controller, Action, Icon, ParentId, DisplayOrder, IsAdminOnly) VALUES
(5,  N'數據上傳（範疇一二）', N'Upload',        N'Index',     N'bi bi-cloud-upload',       2, 1, 0),
(6,  N'範疇三管理',           N'Scope3',        N'Index',     N'bi bi-diagram-3',          2, 2, 0),
(7,  N'計算結果匯出',         N'Export',        N'Index',     N'bi bi-file-earmark-excel', 2, 3, 0);

-- 二級選單：ESG 文件監控（ParentId = 3）
INSERT INTO NavItems (Id, Title, Controller, Action, Icon, ParentId, DisplayOrder, IsAdminOnly) VALUES
(8,  N'文件監控看板', N'EsgDocument', N'Index',     N'bi bi-traffic-light',  3, 1, 0),
(9,  N'排程維護',     N'EsgDocument', N'Schedules', N'bi bi-calendar-event', 3, 2, 1);

-- 二級選單：系統管理（ParentId = 4）
INSERT INTO NavItems (Id, Title, Controller, Action, Icon, ParentId, DisplayOrder, IsAdminOnly) VALUES
(10, N'報告排程',    N'ReportSchedule',  N'Index', N'bi bi-calendar-check',  4, 1, 1),
(11, N'碳排係數維護', N'EmissionConfig',  N'Index', N'bi bi-database-gear',   4, 2, 1),
(12, N'權限管理',    N'UserManagement',  N'Index', N'bi bi-people-fill',      4, 3, 1);

SET IDENTITY_INSERT NavItems OFF;
GO

-- ============================================================
-- 4. EmissionConfigs 碳排係數資料表（範疇一、二）
-- ============================================================
CREATE TABLE EmissionConfigs (
    Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Scope       INT             NOT NULL,           -- 1 或 2
    Category    NVARCHAR(100)   NOT NULL,           -- 例：固定燃燒、移動燃燒
    ItemName    NVARCHAR(200)   NOT NULL,           -- 例：天然氣、柴油
    Factor      DECIMAL(18,6)   NOT NULL,           -- 排放係數
    GWP         DECIMAL(10,4)   NOT NULL DEFAULT 1, -- 全球暖化潛勢
    Unit        NVARCHAR(50)    NOT NULL,           -- 例：kg CO2e/kWh
    SourceUrl   NVARCHAR(500)   NULL,               -- 原始文獻來源網址
    UpdatedAt   DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_EmissionConfigs UNIQUE (Scope, Category, ItemName)
);
GO

-- ============================================================
-- 5. Scope3Categories 範疇三 15 項類別表
-- ============================================================
CREATE TABLE Scope3Categories (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CategoryNumber  INT             NOT NULL UNIQUE,    -- 1 ~ 15
    Name            NVARCHAR(200)   NOT NULL,           -- 中文類別名稱
    Description     NVARCHAR(1000)  NOT NULL            -- 類別說明
);
GO

-- ============================================================
-- 6. Scope3CalculationMethods 範疇三計算方法表
--    RequiredFieldsJson：定義前端應動態渲染的輸入欄位
--    欄位結構：[{"fieldName":"...","label":"...","type":"number|text|select","unit":"...","options":["..."],"required":true}]
-- ============================================================
CREATE TABLE Scope3CalculationMethods (
    Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    CategoryId          INT             NOT NULL,
    MethodName          NVARCHAR(200)   NOT NULL,
    CalculationFormula  NVARCHAR(50)    NOT NULL,   -- spend/supplier/average/activity_transport/direct
    RequiredFieldsJson  NVARCHAR(MAX)   NOT NULL,
    CONSTRAINT FK_Scope3Methods_Category FOREIGN KEY (CategoryId) REFERENCES Scope3Categories(Id)
);
GO

CREATE INDEX IX_Scope3Methods_CategoryId ON Scope3CalculationMethods(CategoryId);
GO

-- ============================================================
-- 7. Scope3CalculationResults 範疇三計算結果記錄
-- ============================================================
CREATE TABLE Scope3CalculationResults (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    UserId          INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    MethodId        INT             NOT NULL,
    InputDataJson   NVARCHAR(MAX)   NOT NULL,   -- 輸入欄位的 JSON 快照
    TotalCO2e       DECIMAL(18,4)   NOT NULL,   -- 公噸 CO2e
    Period          NVARCHAR(20)    NOT NULL,   -- 例：2024-Q1、2024-01
    Remark          NVARCHAR(500)   NULL,
    CalculatedAt    DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Scope3Calc_Users    FOREIGN KEY (UserId)     REFERENCES Users(Id),
    CONSTRAINT FK_Scope3Calc_Category FOREIGN KEY (CategoryId) REFERENCES Scope3Categories(Id),
    CONSTRAINT FK_Scope3Calc_Method   FOREIGN KEY (MethodId)   REFERENCES Scope3CalculationMethods(Id)
);
GO

CREATE INDEX IX_Scope3CalcResults_UserId     ON Scope3CalculationResults(UserId);
CREATE INDEX IX_Scope3CalcResults_CategoryId ON Scope3CalculationResults(CategoryId);
GO

-- ============================================================
-- 8. RawDataUploads 原始數據上傳資料表（範疇一、二）
-- ============================================================
CREATE TABLE RawDataUploads (
    Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    UserId      INT             NOT NULL,
    Scope       INT             NOT NULL,           -- 1 或 2
    Category    NVARCHAR(100)   NOT NULL,
    ItemName    NVARCHAR(200)   NOT NULL,
    Value       DECIMAL(18,4)   NOT NULL,           -- 活動數據量
    Unit        NVARCHAR(50)    NOT NULL,
    UploadDate  DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_RawDataUploads_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_RawDataUploads_UserId     ON RawDataUploads(UserId);
CREATE INDEX IX_RawDataUploads_UploadDate ON RawDataUploads(UploadDate);
GO

-- ============================================================
-- 9. CalculationResults 計算結果資料表（範疇一、二）
-- ============================================================
CREATE TABLE CalculationResults (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    UploadId        INT             NOT NULL,
    TotalCO2e       DECIMAL(18,4)   NOT NULL,       -- 單位：公噸 CO2e
    CalculatedAt    DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_CalculationResults_Uploads FOREIGN KEY (UploadId) REFERENCES RawDataUploads(Id)
);
GO

CREATE INDEX IX_CalculationResults_UploadId ON CalculationResults(UploadId);
GO

-- ============================================================
-- 10. EsgDocumentSchedules ESG 文件排程
-- ============================================================
CREATE TABLE EsgDocumentSchedules (
    Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    DocumentName        NVARCHAR(200)   NOT NULL,       -- 文件內容名稱
    Frequency           NVARCHAR(20)    NOT NULL        -- Monthly / Yearly
                        CHECK (Frequency IN (N'Monthly', N'Yearly')),
    ResponsiblePerson   NVARCHAR(100)   NOT NULL,       -- 負責窗口
    WarningDays         INT             NOT NULL,       -- 預警天數
    NextDueDate         DATE            NOT NULL,       -- 下次截止日
    IsActive            BIT             NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 11. EsgDocumentUploads 文件上傳紀錄與路徑
-- ============================================================
CREATE TABLE EsgDocumentUploads (
    Id               INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ScheduleId       INT             NOT NULL,
    UserId           INT             NOT NULL,
    OriginalFileName NVARCHAR(500)   NOT NULL,           -- 原始檔名（供顯示）
    StoredFilePath   NVARCHAR(1000)  NOT NULL,           -- 儲存路徑（GUID 重命名）
    FileSizeBytes    BIGINT          NOT NULL,
    VersionNumber    INT             NOT NULL DEFAULT 1,  -- 版本序號（同排程遞增）
    UploadedAt       DATETIME2       NOT NULL DEFAULT GETDATE(),
    Remark           NVARCHAR(500)   NULL,
    CONSTRAINT FK_EsgDocUploads_Schedule FOREIGN KEY (ScheduleId) REFERENCES EsgDocumentSchedules(Id),
    CONSTRAINT FK_EsgDocUploads_User     FOREIGN KEY (UserId)     REFERENCES Users(Id)
);
GO

CREATE INDEX IX_EsgDocUploads_ScheduleId ON EsgDocumentUploads(ScheduleId);
CREATE INDEX IX_EsgDocUploads_UploadedAt ON EsgDocumentUploads(UploadedAt);
GO

-- ============================================================
-- 12. EmissionFactorLogs 係數異動紀錄（管理員手動修改追蹤）
-- ============================================================
CREATE TABLE EmissionFactorLogs (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ConfigId        INT             NOT NULL,
    OldFactor       DECIMAL(18,6)   NOT NULL,       -- 修改前係數
    NewFactor       DECIMAL(18,6)   NOT NULL,       -- 修改後係數
    OldSourceUrl    NVARCHAR(500)   NULL,           -- 修改前來源網址
    NewSourceUrl    NVARCHAR(500)   NULL,           -- 修改後來源網址
    ChangeReason    NVARCHAR(500)   NULL,           -- 變更原因說明
    OperatorUserId  INT             NOT NULL,       -- 操作者使用者 ID
    OperatorName    NVARCHAR(100)   NOT NULL,       -- 操作者帳號（快照）
    ChangedAt       DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_EmissionFactorLogs_Config FOREIGN KEY (ConfigId) REFERENCES EmissionConfigs(Id),
    CONSTRAINT FK_EmissionFactorLogs_User   FOREIGN KEY (OperatorUserId) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_EmissionFactorLogs_ConfigId  ON EmissionFactorLogs(ConfigId);
CREATE INDEX IX_EmissionFactorLogs_ChangedAt ON EmissionFactorLogs(ChangedAt DESC);
GO

-- ============================================================
-- 13. RegulationUpdateLogs 法規係數更新紀錄
-- ============================================================
CREATE TABLE RegulationUpdateLogs (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ConfigId        INT             NOT NULL,
    OldValue        DECIMAL(18,6)   NOT NULL,       -- 舊排放係數
    NewValue        DECIMAL(18,6)   NOT NULL,       -- 新排放係數
    ChangeReason    NVARCHAR(500)   NULL,           -- 變更原因說明
    UpdateDate      DATETIME2       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_RegulationUpdateLogs_Configs FOREIGN KEY (ConfigId) REFERENCES EmissionConfigs(Id)
);
GO

CREATE INDEX IX_RegulationUpdateLogs_ConfigId ON RegulationUpdateLogs(ConfigId);
GO

-- ============================================================
-- 14. ReportSchedules 報告排程資料表（原有，保留相容）
-- ============================================================
CREATE TABLE ReportSchedules (
    Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ReportName          NVARCHAR(200)   NOT NULL,
    Frequency           NVARCHAR(20)    NOT NULL CHECK (Frequency IN (N'Monthly', N'Yearly')),
    ResponsiblePerson   NVARCHAR(100)   NOT NULL,
    WarningDays         INT             NOT NULL,
    NextDueDate         DATE            NOT NULL,
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETDATE()
);
GO

-- ============================================================
-- 15. ReportStatusLogs 報告進度燈號資料表
-- ============================================================
CREATE TABLE ReportStatusLogs (
    Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
    ScheduleId      INT             NOT NULL,
    LastUpdateDate  DATETIME2       NULL,
    NextDueDate     DATE            NOT NULL,
    Status          NVARCHAR(10)    NOT NULL CHECK (Status IN (N'Green', N'Yellow', N'Red')),
    CONSTRAINT FK_ReportStatusLogs_Schedules FOREIGN KEY (ScheduleId) REFERENCES ReportSchedules(Id)
);
GO

CREATE INDEX IX_ReportStatusLogs_ScheduleId ON ReportStatusLogs(ScheduleId);
GO

-- ============================================================
-- 種子資料（Seed Data）
-- ============================================================

-- 預設管理員帳號（密碼：Admin@123，BCrypt 雜湊）
INSERT INTO Users (Username, PasswordHash, Email, RoleId)
VALUES (N'admin', N'$2a$11$rLmeRXe7JUNAzDSuHFiDxuRIUGxSBobMWJGm.jxFKnXgJYD3FkLEq', N'admin@esg.com', 1);

-- 預設一般使用者帳號（密碼：User@123）
INSERT INTO Users (Username, PasswordHash, Email, RoleId)
VALUES (N'user01', N'$2a$11$YN3hW0rMV3Tv5fgDkbLnkecRJh5a0D2BqfEqJ7NqTFHM1WQ/mMxLK', N'user01@esg.com', 2);
GO

-- 範疇一 碳排係數
INSERT INTO EmissionConfigs (Scope, Category, ItemName, Factor, GWP, Unit, SourceUrl) VALUES
(1, N'固定燃燒源', N'天然氣',             2.0416,  1.0, N'kg CO2e/m³',  NULL),
(1, N'固定燃燒源', N'柴油',               2.6360,  1.0, N'kg CO2e/L',   NULL),
(1, N'固定燃燒源', N'液化石油氣',         2.9920,  1.0, N'kg CO2e/L',   NULL),
(1, N'移動燃燒源', N'汽油',               2.2637,  1.0, N'kg CO2e/L',   NULL),
(1, N'移動燃燒源', N'柴油(車用)',         2.6280,  1.0, N'kg CO2e/L',   NULL),
(1, N'逸散排放',   N'冷媒R-22',           1810.0,  1.0, N'kg CO2e/kg',  NULL),
(1, N'逸散排放',   N'冷媒R-134a',         1430.0,  1.0, N'kg CO2e/kg',  NULL),
-- 指定規格補充：自來水、天然氣（環境部）、汽油（環境部）、柴油（環境部）
(1, N'用水排放',   N'自來水',             0.1600,  1.0, N'kg CO2e/m³',  N'https://www.water.gov.tw/'),
(1, N'固定燃燒源', N'天然氣（環境部公告）', 2.0900, 1.0, N'kg CO2e/m³', N'https://www.moenv.gov.tw/'),
(1, N'移動燃燒源', N'汽油（環境部公告）',  2.3600, 1.0, N'kg CO2e/L',  N'https://www.moenv.gov.tw/'),
(1, N'移動燃燒源', N'柴油（環境部公告）',  2.6600, 1.0, N'kg CO2e/L',  N'https://www.moenv.gov.tw/');
GO

-- 範疇二 碳排係數（台灣電力排放係數）
INSERT INTO EmissionConfigs (Scope, Category, ItemName, Factor, GWP, Unit, SourceUrl) VALUES
(2, N'外購電力', N'台電電力',       0.4950, 1.0, N'kg CO2e/kWh', NULL),
(2, N'外購電力', N'台電電力（最新公告）', 0.4950, 1.0, N'kg CO2e/kWh', N'https://www.esb.gov.tw/'),
(2, N'外購蒸汽', N'工業蒸汽',       0.0950, 1.0, N'kg CO2e/MJ',  NULL);
GO

-- 範疇三 15 項類別種子資料
INSERT INTO Scope3Categories (CategoryNumber, Name, Description) VALUES
(1,  N'購買商品和服務',             N'採購原物料、零組件、包材及外部服務所產生的上游排放'),
(2,  N'資本財',                     N'採購設備、機械、建築物等資本性資產的排放'),
(3,  N'燃料和能源相關活動',         N'範疇一、二中未涵蓋的燃料/能源相關上游排放（如燃料開採、電力傳輸損失）'),
(4,  N'上游運輸和配送',             N'採購商品從供應商運至本公司，及本公司與第三方倉儲間的運輸'),
(5,  N'業務廢棄物',                 N'營運過程產生廢棄物的處置與處理'),
(6,  N'商務旅行',                   N'員工出差搭乘飛機、火車、租車等交通工具'),
(7,  N'員工通勤',                   N'員工往返公司與住所的日常通勤排放'),
(8,  N'上游租賃資產',               N'公司向外租用設備、辦公室、廠房等資產的排放'),
(9,  N'下游運輸和配送',             N'已售商品從本公司運至終端客戶或零售商的運輸'),
(10, N'已售商品的加工',             N'下游廠商對本公司出售中間品進行進一步加工的排放'),
(11, N'已售商品的使用',             N'消費者或使用者使用本公司出售產品所產生的排放'),
(12, N'已售商品的廢棄處理',         N'消費者廢棄本公司已售商品後，處置或回收的排放'),
(13, N'下游租賃資產',               N'本公司出租給其他組織的資產所產生的排放'),
(14, N'特許加盟',                   N'本公司之加盟商的排放（適用有特許加盟模式的企業）'),
(15, N'投資',                       N'股權投資、債券投資、專案融資等相關的排放');
GO

-- 範疇三計算方法種子資料
DECLARE @c1  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 1);
DECLARE @c2  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 2);
DECLARE @c3  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 3);
DECLARE @c4  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 4);
DECLARE @c5  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 5);
DECLARE @c6  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 6);
DECLARE @c7  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 7);
DECLARE @c8  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 8);
DECLARE @c9  INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 9);
DECLARE @c10 INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 10);
DECLARE @c11 INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 11);
DECLARE @c12 INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 12);
DECLARE @c13 INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 13);
DECLARE @c14 INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 14);
DECLARE @c15 INT = (SELECT Id FROM Scope3Categories WHERE CategoryNumber = 15);

-- 類別 1：購買商品和服務
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c1, N'支出法',
 N'spend',
 N'[{"fieldName":"amount","label":"採購支出金額","type":"number","unit":"千元（TWD）","required":true},{"fieldName":"currency","label":"幣別","type":"select","options":["TWD","USD","EUR","JPY","CNY"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 千元)","type":"number","required":true}]'),
(@c1, N'供應商特有法',
 N'supplier',
 N'[{"fieldName":"quantity","label":"採購數量","type":"number","required":true},{"fieldName":"quantityUnit","label":"數量單位","type":"text","required":true},{"fieldName":"supplierFactor","label":"供應商排放係數 (kg CO₂e / unit)","type":"number","required":true}]'),
(@c1, N'平均資料法（重量）',
 N'average',
 N'[{"fieldName":"weight","label":"採購重量 (kg)","type":"number","required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / kg)","type":"number","required":true}]');

-- 類別 2：資本財
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c2, N'支出法',
 N'spend',
 N'[{"fieldName":"amount","label":"資本支出金額","type":"number","unit":"千元","required":true},{"fieldName":"currency","label":"幣別","type":"select","options":["TWD","USD","EUR"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 千元)","type":"number","required":true}]'),
(@c2, N'供應商特有法',
 N'supplier',
 N'[{"fieldName":"quantity","label":"採購數量（台/套）","type":"number","required":true},{"fieldName":"quantityUnit","label":"單位","type":"text","required":true},{"fieldName":"supplierFactor","label":"供應商設備排放係數 (kg CO₂e / unit)","type":"number","required":true}]');

-- 類別 3：燃料和能源相關活動
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c3, N'平均資料法',
 N'average',
 N'[{"fieldName":"weight","label":"燃料/能源消耗量","type":"number","required":true},{"fieldName":"quantityUnit","label":"計量單位","type":"select","options":["MJ","kWh","公噸","立方公尺"],"required":true},{"fieldName":"emissionFactor","label":"上游排放係數 (kg CO₂e / unit)","type":"number","required":true}]');

-- 類別 4：上游運輸和配送
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c4, N'活動數據法（噸公里）',
 N'activity_transport',
 N'[{"fieldName":"weight","label":"貨物重量 (公噸)","type":"number","required":true},{"fieldName":"distance","label":"運輸距離 (km)","type":"number","required":true},{"fieldName":"transportMode","label":"運輸方式","type":"select","options":["公路（重型貨車）","公路（輕型貨車）","鐵路","海運","空運","內河/沿海"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 公噸·km)","type":"number","required":true}]'),
(@c4, N'支出法',
 N'spend',
 N'[{"fieldName":"amount","label":"運費支出","type":"number","unit":"千元","required":true},{"fieldName":"currency","label":"幣別","type":"select","options":["TWD","USD","EUR"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 千元)","type":"number","required":true}]');

-- 類別 5：業務廢棄物
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c5, N'廢棄物類型法',
 N'average',
 N'[{"fieldName":"weight","label":"廢棄物重量 (kg)","type":"number","required":true},{"fieldName":"wasteType","label":"廢棄物種類","type":"select","options":["一般廢棄物（掩埋）","一般廢棄物（焚化）","金屬廢料（回收）","紙類（回收）","塑膠（回收）","有害廢棄物","電子廢棄物"],"required":true},{"fieldName":"emissionFactor","label":"廢棄物處理排放係數 (kg CO₂e / kg)","type":"number","required":true}]');

-- 類別 6：商務旅行
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c6, N'距離法',
 N'direct',
 N'[{"fieldName":"activityAmount","label":"旅行距離 (km)","type":"number","required":true},{"fieldName":"transportMode","label":"交通方式","type":"select","options":["國際航班（經濟艙）","國際航班（商務艙）","國內航班","火車","租車（汽油）","計程車"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 人·km)","type":"number","required":true}]'),
(@c6, N'支出法',
 N'spend',
 N'[{"fieldName":"amount","label":"差旅費用","type":"number","unit":"千元","required":true},{"fieldName":"currency","label":"幣別","type":"select","options":["TWD","USD","EUR"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 千元)","type":"number","required":true}]');

-- 類別 7：員工通勤
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c7, N'距離法',
 N'direct',
 N'[{"fieldName":"activityAmount","label":"員工通勤人次·公里 (人次·km)","type":"number","required":true},{"fieldName":"transportMode","label":"主要通勤方式","type":"select","options":["私家車（汽油）","私家車（電動）","機車","公車","捷運/地鐵","火車","步行/腳踏車"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 人·km)","type":"number","required":true}]'),
(@c7, N'平均員工法',
 N'direct',
 N'[{"fieldName":"activityAmount","label":"員工人數（人）","type":"number","required":true},{"fieldName":"emissionFactor","label":"每人年均通勤排放 (kg CO₂e / 人)","type":"number","required":true}]');

-- 類別 8：上游租賃資產
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c8, N'平均資料法',
 N'average',
 N'[{"fieldName":"weight","label":"租賃面積 (m²) 或設備數量","type":"number","required":true},{"fieldName":"quantityUnit","label":"計量單位","type":"select","options":["m²","台","套"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / unit)","type":"number","required":true}]');

-- 類別 9：下游運輸和配送
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c9, N'活動數據法（噸公里）',
 N'activity_transport',
 N'[{"fieldName":"weight","label":"配送貨物重量 (公噸)","type":"number","required":true},{"fieldName":"distance","label":"配送距離 (km)","type":"number","required":true},{"fieldName":"transportMode","label":"運輸方式","type":"select","options":["公路（重型貨車）","公路（輕型貨車）","鐵路","海運","空運"],"required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / 公噸·km)","type":"number","required":true}]');

-- 類別 10：已售商品的加工
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c10, N'平均資料法',
 N'average',
 N'[{"fieldName":"weight","label":"中間品重量 (kg)","type":"number","required":true},{"fieldName":"emissionFactor","label":"加工排放係數 (kg CO₂e / kg)","type":"number","required":true}]');

-- 類別 11：已售商品的使用
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c11, N'直接使用階段排放法',
 N'direct',
 N'[{"fieldName":"activityAmount","label":"商品年銷售量（件/台）","type":"number","required":true},{"fieldName":"emissionFactor","label":"每件商品使用期排放 (kg CO₂e / 件)","type":"number","required":true}]'),
(@c11, N'電力使用法',
 N'direct',
 N'[{"fieldName":"activityAmount","label":"商品年耗電量 (kWh / 台)","type":"number","required":true},{"fieldName":"quantity","label":"銷售數量（台）","type":"number","required":true},{"fieldName":"emissionFactor","label":"電力排放係數 (kg CO₂e / kWh)","type":"number","required":true}]');

-- 類別 12：已售商品的廢棄處理
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c12, N'廢棄物類型法',
 N'average',
 N'[{"fieldName":"weight","label":"廢棄商品重量 (kg)","type":"number","required":true},{"fieldName":"wasteType","label":"處置方式","type":"select","options":["掩埋","焚化","回收","堆肥"],"required":true},{"fieldName":"emissionFactor","label":"處置排放係數 (kg CO₂e / kg)","type":"number","required":true}]');

-- 類別 13：下游租賃資產
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c13, N'平均資料法',
 N'average',
 N'[{"fieldName":"weight","label":"租賃面積 (m²)","type":"number","required":true},{"fieldName":"emissionFactor","label":"排放係數 (kg CO₂e / m²)","type":"number","required":true}]');

-- 類別 14：特許加盟
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c14, N'加盟商直報法',
 N'direct',
 N'[{"fieldName":"activityAmount","label":"加盟商數量（家）","type":"number","required":true},{"fieldName":"emissionFactor","label":"每家加盟商平均年排放 (kg CO₂e / 家)","type":"number","required":true}]');

-- 類別 15：投資
INSERT INTO Scope3CalculationMethods (CategoryId, MethodName, CalculationFormula, RequiredFieldsJson) VALUES
(@c15, N'投資比例分攤法',
 N'spend',
 N'[{"fieldName":"amount","label":"投資金額","type":"number","unit":"千元","required":true},{"fieldName":"currency","label":"幣別","type":"select","options":["TWD","USD","EUR"],"required":true},{"fieldName":"emissionFactor","label":"被投資對象排放強度 (kg CO₂e / 千元投資)","type":"number","required":true}]'),
(@c15, N'股權比例法',
 N'direct',
 N'[{"fieldName":"activityAmount","label":"持股比例 (%)","type":"number","required":true},{"fieldName":"emissionFactor","label":"被投資企業總排放 (公噸 CO₂e)","type":"number","required":true}]');
GO

-- ESG 文件排程示範資料
INSERT INTO EsgDocumentSchedules (DocumentName, Frequency, ResponsiblePerson, WarningDays, NextDueDate) VALUES
(N'月度碳排放盤查報告',         N'Monthly', N'環境管理部－王小明',       7,  DATEADD(MONTH, 1, CAST(GETDATE() AS DATE))),
(N'年度溫室氣體盤查報告',       N'Yearly',  N'永續發展委員會－李大華',   90, DATEADD(YEAR,  1, CAST(GETDATE() AS DATE))),
(N'供應鏈碳排放月報',           N'Monthly', N'採購部－陳小玲',           7,  DATEADD(MONTH, 1, CAST(GETDATE() AS DATE))),
(N'年度永續發展報告書（CSR）',  N'Yearly',  N'公關部－林佳穎',           90, DATEADD(YEAR,  1, CAST(GETDATE() AS DATE))),
(N'能源管理月報',               N'Monthly', N'設施管理部－張偉民',       7,  DATEADD(MONTH, 1, CAST(GETDATE() AS DATE)));
GO

-- 報告排程示範資料（原有結構，保留相容）
INSERT INTO ReportSchedules (ReportName, Frequency, ResponsiblePerson, WarningDays, NextDueDate) VALUES
(N'月度碳排放盤查報告',   N'Monthly', N'環境管理部-王小明',     7,  DATEADD(MONTH, 1, CAST(GETDATE() AS DATE))),
(N'年度溫室氣體盤查報告', N'Yearly',  N'永續發展委員會-李大華', 90, DATEADD(YEAR,  1, CAST(GETDATE() AS DATE))),
(N'供應鏈碳排放月報',     N'Monthly', N'採購部-陳小玲',         7,  DATEADD(MONTH, 1, CAST(GETDATE() AS DATE)));
GO

PRINT N'ESG 平台資料庫 v3.0.0 初始化完成！';
PRINT N'預設管理員：admin / Admin@123';
PRINT N'預設一般使用者：user01 / User@123';
PRINT N'新增資料表：NavItems, Scope3Categories, Scope3CalculationMethods, Scope3CalculationResults';
PRINT N'新增資料表：EsgDocumentSchedules, EsgDocumentUploads, EmissionFactorLogs';
PRINT N'新增欄位：EmissionConfigs.SourceUrl, EsgDocumentUploads.VersionNumber, Users.IsActive';
PRINT N'範疇三類別：15 筆，計算方法：20+ 筆';
PRINT N'ESG 文件排程：5 筆示範資料';
PRINT N'NavItems：新增 Id=12 權限管理';
GO
