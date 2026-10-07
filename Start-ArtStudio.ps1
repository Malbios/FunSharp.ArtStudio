#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes the current code into a separate folder and starts Art Studio from there.

.DESCRIPTION
    The running app uses its own copy, so builds and tests in the repository never lock its files.
    To pick up new code, stop the app (Ctrl+C) and run this script again.
#>
$ErrorActionPreference = 'Stop'

$Url = 'http://localhost:5180'
$AppDirectory = Join-Path $env:LOCALAPPDATA 'ArtStudio\app'
$Project = Join-Path $PSScriptRoot 'src\ArtStudio.Server\ArtStudio.Server.csproj'

if (Get-NetTCPConnection -LocalPort 5180 -State Listen -ErrorAction SilentlyContinue) {
    throw "Something is already running on $Url. Stop the running Art Studio (Ctrl+C in its window) first."
}

Write-Host "Publishing Art Studio to $AppDirectory ..."
dotnet publish $Project --configuration Release --output $AppDirectory
if ($LASTEXITCODE -ne 0) {
    throw 'Publishing Art Studio failed. See the build output above.'
}

Write-Host "Starting Art Studio on $Url (Ctrl+C to stop)"
Push-Location $AppDirectory
try {
    & (Join-Path $AppDirectory 'ArtStudio.Server.exe') --urls $Url
}
finally {
    Pop-Location
}
