-- =============================================================================
-- GOLIS Mart — 005_SeedMoreProducts.sql
-- Adds 18 more demo products to reach 56 total across 10 categories.
-- Also corrects ImagePath for all existing 38 products via MERGE update.
-- ⚠️  DEMO DATA ONLY — prices are illustrative and must be verified before launch.
-- Safe to rerun: uses MERGE to upsert by SKU.
-- Execute against: GOLISMartDb after 004_SeedDemoProducts.sql
-- =============================================================================

USE GOLISMartDb;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- -----------------------------------------------------------------------
-- Helper: map category slugs to IDs
-- -----------------------------------------------------------------------
DECLARE @CatVeg    INT = (SELECT Id FROM Categories WHERE Slug = 'fresh-vegetables');
DECLARE @CatFruit  INT = (SELECT Id FROM Categories WHERE Slug = 'fresh-fruits');
DECLARE @CatRice   INT = (SELECT Id FROM Categories WHERE Slug = 'rice-and-atta');
DECLARE @CatDal    INT = (SELECT Id FROM Categories WHERE Slug = 'dal-and-pulses');
DECLARE @CatDairy  INT = (SELECT Id FROM Categories WHERE Slug = 'dairy-and-eggs');
DECLARE @CatOil    INT = (SELECT Id FROM Categories WHERE Slug = 'cooking-oils');
DECLARE @CatSpice  INT = (SELECT Id FROM Categories WHERE Slug = 'spices-and-masalas');
DECLARE @CatSnack  INT = (SELECT Id FROM Categories WHERE Slug = 'snacks-packaged-foods');
DECLARE @CatBev    INT = (SELECT Id FROM Categories WHERE Slug = 'beverages');
DECLARE @CatHouse  INT = (SELECT Id FROM Categories WHERE Slug = 'household-essentials');

-- -----------------------------------------------------------------------
-- Seed table (temp)
-- -----------------------------------------------------------------------
CREATE TABLE #SeedProducts2 (
    CategoryId  INT,
    SKU         NVARCHAR(50),
    Name        NVARCHAR(200),
    Slug        NVARCHAR(200),
    Description NVARCHAR(2000),
    UnitCode    NVARCHAR(20),
    UnitLabel   NVARCHAR(50),
    Price       DECIMAL(10,2),
    OfferPrice  DECIMAL(10,2) NULL,
    IsFeatured  BIT,
    ImagePath   NVARCHAR(500),
    Stock       DECIMAL(10,3),
    LowStock    DECIMAL(10,3)
);

-- ── Fresh Vegetables (4 new) ─────────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatVeg, 'VEG-CAU-001', 'Cauliflower (Gobi)',   'cauliflower',          'Firm white cauliflower heads, great for aloo-gobi and pakoras.',             'pc',  '1 pc',   35.00,  NULL,  0, '/images/products/cauliflower.jpg',  40, 8),
(@CatVeg, 'VEG-CUC-001', 'Cucumber (Kheera)',    'cucumber',             'Cool, crisp cucumbers — ideal for raita and salads.',                        'kg',  '500 g',  28.00,  24.00, 0, '/images/products/cucumber.jpg',     50, 10),
(@CatVeg, 'VEG-BRJ-001', 'Brinjal (Baingan)',    'brinjal',              'Purple brinjals, perfect for baingan bharta and curry.',                     'kg',  '500 g',  30.00,  NULL,  0, '/images/products/brinjal.jpg',      35, 8),
(@CatVeg, 'VEG-CAP-001', 'Capsicum (Shimla Mirch)','capsicum',           'Bright green capsicum, crunchy and sweet. Good for stir-fry.',               'kg',  '250 g',  40.00,  35.00, 0, '/images/products/capsicum.jpg',     30, 6);

-- ── Fresh Fruits (3 new) ─────────────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatFruit, 'FRT-POM-001', 'Pomegranate (Anar)',  'pomegranate',          'Sweet ruby-red pomegranate, packed with antioxidants.',                     'pc',  '1 pc',   80.00,  70.00, 0, '/images/products/pomegranate.jpg',  25, 5),
(@CatFruit, 'FRT-WAT-001', 'Watermelon (Tarbooz)','watermelon',           'Sweet and refreshing whole watermelon, approx 3–4 kg.',                     'pc',  '1 pc',   120.00, NULL,  0, '/images/products/watermelon.jpg',   15, 3),
(@CatFruit, 'FRT-PAP-001', 'Papaya (Papita)',     'papaya',               'Ripe yellow papaya — great for digestion and smoothies.',                   'kg',  '1 kg',   55.00,  NULL,  0, '/images/products/papaya.jpg',       20, 4);

-- ── Rice & Atta (1 new) ──────────────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatRice, 'RIC-IDL-001', 'Idli Rice',            'idli-rice-5kg',        'Short-grain parboiled rice ideal for making soft idlis and dosas.',          'kg',  '5 kg',   270.00, NULL,  0, '/images/products/idli-rice.jpg',    30, 5);

-- ── Dal & Pulses (2 new) ─────────────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatDal, 'DAL-URA-001', 'Urad Dal (Black)',      'urad-dal-1kg',         'Whole black lentils — essential for dal makhani.',                           'kg',  '1 kg',   130.00, 120.00,0, '/images/products/urad-dal.jpg',     40, 8),
(@CatDal, 'DAL-RAJ-001', 'Rajma (Kidney Beans)',  'rajma-1kg',            'Red kidney beans for the classic Punjabi rajma curry.',                      'kg',  '500 g',  95.00,  NULL,  0, '/images/products/rajma.jpg',        35, 6);

-- ── Dairy & Eggs (1 new) ─────────────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatDairy, 'DAI-PAN-001', 'Paneer (Fresh)',       'fresh-paneer-200g',    'Fresh, soft homestyle paneer made from whole milk. 200 g block.',            'g',   '200 g',  75.00,  65.00, 1, '/images/products/paneer.jpg',       50, 10);

-- ── Cooking Oils (2 new) ─────────────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatOil, 'OIL-GRD-001', 'Groundnut Oil',          'groundnut-oil-1l',     'Cold-pressed groundnut (peanut) oil — classic South Indian cooking oil.',    'litre','1 Litre',195.00, NULL,  0, '/images/products/groundnut-oil.jpg',35, 5),
(@CatOil, 'OIL-COC-001', 'Virgin Coconut Oil',     'virgin-coconut-oil-500ml','Cold-pressed virgin coconut oil, unrefined and fragrant.',               'ml',  '500 ml', 360.00, 320.00,0, '/images/products/coconut-oil.jpg',  25, 4);

-- ── Spices & Masalas (1 new) ─────────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatSpice, 'SPI-COR-001', 'Coriander Powder',     'coriander-powder-100g','Freshly ground coriander powder with a mild citrusy flavour.',               'g',   '100 g',  50.00,  NULL,  0, '/images/products/coriander-powder.jpg',65,8);

-- ── Snacks & Packaged Foods (2 new) ──────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatSnack, 'SNK-CHI-001', 'Potato Chips (Classic)','potato-chips-classic', 'Thin, crisp salted potato chips. Perfect evening snack.',                   'pack','45 g',   20.00,  NULL,  0, '/images/products/potato-chips.jpg', 100,15),
(@CatSnack, 'SNK-MIX-001', 'Punjabi Mixture',       'punjabi-mixture',      'Spicy namkeen mixture with sev, peanuts and dried fruits.',                  'pack','200 g',  65.00,  60.00, 0, '/images/products/mixture-namkeen.jpg',60,10);

-- ── Household Essentials (2 new) ─────────────────────────────────────────────
INSERT INTO #SeedProducts2 VALUES
(@CatHouse, 'HOU-FLC-001', 'Floor Cleaner (Lemon)', 'floor-cleaner-1l',    'Antibacterial floor cleaning liquid with fresh lemon fragrance.',            'litre','1 Litre',120.00, 99.00, 0, '/images/products/floor-cleaner.jpg',40, 6),
(@CatHouse, 'HOU-GBG-001', 'Garbage Bags (Medium)', 'garbage-bags-medium', 'Medium-size oxo-biodegradable garbage bags, pack of 30.',                    'pack','30 pcs', 85.00,  75.00, 0, '/images/products/garbage-bags.jpg', 50, 8);

-- -----------------------------------------------------------------------
-- Upsert new products
-- -----------------------------------------------------------------------
MERGE Products AS target
USING (SELECT * FROM #SeedProducts2) AS source
ON target.SKU = source.SKU
WHEN MATCHED THEN
    UPDATE SET
        CategoryId  = source.CategoryId,
        Name        = source.Name,
        Slug        = source.Slug,
        Description = source.Description,
        UnitCode    = source.UnitCode,
        UnitLabel   = source.UnitLabel,
        Price       = source.Price,
        OfferPrice  = source.OfferPrice,
        IsFeatured  = source.IsFeatured,
        ImagePath   = source.ImagePath,
        IsActive    = 1,
        UpdatedAt   = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (CategoryId, SKU, Name, Slug, Description, UnitCode, UnitLabel, Price, OfferPrice, IsFeatured, IsActive, ImagePath, CreatedAt, UpdatedAt)
    VALUES (source.CategoryId, source.SKU, source.Name, source.Slug, source.Description,
            source.UnitCode, source.UnitLabel, source.Price, source.OfferPrice,
            source.IsFeatured, 1, source.ImagePath, SYSUTCDATETIME(), SYSUTCDATETIME());

-- -----------------------------------------------------------------------
-- Upsert Inventory for new products only
-- -----------------------------------------------------------------------
MERGE Inventory AS target
USING (
    SELECT p.Id AS ProductId, sp.Stock AS QuantityOnHand, sp.LowStock AS LowStockThreshold
    FROM #SeedProducts2 sp
    JOIN Products p ON p.SKU = sp.SKU
) AS source
ON target.ProductId = source.ProductId
WHEN NOT MATCHED THEN
    INSERT (ProductId, QuantityOnHand, LowStockThreshold, ReservedQuantity, UpdatedAt)
    VALUES (source.ProductId, source.QuantityOnHand, source.LowStockThreshold, 0, SYSUTCDATETIME());

-- -----------------------------------------------------------------------
-- Patch ImagePaths on existing products (004 products) to match local files
-- -----------------------------------------------------------------------
UPDATE Products SET ImagePath = '/images/products/moong-dal.jpg',    UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'DAL-MOO-001';
UPDATE Products SET ImagePath = '/images/products/chana-dal.jpg',    UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'DAL-CHA-001';
UPDATE Products SET ImagePath = '/images/products/masoor-dal.jpg',   UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'DAL-MAS-001';
UPDATE Products SET ImagePath = '/images/products/sona-masoori.jpg', UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'RIC-SON-001';
UPDATE Products SET ImagePath = '/images/products/atta.jpg',         UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'ATT-WHL-001';
UPDATE Products SET ImagePath = '/images/products/multigrain-atta.jpg', UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'ATT-MUL-001';
UPDATE Products SET ImagePath = '/images/products/butter.jpg',       UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'DAI-BUT-001';
UPDATE Products SET ImagePath = '/images/products/mustard-oil.jpg',  UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'OIL-MUS-001';
UPDATE Products SET ImagePath = '/images/products/red-chilli-powder.jpg', UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'SPI-RED-001';
UPDATE Products SET ImagePath = '/images/products/garam-masala.jpg', UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'SPI-GAR-001';
UPDATE Products SET ImagePath = '/images/products/cumin-seeds.jpg',  UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'SPI-CUM-001';
UPDATE Products SET ImagePath = '/images/products/good-day-biscuits.jpg', UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'SNK-BIS-001';
UPDATE Products SET ImagePath = '/images/products/maggi.jpg',        UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'SNK-NOO-001';
UPDATE Products SET ImagePath = '/images/products/red-label-tea.jpg', UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'BEV-TEA-001';
UPDATE Products SET ImagePath = '/images/products/bru-coffee.jpg',   UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'BEV-COF-001';
UPDATE Products SET ImagePath = '/images/products/mango-juice.jpg',  UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'BEV-JUI-001';
UPDATE Products SET ImagePath = '/images/products/surf-excel.jpg',   UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'HOU-DET-001';
UPDATE Products SET ImagePath = '/images/products/vim-bar.jpg',      UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'HOU-DIS-001';
UPDATE Products SET ImagePath = '/images/products/dettol-soap.jpg',  UpdatedAt = SYSUTCDATETIME() WHERE SKU = 'HOU-SOP-001';

DROP TABLE #SeedProducts2;

COMMIT TRANSACTION;

-- Summary
SELECT
    c.Name AS Category,
    COUNT(p.Id) AS ProductCount
FROM Products p
JOIN Categories c ON c.Id = p.CategoryId
WHERE p.IsActive = 1
GROUP BY c.Name
ORDER BY c.Name;

DECLARE @total INT = (SELECT COUNT(*) FROM Products WHERE IsActive = 1);
PRINT CONCAT('Total active products: ', @total);
PRINT 'NOTE: All prices are DEMO DATA only and must be verified before launch.';
GO
