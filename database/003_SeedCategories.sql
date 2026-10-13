-- =============================================================================
-- GOLIS Mart — 003_SeedCategories.sql
-- Seeds the 10 product categories.
-- Safe to rerun: uses MERGE to upsert by Slug.
-- Execute against: GOLISMartDb after 001_CreateSchema.sql
-- =============================================================================

USE GOLISMartDb;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

MERGE Categories AS target
USING (VALUES
    ('Fresh Vegetables',         'fresh-vegetables',       'Farm-fresh vegetables delivered daily.',           '/images/categories/fresh-vegetables.jpg',   1),
    ('Fresh Fruits',             'fresh-fruits',           'Seasonal and exotic fruits sourced locally.',      '/images/categories/fresh-fruits.jpg',         2),
    ('Rice and Atta',            'rice-and-atta',          'Basmati, sona masoori, multi-grain atta and more.','/images/categories/rice-and-atta.jpg',        3),
    ('Dal and Pulses',           'dal-and-pulses',         'Toor dal, moong dal, chana dal and more.',         '/images/categories/dal-and-pulses.jpg',       4),
    ('Dairy and Eggs',           'dairy-and-eggs',         'Fresh milk, curd, paneer, butter and eggs.',       '/images/categories/dairy-and-eggs.jpg',       5),
    ('Cooking Oils',             'cooking-oils',           'Refined, cold-pressed and speciality oils.',       '/images/categories/cooking-oils.jpg',         6),
    ('Spices and Masalas',       'spices-and-masalas',     'Whole spices, ground masalas and blends.',         '/images/categories/spices-and-masalas.jpg',   7),
    ('Snacks and Packaged Foods','snacks-packaged-foods',  'Namkeen, biscuits, noodles and ready-to-eat.',    '/images/categories/snacks-packaged-foods.jpg', 8),
    ('Beverages',                'beverages',              'Tea, coffee, juices and cold drinks.',             '/images/categories/beverages.jpg',             9),
    ('Household Essentials',     'household-essentials',   'Cleaning, hygiene and daily-use household items.','/images/categories/household-essentials.jpg',  10)
) AS source (Name, Slug, Description, ImagePath, SortOrder)
ON target.Slug = source.Slug
WHEN MATCHED THEN
    UPDATE SET
        Name        = source.Name,
        Description = source.Description,
        ImagePath   = source.ImagePath,
        SortOrder   = source.SortOrder,
        IsActive    = 1,
        UpdatedAt   = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Name, Slug, Description, ImagePath, SortOrder, IsActive, CreatedAt, UpdatedAt)
    VALUES (source.Name, source.Slug, source.Description, source.ImagePath, source.SortOrder, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

COMMIT TRANSACTION;

PRINT '10 categories seeded successfully.';
GO
