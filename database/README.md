# Database — GOLIS Mart

This directory contains SQL scripts for setting up and maintaining the GOLISMart database on SQL Server Developer Edition.

## Prerequisites

- **SQL Server Developer Edition** installed locally
- SQL Server is running and accessible at `localhost` via Windows Authentication
- SQL Server Management Studio (SSMS) or `sqlcmd` command-line tool

## Creating the Database (fresh install)

Run these scripts in order:

| # | Script | Description |
|---|--------|-------------|
| 1 | `000_CreateDatabase.sql` | Creates the `GOLISMartDb` database |
| 2 | `001_CreateSchema.sql` | Creates all tables, constraints, Identity tables |
| 3 | `002_CreateIndexes.sql` | Performance indexes |
| 4 | `003_SeedCategories.sql` | Seed categories |
| 5 | `004_SeedDemoProducts.sql` | Demo products |
| 6 | `005_SeedMoreProducts.sql` | Additional products |
| 7 | `006_Auth_And_Addresses.sql` | **Phase 4** — Auth & address columns/indexes |

### Using sqlcmd

```bash
sqlcmd -S .\SQLEXPRESS -E -i 000_CreateDatabase.sql
sqlcmd -S .\SQLEXPRESS -E -d GOLISMartDb -i 001_CreateSchema.sql
sqlcmd -S .\SQLEXPRESS -E -d GOLISMartDb -i 002_CreateIndexes.sql
sqlcmd -S .\SQLEXPRESS -E -d GOLISMartDb -i 003_SeedCategories.sql
sqlcmd -S .\SQLEXPRESS -E -d GOLISMartDb -i 004_SeedDemoProducts.sql
sqlcmd -S .\SQLEXPRESS -E -d GOLISMartDb -i 005_SeedMoreProducts.sql
sqlcmd -S .\SQLEXPRESS -E -d GOLISMartDb -i 006_Auth_And_Addresses.sql
```

## Upgrading an existing database (Phase 4 migration)

If you already have the database from Phase 1–3, only run the Phase 4 script:

```bash
sqlcmd -S .\SQLEXPRESS -E -d GOLISMartDb -i 006_Auth_And_Addresses.sql
```

All Phase 4 DDL is idempotent (IF NOT EXISTS guards throughout).

`-E` uses Windows Authentication (trusted connection). No password required.

### Using SSMS

1. Open SSMS and connect to `localhost` with Windows Authentication.
2. Open each script file in order via **File → Open → File…**.
3. Execute each script with **F5** or the **Execute** button.

## Connection String Reference

The application reads its connection string from:

- **Development**: `GOLISMart.API/appsettings.Development.json`
  ```
  Server=localhost;Database=GOLISMartDb;Trusted_Connection=True;TrustServerCertificate=True;
  ```
- **Production**: Set the `ConnectionStrings__GOLISMartDb` environment variable (never commit production credentials).

## Notes

- The connection string uses Windows Authentication — no username or password is stored in source code.
- `appsettings.Development.json` is excluded from source control via `.gitignore`.
