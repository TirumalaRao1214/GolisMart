-- =============================================================================
-- GOLIS Mart — 007_AddCoordinates.sql
-- Phase 5: Optional GPS coordinates on UserAddresses
--
-- Adds nullable Latitude / Longitude FLOAT columns.
-- Coordinates are optional metadata stored when the customer uses the
-- "Use My Current Location" feature.  They are NOT required to save an address.
--
-- Safe to rerun: guarded by column-existence checks.
-- Execute against: GOLISMartDb
-- =============================================================================

USE GOLISMartDb;
GO

-- Latitude  (-90 to 90)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'Latitude'
)
BEGIN
    ALTER TABLE UserAddresses ADD Latitude FLOAT NULL;
    PRINT 'Added UserAddresses.Latitude';
END
GO

-- Longitude  (-180 to 180)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('UserAddresses') AND name = 'Longitude'
)
BEGIN
    ALTER TABLE UserAddresses ADD Longitude FLOAT NULL;
    PRINT 'Added UserAddresses.Longitude';
END
GO

-- Optional check constraints
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_UserAddresses_Latitude'
      AND  parent_object_id = OBJECT_ID('UserAddresses')
)
BEGIN
    ALTER TABLE UserAddresses
        ADD CONSTRAINT CK_UserAddresses_Latitude CHECK (Latitude IS NULL OR (Latitude >= -90  AND Latitude <= 90));
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_UserAddresses_Longitude'
      AND  parent_object_id = OBJECT_ID('UserAddresses')
)
BEGIN
    ALTER TABLE UserAddresses
        ADD CONSTRAINT CK_UserAddresses_Longitude CHECK (Longitude IS NULL OR (Longitude >= -180 AND Longitude <= 180));
END
GO

PRINT '007_AddCoordinates migration complete.';
GO
