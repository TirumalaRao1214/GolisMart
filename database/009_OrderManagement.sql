-- =============================================================================
-- GOLIS Mart — 009_OrderManagement.sql
-- Phase 6: Order Management & Order History
--
-- The Orders, OrderItems, OrderStatusHistory, Payments and InventoryTransactions
-- tables were already created by 001_CreateSchema.sql.  This migration:
--   1. Adds PaymentMethod and PaymentStatus columns to Orders (for COD UX).
--   2. Adds a client-side idempotency key column to Orders.
--   3. Creates performance indexes for customer order lookups.
--   4. Creates an index for order status history queries.
--   5. Creates an index for inventory deduction queries.
--
-- Safe to rerun: all DDL is guarded by existence checks.
-- Execute against: GOLISMartDb
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- 1. Orders — add PaymentMethod column
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('Orders') AND name = 'PaymentMethod'
)
BEGIN
    ALTER TABLE Orders ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'COD';
    PRINT 'Added Orders.PaymentMethod';
END
GO

-- ---------------------------------------------------------------------------
-- 2. Orders — add PaymentStatus column
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('Orders') AND name = 'PaymentStatus'
)
BEGIN
    ALTER TABLE Orders ADD PaymentStatus NVARCHAR(30) NOT NULL DEFAULT 'Pending';
    PRINT 'Added Orders.PaymentStatus';
END
GO

-- PaymentStatus check constraint
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_Orders_PaymentStatus'
      AND  parent_object_id = OBJECT_ID('Orders')
)
BEGIN
    ALTER TABLE Orders
        ADD CONSTRAINT CK_Orders_PaymentStatus
            CHECK (PaymentStatus IN ('Pending','Paid','Failed','Refunded'));
    PRINT 'Added CK_Orders_PaymentStatus constraint.';
END
GO

-- ---------------------------------------------------------------------------
-- 3. Orders — add idempotency key (prevents duplicate submissions)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('Orders') AND name = 'IdempotencyKey'
)
BEGIN
    ALTER TABLE Orders ADD IdempotencyKey NVARCHAR(100) NULL;
    PRINT 'Added Orders.IdempotencyKey';
END
GO

-- Unique index on IdempotencyKey (partial — only when not null)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'UQ_Orders_IdempotencyKey'
      AND  object_id = OBJECT_ID('Orders')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Orders_IdempotencyKey
        ON Orders (IdempotencyKey)
        WHERE IdempotencyKey IS NOT NULL;
    PRINT 'Created UQ_Orders_IdempotencyKey index.';
END
GO

-- ---------------------------------------------------------------------------
-- 4. Orders — customer lookup index
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_Orders_UserId_CreatedAt'
      AND  object_id = OBJECT_ID('Orders')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Orders_UserId_CreatedAt
        ON Orders (UserId, CreatedAt DESC)
        INCLUDE (OrderNumber, Status, PaymentStatus, TotalAmount);
    PRINT 'Created IX_Orders_UserId_CreatedAt index.';
END
GO

-- ---------------------------------------------------------------------------
-- 5. Orders — status lookup index
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_Orders_Status'
      AND  object_id = OBJECT_ID('Orders')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Orders_Status
        ON Orders (Status)
        INCLUDE (UserId, OrderNumber, CreatedAt);
    PRINT 'Created IX_Orders_Status index.';
END
GO

-- ---------------------------------------------------------------------------
-- 6. OrderStatusHistory — lookup index
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_OrderStatusHistory_OrderId'
      AND  object_id = OBJECT_ID('OrderStatusHistory')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_OrderStatusHistory_OrderId
        ON OrderStatusHistory (OrderId, ChangedAt DESC);
    PRINT 'Created IX_OrderStatusHistory_OrderId index.';
END
GO

-- ---------------------------------------------------------------------------
-- 7. OrderItems — lookup index
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_OrderItems_OrderId'
      AND  object_id = OBJECT_ID('OrderItems')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_OrderItems_OrderId
        ON OrderItems (OrderId);
    PRINT 'Created IX_OrderItems_OrderId index.';
END
GO

-- ---------------------------------------------------------------------------
-- 8. InventoryTransactions — reference lookup index
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_InventoryTx_ReferenceId'
      AND  object_id = OBJECT_ID('InventoryTransactions')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_InventoryTx_ReferenceId
        ON InventoryTransactions (ReferenceId, ReferenceType)
        INCLUDE (ProductId, TransactionType);
    PRINT 'Created IX_InventoryTx_ReferenceId index.';
END
GO

PRINT '009_OrderManagement migration complete.';
GO
