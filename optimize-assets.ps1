# Imperial VIP - Asset Optimization Script
# Bu script CSS ve JS dosyalarını optimize eder ve sıkıştırır

Write-Host "🚀 Imperial VIP Asset Optimization Started..." -ForegroundColor Green

# CSS Optimization
Write-Host "`n📦 Optimizing CSS files..." -ForegroundColor Cyan

$cssFiles = Get-ChildItem -Path "wwwroot/css" -Filter "*.css" -Recurse | Where-Object { $_.Name -notlike "*.min.css" }

foreach ($file in $cssFiles) {
    Write-Host "  - Processing: $($file.Name)" -ForegroundColor Yellow
    
    $content = Get-Content $file.FullName -Raw
    
    # Remove comments
    $content = $content -replace '/\*[\s\S]*?\*/', ''
    
    # Remove extra whitespace
    $content = $content -replace '\s+', ' '
    $content = $content -replace '\s*{\s*', '{'
    $content = $content -replace '\s*}\s*', '}'
    $content = $content -replace '\s*:\s*', ':'
    $content = $content -replace '\s*;\s*', ';'
    $content = $content -replace '\s*,\s*', ','
    
    # Remove last semicolon in blocks
    $content = $content -replace ';}', '}'
    
    # Create minified version
    $minFile = $file.FullName -replace '\.css$', '.min.css'
    Set-Content -Path $minFile -Value $content.Trim()
    
    $originalSize = (Get-Item $file.FullName).Length
    $minifiedSize = (Get-Item $minFile).Length
    $savings = [math]::Round((($originalSize - $minifiedSize) / $originalSize) * 100, 2)
    
    Write-Host "    ✓ Saved $savings% ($(($originalSize - $minifiedSize) / 1KB) KB)" -ForegroundColor Green
}

# JS Optimization
Write-Host "`n📦 Optimizing JS files..." -ForegroundColor Cyan

$jsFiles = Get-ChildItem -Path "wwwroot/js" -Filter "*.js" -Recurse | Where-Object { $_.Name -notlike "*.min.js" }

foreach ($file in $jsFiles) {
    Write-Host "  - Processing: $($file.Name)" -ForegroundColor Yellow
    
    $content = Get-Content $file.FullName -Raw
    
    # Basic minification
    # Remove single-line comments (but keep URLs)
    $content = $content -replace '(?<!:)//[^\n]*', ''
    
    # Remove multi-line comments
    $content = $content -replace '/\*[\s\S]*?\*/', ''
    
    # Remove extra whitespace
    $content = $content -replace '\s+', ' '
    $content = $content -replace '\s*{\s*', '{'
    $content = $content -replace '\s*}\s*', '}'
    $content = $content -replace '\s*\(\s*', '('
    $content = $content -replace '\s*\)\s*', ')'
    $content = $content -replace '\s*;\s*', ';'
    $content = $content -replace '\s*,\s*', ','
    $content = $content -replace '\s*=\s*', '='
    
    # Create minified version
    $minFile = $file.FullName -replace '\.js$', '.min.js'
    Set-Content -Path $minFile -Value $content.Trim()
    
    $originalSize = (Get-Item $file.FullName).Length
    $minifiedSize = (Get-Item $minFile).Length
    $savings = [math]::Round((($originalSize - $minifiedSize) / $originalSize) * 100, 2)
    
    Write-Host "    ✓ Saved $savings% ($(($originalSize - $minifiedSize) / 1KB) KB)" -ForegroundColor Green
}

Write-Host "`n✅ Asset Optimization Complete!" -ForegroundColor Green
Write-Host "`n💡 Tip: Production'da .min.css ve .min.js dosyalarını kullanın." -ForegroundColor Yellow
Write-Host "💡 Tip: Gzip compression zaten aktif (Program.cs'de)." -ForegroundColor Yellow

# Image Optimization Info
Write-Host "`n📸 Image Optimization Tips:" -ForegroundColor Cyan
Write-Host "  - WebP formatını kullanın (70% daha küçük)" -ForegroundColor White
Write-Host "  - Lazy loading aktif (performance-boost.js)" -ForegroundColor White
Write-Host "  - Büyük görselleri 1920px'den küçük tutun" -ForegroundColor White
Write-Host "  - Thumbnails için 400x300 boyutunu kullanın" -ForegroundColor White
