# Rgt.Space Renaming Script
$ErrorActionPreference = "Stop"

Write-Host "Rgt.Space Namespace Renaming Tool" -ForegroundColor Cyan

# Define replacements
$replacements = @{
    "namespace MicroservicesBase.API" = "namespace Rgt.Space.API"
    "namespace MicroservicesBase.Core" = "namespace Rgt.Space.Core"
    "namespace MicroservicesBase.Infrastructure" = "namespace Rgt.Space.Infrastructure"
    "namespace MicroservicesBase.Schedulers" = "namespace Rgt.Space.Schedulers"
    
    "using MicroservicesBase.API" = "using Rgt.Space.API"
    "using MicroservicesBase.Core" = "using Rgt.Space.Core"
    "using MicroservicesBase.Infrastructure" = "using Rgt.Space.Infrastructure"
    "using MicroservicesBase.Schedulers" = "using Rgt.Space.Schedulers"
}

# Get all .cs files
$csFiles = Get-ChildItem -Path . -Recurse -Filter *.cs | Where-Object {
    $_.FullName -notlike "*\obj\*" -and 
    $_.FullName -notlike "*\bin\*" -and
    $_.FullName -notlike "*\.git\*"
}

Write-Host "Found $($csFiles.Count) C# files to process"

$totalReplacements = 0

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $fileModified = $false
    
    foreach ($old in $replacements.Keys) {
        $new = $replacements[$old]
        if ($content -match [regex]::Escape($old)) {
            $content = $content -replace [regex]::Escape($old), $new
            $fileModified = $true
        }
    }
    
    if ($fileModified) {
        Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
        Write-Host "Updated: $($file.Name)" -ForegroundColor Green
        $totalReplacements++
    }
}

Write-Host "Complete! Modified $totalReplacements files" -ForegroundColor Green
