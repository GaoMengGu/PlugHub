param(
    [string]$Configuration = "Release",
    [string]$RevitApiDir = "",
    [switch]$UseRevitApiNuGet,
    [string]$RevitApiNuGetVersion = "",
    [string]$OutputDir = "$PSScriptRoot\..\dist\Revit2020",
    [switch]$InstallAddin
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path "$PSScriptRoot\.."
$Project = Join-Path $Root "src\PlugHub.Revit2020\PlugHub.Revit2020.csproj"

function Test-RevitApiDir {
    param([string]$Path)
    return ![string]::IsNullOrWhiteSpace($Path) `
        -and (Test-Path (Join-Path $Path "RevitAPI.dll")) `
        -and (Test-Path (Join-Path $Path "RevitAPIUI.dll"))
}

$candidateApiDirs = @(
    $RevitApiDir,
    "D:\Program Files\Autodesk\Revit 2020",
    "D:\Program Files\Autodesk\Revit",
    "C:\Program Files\Autodesk\Revit 2020",
    "C:\Program Files\Autodesk\Revit"
)

$resolvedApiDir = ""
if (!$UseRevitApiNuGet) {
    $resolvedApiDir = $candidateApiDirs | Where-Object { Test-RevitApiDir $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($resolvedApiDir)) {
        throw "Revit 2020 API DLLs were not found. Pass -RevitApiDir with a folder containing RevitAPI.dll and RevitAPIUI.dll, or pass -UseRevitApiNuGet for CI compile references."
    }
    $RevitApiDir = (Resolve-Path $resolvedApiDir).Path
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path

$RemovedModuleName = "PlugHub." + "Sample" + "Module"
$StaleOutputPaths = @(
    (Join-Path $OutputDir ($RemovedModuleName + ".dll")),
    (Join-Path $OutputDir ($RemovedModuleName + ".pdb")),
    (Join-Path $OutputDir "PlugHub.BuiltinModule.dll"),
    (Join-Path $OutputDir "PlugHub.BuiltinModule.pdb"),
    (Join-Path $OutputDir "config\modules.json"),
    (Join-Path $OutputDir "config\plugin-sources.json"),
    (Join-Path $OutputDir ("modules\" + "samples")),
    (Join-Path $OutputDir ("modules\" + "dropins")),
    (Join-Path $OutputDir "modules")
)
foreach ($StaleOutputPath in $StaleOutputPaths) {
    if (Test-Path $StaleOutputPath) {
        Remove-Item -LiteralPath $StaleOutputPath -Recurse -Force
    }
}

$buildArguments = @(
    "build",
    $Project,
    "-c",
    $Configuration,
    "/p:RevitVersion=2020",
    "/p:PlugHubOutputDir=$OutputDir"
)

if ($UseRevitApiNuGet) {
    $buildArguments += "/p:RevitApiReferenceMode=NuGet"
    if (![string]::IsNullOrWhiteSpace($RevitApiNuGetVersion)) {
        $buildArguments += "/p:RevitApiNuGetVersion=$RevitApiNuGetVersion"
    }
}
else {
    $buildArguments += "/p:RevitApiReferenceMode=Installed"
    $buildArguments += "/p:RevitApiDir=$RevitApiDir"
}

& dotnet $buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$Addin = Join-Path $OutputDir "PlugHub.addin"
$Dll = Join-Path $OutputDir "PlugHub.Revit2020.dll"
if (!(Test-Path $Dll)) { throw "Build finished but $Dll was not found." }
if (!(Test-Path $Addin)) { throw "Build finished but $Addin was not found." }

$RequiredConfigFiles = @(
    "config\sources.json",
    "config\views.json",
    "config\feature-combinations.json"
)
foreach ($RelativeConfigPath in $RequiredConfigFiles) {
    $ConfigPath = Join-Path $OutputDir $RelativeConfigPath
    if (!(Test-Path $ConfigPath)) {
        throw "Build finished but required runtime config was not found: $ConfigPath"
    }
}

# Replace relative assembly path with absolute DLL path for Revit addins folder usage.
$xml = [xml](Get-Content $Addin -Raw)
$assemblyNode = $xml.SelectSingleNode('//RevitAddIns/AddIn/Assembly')
if ($null -eq $assemblyNode) { throw "Missing Assembly node in $Addin." }
$assemblyNode.InnerText = $Dll
$xml.Save($Addin)

if ($InstallAddin) {
    $AddinsDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
    New-Item -ItemType Directory -Force -Path $AddinsDir | Out-Null
    Copy-Item $Addin (Join-Path $AddinsDir "PlugHub.addin") -Force
    Write-Host "Installed addin manifest to $AddinsDir\PlugHub.addin"
}

Write-Host "PlugHub build output: $OutputDir"
Write-Host "DLL: $Dll"
Write-Host "ADDIN: $Addin"
