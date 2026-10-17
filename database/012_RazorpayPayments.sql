-- =============================================================================
-- GOLIS Mart — 012_RazorpayPayments.sql
-- Razorpay Test Mode Integration — schema enhancements.
--
-- The Payments and PaymentWebhookEvents tables already exist from 001_CreateSchema.sql.
-- This migration:
--   1. Adds RazorpayOrderId index on Payments for fast gateway-order lookups.
--   2. Adds GatewayEventId column to PaymentWebhookEvents for idempotency.
--   3. Adds unique index on GatewayEventId to prevent duplicate event processing.
--   4. Adds GatewayOrderId index on Payments.
--
-- Safe to rerun: all DDL is guarded by existence checks.
-- Execute against: GOLISMartDb
-- =============================================================================

USE GOLISMartDb;
GO

-- ---------------------------------------------------------------------------
-- 1. Payments — index on GatewayOrderId (Razorpay order_id lookups)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_Payments_GatewayOrderId'
      AND  object_id = OBJECT_ID('Payments')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Payments_GatewayOrderId
        ON Payments (GatewayOrderId)
        INCLUDE (OrderId, Status, Amount, Currency)
        WHERE GatewayOrderId IS NOT NULL;
    PRINT 'Created IX_Payments_GatewayOrderId index.';
END
GO

-- ---------------------------------------------------------------------------
-- 2. Payments — index on OrderId for order-payment lookups
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_Payments_OrderId'
      AND  object_id = OBJECT_ID('Payments')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Payments_OrderId
        ON Payments (OrderId)
        INCLUDE (GatewayName, Status, Amount, Currency, GatewayOrderId, GatewayPaymentId);
    PRINT 'Created IX_Payments_OrderId index.';
END
GO

-- ---------------------------------------------------------------------------
-- 3. Payments — unique index on GatewayPaymentId (prevents duplicate capture)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'UQ_Payments_GatewayPaymentId'
      AND  object_id = OBJECT_ID('Payments')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Payments_GatewayPaymentId
        ON Payments (GatewayPaymentId)
        WHERE GatewayPaymentId IS NOT NULL;
    PRINT 'Created UQ_Payments_GatewayPaymentId index.';
END
GO

-- ---------------------------------------------------------------------------
-- 4. PaymentWebhookEvents — add GatewayEventId column for idempotency
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('PaymentWebhookEvents') AND name = 'GatewayEventId'
)
BEGIN
    ALTER TABLE PaymentWebhookEvents ADD GatewayEventId NVARCHAR(200) NULL;
    PRINT 'Added PaymentWebhookEvents.GatewayEventId';
END
GO

-- ---------------------------------------------------------------------------
-- 5. PaymentWebhookEvents — unique index on GatewayEventId (idempotency)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'UQ_WebhookEvents_GatewayEventId'
      AND  object_id = OBJECT_ID('PaymentWebhookEvents')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_WebhookEvents_GatewayEventId
        ON PaymentWebhookEvents (GatewayEventId)
        WHERE GatewayEventId IS NOT NULL;
    PRINT 'Created UQ_WebhookEvents_GatewayEventId index.';
END
GO

-- ---------------------------------------------------------------------------
-- 6. PaymentWebhookEvents — index on GatewayName + EventType for lookups
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name = 'IX_WebhookEvents_GatewayName_EventType'
      AND  object_id = OBJECT_ID('PaymentWebhookEvents')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_WebhookEvents_GatewayName_EventType
        ON PaymentWebhookEvents (GatewayName, EventType, ReceivedAt DESC)
        INCLUDE (GatewayEventId, ProcessedAt);
    PRINT 'Created IX_WebhookEvents_GatewayName_EventType index.';
END
GO

PRINT '012_RazorpayPayments migration complete.';
GO
