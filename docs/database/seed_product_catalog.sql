-- Optional local demo catalog. Apply AddProductCatalog migration first.
-- Does not touch auth or order data, and does not append to an existing catalog.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.Products WITH (UPDLOCK, HOLDLOCK))
BEGIN
    PRINT 'Products already exist; demo seed skipped.';
    COMMIT TRANSACTION;
    RETURN;
END;

DECLARE @now datetime2 = SYSUTCDATETIME();
DECLARE @catalog TABLE (
    Name nvarchar(200), Description nvarchar(max), CategoryName nvarchar(150),
    Sku nvarchar(100), VariantName nvarchar(200), Size nvarchar(50), Color nvarchar(50),
    DailyPrice decimal(18,0), SalePrice decimal(18,0), ImageUrl nvarchar(2048)
);

INSERT INTO @catalog VALUES
(N'Lều cắm trại 2 người', N'Lều hai lớp gọn nhẹ, chống mưa nhẹ và có cửa lưới thông gió.\nPhù hợp cho chuyến cắm trại cuối tuần của hai người.', N'Lều cắm trại', 'TENT-2-GREEN', N'2 người / xanh lá', N'2 người', N'Xanh lá', 120000, 1800000, '/images/products/tent.svg'),
(N'Lều gia đình 4 người', N'Không gian rộng cho gia đình hoặc nhóm bạn. Có mái che và túi đựng phụ kiện.', N'Lều cắm trại', 'TENT-4', N'4 người', N'4 người', N'Cam đất', 200000, NULL, '/images/products/tent.svg'),
(N'Lều trekking siêu nhẹ', N'Lều nhỏ gọn dành cho người thích di chuyển. Dễ đóng gói trong balô.', N'Lều cắm trại', 'TENT-LIGHT', N'1 người', N'1 người', N'Xanh rêu', NULL, 2450000, '/images/products/tent.svg'),
(N'Balô trekking 40L', N'Balô có đai hông, dây ngực và ngăn đựng bình nước. Phù hợp hành trình 1–2 ngày.', N'Balô & phụ kiện', 'PACK-40', N'40 lít', N'40L', N'Xanh lá', 60000, 950000, '/images/products/backpack.svg'),
(N'Balô trekking 60L', N'Sức chứa lớn, nhiều ngăn và dây siết hành lý cho chuyến đi dài ngày.', N'Balô & phụ kiện', 'PACK-60', N'60 lít', N'60L', N'Xanh rêu', 85000, NULL, '/images/products/backpack.svg'),
(N'Balô đi bộ 20L', N'Balô nhỏ cho hành trình trong ngày, có ngăn phụ dễ tiếp cận.', N'Balô & phụ kiện', 'PACK-20', N'20 lít', N'20L', N'Cam đất', NULL, 450000, '/images/products/backpack.svg'),
(N'Túi ngủ giữ ấm', N'Túi ngủ mềm và gọn, kèm túi nén. Dùng cùng đệm cách nhiệt khi trời lạnh.', N'Túi ngủ & đệm', 'SLEEP-WARM', N'Tiêu chuẩn', N'210 × 75 cm', N'Xanh rêu', 50000, 720000, '/images/products/sleeping-bag.svg'),
(N'Đệm ngủ tự bơm', N'Đệm cách nhiệt gọn nhẹ, tạo bề mặt nghỉ ngơi êm hơn khi cắm trại.', N'Túi ngủ & đệm', 'MAT-AIR', N'Một người', N'185 × 55 cm', N'Xanh lá', 40000, NULL, '/images/products/sleeping-bag.svg'),
(N'Túi ngủ mùa hè', N'Túi ngủ mỏng, dễ giặt và đóng gói cho thời tiết ấm.', N'Túi ngủ & đệm', 'SLEEP-SUMMER', N'Tiêu chuẩn', N'200 × 70 cm', N'Cam đất', NULL, 380000, '/images/products/sleeping-bag.svg'),
(N'Đèn lều sạc USB', N'Đèn treo có nhiều mức sáng, cổng sạc USB và tay cầm tiện mang theo.', N'Đèn & dụng cụ', 'LIGHT-USB', N'Sạc USB', NULL, N'Xanh rêu', 30000, 280000, '/images/products/lantern.svg'),
(N'Đèn cắm trại nhóm', N'Đèn chiếu sáng khu vực sinh hoạt chung, có tay xách và chân đế chắc chắn.', N'Đèn & dụng cụ', 'LIGHT-GROUP', N'Cỡ lớn', NULL, N'Xanh lá', 60000, NULL, '/images/products/lantern.svg'),
(N'Đèn pin dã ngoại', N'Đèn cầm tay nhỏ gọn, thuận tiện cho sinh hoạt ngoài trời buổi tối.', N'Đèn & dụng cụ', 'LIGHT-HAND', N'Cầm tay', NULL, N'Cam đất', NULL, 190000, '/images/products/lantern.svg');

INSERT INTO dbo.Categories (Name, Status, CreatedAt, IsDeleted)
SELECT DISTINCT catalog.CategoryName, 'Active', @now, 0 FROM @catalog catalog
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories category WHERE category.Name = catalog.CategoryName AND category.IsDeleted = 0);

INSERT INTO dbo.Products (CategoryId, Name, Description, Status, CreatedAt, IsDeleted)
SELECT category.Id, catalog.Name, REPLACE(catalog.Description, N'\n', NCHAR(10)), 'Active', @now, 0
FROM @catalog catalog
CROSS APPLY (SELECT TOP (1) Id FROM dbo.Categories WHERE Name = catalog.CategoryName AND IsDeleted = 0 ORDER BY Id) category;

INSERT INTO dbo.ProductVariants (ProductId, Sku, Name, Size, Color, SalePrice, Status, CreatedAt, IsDeleted)
SELECT product.Id, catalog.Sku, catalog.VariantName, catalog.Size, catalog.Color, catalog.SalePrice, 'Active', @now, 0
FROM @catalog catalog JOIN dbo.Products product ON product.Name = catalog.Name;

INSERT INTO dbo.ProductVariantImages (ProductVariantId, ImageUrl, DisplayOrder, IsPrimary, CreatedAt, IsDeleted)
SELECT variant.Id, catalog.ImageUrl, 0, 1, @now, 0
FROM @catalog catalog JOIN dbo.ProductVariants variant ON variant.Sku = catalog.Sku;

INSERT INTO dbo.RentalPriceTiers (ProductVariantId, MinDays, MaxDays, DailyPrice, CreatedAt, IsDeleted)
SELECT variant.Id, 1, 3, catalog.DailyPrice, @now, 0
FROM @catalog catalog JOIN dbo.ProductVariants variant ON variant.Sku = catalog.Sku WHERE catalog.DailyPrice IS NOT NULL
UNION ALL
SELECT variant.Id, 4, NULL, catalog.DailyPrice * 0.75, @now, 0
FROM @catalog catalog JOIN dbo.ProductVariants variant ON variant.Sku = catalog.Sku WHERE catalog.DailyPrice IS NOT NULL;

-- A second variant and gallery let the detail page demonstrate selection.
INSERT INTO dbo.ProductVariants (ProductId, Sku, Name, Size, Color, SalePrice, Status, CreatedAt, IsDeleted)
SELECT Id, 'TENT-2-SAND', N'2 người / màu cát', N'2 người', N'Màu cát', 1950000, 'Active', @now, 0
FROM dbo.Products WHERE Name = N'Lều cắm trại 2 người';
INSERT INTO dbo.RentalPriceTiers (ProductVariantId, MinDays, MaxDays, DailyPrice, CreatedAt, IsDeleted)
SELECT Id, 1, NULL, 130000, @now, 0 FROM dbo.ProductVariants WHERE Sku = 'TENT-2-SAND';
INSERT INTO dbo.ProductVariantImages (ProductVariantId, ImageUrl, DisplayOrder, IsPrimary, CreatedAt, IsDeleted)
SELECT Id, '/images/products/tent-sand.svg', 0, 1, @now, 0 FROM dbo.ProductVariants WHERE Sku = 'TENT-2-SAND'
UNION ALL
SELECT Id, '/images/products/tent-sand.svg', 1, 0, @now, 0 FROM dbo.ProductVariants WHERE Sku = 'TENT-2-GREEN';

COMMIT TRANSACTION;
PRINT 'Created 12 demo products with rental, sale, and combined offerings.';
