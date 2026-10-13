-- =============================================================================
-- GOLIS Mart — 008_DeliveryServiceability.sql
-- Phase 5: Delivery Serviceability
--
-- Creates the DeliveryAreas table that drives PIN-code-based delivery
-- availability checks.  Each row represents one serviceable area.
--
-- Columns:
--   Id                  – surrogate key
--   PinCode             – 6-digit Indian PIN code (unique per active record)
--   AreaName            – human-readable locality/district name
--   IsActive            – soft on/off switch; inactive areas report unavailable
--   EstimatedDeliveryWindow – optional free text, e.g. "Today 6 – 9 PM"
--   MinOrderValue       – minimum cart subtotal for delivery (NULL = no minimum)
--   DeliveryFee         – fee charged for delivery (NULL = use application default)
--   FreeDeliveryAbove   – subtotal threshold above which delivery is free
--                         (NULL = use application default)
--   CreatedAt / UpdatedAt
--
-- Safe to rerun: all DDL is guarded by existence checks.
-- Execute against: GOLISMartDb
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- 1. DeliveryAreas table
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE  object_id = OBJECT_ID(N'DeliveryAreas') AND type = 'U'
)
BEGIN
    CREATE TABLE DeliveryAreas (
        Id                      INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
        PinCode                 CHAR(6)       NOT NULL,
        AreaName                NVARCHAR(200) NOT NULL,
        IsActive                BIT           NOT NULL DEFAULT 1,
        EstimatedDeliveryWindow NVARCHAR(100) NULL,
        MinOrderValue           DECIMAL(10,2) NULL,
        DeliveryFee             DECIMAL(10,2) NULL,
        FreeDeliveryAbove       DECIMAL(10,2) NULL,
        CreatedAt               DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt               DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
    PRINT 'Created DeliveryAreas table.';
END
GO

-- ---------------------------------------------------------------------------
-- 2. PIN code format constraint (6 digits)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_DeliveryAreas_PinCode'
      AND  parent_object_id = OBJECT_ID('DeliveryAreas')
)
BEGIN
    ALTER TABLE DeliveryAreas
        ADD CONSTRAINT CK_DeliveryAreas_PinCode
            CHECK (PinCode LIKE '[0-9][0-9][0-9][0-9][0-9][0-9]');
    PRINT 'Added CK_DeliveryAreas_PinCode constraint.';
END
GO

-- ---------------------------------------------------------------------------
-- 3. Unique active PIN code index
--    Allows the same PIN to exist as inactive (historical) but enforces
--    uniqueness among active records.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'UQ_DeliveryAreas_PinCode_Active'
      AND  object_id = OBJECT_ID('DeliveryAreas')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_DeliveryAreas_PinCode_Active
        ON DeliveryAreas (PinCode)
        WHERE IsActive = 1;
    PRINT 'Created UQ_DeliveryAreas_PinCode_Active index.';
END
GO

-- ---------------------------------------------------------------------------
-- 4. Lookup index for IsActive
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_DeliveryAreas_IsActive'
      AND  object_id = OBJECT_ID('DeliveryAreas')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DeliveryAreas_IsActive
        ON DeliveryAreas (IsActive)
        INCLUDE (PinCode, AreaName, EstimatedDeliveryWindow, MinOrderValue, DeliveryFee, FreeDeliveryAbove);
    PRINT 'Created IX_DeliveryAreas_IsActive index.';
END
GO

-- ---------------------------------------------------------------------------
-- 5. DEMO seed data (clearly labelled — for local development only)
--    These are fictional PIN codes for the Bengaluru area used as demo data.
--    Replace with real serviceability data before going to production.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM DeliveryAreas WHERE PinCode = '560001')
BEGIN
    INSERT INTO DeliveryAreas (PinCode, AreaName, IsActive, EstimatedDeliveryWindow, MinOrderValue, DeliveryFee, FreeDeliveryAbove)
    VALUES
        ('560001', 'Bengaluru – MG Road (DEMO)',          1, 'Today 6 – 9 PM', 100.00, 40.00, 500.00),
        ('560002', 'Bengaluru – Shivajinagar (DEMO)',     1, 'Today 6 – 9 PM', 100.00, 40.00, 500.00),
        ('560003', 'Bengaluru – Malleshwaram (DEMO)',     1, 'Same day 4 – 8 PM', NULL, 40.00, 500.00),
        ('560011', 'Bengaluru – Koramangala (DEMO)',      1, 'Same day 4 – 8 PM', 100.00, 30.00, 400.00),
        ('560034', 'Bengaluru – Jayanagar (DEMO)',        1, 'Tomorrow morning', 150.00, 50.00, 600.00),
        ('560038', 'Bengaluru – JP Nagar (DEMO)',         1, 'Tomorrow morning', NULL,   40.00, 500.00),
        ('560068', 'Bengaluru – Whitefield (DEMO)',       1, 'Tomorrow 8 – 11 AM', 200.00, 60.00, 700.00),
        ('560100', 'Bengaluru – Electronic City (DEMO)', 1, 'Tomorrow 8 – 11 AM', 200.00, 60.00, 700.00);
    PRINT 'Inserted demo delivery areas (DEMO data — replace before production).';
END
GO

PRINT '008_DeliveryServiceability migration complete.';
GO
