# Build script — ensures user-local .NET 9 SDK is on PATH
$dotnetRoot = Join-Path $env:USERPROFILE ".dotnet"
if (Test-Path "$dotnetRoot\dotnet.exe") {
    $env:PATH = "$dotnetRoot;$dotnetRoot\tools;$env:PATH"
}

Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    Write-Host "dotnet version:" (& dotnet --version)
    dotnet restore Didibood.LocationAccess.sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build Didibood.LocationAccess.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet test tests\Didibood.LocationAccess.Tests\Didibood.LocationAccess.Tests.csproj -c Release --no-build
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
