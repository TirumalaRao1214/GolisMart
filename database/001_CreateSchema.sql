-- =============================================================================
-- GOLIS Mart — 001_CreateSchema.sql
-- Creates all tables for the GOLISMartDb database.
-- Safe to rerun: uses IF NOT EXISTS / IF OBJECT_ID checks throughout.
-- Execute against: GOLISMartDb (run 000_CreateDatabase.sql first)
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- 1. ASP.NET Core Identity tables (Users + credentials/roles)
-- ---------------------------------------------------------------------------
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUsers')
BEGIN
    CREATE TABLE AspNetUsers (
        Id                          NVARCHAR(450)   NOT NULL PRIMARY KEY,
        UserName                    NVARCHAR(256)   NULL,
        NormalizedUserName          NVARCHAR(256)   NULL,
        Email                       NVARCHAR(256)   NULL,
        NormalizedEmail             NVARCHAR(256)   NULL,
        EmailConfirmed              BIT             NOT NULL DEFAULT 0,
        PasswordHash                NVARCHAR(MAX)   NULL,    -- bcrypt hash only
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetUserClaims')
BEGIN
    CREATE TABLE AspNetUserClaims (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
        UserId      NVARCHAR(450)   NOT NULL,
        ClaimType   NVARCHAR(MAX)   NULL,
        ClaimValue  NVARCHAR(MAX)   NULL,
        CONSTRAINT FK_UserClaims_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
END
GO

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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AspNetRoleClaims')
BEGIN
    CREATE TABLE AspNetRoleClaims (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
        RoleId      NVARCHAR(450)   NOT NULL,
        ClaimType   NVARCHAR(MAX)   NULL,
        ClaimValue  NVARCHAR(MAX)   NULL,
        CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES AspNetRoles(Id) ON DELETE CASCADE
    );
END
GO

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

-- ---------------------------------------------------------------------------
-- 2. UserAddresses
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserAddresses')
BEGIN
    CREATE TABLE UserAddresses (
        Id              INT             NOT NULL IDENTITY PRIMARY KEY,
        UserId          NVARCHAR(450)   NOT NULL,
        AddressLine1    NVARCHAR(200)   NOT NULL DEFAULT '',  -- legacy; superseded by Phase-4 columns
        AddressLine2    NVARCHAR(200)   NULL,
        City            NVARCHAR(100)   NOT NULL,
        State           NVARCHAR(100)   NOT NULL,
        Pincode         NVARCHAR(10)    NOT NULL,
        IsDefault       BIT             NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_UserAddresses_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
END
GO

-- ---------------------------------------------------------------------------
-- 3. Categories
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE Categories (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
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

-- ---------------------------------------------------------------------------
-- 4. Products
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE Products (
        Id              INT             NOT NULL IDENTITY PRIMARY KEY,
        CategoryId      INT             NOT NULL,
        SKU             NVARCHAR(50)    NOT NULL,
        Name            NVARCHAR(200)   NOT NULL,
        Slug            NVARCHAR(200)   NOT NULL,
        Description     NVARCHAR(2000)  NULL,
        UnitCode        NVARCHAR(20)    NOT NULL,   -- e.g. kg, g, litre, pcs, pack
        UnitLabel       NVARCHAR(50)    NOT NULL,   -- e.g. "1 kg", "500 g", "1 Litre"
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

-- ---------------------------------------------------------------------------
-- 5. ProductImages
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ProductImages')
BEGIN
    CREATE TABLE ProductImages (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
        ProductId   INT             NOT NULL,
        ImagePath   NVARCHAR(500)   NOT NULL,   -- relative path or CDN URL — no binaries
        AltText     NVARCHAR(200)   NULL,
        SortOrder   INT             NOT NULL DEFAULT 0,
        IsPrimary   BIT             NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ProductImages_Products FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
    );
END
GO

-- ---------------------------------------------------------------------------
-- 6. Inventory
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Inventory')
BEGIN
    CREATE TABLE Inventory (
        Id                  INT             NOT NULL IDENTITY PRIMARY KEY,
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

-- ---------------------------------------------------------------------------
-- 7. Carts
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Carts')
BEGIN
    CREATE TABLE Carts (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
        UserId      NVARCHAR(450)   NULL,           -- NULL for anonymous/session carts
        SessionKey  NVARCHAR(100)   NULL,
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        ExpiresAt   DATETIME2(7)    NULL
    );
END
GO

-- ---------------------------------------------------------------------------
-- 8. CartItems
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CartItems')
BEGIN
    CREATE TABLE CartItems (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
        CartId      INT             NOT NULL,
        ProductId   INT             NOT NULL,
        Quantity    DECIMAL(10,3)   NOT NULL DEFAULT 1,
        UnitPrice   DECIMAL(10,2)   NOT NULL,       -- snapshot at add time
        CreatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_CartItems_CartProduct UNIQUE (CartId, ProductId),
        CONSTRAINT FK_CartItems_Carts    FOREIGN KEY (CartId)    REFERENCES Carts(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CartItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT CK_CartItems_Qty CHECK (Quantity > 0)
    );
END
GO

-- ---------------------------------------------------------------------------
-- 9. DeliveryZones
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryZones')
BEGIN
    CREATE TABLE DeliveryZones (
        Id              INT             NOT NULL IDENTITY PRIMARY KEY,
        Name            NVARCHAR(100)   NOT NULL,
        Pincodes        NVARCHAR(MAX)   NOT NULL,   -- comma-separated or JSON array
        DeliveryFee     DECIMAL(10,2)   NOT NULL DEFAULT 0,
        MinOrderValue   DECIMAL(10,2)   NOT NULL DEFAULT 0,
        IsActive        BIT             NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

-- ---------------------------------------------------------------------------
-- 10. DeliverySlots
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliverySlots')
BEGIN
    CREATE TABLE DeliverySlots (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
        ZoneId      INT             NOT NULL,
        SlotLabel   NVARCHAR(50)    NOT NULL,   -- e.g. "9 AM – 12 PM"
        StartTime   TIME            NOT NULL,
        EndTime     TIME            NOT NULL,
        Capacity    INT             NOT NULL DEFAULT 10,
        IsActive    BIT             NOT NULL DEFAULT 1,
        CONSTRAINT FK_DeliverySlots_Zones FOREIGN KEY (ZoneId) REFERENCES DeliveryZones(Id) ON DELETE CASCADE
    );
END
GO

-- ---------------------------------------------------------------------------
-- 11. Orders
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Orders')
BEGIN
    CREATE TABLE Orders (
        Id                  INT             NOT NULL IDENTITY PRIMARY KEY,
        OrderNumber         NVARCHAR(30)    NOT NULL,
        UserId              NVARCHAR(450)   NOT NULL,
        -- Snapshot of delivery address at order time
        ShipToAddressLine1  NVARCHAR(200)   NOT NULL,
        ShipToAddressLine2  NVARCHAR(200)   NULL,
        ShipToCity          NVARCHAR(100)   NOT NULL,
        ShipToState         NVARCHAR(100)   NOT NULL,
        ShipToPincode       NVARCHAR(10)    NOT NULL,
        -- Financials snapshot
        SubTotal            DECIMAL(10,2)   NOT NULL,
        DiscountAmount      DECIMAL(10,2)   NOT NULL DEFAULT 0,
        DeliveryFee         DECIMAL(10,2)   NOT NULL DEFAULT 0,
        TotalAmount         DECIMAL(10,2)   NOT NULL,
        -- Coupon snapshot
        CouponCode          NVARCHAR(50)    NULL,
        CouponDiscount      DECIMAL(10,2)   NOT NULL DEFAULT 0,
        -- Status
        Status              NVARCHAR(30)    NOT NULL DEFAULT 'Pending',
        DeliverySlotId      INT             NULL,
        ScheduledDelivery   DATETIME2(7)    NULL,
        Notes               NVARCHAR(1000)  NULL,
        CreatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        RowVersion          ROWVERSION,
        CONSTRAINT UQ_Orders_OrderNumber UNIQUE (OrderNumber),
        CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id),
        CONSTRAINT FK_Orders_DeliverySlots FOREIGN KEY (DeliverySlotId) REFERENCES DeliverySlots(Id),
        CONSTRAINT CK_Orders_Status CHECK (Status IN ('Pending','Confirmed','Processing','OutForDelivery','Delivered','Cancelled','Refunded')),
        CONSTRAINT CK_Orders_Total CHECK (TotalAmount >= 0)
    );
END
GO

-- ---------------------------------------------------------------------------
-- 12. OrderItems
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderItems')
BEGIN
    CREATE TABLE OrderItems (
        Id              INT             NOT NULL IDENTITY PRIMARY KEY,
        OrderId         INT             NOT NULL,
        ProductId       INT             NOT NULL,
        -- Purchase-time snapshots (immutable after creation)
        ProductName     NVARCHAR(200)   NOT NULL,
        ProductSKU      NVARCHAR(50)    NOT NULL,
        UnitLabel       NVARCHAR(50)    NOT NULL,
        Quantity        DECIMAL(10,3)   NOT NULL,
        UnitPrice       DECIMAL(10,2)   NOT NULL,
        OfferPrice      DECIMAL(10,2)   NULL,
        EffectivePrice  DECIMAL(10,2)   NOT NULL,   -- price actually charged
        LineTotal       DECIMAL(10,2)   NOT NULL,
        CONSTRAINT FK_OrderItems_Orders   FOREIGN KEY (OrderId)   REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT CK_OrderItems_Qty   CHECK (Quantity > 0),
        CONSTRAINT CK_OrderItems_Price CHECK (EffectivePrice > 0)
    );
END
GO

-- ---------------------------------------------------------------------------
-- 13. Payments
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE Payments (
        Id                  INT             NOT NULL IDENTITY PRIMARY KEY,
        OrderId             INT             NOT NULL,
        GatewayName         NVARCHAR(50)    NOT NULL,   -- e.g. Razorpay, Stripe
        GatewayPaymentId    NVARCHAR(200)   NULL,
        GatewayOrderId      NVARCHAR(200)   NULL,
        Amount              DECIMAL(10,2)   NOT NULL,
        Currency            NVARCHAR(5)     NOT NULL DEFAULT 'INR',
        Status              NVARCHAR(30)    NOT NULL DEFAULT 'Initiated',
        Method              NVARCHAR(50)    NULL,       -- UPI, Card, NetBanking, Wallet
        -- NEVER store card number or CVV
        FailureReason       NVARCHAR(500)   NULL,
        CreatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Payments_Status CHECK (Status IN ('Initiated','Pending','Captured','Failed','Refunded','PartialRefund'))
    );
END
GO

-- ---------------------------------------------------------------------------
-- 14. PaymentWebhookEvents
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PaymentWebhookEvents')
BEGIN
    CREATE TABLE PaymentWebhookEvents (
        Id              INT             NOT NULL IDENTITY PRIMARY KEY,
        PaymentId       INT             NULL,
        GatewayName     NVARCHAR(50)    NOT NULL,
        EventType       NVARCHAR(100)   NOT NULL,
        RawPayload      NVARCHAR(MAX)   NOT NULL,   -- JSON from gateway
        ProcessedAt     DATETIME2(7)    NULL,
        ProcessingError NVARCHAR(1000)  NULL,
        ReceivedAt      DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WebhookEvents_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id) ON DELETE SET NULL
    );
END
GO

-- ---------------------------------------------------------------------------
-- 15a. Coupons
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Coupons')
BEGIN
    CREATE TABLE Coupons (
        Id                  INT             NOT NULL IDENTITY PRIMARY KEY,
        Code                NVARCHAR(50)    NOT NULL,
        Description         NVARCHAR(300)   NULL,
        DiscountType        NVARCHAR(20)    NOT NULL,   -- 'Percentage' or 'Fixed'
        DiscountValue       DECIMAL(10,2)   NOT NULL,
        MinOrderValue       DECIMAL(10,2)   NOT NULL DEFAULT 0,
        MaxDiscountAmount   DECIMAL(10,2)   NULL,       -- cap for percentage coupons
        UsageLimit          INT             NULL,       -- NULL = unlimited
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

-- ---------------------------------------------------------------------------
-- 15b. CouponRedemptions
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CouponRedemptions')
BEGIN
    CREATE TABLE CouponRedemptions (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
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

-- ---------------------------------------------------------------------------
-- 16. OrderStatusHistory
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OrderStatusHistory')
BEGIN
    CREATE TABLE OrderStatusHistory (
        Id          INT             NOT NULL IDENTITY PRIMARY KEY,
        OrderId     INT             NOT NULL,
        Status      NVARCHAR(30)    NOT NULL,
        Notes       NVARCHAR(500)   NULL,
        ChangedBy   NVARCHAR(450)   NULL,   -- UserId who made the change
        ChangedAt   DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
    );
END
GO

-- ---------------------------------------------------------------------------
-- 17. InventoryTransactions  (audit trail — append only)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'InventoryTransactions')
BEGIN
    CREATE TABLE InventoryTransactions (
        Id              INT             NOT NULL IDENTITY PRIMARY KEY,
        ProductId       INT             NOT NULL,
        TransactionType NVARCHAR(30)    NOT NULL,   -- Purchase, Sale, Adjustment, Return, Reserved, Released
        QuantityChange  DECIMAL(10,3)   NOT NULL,   -- positive = in, negative = out
        QuantityAfter   DECIMAL(10,3)   NOT NULL,   -- running balance at time of transaction
        ReferenceId     INT             NULL,        -- OrderId or PurchaseOrderId
        ReferenceType   NVARCHAR(50)    NULL,
        Notes           NVARCHAR(500)   NULL,
        CreatedBy       NVARCHAR(450)   NULL,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_InventoryTx_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT CK_InventoryTx_Type CHECK (TransactionType IN ('Purchase','Sale','Adjustment','Return','Reserved','Released','Wastage'))
    );
END
GO

PRINT 'Schema creation complete. All 17 logical entities created (plus ASP.NET Identity supporting tables).';
GO
