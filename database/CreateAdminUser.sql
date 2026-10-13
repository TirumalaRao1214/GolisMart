-- =============================================================================
-- GOLIS Mart — CreateAdminUser.sql
-- Creates the first administrator account.
--
-- HOW TO USE:
--   1. Replace ADMIN_EMAIL and ADMIN_DISPLAY_NAME below with real values.
--   2. Replace BCRYPT_HASH_HERE with a bcrypt hash of your chosen password.
--      Generate one with: https://bcrypt.online/ (or any bcrypt tool, cost factor 11)
--      OR use the GOLISMart API seed endpoint (see README).
--   3. Run this script against GOLISMartDb AFTER running 010_AdminAndSettings.sql.
--   4. Delete or archive this script after use — never commit credentials.
--
-- ALTERNATIVE: Use the bootstrap endpoint:
--   POST /api/v1/admin/bootstrap
--   Body: { "email": "...", "password": "...", "displayName": "...", "bootstrapSecret": "<value from appsettings>" }
--   This endpoint is only enabled when AdminBootstrap:Enabled = true in appsettings.
-- =============================================================================

USE GOLISMartDb;
GO

DECLARE @AdminEmail     NVARCHAR(256) = N'admin@golismart.local';
DECLARE @AdminName      NVARCHAR(200) = N'Store Admin';

-- Do NOT put real passwords in this file. Use the bootstrap endpoint instead.
-- If you must use SQL, replace the hash below with a proper bcrypt hash.
DECLARE @PasswordHash   NVARCHAR(MAX) = N'REPLACE_WITH_BCRYPT_HASH';

-- ── 1. Insert admin user if not already present ───────────────────────────────
DECLARE @UserId NVARCHAR(450) = CAST(NEWID() AS NVARCHAR(450));
DECLARE @NormalizedEmail NVARCHAR(256) = UPPER(@AdminEmail);

IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE NormalizedEmail = @NormalizedEmail)
BEGIN
    INSERT INTO AspNetUsers
        (Id, UserName, NormalizedUserName, Email, NormalizedEmail,
         EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
         PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
         LockoutEnabled, AccessFailedCount,
         DisplayName, IsActive, CreatedAt, UpdatedAt)
    VALUES
        (@UserId, @NormalizedEmail, @NormalizedEmail, @AdminEmail, @NormalizedEmail,
         1, @PasswordHash, CAST(NEWID() AS NVARCHAR(MAX)), CAST(NEWID() AS NVARCHAR(MAX)),
         NULL, 0, 0,
         1, 0,
         @AdminName, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
    PRINT 'Admin user created: ' + @AdminEmail;
END
ELSE
BEGIN
    -- Retrieve existing user id
    SELECT @UserId = Id FROM AspNetUsers WHERE NormalizedEmail = @NormalizedEmail;
    PRINT 'Admin user already exists: ' + @AdminEmail;
END

-- ── 2. Assign Admin role ──────────────────────────────────────────────────────
DECLARE @AdminRoleId NVARCHAR(450);
SELECT @AdminRoleId = Id FROM AspNetRoles WHERE NormalizedName = 'ADMIN';

IF @AdminRoleId IS NULL
BEGIN
    PRINT 'ERROR: Admin role not found. Run 010_AdminAndSettings.sql first.';
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @UserId AND RoleId = @AdminRoleId)
BEGIN
    INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @AdminRoleId);
    PRINT 'Admin role assigned to user.';
END
ELSE
    PRINT 'Admin role already assigned.';

PRINT 'Admin setup complete for: ' + @AdminEmail;
GO
