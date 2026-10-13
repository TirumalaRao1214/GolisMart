-- =============================================================================
-- GOLIS Mart — 004_SeedDemoProducts.sql
-- Seeds 34 demo products (at least 30 required) across all 10 categories.
-- ⚠️  DEMO DATA ONLY — prices are illustrative and must be verified before launch.
-- Safe to rerun: uses MERGE to upsert by SKU.
-- Execute against: GOLISMartDb after 003_SeedCategories.sql
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
-- Products source table (temp)
-- -----------------------------------------------------------------------
CREATE TABLE #SeedProducts (
    CategoryId  INT,
    SKU         NVARCHAR(50),
    Name        NVARCHAR(200),
    Slug        NVARCHAR(200),
    Description NVARCHAR(2000),
    UnitCode    NVARCHAR(20),
    UnitLabel   NVARCHAR(50),
    Price       DECIMAL(10,2),
    OfferPrice  DECIMAL(10,2),
    IsFeatured  BIT,
    ImagePath   NVARCHAR(500),
    Stock       DECIMAL(10,3),
    LowStock    DECIMAL(10,3)
);

-- Fresh Vegetables (6 products)
INSERT INTO #SeedProducts VALUES
(@CatVeg, 'VEG-TOM-001', 'Fresh Tomatoes',       'fresh-tomatoes',       'Red, ripe farm tomatoes. Great for curries and salads.',                   'kg',  '1 kg',    35.00,  NULL,  1, '/images/products/tomatoes.jpg',       50, 10),
(@CatVeg, 'VEG-ONI-001', 'Red Onions',           'red-onions',           'Medium-sized red onions with a sharp flavour.',                            'kg',  '1 kg',    40.00,  35.00, 0, '/images/products/onions.jpg',         80, 15),
(@CatVeg, 'VEG-POT-001', 'Potatoes',             'potatoes',             'Fresh potatoes ideal for sabzi, fries and curries.',                       'kg',  '1 kg',    30.00,  NULL,  1, '/images/products/potatoes.jpg',       100, 20),
(@CatVeg, 'VEG-CHI-001', 'Green Chillies',       'green-chillies',       'Fresh hot green chillies, 100 g punnet.',                                  'g',   '100 g',   15.00,  NULL,  0, '/images/products/green-chillies.jpg', 30,  5),
(@CatVeg, 'VEG-SPI-001', 'Spinach (Palak)',       'spinach-palak',        'Tender baby spinach leaves, washed and ready to cook.',                    'pack','1 bunch', 25.00,  20.00, 0, '/images/products/spinach.jpg',        40, 8),
(@CatVeg, 'VEG-CAR-001', 'Carrots',              'carrots',              'Crunchy orange carrots, great for halwa and sabzi.',                       'kg',  '500 g',   28.00,  25.00, 0, '/images/products/carrots.jpg',        60, 10);

-- Fresh Fruits (5 products)
INSERT INTO #SeedProducts VALUES
(@CatFruit, 'FRT-BAN-001', 'Bananas (Robusta)',  'bananas-robusta',      'Sweet Robusta bananas, dozen approximately 700 g.',                        'pack','12 pcs',  45.00,  40.00, 1, '/images/products/bananas.jpg',        50,  8),
(@CatFruit, 'FRT-APP-001', 'Red Apples',         'red-apples',           'Crisp Shimla apples, rich in fibre.',                                      'kg',  '1 kg',    150.00, 130.00,1, '/images/products/apples.jpg',         30,  5),
(@CatFruit, 'FRT-ORG-001', 'Oranges (Nagpur)',   'oranges-nagpur',       'Juicy Nagpur oranges, high in vitamin C.',                                 'kg',  '1 kg',    80.00,  NULL,  0, '/images/products/oranges.jpg',        40,  8),
(@CatFruit, 'FRT-MAN-001', 'Mangoes (Alphonso)', 'mangoes-alphonso',     'Premium Alphonso mangoes — the king of fruits.',                           'pack','6 pcs',   350.00, 300.00,1, '/images/products/mangoes.jpg',        20,  4),
(@CatFruit, 'FRT-GRP-001', 'Grapes (Green)',     'grapes-green',         'Seedless green grapes, sweet and firm.',                                   'kg',  '500 g',   90.00,  80.00, 0, '/images/products/grapes.jpg',         25,  5);

-- Rice and Atta (4 products)
INSERT INTO #SeedProducts VALUES
(@CatRice, 'RIC-BAS-001', 'Basmati Rice',        'basmati-rice-5kg',     'Long-grain aged Basmati rice. Aromatic and fluffy.',                       'kg',  '5 kg',    399.00, 349.00,1, '/images/products/basmati-rice.jpg',   40, 5),
(@CatRice, 'RIC-SON-001', 'Sona Masoori Rice',   'sona-masoori-rice-5kg','Light and low-starch Sona Masoori rice. Ideal for everyday meals.',        'kg',  '5 kg',    310.00, NULL,  0, '/images/products/sona-masoori.jpg',   30, 5),
(@CatRice, 'ATT-WHL-001', 'Whole Wheat Atta',    'whole-wheat-atta-5kg', '100% whole wheat atta, stone-ground for softness and nutrition.',           'kg',  '5 kg',    245.00, 225.00,1, '/images/products/atta.jpg',           50, 8),
(@CatRice, 'ATT-MUL-001', 'Multi-grain Atta',    'multigrain-atta-5kg',  'Blend of wheat, oats, soya and ragi for healthy rotis.',                   'kg',  '5 kg',    295.00, NULL,  0, '/images/products/multigrain-atta.jpg',30, 5);

-- Dal and Pulses (4 products)
INSERT INTO #SeedProducts VALUES
(@CatDal, 'DAL-TOR-001', 'Toor Dal (Arhar)',     'toor-dal-1kg',         'Split pigeon peas, the most popular dal in Indian cooking.',               'kg',  '1 kg',    135.00, NULL,  1, '/images/products/toor-dal.jpg',       60, 10),
(@CatDal, 'DAL-MOO-001', 'Moong Dal (Split)',    'moong-dal-1kg',        'Split yellow moong dal, easy to digest and nutritious.',                   'kg',  '1 kg',    120.00, 110.00,0, '/images/products/moong-dal.jpg',      50,  8),
(@CatDal, 'DAL-CHA-001', 'Chana Dal',            'chana-dal-1kg',        'Split Bengal gram — used in dals, curries and sweets.',                    'kg',  '1 kg',    110.00, NULL,  0, '/images/products/chana-dal.jpg',      45,  8),
(@CatDal, 'DAL-MAS-001', 'Masoor Dal (Red)',     'masoor-dal-1kg',       'Red lentils that cook quickly without soaking.',                            'kg',  '1 kg',    105.00, 95.00, 0, '/images/products/masoor-dal.jpg',     40,  8);

-- Dairy and Eggs (4 products)
INSERT INTO #SeedProducts VALUES
(@CatDairy, 'DAI-MLK-001', 'Full Cream Milk',     'full-cream-milk-1l',   'Fresh toned cow milk, pasteurised. Best for tea and cooking.',             'litre','1 Litre', 62.00, NULL,  1, '/images/products/milk.jpg',           100, 20),
(@CatDairy, 'DAI-CRD-001', 'Natural Curd (Dahi)', 'natural-curd-400g',    'Thick set curd made from full cream milk.',                                'g',    '400 g',   40.00, 35.00, 0, '/images/products/curd.jpg',           80,  15),
(@CatDairy, 'DAI-BUT-001', 'Butter (Salted)',      'salted-butter-500g',   'Rich and creamy salted table butter.',                                     'g',    '500 g',   260.00,240.00,0, '/images/products/butter.jpg',         40,  8),
(@CatDairy, 'DAI-EGG-001', 'Eggs (Farm Fresh)',    'farm-fresh-eggs-12',   'White eggs from free-range farm hens, tray of 12.',                        'pack', '12 pcs',  90.00,  80.00, 1, '/images/products/eggs.jpg',           60, 10);

-- Cooking Oils (3 products)
INSERT INTO #SeedProducts VALUES
(@CatOil, 'OIL-SUN-001', 'Sunflower Oil',         'sunflower-oil-1l',     'Refined sunflower oil, light and healthy for daily cooking.',              'litre','1 Litre', 145.00,130.00,0, '/images/products/sunflower-oil.jpg',  50, 8),
(@CatOil, 'OIL-MUS-001', 'Mustard Oil',           'mustard-oil-1l',       'Kachi ghani mustard oil — pungent and flavourful.',                        'litre','1 Litre', 185.00,NULL,  0, '/images/products/mustard-oil.jpg',    40, 6),
(@CatOil, 'OIL-GHE-001', 'Desi Cow Ghee',         'desi-cow-ghee-500g',   'Pure clarified butter made from A2 cow milk.',                             'g',    '500 g',   450.00,420.00,1, '/images/products/ghee.jpg',           30, 5);

-- Spices and Masalas (4 products)
INSERT INTO #SeedProducts VALUES
(@CatSpice, 'SPI-TUR-001', 'Turmeric Powder',     'turmeric-powder-100g', 'Pure haldi powder with rich colour and earthy aroma.',                     'g',   '100 g',   45.00,  40.00, 0, '/images/products/turmeric.jpg',       80, 10),
(@CatSpice, 'SPI-RED-001', 'Red Chilli Powder',   'red-chilli-powder-100g','Mild to medium heat red chilli powder, sun-dried.',                        'g',   '100 g',   55.00,  NULL,  0, '/images/products/red-chilli-powder.jpg',60,8),
(@CatSpice, 'SPI-GAR-001', 'Garam Masala',        'garam-masala-100g',    'Classic North Indian spice blend, freshly ground.',                        'g',   '100 g',   75.00,  65.00, 1, '/images/products/garam-masala.jpg',   70, 10),
(@CatSpice, 'SPI-CUM-001', 'Cumin Seeds (Jeera)', 'cumin-seeds-100g',     'Whole cumin seeds, aromatic and fresh.',                                   'g',   '100 g',   60.00,  NULL,  0, '/images/products/cumin-seeds.jpg',    50, 8);

-- Snacks and Packaged Foods (2 products)
INSERT INTO #SeedProducts VALUES
(@CatSnack, 'SNK-BIS-001', 'Britannia Good Day Butter Biscuits','good-day-butter-biscuits','Classic butter-rich biscuits, a teatime favourite.',     'pack','200 g',   45.00,  40.00, 0, '/images/products/good-day-biscuits.jpg',50,8),
(@CatSnack, 'SNK-NOO-001', 'Maggi 2-Minute Noodles (Pack of 4)','maggi-noodles-pack-4',   '4-pack of Masala Maggi noodles, ready in minutes.',       'pack','4 × 70 g',80.00, 72.00, 1, '/images/products/maggi.jpg',          60, 10);

-- Beverages (3 products)
INSERT INTO #SeedProducts VALUES
(@CatBev, 'BEV-TEA-001', 'Brooke Bond Red Label Tea', 'brooke-bond-red-label-500g','Rich and refreshing CTC tea blend.',                              'g',   '500 g',  215.00, 199.00,1, '/images/products/red-label-tea.jpg',  40, 6),
(@CatBev, 'BEV-COF-001', 'Bru Instant Coffee',        'bru-instant-coffee-200g',  'South Indian-inspired instant coffee blend.',                      'g',   '200 g',  285.00, NULL,  0, '/images/products/bru-coffee.jpg',     30, 5),
(@CatBev, 'BEV-JUI-001', 'Real Mango Juice',          'real-mango-juice-1l',       'Mixed-fruit mango juice drink with no added preservatives.',      'litre','1 Litre',110.00, 99.00, 0, '/images/products/mango-juice.jpg',    35, 6);

-- Household Essentials (3 products)
INSERT INTO #SeedProducts VALUES
(@CatHouse, 'HOU-DET-001', 'Surf Excel Easy Wash Detergent','surf-excel-easy-wash-1kg','Tough stain removal in less water.',                           'kg',  '1 kg',   175.00, 159.00,1, '/images/products/surf-excel.jpg',     50, 8),
(@CatHouse, 'HOU-DIS-001', 'Vim Dishwash Bar',              'vim-dishwash-bar-250g',  'Effective grease remover for utensils.',                         'g',   '250 g',   40.00,  NULL,  0, '/images/products/vim-bar.jpg',        60, 10),
(@CatHouse, 'HOU-SOP-001', 'Dettol Original Soap',          'dettol-original-soap-75g','Antibacterial soap for everyday hand hygiene.',                 'g',   '75 g',    45.00,  40.00, 0, '/images/products/dettol-soap.jpg',    80, 15);

-- -----------------------------------------------------------------------
-- Upsert Products
-- -----------------------------------------------------------------------
MERGE Products AS target
USING (SELECT * FROM #SeedProducts) AS source
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
    VALUES (source.CategoryId, source.SKU, source.Name, source.Slug, source.Description, source.UnitCode, source.UnitLabel, source.Price, source.OfferPrice, source.IsFeatured, 1, source.ImagePath, SYSUTCDATETIME(), SYSUTCDATETIME());

-- -----------------------------------------------------------------------
-- Upsert Inventory for new products (never reduce existing stock to 0)
-- -----------------------------------------------------------------------
MERGE Inventory AS target
USING (
    SELECT p.Id AS ProductId, sp.Stock AS QuantityOnHand, sp.LowStock AS LowStockThreshold
    FROM #SeedProducts sp
    JOIN Products p ON p.SKU = sp.SKU
) AS source
ON target.ProductId = source.ProductId
WHEN NOT MATCHED THEN
    INSERT (ProductId, QuantityOnHand, LowStockThreshold, ReservedQuantity, UpdatedAt)
    VALUES (source.ProductId, source.QuantityOnHand, source.LowStockThreshold, 0, SYSUTCDATETIME());

DROP TABLE #SeedProducts;

COMMIT TRANSACTION;

PRINT '34 demo products seeded successfully across 10 categories.';
PRINT 'NOTE: All prices are DEMO DATA only and must be verified before launch.';
GO
