-- =============================================================================
-- GOLIS Mart — 011_DeliveryManagement.sql
-- Phase 9: Delivery Management & Customer Notifications
--
-- Creates / modifies:
--   1.  Orders — widen Status check constraint (add PickedUp, DeliveryFailed)
--   2.  Seed DeliveryPersonnel role in AspNetRoles
--   3.  DeliveryPersonnel table
--   4.  DeliveryAssignments table  (tracks current + history of assignments)
--   5.  NotificationEvents table   (audit + idempotency for all notification types)
--   6.  Performance indexes
--
-- Safe to rerun: all DDL is guarded by existence checks.
-- Execute against: GOLISMartDb
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- 1. Orders — widen Status check constraint to include delivery statuses
--    PickedUp        : delivery person collected the order from the store
--    DeliveryFailed  : delivery attempt failed (reason captured in history notes)
-- ---------------------------------------------------------------------------
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_Orders_Status'
      AND  parent_object_id = OBJECT_ID('Orders')
)
BEGIN
    ALTER TABLE Orders DROP CONSTRAINT CK_Orders_Status;
    PRINT 'Dropped old CK_Orders_Status.';
END
GO

-- The widened constraint also keeps all original statuses
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_Orders_Status'
      AND  parent_object_id = OBJECT_ID('Orders')
)
BEGIN
    ALTER TABLE Orders
        ADD CONSTRAINT CK_Orders_Status
            CHECK (Status IN (
                'Pending', 'Confirmed', 'Processing',
                'PickedUp', 'OutForDelivery', 'Delivered',
                'DeliveryFailed', 'Cancelled', 'Refunded'
            ));
    PRINT 'Added widened CK_Orders_Status constraint.';
END
GO

-- ---------------------------------------------------------------------------
-- 2. Seed the 'DeliveryPersonnel' role
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = 'DELIVERYPERSONNEL')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'DeliveryPersonnel', 'DELIVERYPERSONNEL', CAST(NEWID() AS NVARCHAR(MAX)));
    PRINT 'DeliveryPersonnel role created.';
END
ELSE
    PRINT 'DeliveryPersonnel role already exists — skipped.';
GO

-- ---------------------------------------------------------------------------
-- 3. DeliveryPersonnel
--    Maps an authenticated user to a delivery worker profile.
--    One row per delivery employee; linked to AspNetUsers for authentication.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryPersonnel')
BEGIN
    CREATE TABLE DeliveryPersonnel (
        Id              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId          NVARCHAR(450)   NULL,        -- FK to AspNetUsers; NULL = no app account yet
        FullName        NVARCHAR(200)   NOT NULL,
        ContactNumber   NVARCHAR(20)    NOT NULL,
        IsActive        BIT             NOT NULL DEFAULT 1,
        Notes           NVARCHAR(500)   NULL,
        CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_DeliveryPersonnel_UserId
            UNIQUE (UserId),                         -- one person : one account
        CONSTRAINT FK_DeliveryPersonnel_Users
            FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    );
    PRINT 'Created DeliveryPersonnel table.';
END
GO

-- ---------------------------------------------------------------------------
-- 4. DeliveryAssignments
--    Tracks which delivery person is assigned to which order.
--    One ACTIVE assignment per order at any given time (enforced by
--    filtered unique index). History is preserved (IsActive = 0).
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DeliveryAssignments')
BEGIN
    CREATE TABLE DeliveryAssignments (
        Id                  INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,
        OrderId             INT             NOT NULL,
        DeliveryPersonnelId INT             NOT NULL,
        AssignedBy          NVARCHAR(450)   NULL,    -- admin UserId
        AssignedAt          DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        UnassignedAt        DATETIME2(7)    NULL,
        UnassignedBy        NVARCHAR(450)   NULL,
        UnassignedReason    NVARCHAR(300)   NULL,
        IsActive            BIT             NOT NULL DEFAULT 1,   -- 0 = historical / superseded
        CONSTRAINT FK_DeliveryAssignments_Orders
            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_DeliveryAssignments_Personnel
            FOREIGN KEY (DeliveryPersonnelId) REFERENCES DeliveryPersonnel(Id)
    );
    PRINT 'Created DeliveryAssignments table.';
END
GO

-- Only one active assignment per order at a time
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'UQ_DeliveryAssignments_ActiveOrder'
      AND  object_id = OBJECT_ID('DeliveryAssignments')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_DeliveryAssignments_ActiveOrder
        ON DeliveryAssignments (OrderId)
        WHERE IsActive = 1;
    PRINT 'Created UQ_DeliveryAssignments_ActiveOrder filtered index.';
END
GO

-- ---------------------------------------------------------------------------
-- 5. NotificationEvents
--    Idempotent log of every notification that should be / has been sent.
--    Failures here must never roll back order or delivery status changes.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NotificationEvents')
BEGIN
    CREATE TABLE NotificationEvents (
        Id              BIGINT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
        -- Subject
        OrderId         INT             NOT NULL,
        EventType       NVARCHAR(60)    NOT NULL,   -- e.g. 'OrderConfirmed', 'PickedUp', 'Delivered'
        -- De-duplication key: same event on same order fires at most once
        IdempotencyKey  NVARCHAR(100)   NOT NULL,
        -- Recipient snapshot (stored at fire time; no FK to allow account deletion)
        RecipientUserId NVARCHAR(450)   NULL,
        RecipientEmail  NVARCHAR(256)   NULL,
        RecipientPhone  NVARCHAR(30)    NULL,
        -- Channel + result
        Channel         NVARCHAR(30)    NOT NULL DEFAULT 'InApp',  -- InApp | Email | WhatsApp
        Status          NVARCHAR(20)    NOT NULL DEFAULT 'Queued', -- Queued | Sent | Failed | Skipped
        FailureReason   NVARCHAR(1000)  NULL,
        -- Timestamps
        QueuedAt        DATETIME2(7)    NOT NULL DEFAULT SYSUTCDATETIME(),
        SentAt          DATETIME2(7)    NULL,
        CONSTRAINT UQ_NotificationEvents_Idempotency
            UNIQUE (IdempotencyKey),
        CONSTRAINT FK_NotificationEvents_Orders
            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT CK_NotificationEvents_EventType
            CHECK (EventType IN (
                'OrderConfirmed', 'PreparationStarted', 'PickedUp',
                'OutForDelivery', 'Delivered', 'DeliveryFailed', 'Rescheduled'
            )),
        CONSTRAINT CK_NotificationEvents_Channel
            CHECK (Channel IN ('InApp', 'Email', 'WhatsApp')),
        CONSTRAINT CK_NotificationEvents_Status
            CHECK (Status IN ('Queued', 'Sent', 'Failed', 'Skipped'))
    );
    PRINT 'Created NotificationEvents table.';
END
GO

-- ---------------------------------------------------------------------------
-- 6. Indexes
-- ---------------------------------------------------------------------------

-- DeliveryPersonnel — active lookup
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_DeliveryPersonnel_IsActive'
      AND  object_id = OBJECT_ID('DeliveryPersonnel')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DeliveryPersonnel_IsActive
        ON DeliveryPersonnel (IsActive)
        INCLUDE (FullName, ContactNumber, UserId);
    PRINT 'Created IX_DeliveryPersonnel_IsActive index.';
END
GO

-- DeliveryPersonnel — UserId lookup (for login → profile resolution)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_DeliveryPersonnel_UserId'
      AND  object_id = OBJECT_ID('DeliveryPersonnel')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DeliveryPersonnel_UserId
        ON DeliveryPersonnel (UserId)
        INCLUDE (FullName, IsActive);
    PRINT 'Created IX_DeliveryPersonnel_UserId index.';
END
GO

-- DeliveryAssignments — per-personnel active workload
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_DeliveryAssignments_Personnel_Active'
      AND  object_id = OBJECT_ID('DeliveryAssignments')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_DeliveryAssignments_Personnel_Active
        ON DeliveryAssignments (DeliveryPersonnelId, IsActive)
        INCLUDE (OrderId, AssignedAt);
    PRINT 'Created IX_DeliveryAssignments_Personnel_Active index.';
END
GO

-- NotificationEvents — order event lookup
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_NotificationEvents_OrderId_EventType'
      AND  object_id = OBJECT_ID('NotificationEvents')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_NotificationEvents_OrderId_EventType
        ON NotificationEvents (OrderId, EventType)
        INCLUDE (Status, QueuedAt);
    PRINT 'Created IX_NotificationEvents_OrderId_EventType index.';
END
GO

-- NotificationEvents — recipient inbox (in-app notifications)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_NotificationEvents_RecipientUserId'
      AND  object_id = OBJECT_ID('NotificationEvents')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_NotificationEvents_RecipientUserId
        ON NotificationEvents (RecipientUserId, QueuedAt DESC)
        WHERE RecipientUserId IS NOT NULL
        INCLUDE (EventType, Status, OrderId);
    PRINT 'Created IX_NotificationEvents_RecipientUserId index.';
END
GO

PRINT '011_DeliveryManagement migration complete.';
GO
