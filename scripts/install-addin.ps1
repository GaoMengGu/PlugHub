param(
    [string]$BuiltDir = "$PSScriptRoot\..\dist\Revit2020"
)

$ErrorActionPreference = "Stop"
$BuiltDir = Resolve-Path $BuiltDir
$Dll = Join-Path $BuiltDir "PlugHub.Revit2020.dll"
$Addin = Join-Path $BuiltDir "PlugHub.addin"
if (!(Test-Path $Dll)) { throw "Missing $Dll. Run scripts\build-revit2020.ps1 first." }
if (!(Test-Path $Addin)) { throw "Missing $Addin. Run scripts\build-revit2020.ps1 first." }

$RequiredConfigFiles = @(
    "config\modules.json",
    "config\views.json",
    "config\feature-combinations.json"
)
foreach ($RelativeConfigPath in $RequiredConfigFiles) {
    $ConfigPath = Join-Path $BuiltDir $RelativeConfigPath
    if (!(Test-Path $ConfigPath)) {
        throw "Missing $ConfigPath. Run scripts\build-revit2020.ps1 first."
    }
}

$xml = [xml](Get-Content $Addin -Raw)
$assemblyNode = $xml.SelectSingleNode('//RevitAddIns/AddIn/Assembly')
if ($null -eq $assemblyNode) { throw "Missing Assembly node in $Addin." }
$assemblyNode.InnerText = $Dll
$xml.Save($Addin)

$AddinsDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\2020"
New-Item -ItemType Directory -Force -Path $AddinsDir | Out-Null
Copy-Item $Addin (Join-Path $AddinsDir "PlugHub.addin") -Force
Write-Host "Installed: $AddinsDir\PlugHub.addin"
