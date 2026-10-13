-- =============================================================================
-- GOLIS Mart — 002_CreateIndexes.sql
-- Performance indexes for the GOLISMartDb database.
-- Safe to rerun: uses IF NOT EXISTS checks on index names.
-- Execute against: GOLISMartDb after 001_CreateSchema.sql
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- Categories
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Categories_Slug' AND object_id = OBJECT_ID('Categories'))
    CREATE UNIQUE INDEX IX_Categories_Slug ON Categories (Slug);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Categories_IsActive_SortOrder' AND object_id = OBJECT_ID('Categories'))
    CREATE INDEX IX_Categories_IsActive_SortOrder ON Categories (IsActive, SortOrder);
GO

-- ---------------------------------------------------------------------------
-- Products
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Slug' AND object_id = OBJECT_ID('Products'))
    CREATE UNIQUE INDEX IX_Products_Slug ON Products (Slug);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_SKU' AND object_id = OBJECT_ID('Products'))
    CREATE UNIQUE INDEX IX_Products_SKU ON Products (SKU);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_CategoryId_IsActive' AND object_id = OBJECT_ID('Products'))
    CREATE INDEX IX_Products_CategoryId_IsActive ON Products (CategoryId, IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_IsActive_IsFeatured' AND object_id = OBJECT_ID('Products'))
    CREATE INDEX IX_Products_IsActive_IsFeatured ON Products (IsActive, IsFeatured);
GO

-- Full-text-style search support (covering index on Name for LIKE queries)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Name_IsActive' AND object_id = OBJECT_ID('Products'))
    CREATE INDEX IX_Products_Name_IsActive ON Products (IsActive) INCLUDE (Name, Slug, CategoryId, Price, OfferPrice, UnitLabel, ImagePath);
GO

-- ---------------------------------------------------------------------------
-- Inventory
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Inventory_ProductId' AND object_id = OBJECT_ID('Inventory'))
    CREATE UNIQUE INDEX IX_Inventory_ProductId ON Inventory (ProductId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Inventory_LowStock' AND object_id = OBJECT_ID('Inventory'))
    CREATE INDEX IX_Inventory_LowStock ON Inventory (QuantityOnHand, LowStockThreshold);
GO

-- ---------------------------------------------------------------------------
-- ProductImages
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductImages_ProductId_SortOrder' AND object_id = OBJECT_ID('ProductImages'))
    CREATE INDEX IX_ProductImages_ProductId_SortOrder ON ProductImages (ProductId, SortOrder);
GO

-- ---------------------------------------------------------------------------
-- Carts
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Carts_UserId' AND object_id = OBJECT_ID('Carts'))
    CREATE INDEX IX_Carts_UserId ON Carts (UserId) WHERE UserId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Carts_SessionKey' AND object_id = OBJECT_ID('Carts'))
    CREATE INDEX IX_Carts_SessionKey ON Carts (SessionKey) WHERE SessionKey IS NOT NULL;
GO

-- ---------------------------------------------------------------------------
-- Orders
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_UserId_CreatedAt' AND object_id = OBJECT_ID('Orders'))
    CREATE INDEX IX_Orders_UserId_CreatedAt ON Orders (UserId, CreatedAt DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_Status' AND object_id = OBJECT_ID('Orders'))
    CREATE INDEX IX_Orders_Status ON Orders (Status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_OrderNumber' AND object_id = OBJECT_ID('Orders'))
    CREATE UNIQUE INDEX IX_Orders_OrderNumber ON Orders (OrderNumber);
GO

-- ---------------------------------------------------------------------------
-- OrderItems
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderItems_OrderId' AND object_id = OBJECT_ID('OrderItems'))
    CREATE INDEX IX_OrderItems_OrderId ON OrderItems (OrderId);
GO

-- ---------------------------------------------------------------------------
-- Payments
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_OrderId' AND object_id = OBJECT_ID('Payments'))
    CREATE INDEX IX_Payments_OrderId ON Payments (OrderId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_GatewayPaymentId' AND object_id = OBJECT_ID('Payments'))
    CREATE INDEX IX_Payments_GatewayPaymentId ON Payments (GatewayPaymentId) WHERE GatewayPaymentId IS NOT NULL;
GO

-- ---------------------------------------------------------------------------
-- Coupons
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Coupons_Code' AND object_id = OBJECT_ID('Coupons'))
    CREATE UNIQUE INDEX IX_Coupons_Code ON Coupons (Code);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Coupons_IsActive_ValidUntil' AND object_id = OBJECT_ID('Coupons'))
    CREATE INDEX IX_Coupons_IsActive_ValidUntil ON Coupons (IsActive, ValidUntil) WHERE IsActive = 1;
GO

-- ---------------------------------------------------------------------------
-- CouponRedemptions
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CouponRedemptions_UserId_CouponId' AND object_id = OBJECT_ID('CouponRedemptions'))
    CREATE INDEX IX_CouponRedemptions_UserId_CouponId ON CouponRedemptions (UserId, CouponId);
GO

-- ---------------------------------------------------------------------------
-- InventoryTransactions
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryTx_ProductId_CreatedAt' AND object_id = OBJECT_ID('InventoryTransactions'))
    CREATE INDEX IX_InventoryTx_ProductId_CreatedAt ON InventoryTransactions (ProductId, CreatedAt DESC);
GO

-- ---------------------------------------------------------------------------
-- Identity tables
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_NormalizedEmail' AND object_id = OBJECT_ID('AspNetUsers'))
    CREATE INDEX IX_AspNetUsers_NormalizedEmail ON AspNetUsers (NormalizedEmail);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_NormalizedUserName' AND object_id = OBJECT_ID('AspNetUsers'))
    CREATE UNIQUE INDEX IX_AspNetUsers_NormalizedUserName ON AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetRoles_NormalizedName' AND object_id = OBJECT_ID('AspNetRoles'))
    CREATE UNIQUE INDEX IX_AspNetRoles_NormalizedName ON AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;
GO

-- ---------------------------------------------------------------------------
-- UserAddresses
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserAddresses_UserId' AND object_id = OBJECT_ID('UserAddresses'))
    CREATE INDEX IX_UserAddresses_UserId ON UserAddresses (UserId);
GO

PRINT 'Index creation complete.';
GO
