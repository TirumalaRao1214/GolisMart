-- =============================================================================
-- GOLIS Mart — 010_AdminAndSettings.sql
-- Phase 7: Admin Dashboard & Store Management
--
-- Creates:
--   1. Admin role in AspNetRoles
--   2. StoreSettings table
--   3. AdminLogs table  (audit trail for admin actions)
--   4. Indexes for admin dashboard queries
--
-- NOTE: Does NOT create admin users here (use the documented seeding process).
-- Safe to rerun: all DDL is guarded by existence checks.
-- Execute against: GOLISMartDb
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- 1. Seed the 'Admin' role
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'ADMIN')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Admin', 'ADMIN', CAST(NEWID() AS NVARCHAR(MAX)));
    PRINT 'Admin role created.';
END
ELSE
    PRINT 'Admin role already exists — skipped.';
GO

-- ---------------------------------------------------------------------------
-- 2. StoreSettings table
--    Stores key–value configuration managed from the admin dashboard.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StoreSettings')
BEGIN
    CREATE TABLE StoreSettings (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        SettingKey  NVARCHAR(100)   NOT NULL,
        SettingValue NVARCHAR(MAX)  NOT NULL DEFAULT '',
        DataType    NVARCHAR(20)    NOT NULL DEFAULT 'String',  -- String|Decimal|Int|Bool
        Category    NVARCHAR(50)    NOT NULL DEFAULT 'General',
        Description NVARCHAR(500)   NULL,
        UpdatedBy   NVARCHAR(450)   NULL,
        UpdatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_StoreSettings_Key UNIQUE (SettingKey)
    );
    PRINT 'Created StoreSettings table.';
END
GO

-- ---------------------------------------------------------------------------
-- 3. Seed default store settings (only if not already present)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM StoreSettings WHERE SettingKey = 'Store.Name')
BEGIN
    INSERT INTO StoreSettings (SettingKey, SettingValue, DataType, Category, Description)
    VALUES
        ('Store.Name',             'GOLIS Mart',    'String',  'General',  'Public store display name'),
        ('Store.ContactEmail',     '',               'String',  'General',  'Customer support email'),
        ('Store.ContactPhone',     '',               'String',  'General',  'Customer support phone'),
        ('Store.IsOpen',           '1',              'Bool',    'General',  '1 = store accepting orders, 0 = closed'),
        ('Delivery.DefaultFee',    '40.00',          'Decimal', 'Delivery', 'Default delivery fee when area has no override'),
        ('Delivery.FreeAbove',     '500.00',         'Decimal', 'Delivery', 'Cart subtotal above which delivery is free'),
        ('Delivery.MinOrder',      '0.00',           'Decimal', 'Delivery', 'Minimum cart subtotal to place an order (0 = no minimum)'),
        ('Inventory.LowStockDefault', '5',           'Int',     'Inventory','Default low-stock threshold for new products');
    PRINT 'Seeded default store settings.';
END
GO

-- ---------------------------------------------------------------------------
-- 4. AdminLogs table  (audit trail for admin actions)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdminLogs')
BEGIN
    CREATE TABLE AdminLogs (
        Id          BIGINT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
        AdminUserId NVARCHAR(450)   NOT NULL,
        Action      NVARCHAR(100)   NOT NULL,   -- e.g. 'ProductCreated', 'OrderStatusChanged'
        EntityType  NVARCHAR(50)    NULL,        -- e.g. 'Product', 'Order'
        EntityId    NVARCHAR(100)   NULL,
        Details     NVARCHAR(MAX)   NULL,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT 'Created AdminLogs table.';
END
GO

-- ---------------------------------------------------------------------------
-- 5. Index: Orders — admin list view (all orders, newest first)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_Orders_CreatedAt_Desc'
      AND  object_id = OBJECT_ID('Orders')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Orders_CreatedAt_Desc
        ON Orders (CreatedAt DESC)
        INCLUDE (OrderNumber, UserId, Status, PaymentStatus, TotalAmount);
    PRINT 'Created IX_Orders_CreatedAt_Desc index.';
END
GO

-- ---------------------------------------------------------------------------
-- 6. Index: AdminLogs — admin activity lookup
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_AdminLogs_AdminUserId_CreatedAt'
      AND  object_id = OBJECT_ID('AdminLogs')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_AdminLogs_AdminUserId_CreatedAt
        ON AdminLogs (AdminUserId, CreatedAt DESC);
    PRINT 'Created IX_AdminLogs_AdminUserId_CreatedAt index.';
END
GO

-- ---------------------------------------------------------------------------
-- 7. Products — add ImagePath index for filtering
--    (products without images need to be filterable in admin panel)
-- ---------------------------------------------------------------------------
-- (No additional column changes needed — all product management columns exist)

PRINT '010_AdminAndSettings migration complete.';
GO
