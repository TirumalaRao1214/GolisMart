-- =============================================================================
-- GOLIS Mart — 000_CreateDatabase.sql
-- Creates the GOLISMartDb database if it does not already exist.
-- Run this against the master database BEFORE all other scripts.
-- =============================================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'GOLISMartDb')
BEGIN
    CREATE DATABASE GOLISMartDb
        COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Database GOLISMartDb created.';
END
ELSE
BEGIN
    PRINT 'Database GOLISMartDb already exists — skipped.';
END
GO
