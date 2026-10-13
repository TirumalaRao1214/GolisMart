# GOLIS Mart

A full-stack Indian online grocery store built with ASP.NET Core 10 (.NET 10), Dapper, and SQL Server.

---

## Architecture

| Project | Role |
|---|---|
| `GOLISMart.API` | ASP.NET Core Web API — sole access to SQL Server |
| `GOLISMart.Web` | ASP.NET Core MVC — calls the API via `ICatalogApiClient` |
| `tests/GOLISMart.API.Tests` | xUnit unit tests for API services and query logic |
| `tests/GOLISMart.Web.Tests` | xUnit unit tests for MVC controllers |

The MVC project **never** accesses SQL Server directly. All data flows through the API.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| .NET 10 SDK | `dotnet --version` should show `10.x.x` |
| SQL Server / SQL Express | Windows Authentication (no password needed in dev) |
| `sqlcmd` | Ships with SQL Server tools or SSMS |

---

## Database Setup

### 1. Create the database

```bash
sqlcmd -S ".\SQLEXPRESS" -E -C -i database\000_CreateDatabase.sql
```

### 2. Run all schema and seed scripts in order

```bash
sqlcmd -S ".\SQLEXPRESS" -E -C -d GOLISMartDb -i database\001_CreateSchema.sql
sqlcmd -S ".\SQLEXPRESS" -E -C -d GOLISMartDb -i database\002_CreateIndexes.sql
sqlcmd -S ".\SQLEXPRESS" -E -C -d GOLISMartDb -i database\003_SeedCategories.sql
sqlcmd -S ".\SQLEXPRESS" -E -C -d GOLISMartDb -i database\004_SeedDemoProducts.sql
sqlcmd -S ".\SQLEXPRESS" -E -C -d GOLISMartDb -i database\005_SeedMoreProducts.sql
```

All scripts are idempotent — safe to rerun without creating duplicates.

### Script execution order

| File | Purpose |
|---|---|
| `000_CreateDatabase.sql` | Creates `GOLISMartDb` if not exists |
| `001_CreateSchema.sql` | All 17 logical entities + ASP.NET Identity tables |
| `002_CreateIndexes.sql` | Performance indexes |
| `003_SeedCategories.sql` | 10 categories |
| `004_SeedDemoProducts.sql` | 38 demo products + inventory records |
| `005_SeedMoreProducts.sql` | +18 products → **56 total**; patches image paths |

> **⚠️ DEMO DATA** — all prices in the seed scripts are for illustration only and must be verified before any production launch.

### Database tables (17 logical entities)

1. `AspNetUsers` — Customer accounts (ASP.NET Core Identity)
2. `AspNetRoles` + `AspNetUserRoles` + `AspNetUserClaims` + `AspNetUserLogins` + `AspNetRoleClaims` + `AspNetUserTokens` — Identity supporting tables
3. `UserAddresses`
4. `Categories`
5. `Products`
6. `ProductImages`
7. `Inventory`
8. `Carts`
9. `CartItems`
10. `DeliveryZones`
11. `DeliverySlots`
12. `Orders`
13. `OrderItems`
14. `Payments`
15. `PaymentWebhookEvents`
16. `Coupons` + `CouponRedemptions`
17. `OrderStatusHistory`
18. `InventoryTransactions`

---

## Connection String Configuration

The API reads its connection string from `GOLISMart.API/appsettings.Development.json` (excluded from git):

```json
{
  "ConnectionStrings": {
    "GOLISMartDb": "Server=.\\SQLEXPRESS;Database=GOLISMartDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

**Never commit production connection strings or passwords.**

---

## Running the Applications

### API (port 5000 HTTP / 5001 HTTPS)

```bash
cd GOLISMart.API
dotnet run --launch-profile http
```

### MVC Frontend (port 5002 HTTP / 5003 HTTPS)

```bash
cd GOLISMart.Web
dotnet run --launch-profile http
```

The MVC app requires the API to be running first. The API base URL is configured in `GOLISMart.Web/appsettings.Development.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

> **Important:** Always use the `http` launch profile for local development. The dev HTTPS certificate is not trusted by
> default, which causes the Web app to fail silently when calling the API. Use HTTP for all local API communication.

---

## Local URLs

| Service | URL |
|---|---|
| API (Swagger) | http://localhost:5000/swagger |
| API health | http://localhost:5000/health |
| MVC Homepage | http://localhost:5002/ |
| MVC Shop | http://localhost:5002/catalog |
| MVC Search | http://localhost:5002/catalog/search?q=rice |
| Category page | http://localhost:5002/catalog/category/fresh-vegetables |
| Product detail | http://localhost:5002/product/fresh-tomatoes |

---

## Example API Requests

```bash
# All categories with product counts
GET http://localhost:5000/api/v1/categories

# Category by slug
GET http://localhost:5000/api/v1/categories/fresh-vegetables

# Products (page 1, sorted by price asc, in-stock only)
GET http://localhost:5000/api/v1/products?page=1&pageSize=20&sortBy=price&sortOrder=asc&inStock=true

# Search for "dal"
GET http://localhost:5000/api/v1/products?search=dal

# Filter by category
GET http://localhost:5000/api/v1/products?category=dairy-and-eggs

# Product detail
GET http://localhost:5000/api/v1/products/fresh-tomatoes

# Featured products
GET http://localhost:5000/api/v1/products/featured?max=8

# Related products
GET http://localhost:5000/api/v1/products/fresh-tomatoes/related
```

---

## Running Tests

```bash
dotnet test
```

Current test status (Phase 2):

| Suite | Tests | Status |
|---|---|---|
| `GOLISMart.API.Tests` | 30 | ✅ All passing |
| `GOLISMart.Web.Tests` | 14 | ✅ All passing |

Tests are pure unit tests — no database or HTTP server required.

---

## Phase 2 — Completed Features

- ✅ All 17 database entities (schema, indexes, seed scripts)
- ✅ 34 realistic Indian grocery demo products across 10 categories
- ✅ Dapper repositories: `ICategoryRepository`, `IProductRepository`, `IInventoryRepository`
- ✅ `CatalogService` with full business logic
- ✅ REST API: `GET /api/v1/categories`, `GET /api/v1/products`, `GET /api/v1/products/{slug}`, `/featured`, `/{slug}/related`
- ✅ Pagination, search, category filter, sort, in-stock filter
- ✅ Typed API client (`ICatalogApiClient` / `CatalogApiClient`) via `IHttpClientFactory`
- ✅ MVC Homepage with hero, category grid, featured products, vegetables/fruits/essentials sections
- ✅ Category listing page with new filter sidebar (`.gm-filter-sidebar`, `.gm-filter-link`)
- ✅ Product listing page with filters, sort controls, pagination
- ✅ Product detail page with images, related products, promise strip
- ✅ Search page with empty/no-results states
- ✅ Reusable partials: `_Header`, `_Footer`, `_ProductCard`, `_CategoryCard`, `_Pagination`, `_ApiError`
- ✅ Full production CSS redesign (`site.css`): Inter font, design tokens, green brand color, no emojis
- ✅ Two-row header (top bar + category nav bar) with SVG icons
- ✅ Mobile navigation drawer with backdrop, Escape-key close, focus trap via `site.js`
- ✅ Photo-led hero section with stats strip and pincode checker UI
- ✅ Dark multi-column footer with contact details, legal links, and demo disclaimer
- ✅ Product cards with discount badge, stock chip, hover zoom
- ✅ Category cards with 4:3 aspect ratio, image zoom on hover, SVG fallback placeholder
- ✅ API error / unavailable states shown gracefully
- ✅ Missing image fallback (`/images/products/placeholder.svg`)

### Static image assets

All images are local (no hotlinking). Paths match database `ImagePath` values:

| Directory | Contents |
|---|---|
| `wwwroot/images/categories/` | 10 category photos (`.jpg`) |
| `wwwroot/images/products/` | 19+ product photos (`.jpg`) + `placeholder.svg` |
| `wwwroot/images/hero/` | `hero-groceries.jpg`, `hero-produce.jpg` |

---

## Known Limitations / Remaining Work

| Area | Notes |
|---|---|
| Cart & Checkout | Phase 3 — Add to Cart buttons are visible but disabled |
| User Authentication | Phase 3 — Account nav is placeholder |
| Pincode Serviceability | Phase 3 — UI prepared, backend not yet wired |
| Admin Dashboard | Phase 4 |
| Payment Integration | Phase 4 |
| Application Control | In this playground environment the API executable is blocked by a local security policy. Run from your own dev machine with normal permissions. |

---

## Project Structure

```
GOLISMart/
├── database/
│   ├── 000_CreateDatabase.sql
│   ├── 001_CreateSchema.sql
│   ├── 002_CreateIndexes.sql
│   ├── 003_SeedCategories.sql
│   └── 004_SeedDemoProducts.sql
├── GOLISMart.API/
│   ├── Controllers/        # CategoriesController, ProductsController, HealthController
│   ├── Data/               # SqlConnectionFactory
│   ├── DTOs/               # CategoryDto, ProductDto, PagedResult, ErrorResponse
│   ├── Middleware/         # ExceptionHandlingMiddleware
│   ├── Models/             # Category, Product, ProductImage, InventoryStatus
│   ├── Repositories/
│   │   ├── Interfaces/     # ICategoryRepository, IProductRepository, IInventoryRepository
│   │   └── Implementations/
│   └── Services/
│       ├── Interfaces/     # ICatalogService
│       └── Implementations/
├── GOLISMart.Web/
│   ├── Controllers/        # HomeController, CatalogController, ProductController
│   ├── Models/             # ViewModels, ErrorViewModel
│   ├── Services/
│   │   ├── ApiDtos.cs      # Mirror DTOs (no project reference to API)
│   │   ├── Interfaces/     # ICatalogApiClient
│   │   └── Implementations/
│   ├── Views/
│   │   ├── Home/           # Index, Privacy
│   │   ├── Catalog/        # Index, Search
│   │   ├── Product/        # Detail
│   │   └── Shared/         # _Layout, _Header, _Footer, _ProductCard,
│   │                         _CategoryCard, _Pagination, _ApiError
│   └── wwwroot/
│       └── css/site.css    # GOLIS Mart grocery theme
└── tests/
    ├── GOLISMart.API.Tests/ # CatalogServiceTests, ProductQueryTests
    └── GOLISMart.Web.Tests/ # HomeControllerTests, CatalogControllerTests,
                               ProductControllerTests
```
