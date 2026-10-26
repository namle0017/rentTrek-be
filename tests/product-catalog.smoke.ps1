# Smoke test API product catalog trên môi trường đang chạy.
# Cần dữ liệu mẫu: chạy API ở Development lần đầu với DB trống để TourSeedData tạo thiết bị + giá thuê.
param(
    [string]$BaseUrl = 'https://localhost:7000/api/v1'
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

function Get-ErrorResponse([string]$Path) {
    try {
        Invoke-WebRequest "$BaseUrl/products$Path" -UseBasicParsing | Out-Null
        throw "Expected an error for $Path"
    } catch {
        if (-not $_.Exception.Response) { throw }
        $body = $_.ErrorDetails.Message
        if (-not $body) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        return @{ Status = [int]$_.Exception.Response.StatusCode; Body = $body }
    }
}

function Assert-Error([string]$Path, [int]$Status) {
    $result = Get-ErrorResponse $Path
    Assert-True ($result.Status -eq $Status) "Wrong status for $Path"
    $body = $result.Body | ConvertFrom-Json
    Assert-True (-not $body.success -and $body.statusCode -eq $Status) "Invalid error wrapper for $Path"
    if ($Status -eq 400) { Assert-True ($null -ne $body.errors) "Missing field errors for $Path" }
}

# Route {id:guid} không khớp nên ASP.NET trả 404 không có body ApiResponse
function Assert-RouteNotFound([string]$Path) {
    $result = Get-ErrorResponse $Path
    Assert-True ($result.Status -eq 404) "Expected 404 for $Path"
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
Assert-True ($products.items.Count -gt 1) 'Need at least 2 products; start the API once against an empty DB to seed.'
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
foreach ($product in $products.items) { Assert-True $product.isRentable 'Public product without rental price' }

foreach ($property in @('pricePerDay', 'createdAt')) {
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
Assert-True ($tiers.Count -gt 0) 'Detail has no rental price tiers'
Assert-True ($detail.pricePerDay -eq ($tiers.dailyPrice | Measure-Object -Minimum).Minimum) 'List rental price differs from detail tiers'

Assert-Error '/00000000-0000-0000-0000-000000000001' 404
Assert-RouteNotFound '/abc'
Assert-Error '?sortBy=unknown' 400
Assert-Error '?sortBy=salePrice' 400
Assert-Error '?sortOrder=unknown' 400
Assert-Error '?categoryId=not-a-guid' 400
Assert-Error '?pageNumber=2147483647' 400
Assert-Error ('?search=' + ('a' * 201)) 400

Write-Host "Product catalog smoke test passed ($script:checks checks)."
