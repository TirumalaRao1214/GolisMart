-- =============================================================================
-- GOLIS Mart — Complete Azure SQL Database Deployment Script
-- =============================================================================
-- Target Database: Azure SQL Database (or SQL Server 2019+ / SQLEXPRESS)
-- Compatibility: 100% Azure SQL compatible (no USE statements, no server-level DDL).
-- Idempotency: Fully idempotent — safe to run on fresh databases or rerun on existing.
--
-- INSTRUCTIONS FOR AZURE SQL:
-- 1. In Azure Portal, Azure Data Studio, SSMS, or VS Code mssql:
--    Connect directly to your Azure SQL Database (e.g. Server: myserver.database.windows.net, Database: GOLISMartDb).
-- 2. Open this file and execute the entire script.
-- 3. To provision an administrator account:
--    - Call POST /api/v1/admin/bootstrap with AdminBootstrap:BootstrapSecret, OR
--    - Use database/CreateAdminUser.sql with a valid bcrypt hash.
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- =============================================================================
-- SECTION 1: ASP.NET IDENTITY TABLES
-- =============================================================================

-- 1.1 AspNetRoles
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetRoles')
BEGIN
    CREATE TABLE AspNetRoles (
        Id                  NVARCHAR(450)   NOT NULL PRIMARY KEY,
        Name                NVARCHAR(256)   NULL,
        NormalizedName      NVARCHAR(256)   NULL,
        ConcurrencyStamp    NVARCHAR(MAX)   NULL
    );
END
GO

-- 1.2 AspNetUsers
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUsers')
BEGIN
    CREATE TABLE AspNetUsers (
        Id                          NVARCHAR(450)   NOT NULL PRIMARY KEY,
        UserName                    NVARCHAR(256)   NULL,
        NormalizedUserName          NVARCHAR(256)   NULL,
        Email                       NVARCHAR(256)   NULL,
        NormalizedEmail             NVARCHAR(256)   NULL,
        EmailConfirmed              BIT             NOT NULL DEFAULT 0,
        PasswordHash                NVARCHAR(MAX)   NULL,    -- bcrypt hash
        SecurityStamp               NVARCHAR(MAX)   NULL,
        ConcurrencyStamp            NVARCHAR(MAX)   NULL,
        PhoneNumber                 NVARCHAR(50)    NULL,
        PhoneNumberConfirmed        BIT             NOT NULL DEFAULT 0,
        TwoFactorEnabled            BIT             NOT NULL DEFAULT 0,
        LockoutEnd                  DATETIMEOFFSET  NULL,
        LockoutEnabled              BIT             NOT NULL DEFAULT 0,
        AccessFailedCount           INT             NOT NULL DEFAULT 0,
        DisplayName                 NVARCHAR(200)   NULL,
        IsActive                    BIT             NOT NULL DEFAULT 1,
        CreatedAt                   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt                   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 1.3 AspNetUserRoles
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserRoles')
BEGIN
    CREATE TABLE AspNetUserRoles (
        UserId  NVARCHAR(450) NOT NULL,
        RoleId  NVARCHAR(450) NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
    );
END
GO

-- 1.4 AspNetUserClaims
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserClaims')
BEGIN
    CREATE TABLE AspNetUserClaims (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId      NVARCHAR(450)   NOT NULL,
        ClaimType   NVARCHAR(MAX)   NULL,
        ClaimValue  NVARCHAR(MAX)   NULL,
        CONSTRAINT FK_UserClaims_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
END
GO

-- 1.5 AspNetUserLogins
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserLogins')
BEGIN
    CREATE TABLE AspNetUserLogins (
        LoginProvider       NVARCHAR(128)   NOT NULL,
        ProviderKey         NVARCHAR(128)   NOT NULL,
        ProviderDisplayName NVARCHAR(MAX)   NULL,
        UserId              NVARCHAR(450)   NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
        CONSTRAINT FK_UserLogins_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
END
GO

-- 1.6 AspNetRoleClaims
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetRoleClaims')
BEGIN
    CREATE TABLE AspNetRoleClaims (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        RoleId      NVARCHAR(450)   NOT NULL,
        ClaimType   NVARCHAR(MAX)   NULL,
        ClaimValue  NVARCHAR(MAX)   NULL,
        CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
    );
END
GO

-- 1.7 AspNetUserTokens
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserTokens')
BEGIN
    CREATE TABLE AspNetUserTokens (
        UserId          NVARCHAR(450)   NOT NULL,
        LoginProvider   NVARCHAR(128)   NOT NULL,
        Name            NVARCHAR(128)   NOT NULL,
        Value           NVARCHAR(MAX)   NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
        CONSTRAINT FK_UserTokens_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
END
GO

-- =============================================================================
-- SECTION 2: CORE STORE DOMAIN TABLES
-- =============================================================================

-- 2.1 Categories
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE Categories (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Name        NVARCHAR(100)   NOT NULL,
        Slug        NVARCHAR(100)   NOT NULL,
        Description NVARCHAR(500)   NULL,
        ImagePath   NVARCHAR(500)   NULL,
        SortOrder   INT             NOT NULL DEFAULT 0,
        IsActive    BIT             NOT NULL DEFAULT 1,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Categories_Slug UNIQUE (Slug)
    );
END
GO

-- 2.2 Products
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE Products (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        CategoryId      INT             NOT NULL,
        SKU             NVARCHAR(50)    NOT NULL,
        Name            NVARCHAR(200)   NOT NULL,
        Slug            NVARCHAR(200)   NOT NULL,
        Description     NVARCHAR(2000)  NULL,
        UnitCode        NVARCHAR(20)    NOT NULL,   -- kg, g, litre, pcs, pack
        UnitLabel       NVARCHAR(50)    NOT NULL,   -- "1 kg", "500 g", "1 Litre"
        Price           DECIMAL(10,2)   NOT NULL,
        OfferPrice      DECIMAL(10,2)   NULL,
        IsFeatured      BIT             NOT NULL DEFAULT 0,
        IsActive        BIT             NOT NULL DEFAULT 1,
        ImagePath       NVARCHAR(500)   NULL,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion      ROWVERSION,
        CONSTRAINT UQ_Products_SKU  UNIQUE (SKU),
        CONSTRAINT UQ_Products_Slug UNIQUE (Slug),
        CONSTRAINT CK_Products_Price      CHECK (Price > 0),
        CONSTRAINT CK_Products_OfferPrice CHECK (OfferPrice IS NULL OR (OfferPrice > 0 AND OfferPrice < Price)),
        CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
    );
END
GO

-- 2.3 ProductImages
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductImages')
BEGIN
    CREATE TABLE ProductImages (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ProductId   INT             NOT NULL,
        ImagePath   NVARCHAR(500)   NOT NULL,
        AltText     NVARCHAR(200)   NULL,
        SortOrder   INT             NOT NULL DEFAULT 0,
        IsPrimary   BIT             NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ProductImages_Products FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
    );
END
GO

-- 2.4 Inventory
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Inventory')
BEGIN
    CREATE TABLE Inventory (
        Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ProductId           INT             NOT NULL,
        QuantityOnHand      DECIMAL(10,3)   NOT NULL DEFAULT 0,
        LowStockThreshold   DECIMAL(10,3)   NOT NULL DEFAULT 5,
        ReservedQuantity    DECIMAL(10,3)   NOT NULL DEFAULT 0,
        UpdatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION,
        CONSTRAINT UQ_Inventory_Product UNIQUE (ProductId),
        CONSTRAINT FK_Inventory_Products FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Inventory_QtyOnHand    CHECK (QuantityOnHand >= 0),
        CONSTRAINT CK_Inventory_Reserved     CHECK (ReservedQuantity >= 0),
        CONSTRAINT CK_Inventory_Threshold    CHECK (LowStockThreshold >= 0)
    );
END
GO

-- 2.5 Carts
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carts')
BEGIN
    CREATE TABLE Carts (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId      NVARCHAR(450)   NULL,
        SessionKey  NVARCHAR(100)   NULL,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        ExpiresAt   DATETIME2(7)    NULL
    );
END
GO

-- 2.6 CartItems
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CartItems')
BEGIN
    CREATE TABLE CartItems (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        CartId      INT             NOT NULL,
        ProductId   INT             NOT NULL,
        Quantity    DECIMAL(10,3)   NOT NULL DEFAULT 1,
        UnitPrice   DECIMAL(10,2)   NOT NULL,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CartItems_CartProduct UNIQUE (CartId, ProductId),
        CONSTRAINT FK_CartItems_Carts    FOREIGN KEY (CartId)    REFERENCES Carts(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CartItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT CK_CartItems_Qty CHECK (Quantity > 0)
    );
END
GO

-- 2.7 UserAddresses
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserAddresses')
BEGIN
    CREATE TABLE UserAddresses (
        Id                      INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId                  NVARCHAR(450)   NOT NULL,
        AddressLine1            NVARCHAR(200)   NOT NULL DEFAULT '',
        AddressLine2            NVARCHAR(200)   NULL,
        City                    NVARCHAR(100)   NOT NULL,
        State                   NVARCHAR(100)   NOT NULL,
        Pincode                 NVARCHAR(10)    NOT NULL,
        IsDefault               BIT             NOT NULL DEFAULT 0,
        RecipientName           NVARCHAR(200)   NOT NULL DEFAULT '',
        RecipientMobile         NVARCHAR(15)    NOT NULL DEFAULT '',
        AddressLabel            NVARCHAR(20)    NOT NULL DEFAULT 'Home',
        HouseNumber             NVARCHAR(100)   NOT NULL DEFAULT '',
        BuildingStreet          NVARCHAR(200)   NOT NULL DEFAULT '',
        Locality                NVARCHAR(200)   NOT NULL DEFAULT '',
        Landmark                NVARCHAR(200)   NULL,
        DeliveryInstructions    NVARCHAR(500)   NULL,
        IsDeleted               BIT             NOT NULL DEFAULT 0,
        Latitude                FLOAT           NULL,
        Longitude               FLOAT           NULL,
        CreatedAt               DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt               DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_UserAddresses_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
        CONSTRAINT CK_UserAddresses_Label CHECK (AddressLabel IN ('Home', 'Work', 'Other')),
        CONSTRAINT CK_UserAddresses_Mobile CHECK (RecipientMobile = '' OR RecipientMobile LIKE '+91[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'),
        CONSTRAINT CK_UserAddresses_Pincode CHECK (Pincode LIKE '[0-9][0-9][0-9][0-9][0-9][0-9]'),
        CONSTRAINT CK_UserAddresses_Latitude CHECK (Latitude IS NULL OR (Latitude >= -90 AND Latitude <= 90)),
        CONSTRAINT CK_UserAddresses_Longitude CHECK (Longitude IS NULL OR (Longitude >= -180 AND Longitude <= 180))
    );
END
GO

-- 2.8 DeliveryZones
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryZones')
BEGIN
    CREATE TABLE DeliveryZones (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Name            NVARCHAR(100)   NOT NULL,
        Pincodes        NVARCHAR(MAX)   NOT NULL,
        DeliveryFee     DECIMAL(10,2)   NOT NULL DEFAULT 0,
        MinOrderValue   DECIMAL(10,2)   NOT NULL DEFAULT 0,
        IsActive        BIT             NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 2.9 DeliverySlots
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliverySlots')
BEGIN
    CREATE TABLE DeliverySlots (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ZoneId      INT             NOT NULL,
        SlotLabel   NVARCHAR(50)    NOT NULL,
        StartTime   TIME            NOT NULL,
        EndTime     TIME            NOT NULL,
        Capacity    INT             NOT NULL DEFAULT 10,
        IsActive    BIT             NOT NULL DEFAULT 1,
        CONSTRAINT FK_DeliverySlots_Zones FOREIGN KEY (ZoneId) REFERENCES DeliveryZones(Id) ON DELETE CASCADE
    );
END
GO

-- 2.10 DeliveryAreas
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryAreas')
BEGIN
    CREATE TABLE DeliveryAreas (
        Id                      INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PinCode                 CHAR(6)         NOT NULL,
        AreaName                NVARCHAR(200)   NOT NULL,
        IsActive                BIT             NOT NULL DEFAULT 1,
        EstimatedDeliveryWindow NVARCHAR(100)   NULL,
        MinOrderValue           DECIMAL(10,2)   NULL,
        DeliveryFee             DECIMAL(10,2)   NULL,
        FreeDeliveryAbove       DECIMAL(10,2)   NULL,
        CreatedAt               DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt               DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_DeliveryAreas_PinCode CHECK (PinCode LIKE '[0-9][0-9][0-9][0-9][0-9][0-9]')
    );
END
GO

-- 2.11 Orders
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE Orders (
        Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderNumber         NVARCHAR(30)    NOT NULL,
        UserId              NVARCHAR(450)   NOT NULL,
        ShipToAddressLine1  NVARCHAR(200)   NOT NULL,
        ShipToAddressLine2  NVARCHAR(200)   NULL,
        ShipToCity          NVARCHAR(100)   NOT NULL,
        ShipToState         NVARCHAR(100)   NOT NULL,
        ShipToPincode       NVARCHAR(10)    NOT NULL,
        SubTotal            DECIMAL(10,2)   NOT NULL,
        DiscountAmount      DECIMAL(10,2)   NOT NULL DEFAULT 0,
        DeliveryFee         DECIMAL(10,2)   NOT NULL DEFAULT 0,
        TotalAmount         DECIMAL(10,2)   NOT NULL,
        CouponCode          NVARCHAR(50)    NULL,
        CouponDiscount      DECIMAL(10,2)   NOT NULL DEFAULT 0,
        Status              NVARCHAR(30)    NOT NULL DEFAULT 'Pending',
        PaymentMethod       NVARCHAR(50)    NOT NULL DEFAULT 'COD',
        PaymentStatus       NVARCHAR(30)    NOT NULL DEFAULT 'Pending',
        IdempotencyKey      NVARCHAR(100)   NULL,
        DeliverySlotId      INT             NULL,
        ScheduledDelivery   DATETIME2(7)    NULL,
        Notes               NVARCHAR(1000)  NULL,
        CreatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION,
        CONSTRAINT UQ_Orders_OrderNumber UNIQUE (OrderNumber),
        CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id),
        CONSTRAINT FK_Orders_DeliverySlots FOREIGN KEY (DeliverySlotId) REFERENCES DeliverySlots(Id),
        CONSTRAINT CK_Orders_Status CHECK (Status IN (
            'Pending', 'Confirmed', 'Processing',
            'PickedUp', 'OutForDelivery', 'Delivered',
            'DeliveryFailed', 'Cancelled', 'Refunded'
        )),
        CONSTRAINT CK_Orders_PaymentStatus CHECK (PaymentStatus IN ('Pending','Paid','Failed','Refunded')),
        CONSTRAINT CK_Orders_Total CHECK (TotalAmount >= 0)
    );
END
GO

-- 2.12 OrderItems
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderItems')
BEGIN
    CREATE TABLE OrderItems (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderId         INT             NOT NULL,
        ProductId       INT             NOT NULL,
        ProductName     NVARCHAR(200)   NOT NULL,
        ProductSKU      NVARCHAR(50)    NOT NULL,
        UnitLabel       NVARCHAR(50)    NOT NULL,
        Quantity        DECIMAL(10,3)   NOT NULL,
        UnitPrice       DECIMAL(10,2)   NOT NULL,
        OfferPrice      DECIMAL(10,2)   NULL,
        EffectivePrice  DECIMAL(10,2)   NOT NULL,
        LineTotal       DECIMAL(10,2)   NOT NULL,
        CONSTRAINT FK_OrderItems_Orders   FOREIGN KEY (OrderId)   REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT CK_OrderItems_Qty   CHECK (Quantity > 0),
        CONSTRAINT CK_OrderItems_Price CHECK (EffectivePrice > 0)
    );
END
GO

-- 2.13 Payments
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE Payments (
        Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderId             INT             NOT NULL,
        GatewayName         NVARCHAR(50)    NOT NULL,   -- e.g. Razorpay
        GatewayPaymentId    NVARCHAR(200)   NULL,       -- e.g. pay_xxx
        GatewayOrderId      NVARCHAR(200)   NULL,       -- e.g. order_xxx
        Amount              DECIMAL(10,2)   NOT NULL,
        Currency            NVARCHAR(5)     NOT NULL DEFAULT 'INR',
        Status              NVARCHAR(30)    NOT NULL DEFAULT 'Initiated',
        Method              NVARCHAR(50)    NULL,       -- UPI, Card, NetBanking, Wallet, COD
        FailureReason       NVARCHAR(500)   NULL,
        CreatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Payments_Status CHECK (Status IN ('Initiated','Pending','Captured','Failed','Refunded','PartialRefund'))
    );
END
GO

-- 2.14 PaymentWebhookEvents
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentWebhookEvents')
BEGIN
    CREATE TABLE PaymentWebhookEvents (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PaymentId       INT             NULL,
        GatewayName     NVARCHAR(50)    NOT NULL,
        EventType       NVARCHAR(100)   NOT NULL,
        GatewayEventId  NVARCHAR(200)   NULL,
        RawPayload      NVARCHAR(MAX)   NOT NULL,
        ProcessedAt     DATETIME2(7)    NULL,
        ProcessingError NVARCHAR(1000)  NULL,
        ReceivedAt      DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WebhookEvents_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id) ON DELETE SET NULL
    );
END
GO

-- 2.15 Coupons
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Coupons')
BEGIN
    CREATE TABLE Coupons (
        Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        Code                NVARCHAR(50)    NOT NULL,
        Description         NVARCHAR(300)   NULL,
        DiscountType        NVARCHAR(20)    NOT NULL,   -- Percentage or Fixed
        DiscountValue       DECIMAL(10,2)   NOT NULL,
        MinOrderValue       DECIMAL(10,2)   NOT NULL DEFAULT 0,
        MaxDiscountAmount   DECIMAL(10,2)   NULL,
        UsageLimit          INT             NULL,
        PerUserLimit        INT             NULL DEFAULT 1,
        ValidFrom           DATETIME2(7)    NOT NULL,
        ValidUntil          DATETIME2(7)    NULL,
        IsActive            BIT             NOT NULL DEFAULT 1,
        CreatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Coupons_Code UNIQUE (Code),
        CONSTRAINT CK_Coupons_DiscountType  CHECK (DiscountType IN ('Percentage','Fixed')),
        CONSTRAINT CK_Coupons_DiscountValue CHECK (DiscountValue > 0),
        CONSTRAINT CK_Coupons_PctCap        CHECK (DiscountType = 'Fixed' OR DiscountValue <= 100)
    );
END
GO

-- 2.16 CouponRedemptions
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CouponRedemptions')
BEGIN
    CREATE TABLE CouponRedemptions (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        CouponId    INT             NOT NULL,
        UserId      NVARCHAR(450)   NOT NULL,
        OrderId     INT             NOT NULL,
        Discount    DECIMAL(10,2)   NOT NULL,
        RedeemedAt  DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CouponRedemptions_Coupons FOREIGN KEY (CouponId) REFERENCES Coupons(Id),
        CONSTRAINT FK_CouponRedemptions_Users   FOREIGN KEY (UserId)   REFERENCES AspNetUsers(Id),
        CONSTRAINT FK_CouponRedemptions_Orders  FOREIGN KEY (OrderId)  REFERENCES Orders(Id) ON DELETE CASCADE
    );
END
GO

-- 2.17 OrderStatusHistory
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderStatusHistory')
BEGIN
    CREATE TABLE OrderStatusHistory (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderId     INT             NOT NULL,
        Status      NVARCHAR(30)    NOT NULL,
        Notes       NVARCHAR(500)   NULL,
        ChangedBy   NVARCHAR(450)   NULL,
        ChangedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
    );
END
GO

-- 2.18 InventoryTransactions
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventoryTransactions')
BEGIN
    CREATE TABLE InventoryTransactions (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        ProductId       INT             NOT NULL,
        TransactionType NVARCHAR(30)    NOT NULL,
        QuantityChange  DECIMAL(10,3)   NOT NULL,
        QuantityAfter   DECIMAL(10,3)   NOT NULL,
        ReferenceId     INT             NULL,
        ReferenceType   NVARCHAR(50)    NULL,
        Notes           NVARCHAR(500)   NULL,
        CreatedBy       NVARCHAR(450)   NULL,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InventoryTx_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT CK_InventoryTx_Type CHECK (TransactionType IN ('Purchase','Sale','Adjustment','Return','Reserved','Released','Wastage'))
    );
END
GO

-- 2.19 StoreSettings
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StoreSettings')
BEGIN
    CREATE TABLE StoreSettings (
        Id          INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        SettingKey  NVARCHAR(100)   NOT NULL,
        SettingValue NVARCHAR(MAX)  NOT NULL DEFAULT '',
        DataType    NVARCHAR(20)    NOT NULL DEFAULT 'String',
        Category    NVARCHAR(50)    NOT NULL DEFAULT 'General',
        Description NVARCHAR(500)   NULL,
        UpdatedBy   NVARCHAR(450)   NULL,
        UpdatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_StoreSettings_Key UNIQUE (SettingKey)
    );
END
GO

-- 2.20 AdminLogs
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdminLogs')
BEGIN
    CREATE TABLE AdminLogs (
        Id          BIGINT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
        AdminUserId NVARCHAR(450)   NOT NULL,
        Action      NVARCHAR(100)   NOT NULL,
        EntityType  NVARCHAR(50)    NULL,
        EntityId    NVARCHAR(100)   NULL,
        Details     NVARCHAR(MAX)   NULL,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- 2.21 DeliveryPersonnel
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryPersonnel')
BEGIN
    CREATE TABLE DeliveryPersonnel (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId          NVARCHAR(450)   NULL,
        FullName        NVARCHAR(200)   NOT NULL,
        ContactNumber   NVARCHAR(20)    NOT NULL,
        IsActive        BIT             NOT NULL DEFAULT 1,
        Notes           NVARCHAR(500)   NULL,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_DeliveryPersonnel_UserId UNIQUE (UserId),
        CONSTRAINT FK_DeliveryPersonnel_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    );
END
GO

-- 2.22 DeliveryAssignments
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryAssignments')
BEGIN
    CREATE TABLE DeliveryAssignments (
        Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderId             INT             NOT NULL,
        DeliveryPersonnelId INT             NOT NULL,
        AssignedBy          NVARCHAR(450)   NULL,
        AssignedAt          DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UnassignedAt        DATETIME2(7)    NULL,
        UnassignedBy        NVARCHAR(450)   NULL,
        UnassignedReason    NVARCHAR(300)   NULL,
        IsActive            BIT             NOT NULL DEFAULT 1,
        CONSTRAINT FK_DeliveryAssignments_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_DeliveryAssignments_Personnel FOREIGN KEY (DeliveryPersonnelId) REFERENCES DeliveryPersonnel(Id)
    );
END
GO

-- 2.23 NotificationEvents
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NotificationEvents')
BEGIN
    CREATE TABLE NotificationEvents (
        Id              BIGINT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderId         INT             NOT NULL,
        EventType       NVARCHAR(60)    NOT NULL,
        IdempotencyKey  NVARCHAR(100)   NOT NULL,
        RecipientUserId NVARCHAR(450)   NULL,
        RecipientEmail  NVARCHAR(256)   NULL,
        RecipientPhone  NVARCHAR(30)    NULL,
        Channel         NVARCHAR(30)    NOT NULL DEFAULT 'InApp',
        Status          NVARCHAR(20)    NOT NULL DEFAULT 'Queued',
        FailureReason   NVARCHAR(1000)  NULL,
        QueuedAt        DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        SentAt          DATETIME2(7)    NULL,
        CONSTRAINT UQ_NotificationEvents_Idempotency UNIQUE (IdempotencyKey),
        CONSTRAINT FK_NotificationEvents_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT CK_NotificationEvents_EventType CHECK (EventType IN (
            'OrderConfirmed', 'PreparationStarted', 'PickedUp',
            'OutForDelivery', 'Delivered', 'DeliveryFailed', 'Rescheduled'
        )),
        CONSTRAINT CK_NotificationEvents_Channel CHECK (Channel IN ('InApp', 'Email', 'WhatsApp')),
        CONSTRAINT CK_NotificationEvents_Status  CHECK (Status IN ('Queued', 'Sent', 'Failed', 'Skipped'))
    );
END
GO

-- =============================================================================
-- SECTION 3: PERFORMANCE INDEXES (WITH FILTERED & COVERING SPECS)
-- =============================================================================

-- Identity Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_AspNetUsers_NormalizedEmail' AND object_id = OBJECT_ID('AspNetUsers'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_AspNetUsers_NormalizedEmail ON AspNetUsers (NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_NormalizedUserName' AND object_id = OBJECT_ID('AspNetUsers'))
    CREATE UNIQUE NONCLUSTERED INDEX IX_AspNetUsers_NormalizedUserName ON AspNetUsers (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetRoles_NormalizedName' AND object_id = OBJECT_ID('AspNetRoles'))
    CREATE UNIQUE NONCLUSTERED INDEX IX_AspNetRoles_NormalizedName ON AspNetRoles (NormalizedName) WHERE NormalizedName IS NOT NULL;
GO

-- Categories Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Categories_Slug' AND object_id = OBJECT_ID('Categories'))
    CREATE UNIQUE INDEX IX_Categories_Slug ON Categories (Slug);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Categories_IsActive_SortOrder' AND object_id = OBJECT_ID('Categories'))
    CREATE INDEX IX_Categories_IsActive_SortOrder ON Categories (IsActive, SortOrder);
GO

-- Products Indexes
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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Name_IsActive' AND object_id = OBJECT_ID('Products'))
    CREATE INDEX IX_Products_Name_IsActive ON Products (IsActive) INCLUDE (Name, Slug, CategoryId, Price, OfferPrice, UnitLabel, ImagePath);
GO

-- Inventory Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Inventory_ProductId' AND object_id = OBJECT_ID('Inventory'))
    CREATE UNIQUE INDEX IX_Inventory_ProductId ON Inventory (ProductId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Inventory_LowStock' AND object_id = OBJECT_ID('Inventory'))
    CREATE INDEX IX_Inventory_LowStock ON Inventory (QuantityOnHand, LowStockThreshold);
GO

-- ProductImages Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductImages_ProductId_SortOrder' AND object_id = OBJECT_ID('ProductImages'))
    CREATE INDEX IX_ProductImages_ProductId_SortOrder ON ProductImages (ProductId, SortOrder);
GO

-- Carts Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Carts_UserId' AND object_id = OBJECT_ID('Carts'))
    CREATE INDEX IX_Carts_UserId ON Carts (UserId) WHERE UserId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Carts_SessionKey' AND object_id = OBJECT_ID('Carts'))
    CREATE INDEX IX_Carts_SessionKey ON Carts (SessionKey) WHERE SessionKey IS NOT NULL;
GO

-- UserAddresses Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserAddresses_UserId' AND object_id = OBJECT_ID('UserAddresses'))
    CREATE INDEX IX_UserAddresses_UserId ON UserAddresses (UserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserAddresses_UserId_IsDeleted' AND object_id = OBJECT_ID('UserAddresses'))
    CREATE NONCLUSTERED INDEX IX_UserAddresses_UserId_IsDeleted ON UserAddresses (UserId, IsDeleted) INCLUDE (IsDefault, AddressLabel);
GO

-- Delivery Serviceability Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_DeliveryAreas_PinCode_Active' AND object_id = OBJECT_ID('DeliveryAreas'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_DeliveryAreas_PinCode_Active ON DeliveryAreas (PinCode) WHERE IsActive = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeliveryAreas_IsActive' AND object_id = OBJECT_ID('DeliveryAreas'))
    CREATE NONCLUSTERED INDEX IX_DeliveryAreas_IsActive ON DeliveryAreas (IsActive) INCLUDE (PinCode, AreaName, EstimatedDeliveryWindow, MinOrderValue, DeliveryFee, FreeDeliveryAbove);
GO

-- Orders Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_OrderNumber' AND object_id = OBJECT_ID('Orders'))
    CREATE UNIQUE INDEX IX_Orders_OrderNumber ON Orders (OrderNumber);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_UserId_CreatedAt' AND object_id = OBJECT_ID('Orders'))
    CREATE NONCLUSTERED INDEX IX_Orders_UserId_CreatedAt ON Orders (UserId, CreatedAt DESC) INCLUDE (OrderNumber, Status, PaymentStatus, TotalAmount);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_Status' AND object_id = OBJECT_ID('Orders'))
    CREATE NONCLUSTERED INDEX IX_Orders_Status ON Orders (Status) INCLUDE (UserId, OrderNumber, CreatedAt);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_CreatedAt_Desc' AND object_id = OBJECT_ID('Orders'))
    CREATE NONCLUSTERED INDEX IX_Orders_CreatedAt_Desc ON Orders (CreatedAt DESC) INCLUDE (OrderNumber, UserId, Status, PaymentStatus, TotalAmount);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Orders_IdempotencyKey' AND object_id = OBJECT_ID('Orders'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Orders_IdempotencyKey ON Orders (IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
GO

-- OrderItems Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderItems_OrderId' AND object_id = OBJECT_ID('OrderItems'))
    CREATE INDEX IX_OrderItems_OrderId ON OrderItems (OrderId);
GO

-- Payments Indexes (Razorpay & Online Gateways)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_OrderId' AND object_id = OBJECT_ID('Payments'))
    CREATE NONCLUSTERED INDEX IX_Payments_OrderId ON Payments (OrderId) INCLUDE (GatewayName, Status, Amount, Currency, GatewayOrderId, GatewayPaymentId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_GatewayOrderId' AND object_id = OBJECT_ID('Payments'))
    CREATE NONCLUSTERED INDEX IX_Payments_GatewayOrderId ON Payments (GatewayOrderId) INCLUDE (OrderId, Status, Amount, Currency) WHERE GatewayOrderId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Payments_GatewayPaymentId' AND object_id = OBJECT_ID('Payments'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Payments_GatewayPaymentId ON Payments (GatewayPaymentId) WHERE GatewayPaymentId IS NOT NULL;
GO

-- Webhook Events Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_WebhookEvents_GatewayEventId' AND object_id = OBJECT_ID('PaymentWebhookEvents'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_WebhookEvents_GatewayEventId ON PaymentWebhookEvents (GatewayEventId) WHERE GatewayEventId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WebhookEvents_GatewayName_EventType' AND object_id = OBJECT_ID('PaymentWebhookEvents'))
    CREATE NONCLUSTERED INDEX IX_WebhookEvents_GatewayName_EventType ON PaymentWebhookEvents (GatewayName, EventType, ReceivedAt DESC) INCLUDE (GatewayEventId, ProcessedAt);
GO

-- Coupons Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Coupons_Code' AND object_id = OBJECT_ID('Coupons'))
    CREATE UNIQUE INDEX IX_Coupons_Code ON Coupons (Code);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Coupons_IsActive_ValidUntil' AND object_id = OBJECT_ID('Coupons'))
    CREATE INDEX IX_Coupons_IsActive_ValidUntil ON Coupons (IsActive, ValidUntil) WHERE IsActive = 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CouponRedemptions_UserId_CouponId' AND object_id = OBJECT_ID('CouponRedemptions'))
    CREATE INDEX IX_CouponRedemptions_UserId_CouponId ON CouponRedemptions (UserId, CouponId);
GO

-- Order Status History & Inventory Transactions Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrderStatusHistory_OrderId' AND object_id = OBJECT_ID('OrderStatusHistory'))
    CREATE NONCLUSTERED INDEX IX_OrderStatusHistory_OrderId ON OrderStatusHistory (OrderId, ChangedAt DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryTx_ProductId_CreatedAt' AND object_id = OBJECT_ID('InventoryTransactions'))
    CREATE INDEX IX_InventoryTx_ProductId_CreatedAt ON InventoryTransactions (ProductId, CreatedAt DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InventoryTx_ReferenceId' AND object_id = OBJECT_ID('InventoryTransactions'))
    CREATE NONCLUSTERED INDEX IX_InventoryTx_ReferenceId ON InventoryTransactions (ReferenceId, ReferenceType) INCLUDE (ProductId, TransactionType);
GO

-- Admin Logs & Personnel Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AdminLogs_AdminUserId_CreatedAt' AND object_id = OBJECT_ID('AdminLogs'))
    CREATE NONCLUSTERED INDEX IX_AdminLogs_AdminUserId_CreatedAt ON AdminLogs (AdminUserId, CreatedAt DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeliveryPersonnel_IsActive' AND object_id = OBJECT_ID('DeliveryPersonnel'))
    CREATE NONCLUSTERED INDEX IX_DeliveryPersonnel_IsActive ON DeliveryPersonnel (IsActive) INCLUDE (FullName, ContactNumber, UserId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeliveryPersonnel_UserId' AND object_id = OBJECT_ID('DeliveryPersonnel'))
    CREATE NONCLUSTERED INDEX IX_DeliveryPersonnel_UserId ON DeliveryPersonnel (UserId) INCLUDE (FullName, IsActive);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeliveryAssignments_Personnel_Active' AND object_id = OBJECT_ID('DeliveryAssignments'))
    CREATE NONCLUSTERED INDEX IX_DeliveryAssignments_Personnel_Active ON DeliveryAssignments (DeliveryPersonnelId, IsActive) INCLUDE (OrderId, AssignedAt);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_DeliveryAssignments_ActiveOrder' AND object_id = OBJECT_ID('DeliveryAssignments'))
    CREATE UNIQUE NONCLUSTERED INDEX UQ_DeliveryAssignments_ActiveOrder ON DeliveryAssignments (OrderId) WHERE IsActive = 1;
GO

-- Notifications Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationEvents_OrderId_EventType' AND object_id = OBJECT_ID('NotificationEvents'))
    CREATE NONCLUSTERED INDEX IX_NotificationEvents_OrderId_EventType ON NotificationEvents (OrderId, EventType) INCLUDE (Status, QueuedAt);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NotificationEvents_RecipientUserId' AND object_id = OBJECT_ID('NotificationEvents'))
    CREATE NONCLUSTERED INDEX IX_NotificationEvents_RecipientUserId ON NotificationEvents (RecipientUserId, QueuedAt DESC) WHERE RecipientUserId IS NOT NULL INCLUDE (EventType, Status, OrderId);
GO

-- =============================================================================
-- SECTION 4: SEED ROLES & DEFAULT STORE SETTINGS
-- =============================================================================

-- 4.1 Default Roles
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'ADMIN')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Admin', 'ADMIN', CAST(NEWID() AS NVARCHAR(MAX)));
END
GO

IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'DELIVERYPERSONNEL')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'DeliveryPersonnel', 'DELIVERYPERSONNEL', CAST(NEWID() AS NVARCHAR(MAX)));
END
GO

-- 4.2 Default Store Settings
IF NOT EXISTS (SELECT 1 FROM StoreSettings WHERE SettingKey = 'Store.Name')
BEGIN
    INSERT INTO StoreSettings (SettingKey, SettingValue, DataType, Category, Description)
    VALUES
        ('Store.Name',             'GOLIS Mart',    'String',  'General',  'Public store display name'),
        ('Store.ContactEmail',     'support@golismart.com', 'String',  'General',  'Customer support email'),
        ('Store.ContactPhone',     '+91 98765 43210',       'String',  'General',  'Customer support phone'),
        ('Store.IsOpen',           '1',              'Bool',    'General',  '1 = store accepting orders, 0 = closed'),
        ('Delivery.DefaultFee',    '40.00',          'Decimal', 'Delivery', 'Default delivery fee when area has no override'),
        ('Delivery.FreeAbove',     '500.00',         'Decimal', 'Delivery', 'Cart subtotal above which delivery is free'),
        ('Delivery.MinOrder',      '0.00',           'Decimal', 'Delivery', 'Minimum cart subtotal to place an order (0 = no minimum)'),
        ('Inventory.LowStockDefault', '5',           'Int',     'Inventory','Default low-stock threshold for new products');
END
GO

-- =============================================================================
-- SECTION 5: SEED CATEGORIES & PRODUCTS & INVENTORY
-- =============================================================================

-- 5.1 Seed Categories (10 categories)
MERGE Categories AS target
USING (VALUES
    ('Fresh Vegetables',         'fresh-vegetables',       'Farm-fresh vegetables delivered daily.',           '/images/categories/fresh-vegetables.jpg',   1),
    ('Fresh Fruits',             'fresh-fruits',           'Seasonal and exotic fruits sourced locally.',      '/images/categories/fresh-fruits.jpg',         2),
    ('Rice and Atta',            'rice-and-atta',          'Basmati, sona masoori, multi-grain atta and more.','/images/categories/rice-and-atta.jpg',        3),
    ('Dal and Pulses',           'dal-and-pulses',         'Toor dal, moong dal, chana dal and more.',         '/images/categories/dal-and-pulses.jpg',       4),
    ('Dairy and Eggs',           'dairy-and-eggs',         'Fresh milk, curd, paneer, butter and eggs.',       '/images/categories/dairy-and-eggs.jpg',       5),
    ('Cooking Oils',             'cooking-oils',           'Refined, cold-pressed and speciality oils.',       '/images/categories/cooking-oils.jpg',         6),
    ('Spices and Masalas',       'spices-and-masalas',     'Whole spices, ground masalas and blends.',         '/images/categories/spices-and-masalas.jpg',   7),
    ('Snacks and Packaged Foods','snacks-packaged-foods',  'Namkeen, biscuits, noodles and ready-to-eat.',    '/images/categories/snacks-packaged-foods.jpg', 8),
    ('Beverages',                'beverages',              'Tea, coffee, juices and cold drinks.',             '/images/categories/beverages.jpg',             9),
    ('Household Essentials',     'household-essentials',   'Cleaning, hygiene and daily-use household items.','/images/categories/household-essentials.jpg',  10)
) AS source (Name, Slug, Description, ImagePath, SortOrder)
ON target.Slug = source.Slug
WHEN MATCHED THEN
    UPDATE SET
        Name        = source.Name,
        Description = source.Description,
        ImagePath   = source.ImagePath,
        SortOrder   = source.SortOrder,
        IsActive    = 1,
        UpdatedAt   = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Name, Slug, Description, ImagePath, SortOrder, IsActive, CreatedAt, UpdatedAt)
    VALUES (source.Name, source.Slug, source.Description, source.ImagePath, source.SortOrder, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
GO

-- 5.2 Seed Products & Initial Inventory
BEGIN
    DECLARE @CatVeg    INT = (SELECT Id FROM Categories WHERE Slug = 'fresh-vegetables');
    DECLARE @CatFruit  INT = (SELECT Id FROM Categories WHERE Slug = 'fresh-fruits');
    DECLARE @CatRice   INT = (SELECT Id FROM Categories WHERE Slug = 'rice-and-atta');
    DECLARE @CatDal    INT = (SELECT Id FROM Categories WHERE Slug = 'dal-and-pulses');
    DECLARE @CatDairy  INT = (SELECT Id FROM Categories WHERE Slug = 'dairy-and-eggs');
    DECLARE @CatOil    INT = (SELECT Id FROM Categories WHERE Slug = 'cooking-oils');
    DECLARE @CatSpice  INT = (SELECT Id FROM Categories WHERE Slug = 'spices-and-masalas');
    DECLARE @CatSnack  INT = (SELECT Id FROM Categories WHERE Slug = 'snacks-packaged-foods');
    DECLARE @CatBev    INT = (SELECT Id FROM Categories WHERE Slug = 'beverages');
    DECLARE @CatHouse  INT = (SELECT Id FROM Categories WHERE Slug = 'household-essentials');

    CREATE TABLE #AllSeedProducts (
        CategoryId  INT,
        SKU         NVARCHAR(50),
        Name        NVARCHAR(200),
        Slug        NVARCHAR(200),
        Description NVARCHAR(2000),
        UnitCode    NVARCHAR(20),
        UnitLabel   NVARCHAR(50),
        Price       DECIMAL(10,2),
        OfferPrice  DECIMAL(10,2),
        IsFeatured  BIT,
        ImagePath   NVARCHAR(500),
        Stock       DECIMAL(10,3),
        LowStock    DECIMAL(10,3)
    );

    -- Fresh Vegetables
    INSERT INTO #AllSeedProducts VALUES
    (@CatVeg, 'VEG-TOM-001', 'Fresh Tomatoes',       'fresh-tomatoes',       'Red, ripe farm tomatoes. Great for curries and salads.',                   'kg',  '1 kg',    35.00,  NULL,  1, '/images/products/tomatoes.jpg',       50, 10),
    (@CatVeg, 'VEG-ONI-001', 'Red Onions',           'red-onions',           'Medium-sized red onions with a sharp flavour.',                            'kg',  '1 kg',    40.00,  35.00, 0, '/images/products/onions.jpg',         80, 15),
    (@CatVeg, 'VEG-POT-001', 'Potatoes',             'potatoes',             'Fresh potatoes ideal for sabzi, fries and curries.',                       'kg',  '1 kg',    30.00,  NULL,  1, '/images/products/potatoes.jpg',       100, 20),
    (@CatVeg, 'VEG-CHI-001', 'Green Chillies',       'green-chillies',       'Fresh hot green chillies, 100 g punnet.',                                  'g',   '100 g',   15.00,  NULL,  0, '/images/products/green-chillies.jpg', 30,  5),
    (@CatVeg, 'VEG-SPI-001', 'Spinach (Palak)',       'spinach-palak',        'Tender baby spinach leaves, washed and ready to cook.',                    'pack','1 bunch', 25.00,  20.00, 0, '/images/products/spinach.jpg',        40, 8),
    (@CatVeg, 'VEG-CAR-001', 'Carrots',              'carrots',              'Crunchy orange carrots, great for halwa and sabzi.',                       'kg',  '500 g',   28.00,  25.00, 0, '/images/products/carrots.jpg',        60, 10),
    (@CatVeg, 'VEG-CAU-001', 'Cauliflower (Gobi)',   'cauliflower',          'Firm white cauliflower heads, great for aloo-gobi and pakoras.',             'pc',  '1 pc',   35.00,  NULL,  0, '/images/products/cauliflower.jpg',  40, 8),
    (@CatVeg, 'VEG-CUC-001', 'Cucumber (Kheera)',    'cucumber',             'Cool, crisp cucumbers — ideal for raita and salads.',                        'kg',  '500 g',  28.00,  24.00, 0, '/images/products/cucumber.jpg',     50, 10),
    (@CatVeg, 'VEG-BRJ-001', 'Brinjal (Baingan)',    'brinjal',              'Purple brinjals, perfect for baingan bharta and curry.',                     'kg',  '500 g',  30.00,  NULL,  0, '/images/products/brinjal.jpg',      35, 8),
    (@CatVeg, 'VEG-CAP-001', 'Capsicum (Shimla Mirch)','capsicum',           'Bright green capsicum, crunchy and sweet. Good for stir-fry.',               'kg',  '250 g',  40.00,  35.00, 0, '/images/products/capsicum.jpg',     30, 6);

    -- Fresh Fruits
    INSERT INTO #AllSeedProducts VALUES
    (@CatFruit, 'FRT-BAN-001', 'Bananas (Robusta)',  'bananas-robusta',      'Sweet Robusta bananas, dozen approximately 700 g.',                        'pack','12 pcs',  45.00,  40.00, 1, '/images/products/bananas.jpg',        50,  8),
    (@CatFruit, 'FRT-APP-001', 'Red Apples',         'red-apples',           'Crisp Shimla apples, rich in fibre.',                                      'kg',  '1 kg',    150.00, 130.00,1, '/images/products/apples.jpg',         30,  5),
    (@CatFruit, 'FRT-ORG-001', 'Oranges (Nagpur)',   'oranges-nagpur',       'Juicy Nagpur oranges, high in vitamin C.',                                 'kg',  '1 kg',    80.00,  NULL,  0, '/images/products/oranges.jpg',        40,  8),
    (@CatFruit, 'FRT-MAN-001', 'Mangoes (Alphonso)', 'mangoes-alphonso',     'Premium Alphonso mangoes — the king of fruits.',                           'pack','6 pcs',   350.00, 300.00,1, '/images/products/mangoes.jpg',        20,  4),
    (@CatFruit, 'FRT-GRP-001', 'Grapes (Green)',     'grapes-green',         'Seedless green grapes, sweet and firm.',                                   'kg',  '500 g',   90.00,  80.00, 0, '/images/products/grapes.jpg',         25,  5),
    (@CatFruit, 'FRT-POM-001', 'Pomegranate (Anar)',  'pomegranate',          'Sweet ruby-red pomegranate, packed with antioxidants.',                     'pc',  '1 pc',   80.00,  70.00, 0, '/images/products/pomegranate.jpg',  25, 5),
    (@CatFruit, 'FRT-WAT-001', 'Watermelon (Tarbooz)','watermelon',           'Sweet and refreshing whole watermelon, approx 3–4 kg.',                     'pc',  '1 pc',   120.00, NULL,  0, '/images/products/watermelon.jpg',   15, 3),
    (@CatFruit, 'FRT-PAP-001', 'Papaya (Papita)',     'papaya',               'Ripe yellow papaya — great for digestion and smoothies.',                   'kg',  '1 kg',   55.00,  NULL,  0, '/images/products/papaya.jpg',       20, 4);

    -- Rice & Atta
    INSERT INTO #AllSeedProducts VALUES
    (@CatRice, 'RIC-BAS-001', 'Basmati Rice',        'basmati-rice-5kg',     'Long-grain aged Basmati rice. Aromatic and fluffy.',                       'kg',  '5 kg',    399.00, 349.00,1, '/images/products/basmati-rice.jpg',   40, 5),
    (@CatRice, 'RIC-SON-001', 'Sona Masoori Rice',   'sona-masoori-rice-5kg','Light and low-starch Sona Masoori rice. Ideal for everyday meals.',        'kg',  '5 kg',    310.00, NULL,  0, '/images/products/sona-masoori.jpg',   30, 5),
    (@CatRice, 'ATT-WHL-001', 'Whole Wheat Atta',    'whole-wheat-atta-5kg', '100% whole wheat atta, stone-ground for softness and nutrition.',           'kg',  '5 kg',    245.00, 225.00,1, '/images/products/atta.jpg',           50, 8),
    (@CatRice, 'ATT-MUL-001', 'Multi-grain Atta',    'multigrain-atta-5kg',  'Blend of wheat, oats, soya and ragi for healthy rotis.',                   'kg',  '5 kg',    295.00, NULL,  0, '/images/products/multigrain-atta.jpg',30, 5),
    (@CatRice, 'RIC-IDL-001', 'Idli Rice',            'idli-rice-5kg',        'Short-grain parboiled rice ideal for making soft idlis and dosas.',          'kg',  '5 kg',   270.00, NULL,  0, '/images/products/idli-rice.jpg',    30, 5);

    -- Dal & Pulses
    INSERT INTO #AllSeedProducts VALUES
    (@CatDal, 'DAL-TOR-001', 'Toor Dal (Arhar)',     'toor-dal-1kg',         'Split pigeon peas, the most popular dal in Indian cooking.',               'kg',  '1 kg',    135.00, NULL,  1, '/images/products/toor-dal.jpg',       60, 10),
    (@CatDal, 'DAL-MOO-001', 'Moong Dal (Split)',    'moong-dal-1kg',        'Split yellow moong dal, easy to digest and nutritious.',                   'kg',  '1 kg',    120.00, 110.00,0, '/images/products/moong-dal.jpg',      50,  8),
    (@CatDal, 'DAL-CHA-001', 'Chana Dal',            'chana-dal-1kg',        'Split Bengal gram — used in dals, curries and sweets.',                    'kg',  '1 kg',    110.00, NULL,  0, '/images/products/chana-dal.jpg',      45,  8),
    (@CatDal, 'DAL-MAS-001', 'Masoor Dal (Red)',     'masoor-dal-1kg',       'Red lentils that cook quickly without soaking.',                            'kg',  '1 kg',    105.00, 95.00, 0, '/images/products/masoor-dal.jpg',     40,  8),
    (@CatDal, 'DAL-URA-001', 'Urad Dal (Black)',      'urad-dal-1kg',         'Whole black lentils — essential for dal makhani.',                           'kg',  '1 kg',   130.00, 120.00,0, '/images/products/urad-dal.jpg',     40, 8),
    (@CatDal, 'DAL-RAJ-001', 'Rajma (Kidney Beans)',  'rajma-1kg',            'Red kidney beans for the classic Punjabi rajma curry.',                      'kg',  '500 g',  95.00,  NULL,  0, '/images/products/rajma.jpg',        35, 6);

    -- Dairy & Eggs
    INSERT INTO #AllSeedProducts VALUES
    (@CatDairy, 'DAI-MLK-001', 'Full Cream Milk',     'full-cream-milk-1l',   'Fresh toned cow milk, pasteurised. Best for tea and cooking.',             'litre','1 Litre', 62.00, NULL,  1, '/images/products/milk.jpg',           100, 20),
    (@CatDairy, 'DAI-CRD-001', 'Natural Curd (Dahi)', 'natural-curd-400g',    'Thick set curd made from full cream milk.',                                'g',    '400 g',   40.00, 35.00, 0, '/images/products/curd.jpg',           80,  15),
    (@CatDairy, 'DAI-BUT-001', 'Butter (Salted)',      'salted-butter-500g',   'Rich and creamy salted table butter.',                                     'g',    '500 g',   260.00,240.00,0, '/images/products/butter.jpg',         40,  8),
    (@CatDairy, 'DAI-EGG-001', 'Eggs (Farm Fresh)',    'farm-fresh-eggs-12',   'White eggs from free-range farm hens, tray of 12.',                        'pack', '12 pcs',  90.00,  80.00, 1, '/images/products/eggs.jpg',           60, 10),
    (@CatDairy, 'DAI-PAN-001', 'Paneer (Fresh)',       'fresh-paneer-200g',    'Fresh, soft homestyle paneer made from whole milk. 200 g block.',            'g',   '200 g',  75.00,  65.00, 1, '/images/products/paneer.jpg',       50, 10);

    -- Cooking Oils
    INSERT INTO #AllSeedProducts VALUES
    (@CatOil, 'OIL-SUN-001', 'Sunflower Oil',         'sunflower-oil-1l',     'Refined sunflower oil, light and healthy for daily cooking.',              'litre','1 Litre', 145.00,130.00,0, '/images/products/sunflower-oil.jpg',  50, 8),
    (@CatOil, 'OIL-MUS-001', 'Mustard Oil',           'mustard-oil-1l',       'Kachi ghani mustard oil — pungent and flavourful.',                        'litre','1 Litre', 185.00,NULL,  0, '/images/products/mustard-oil.jpg',    40, 6),
    (@CatOil, 'OIL-GHE-001', 'Desi Cow Ghee',         'desi-cow-ghee-500g',   'Pure clarified butter made from A2 cow milk.',                             'g',    '500 g',   450.00,420.00,1, '/images/products/ghee.jpg',           30, 5),
    (@CatOil, 'OIL-GRD-001', 'Groundnut Oil',          'groundnut-oil-1l',     'Cold-pressed groundnut (peanut) oil — classic South Indian cooking oil.',    'litre','1 Litre',195.00, NULL,  0, '/images/products/groundnut-oil.jpg',35, 5),
    (@CatOil, 'OIL-COC-001', 'Virgin Coconut Oil',     'virgin-coconut-oil-500ml','Cold-pressed virgin coconut oil, unrefined and fragrant.',               'ml',  '500 ml', 360.00, 320.00,0, '/images/products/coconut-oil.jpg',  25, 4);

    -- Spices & Masalas
    INSERT INTO #AllSeedProducts VALUES
    (@CatSpice, 'SPI-TUR-001', 'Turmeric Powder',     'turmeric-powder-100g', 'Pure haldi powder with rich colour and earthy aroma.',                     'g',   '100 g',   45.00,  40.00, 0, '/images/products/turmeric.jpg',       80, 10),
    (@CatSpice, 'SPI-RED-001', 'Red Chilli Powder',   'red-chilli-powder-100g','Mild to medium heat red chilli powder, sun-dried.',                        'g',   '100 g',   55.00,  NULL,  0, '/images/products/red-chilli-powder.jpg',60,8),
    (@CatSpice, 'SPI-GAR-001', 'Garam Masala',        'garam-masala-100g',    'Classic North Indian spice blend, freshly ground.',                        'g',   '100 g',   75.00,  65.00, 1, '/images/products/garam-masala.jpg',   70, 10),
    (@CatSpice, 'SPI-CUM-001', 'Cumin Seeds (Jeera)', 'cumin-seeds-100g',     'Whole cumin seeds, aromatic and fresh.',                                   'g',   '100 g',   60.00,  NULL,  0, '/images/products/cumin-seeds.jpg',    50, 8),
    (@CatSpice, 'SPI-COR-001', 'Coriander Powder',     'coriander-powder-100g','Freshly ground coriander powder with a mild citrusy flavour.',               'g',   '100 g',  50.00,  NULL,  0, '/images/products/coriander-powder.jpg',65,8);

    -- Snacks & Packaged Foods
    INSERT INTO #AllSeedProducts VALUES
    (@CatSnack, 'SNK-BIS-001', 'Britannia Good Day Butter Biscuits','good-day-butter-biscuits','Classic butter-rich biscuits, a teatime favourite.',     'pack','200 g',   45.00,  40.00, 0, '/images/products/good-day-biscuits.jpg',50,8),
    (@CatSnack, 'SNK-NOO-001', 'Maggi 2-Minute Noodles (Pack of 4)','maggi-noodles-pack-4',   '4-pack of Masala Maggi noodles, ready in minutes.',       'pack','4 × 70 g',80.00, 72.00, 1, '/images/products/maggi.jpg',          60, 10),
    (@CatSnack, 'SNK-CHI-001', 'Potato Chips (Classic)','potato-chips-classic', 'Thin, crisp salted potato chips. Perfect evening snack.',                   'pack','45 g',   20.00,  NULL,  0, '/images/products/potato-chips.jpg', 100,15),
    (@CatSnack, 'SNK-MIX-001', 'Punjabi Mixture',       'punjabi-mixture',      'Spicy namkeen mixture with sev, peanuts and dried fruits.',                  'pack','200 g',  65.00,  60.00, 0, '/images/products/mixture-namkeen.jpg',60,10);

    -- Beverages
    INSERT INTO #AllSeedProducts VALUES
    (@CatBev, 'BEV-TEA-001', 'Brooke Bond Red Label Tea', 'brooke-bond-red-label-500g','Rich and refreshing CTC tea blend.',                              'g',   '500 g',  215.00, 199.00,1, '/images/products/red-label-tea.jpg',  40, 6),
    (@CatBev, 'BEV-COF-001', 'Bru Instant Coffee',        'bru-instant-coffee-200g',  'South Indian-inspired instant coffee blend.',                      'g',   '200 g',  285.00, NULL,  0, '/images/products/bru-coffee.jpg',     30, 5),
    (@CatBev, 'BEV-JUI-001', 'Real Mango Juice',          'real-mango-juice-1l',       'Mixed-fruit mango juice drink with no added preservatives.',      'litre','1 Litre',110.00, 99.00, 0, '/images/products/mango-juice.jpg',    35, 6);

    -- Household Essentials
    INSERT INTO #AllSeedProducts VALUES
    (@CatHouse, 'HOU-DET-001', 'Surf Excel Easy Wash Detergent','surf-excel-easy-wash-1kg','Tough stain removal in less water.',                           'kg',  '1 kg',   175.00, 159.00,1, '/images/products/surf-excel.jpg',     50, 8),
    (@CatHouse, 'HOU-DIS-001', 'Vim Dishwash Bar',              'vim-dishwash-bar-250g',  'Effective grease remover for utensils.',                         'g',   '250 g',   40.00,  NULL,  0, '/images/products/vim-bar.jpg',        60, 10),
    (@CatHouse, 'HOU-SOP-001', 'Dettol Original Soap',          'dettol-original-soap-75g','Antibacterial soap for everyday hand hygiene.',                 'g',   '75 g',    45.00,  40.00, 0, '/images/products/dettol-soap.jpg',    80, 15),
    (@CatHouse, 'HOU-FLC-001', 'Floor Cleaner (Lemon)', 'floor-cleaner-1l',    'Antibacterial floor cleaning liquid with fresh lemon fragrance.',            'litre','1 Litre',120.00, 99.00, 0, '/images/products/floor-cleaner.jpg',40, 6),
    (@CatHouse, 'HOU-GBG-001', 'Garbage Bags (Medium)', 'garbage-bags-medium', 'Medium-size oxo-biodegradable garbage bags, pack of 30.',                    'pack','30 pcs', 85.00,  75.00, 0, '/images/products/garbage-bags.jpg', 50, 8);

    -- Upsert Products
    MERGE Products AS target
    USING #AllSeedProducts AS source
    ON target.SKU = source.SKU
    WHEN MATCHED THEN
        UPDATE SET
            CategoryId  = source.CategoryId,
            Name        = source.Name,
            Slug        = source.Slug,
            Description = source.Description,
            UnitCode    = source.UnitCode,
            UnitLabel   = source.UnitLabel,
            Price       = source.Price,
            OfferPrice  = source.OfferPrice,
            IsFeatured  = source.IsFeatured,
            ImagePath   = source.ImagePath,
            IsActive    = 1,
            UpdatedAt   = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (CategoryId, SKU, Name, Slug, Description, UnitCode, UnitLabel, Price, OfferPrice, IsFeatured, IsActive, ImagePath, CreatedAt, UpdatedAt)
        VALUES (source.CategoryId, source.SKU, source.Name, source.Slug, source.Description,
                source.UnitCode, source.UnitLabel, source.Price, source.OfferPrice,
                source.IsFeatured, 1, source.ImagePath, SYSUTCDATETIME(), SYSUTCDATETIME());

    -- Upsert Inventory
    MERGE Inventory AS target
    USING (
        SELECT p.Id AS ProductId, sp.Stock AS QuantityOnHand, sp.LowStock AS LowStockThreshold
        FROM #AllSeedProducts sp
        JOIN Products p ON p.SKU = sp.SKU
    ) AS source
    ON target.ProductId = source.ProductId
    WHEN NOT MATCHED THEN
        INSERT (ProductId, QuantityOnHand, LowStockThreshold, ReservedQuantity, UpdatedAt)
        VALUES (source.ProductId, source.QuantityOnHand, source.LowStockThreshold, 0, SYSUTCDATETIME());

    DROP TABLE #AllSeedProducts;
END
GO

-- =============================================================================
-- SECTION 6: SEED SERVICEABLE DELIVERY AREAS (BENGALURU DEMO DATA)
-- =============================================================================

MERGE DeliveryAreas AS target
USING (VALUES
    ('560001', 'Bengaluru – MG Road',          1, 'Today 6 – 9 PM', 100.00, 40.00, 500.00),
    ('560002', 'Bengaluru – Shivajinagar',     1, 'Today 6 – 9 PM', 100.00, 40.00, 500.00),
    ('560003', 'Bengaluru – Malleshwaram',     1, 'Same day 4 – 8 PM', NULL, 40.00, 500.00),
    ('560011', 'Bengaluru – Koramangala',      1, 'Same day 4 – 8 PM', 100.00, 30.00, 400.00),
    ('560034', 'Bengaluru – Jayanagar',        1, 'Tomorrow morning', 150.00, 50.00, 600.00),
    ('560038', 'Bengaluru – JP Nagar',         1, 'Tomorrow morning', NULL,   40.00, 500.00),
    ('560068', 'Bengaluru – Whitefield',       1, 'Tomorrow 8 – 11 AM', 200.00, 60.00, 700.00),
    ('560100', 'Bengaluru – Electronic City', 1, 'Tomorrow 8 – 11 AM', 200.00, 60.00, 700.00)
) AS source (PinCode, AreaName, IsActive, EstimatedDeliveryWindow, MinOrderValue, DeliveryFee, FreeDeliveryAbove)
ON target.PinCode = source.PinCode
WHEN MATCHED THEN
    UPDATE SET
        AreaName                = source.AreaName,
        IsActive                = source.IsActive,
        EstimatedDeliveryWindow = source.EstimatedDeliveryWindow,
        MinOrderValue           = source.MinOrderValue,
        DeliveryFee             = source.DeliveryFee,
        FreeDeliveryAbove       = source.FreeDeliveryAbove,
        UpdatedAt               = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (PinCode, AreaName, IsActive, EstimatedDeliveryWindow, MinOrderValue, DeliveryFee, FreeDeliveryAbove, CreatedAt, UpdatedAt)
    VALUES (source.PinCode, source.AreaName, source.IsActive, source.EstimatedDeliveryWindow, source.MinOrderValue, source.DeliveryFee, source.FreeDeliveryAbove, SYSUTCDATETIME(), SYSUTCDATETIME());
GO

-- =============================================================================
-- SECTION 7: DEPLOYMENT VERIFICATION SUMMARY
-- =============================================================================

SELECT 'Categories' AS [Entity], COUNT(*) AS [RecordCount] FROM Categories
UNION ALL
SELECT 'Products', COUNT(*) FROM Products
UNION ALL
SELECT 'Inventory', COUNT(*) FROM Inventory
UNION ALL
SELECT 'DeliveryAreas', COUNT(*) FROM DeliveryAreas
UNION ALL
SELECT 'StoreSettings', COUNT(*) FROM StoreSettings
UNION ALL
SELECT 'AspNetRoles', COUNT(*) FROM AspNetRoles;
GO

PRINT '=============================================================================';
PRINT 'GOLIS Mart Database deployed successfully for Azure SQL Database.';
PRINT '=============================================================================';
GO
