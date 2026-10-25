param(
    [string]$BaseUrl = 'https://localhost:7000/api/v1',
    [switch]$WithDatabaseFixtures,
    [string]$ConnectionString = 'Server=.\SQLEXPRESS;Database=RenTrekDb;Integrated Security=True;TrustServerCertificate=True'
)

$ErrorActionPreference = 'Stop'
$script:checks = 0

function Assert-True($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}

function Get-Catalog([string]$Path) {
    $response = Invoke-RestMethod "$BaseUrl/products$Path"
    Assert-True ($response.success -and $response.statusCode -eq 200) "Invalid success wrapper: $Path"
    return $response.data
}

function Assert-Error([string]$Path, [int]$Status) {
    try {
        Invoke-WebRequest "$BaseUrl/products$Path" -UseBasicParsing | Out-Null
        throw "Expected HTTP $Status for $Path"
    } catch {
        if (-not $_.Exception.Response) { throw }
        Assert-True ([int]$_.Exception.Response.StatusCode -eq $Status) "Wrong status for $Path"
        $errorBody = $_.ErrorDetails.Message
        if (-not $errorBody) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            try { $errorBody = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        $body = $errorBody | ConvertFrom-Json
        Assert-True (-not $body.success -and $body.statusCode -eq $Status) "Invalid error wrapper for $Path"
        if ($Status -eq 400) { Assert-True ($null -ne $body.errors) "Missing field errors for $Path" }
    }
}

function Assert-Sorted($Items, [string]$Property, [bool]$Descending) {
    $hasNull = $false
    $previous = $null
    foreach ($product in $Items) {
        $value = $product.$Property
        if ($null -eq $value) { $hasNull = $true; continue }
        Assert-True (-not $hasNull) "$Property nulls must be last"
        if ($null -ne $previous) {
            $ordered = if ($Descending) { $previous -ge $value } else { $previous -le $value }
            Assert-True $ordered "$Property is not sorted correctly"
        }
        $previous = $value
    }
}

$products = Get-Catalog '?pageSize=100'
Assert-True ($products.items.Count -gt 0) 'Seed a local catalog before running this smoke test.'
Assert-True ($products.items[0].createdAt.EndsWith('Z')) 'Product timestamps must be UTC'
$first = Get-Catalog '?pageSize=1&pageNumber=1'
$second = Get-Catalog '?pageSize=1&pageNumber=2'
Assert-True ($first.items[0].id -ne $second.items[0].id) 'Pages overlap'
Assert-True ($first.hasNext -and $second.hasPrevious) 'Pagination flags are incorrect'
$emptyPage = Get-Catalog '?pageNumber=1000000'
Assert-True ($emptyPage.items.Count -eq 0) 'Out-of-range page should be empty'
$emptySearch = Get-Catalog '?search=__no_product_should_match_this__'
Assert-True ($emptySearch.totalItems -eq 0) 'Unmatched search must return an empty page'
$name = $products.items[0].name
$search = Get-Catalog ('?search=' + [Uri]::EscapeDataString("  $name  "))
Assert-True ($search.items.id -contains $products.items[0].id) 'Trimmed search did not find product'

foreach ($type in @('Rental', 'Sale', 'Both')) {
    $filtered = Get-Catalog "?offeringType=$type&pageSize=100"
    foreach ($product in $filtered.items) {
        if ($type -in @('Rental', 'Both')) { Assert-True $product.isRentable 'Rental filter leaked sale-only item' }
        if ($type -in @('Sale', 'Both')) { Assert-True $product.isForSale 'Sale filter leaked rental-only item' }
    }
}
foreach ($property in @('pricePerDay', 'salePrice', 'createdAt')) {
    foreach ($order in @('asc', 'desc')) {
        $sorted = Get-Catalog "?sortBy=$property&sortOrder=$order&pageSize=100"
        Assert-Sorted $sorted.items $property ($order -eq 'desc')
    }
}
$categories = Get-Catalog '/categories'
Assert-True ($categories.Count -gt 0) 'No public product categories'
foreach ($category in $categories) {
    $filtered = Get-Catalog "?categoryId=$($category.id)&pageSize=100"
    Assert-True ($filtered.totalItems -gt 0) 'Category without public products'
    foreach ($product in $filtered.items) { Assert-True ($product.categoryId -eq $category.id) 'Category filter leaked another category' }
}
$detail = Get-Catalog "/$($products.items[0].id)"
Assert-True ($detail.variants.Count -gt 0) 'Detail has no variants'
$tiers = @($detail.variants | ForEach-Object { $_.rentalPriceTiers } | Where-Object { $null -ne $_ })
if ($tiers.Count -gt 0) {
    Assert-True ($detail.pricePerDay -eq ($tiers.dailyPrice | Measure-Object -Minimum).Minimum) 'List rental price differs from detail tiers'
}
Assert-Error '/9223372036854775807' 404
Assert-Error '/0' 404
Assert-Error '/abc' 400
Assert-Error '?sortBy=unknown' 400
Assert-Error '?sortOrder=unknown' 400
Assert-Error '?offeringType=unknown' 400
Assert-Error '?offeringType=999' 400
Assert-Error '?categoryId=-1' 400
Assert-Error '?pageNumber=2147483647' 400
Assert-Error ('?search=' + ('a' * 201)) 400

if ($WithDatabaseFixtures) {
    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    $categoryId = $null
    $marker = 'catalog-test-' + [Guid]::NewGuid().ToString('N')
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = @'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
INSERT INTO Categories (Name, Status, CreatedAt, IsDeleted) VALUES (@marker, 'Active', SYSUTCDATETIME(), 0);
DECLARE @category bigint = SCOPE_IDENTITY();
INSERT INTO Products (CategoryId, Name, Status, CreatedAt, IsDeleted) VALUES (@category, @marker, 'Active', SYSUTCDATETIME(), 0);
DECLARE @product bigint = SCOPE_IDENTITY();
INSERT INTO ProductVariants (ProductId, Sku, Name, SalePrice, Status, CreatedAt, IsDeleted)
VALUES (@product, @marker + '-visible', 'Visible', 0, 'Active', SYSUTCDATETIME(), 0),
       (@product, @marker + '-inactive', 'Inactive', 1, 'Inactive', SYSUTCDATETIME(), 0),
       (@product, @marker + '-deleted', 'Deleted', 1, 'Active', SYSUTCDATETIME(), 1),
       (@product, @marker + '-unpriced', 'Unpriced', NULL, 'Active', SYSUTCDATETIME(), 0);
DECLARE @variant bigint = (SELECT Id FROM ProductVariants WHERE Sku = @marker + '-visible');
INSERT INTO RentalPriceTiers (ProductVariantId, MinDays, DailyPrice, CreatedAt, IsDeleted)
VALUES (@variant, 1, 0, SYSUTCDATETIME(), 0), (@variant, 2, 999, SYSUTCDATETIME(), 1);
INSERT INTO ProductVariantImages (ProductVariantId, ImageUrl, DisplayOrder, IsPrimary, CreatedAt, IsDeleted)
VALUES (@variant, '/visible.svg', 0, 1, SYSUTCDATETIME(), 0), (@variant, '/deleted.svg', 0, 1, SYSUTCDATETIME(), 1);
COMMIT TRANSACTION;
SELECT @category;
'@
        $command.Parameters.AddWithValue('@marker', $marker) | Out-Null
        $categoryId = [long]$command.ExecuteScalar()
        $fixture = Get-Catalog "?categoryId=$categoryId"
        $productId = $fixture.items[0].id
        Assert-True ($fixture.totalItems -eq 1) 'Fixture product not found'
        Assert-True ($fixture.items[0].isRentable -and $fixture.items[0].isForSale) 'Zero prices must remain valid offerings'
        $fixtureDetail = Get-Catalog "/$productId"
        Assert-True ($fixtureDetail.variants.Count -eq 1) 'Hidden or unpriced variants leaked'
        Assert-True ($fixtureDetail.variants[0].images.Count -eq 1) 'Deleted image leaked'
        Assert-True ($fixtureDetail.variants[0].rentalPriceTiers.Count -eq 1) 'Deleted price tier leaked'

        $command.Parameters.Clear()
        $command.Parameters.AddWithValue('@categoryId', $categoryId) | Out-Null
        foreach ($sql in @(
            "UPDATE Products SET Status='Inactive' WHERE CategoryId=@categoryId",
            "UPDATE Products SET Status='Active', IsDeleted=1 WHERE CategoryId=@categoryId",
            "UPDATE Products SET IsDeleted=0 WHERE CategoryId=@categoryId; UPDATE Categories SET Status='Inactive' WHERE Id=@categoryId",
            "UPDATE Categories SET Status='Active', IsDeleted=1 WHERE Id=@categoryId"
        )) {
            $command.CommandText = $sql
            $command.ExecuteNonQuery() | Out-Null
            $hidden = Get-Catalog "?categoryId=$categoryId"
            Assert-True ($hidden.totalItems -eq 0) 'Hidden product/category leaked into list'
            Assert-Error "/$productId" 404
            $publicCategories = Get-Catalog '/categories'
            Assert-True ($publicCategories.id -notcontains $categoryId) 'Hidden category leaked'
        }
    } finally {
        if ($categoryId) {
            $command.CommandText = @'
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DELETE i FROM ProductVariantImages i JOIN ProductVariants v ON v.Id=i.ProductVariantId JOIN Products p ON p.Id=v.ProductId WHERE p.CategoryId=@categoryId;
DELETE t FROM RentalPriceTiers t JOIN ProductVariants v ON v.Id=t.ProductVariantId JOIN Products p ON p.Id=v.ProductId WHERE p.CategoryId=@categoryId;
DELETE v FROM ProductVariants v JOIN Products p ON p.Id=v.ProductId WHERE p.CategoryId=@categoryId;
DELETE FROM Products WHERE CategoryId=@categoryId;
DELETE FROM Categories WHERE Id=@categoryId;
COMMIT TRANSACTION;
'@
            $command.ExecuteNonQuery() | Out-Null
        }
        $connection.Dispose()
    }
}

Write-Output "PASS: $script:checks product catalog checks."
