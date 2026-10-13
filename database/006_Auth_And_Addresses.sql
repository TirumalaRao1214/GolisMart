-- =============================================================================
-- GOLIS Mart — 006_Auth_And_Addresses.sql
-- Phase 4: Authentication & Customer Address Management
--
-- Adds / updates:
--   1.  Unique + normalised-email index on AspNetUsers
--   2.  Alters UserAddresses to add Phase-4 required columns
--   3.  New indexes for customer address lookups
--
-- Safe to rerun: all DDL is guarded by IF NOT EXISTS / column-existence checks.
-- Execute against: GOLISMartDb
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- 1. AspNetUsers — ensure NormalizedEmail unique index
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'UQ_AspNetUsers_NormalizedEmail'
      AND  object_id = OBJECT_ID('AspNetUsers')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_AspNetUsers_NormalizedEmail
        ON AspNetUsers (NormalizedEmail)
        WHERE NormalizedEmail IS NOT NULL;
    PRINT 'Created UQ_AspNetUsers_NormalizedEmail index.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_AspNetUsers_NormalizedUserName'
      AND  object_id = OBJECT_ID('AspNetUsers')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_AspNetUsers_NormalizedUserName
        ON AspNetUsers (NormalizedUserName)
        WHERE NormalizedUserName IS NOT NULL;
    PRINT 'Created IX_AspNetUsers_NormalizedUserName index.';
END
GO

-- ---------------------------------------------------------------------------
-- 2. UserAddresses — add Phase-4 columns if they do not already exist
-- ---------------------------------------------------------------------------

-- RecipientName
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'RecipientName'
)
BEGIN
    ALTER TABLE UserAddresses ADD RecipientName NVARCHAR(200) NOT NULL DEFAULT '';
    PRINT 'Added UserAddresses.RecipientName';
END
GO

-- RecipientMobile
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'RecipientMobile'
)
BEGIN
    ALTER TABLE UserAddresses ADD RecipientMobile NVARCHAR(15) NOT NULL DEFAULT '';
    PRINT 'Added UserAddresses.RecipientMobile';
END
GO

-- AddressLabel  (Home / Work / Other)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'AddressLabel'
)
BEGIN
    ALTER TABLE UserAddresses ADD AddressLabel NVARCHAR(20) NOT NULL DEFAULT 'Home';
    PRINT 'Added UserAddresses.AddressLabel';
END
GO

-- HouseNumber
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'HouseNumber'
)
BEGIN
    ALTER TABLE UserAddresses ADD HouseNumber NVARCHAR(100) NOT NULL DEFAULT '';
    PRINT 'Added UserAddresses.HouseNumber';
END
GO

-- BuildingStreet
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'BuildingStreet'
)
BEGIN
    ALTER TABLE UserAddresses ADD BuildingStreet NVARCHAR(200) NOT NULL DEFAULT '';
    PRINT 'Added UserAddresses.BuildingStreet';
END
GO

-- Locality (Area)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'Locality'
)
BEGIN
    ALTER TABLE UserAddresses ADD Locality NVARCHAR(200) NOT NULL DEFAULT '';
    PRINT 'Added UserAddresses.Locality';
END
GO

-- Landmark (optional)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'Landmark'
)
BEGIN
    ALTER TABLE UserAddresses ADD Landmark NVARCHAR(200) NULL;
    PRINT 'Added UserAddresses.Landmark';
END
GO

-- DeliveryInstructions (optional)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'DeliveryInstructions'
)
BEGIN
    ALTER TABLE UserAddresses ADD DeliveryInstructions NVARCHAR(500) NULL;
    PRINT 'Added UserAddresses.DeliveryInstructions';
END
GO

-- IsDeleted (soft-delete — never physically remove addresses referenced by future orders)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'IsDeleted'
)
BEGIN
    ALTER TABLE UserAddresses ADD IsDeleted BIT NOT NULL DEFAULT 0;
    PRINT 'Added UserAddresses.IsDeleted';
END
GO

-- AddressLabel check constraint
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_UserAddresses_Label'
      AND  parent_object_id = OBJECT_ID('UserAddresses')
)
BEGIN
    ALTER TABLE UserAddresses
        ADD CONSTRAINT CK_UserAddresses_Label
            CHECK (AddressLabel IN ('Home', 'Work', 'Other'));
    PRINT 'Added CK_UserAddresses_Label constraint.';
END
GO

-- RecipientMobile format constraint  (+91XXXXXXXXXX normalised form)
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_UserAddresses_Mobile'
      AND  parent_object_id = OBJECT_ID('UserAddresses')
)
BEGIN
    ALTER TABLE UserAddresses
        ADD CONSTRAINT CK_UserAddresses_Mobile
            CHECK (RecipientMobile = '' OR RecipientMobile LIKE '+91[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]');
    PRINT 'Added CK_UserAddresses_Mobile constraint.';
END
GO

-- Pincode format constraint  (6 digits)
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_UserAddresses_Pincode'
      AND  parent_object_id = OBJECT_ID('UserAddresses')
)
BEGIN
    ALTER TABLE UserAddresses
        ADD CONSTRAINT CK_UserAddresses_Pincode
            CHECK (Pincode LIKE '[0-9][0-9][0-9][0-9][0-9][0-9]');
    PRINT 'Added CK_UserAddresses_Pincode constraint.';
END
GO

-- ---------------------------------------------------------------------------
-- 3. UserAddresses — lookup indexes
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_UserAddresses_UserId_IsDeleted'
      AND  object_id = OBJECT_ID('UserAddresses')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_UserAddresses_UserId_IsDeleted
        ON UserAddresses (UserId, IsDeleted)
        INCLUDE (IsDefault, AddressLabel);
    PRINT 'Created IX_UserAddresses_UserId_IsDeleted index.';
END
GO

-- ---------------------------------------------------------------------------
-- 4. AspNetUsers — phone-number constraint (Indian mobile, stored normalised)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_AspNetUsers_PhoneNumber'
      AND  parent_object_id = OBJECT_ID('AspNetUsers')
)
BEGIN
    ALTER TABLE AspNetUsers
        ADD CONSTRAINT CK_AspNetUsers_PhoneNumber
            CHECK (PhoneNumber IS NULL OR PhoneNumber LIKE '+91[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]');
    PRINT 'Added CK_AspNetUsers_PhoneNumber constraint.';
END
GO

-- ---------------------------------------------------------------------------
-- 5. UserAddresses — fix AddressLine1 NOT NULL with no default
--    The original 001_CreateSchema.sql created this column as NOT NULL without
--    a default value. Phase-4 inserts don't supply AddressLine1, causing a
--    "Cannot insert NULL" error. Add a default empty string to allow inserts
--    that omit this legacy column.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE  name = 'DF_UserAddresses_AddressLine1'
      AND  parent_object_id = OBJECT_ID('UserAddresses')
)
BEGIN
    ALTER TABLE UserAddresses
        ADD CONSTRAINT DF_UserAddresses_AddressLine1 DEFAULT '' FOR AddressLine1;
    PRINT 'Added DF_UserAddresses_AddressLine1 default constraint.';
END
GO

PRINT '006_Auth_And_Addresses migration complete.';
GO
