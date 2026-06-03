param(
    [string]$Configuration = "Release",
    [string]$RevitApiDir = "",
    [switch]$UseRevitApiNuGet,
    [string]$RevitApiNuGetVersion = "",
    [string]$OutputDir = "$PSScriptRoot\..\dist\Revit2020",
    [string]$PlugHubReleaseTag = "",
    [switch]$UseRelativeAddinAssembly,
    [switch]$InstallAddin,
    [switch]$NoStage,
    [switch]$Clean,
    [switch]$CleanAddin
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path "$PSScriptRoot\.."
$Project = Join-Path $Root "src\PlugHub.Revit2020\PlugHub.Revit2020.csproj"
$UpdaterProject = Join-Path $Root "src\PlugHub.Updater\PlugHub.Updater.csproj"

function Assert-PathInsideRoot {
    param([string]$Path)
    $resolvedRoot = (Resolve-Path $Root).Path.TrimEnd("\")
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd("\")
    $rootPrefix = $resolvedRoot + "\"
    if (![string]::Equals($fullPath, $resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase) -and !$fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the repository: $fullPath"
    }
}

function Remove-RepoPath {
    param([string]$Path)
    if (Test-Path $Path) {
        Assert-PathInsideRoot $Path
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Get-Revit2020AddinManifestPath {
    if ([string]::IsNullOrWhiteSpace($env:ProgramData)) {
        throw "ProgramData is not set; cannot resolve the machine-wide Revit 2020 addins directory."
    }

    return Join-Path $env:ProgramData "Autodesk\Revit\Addins\2020\PlugHub.addin"
}

function Remove-InstalledAddin {
    $target = Get-Revit2020AddinManifestPath
    $expected = [System.IO.Path]::GetFullPath((Join-Path $env:ProgramData "Autodesk\Revit\Addins\2020\PlugHub.addin"))
    $fullPath = [System.IO.Path]::GetFullPath($target)
    if (![string]::Equals($fullPath, $expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean unexpected addin manifest path: $fullPath"
    }

    if (Test-Path $target) {
        Remove-Item -LiteralPath $target -Force
        Write-Host "Removed addin manifest: $target"
    }
}

function Remove-StaleOutputPath {
    param([string]$Path)
    if (Test-Path $Path) {
        Assert-PathInsideRoot $Path
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Test-RevitApiDir {
    param([string]$Path)
    return ![string]::IsNullOrWhiteSpace($Path) `
        -and (Test-Path (Join-Path $Path "RevitAPI.dll")) `
        -and (Test-Path (Join-Path $Path "RevitAPIUI.dll"))
}

function Resolve-PlugHubReleaseVersion {
    param([string]$Tag)

    if ([string]::IsNullOrWhiteSpace($Tag)) {
        $Tag = $env:PLUGHUB_RELEASE_TAG
    }

    if ([string]::IsNullOrWhiteSpace($Tag)) {
        return @{ Version = "0.0.0"; ReleaseTag = "dev" }
    }

    $match = [regex]::Match($Tag.Trim(), "^V(?<version>\d+\.\d+\.\d+)$")
    if (!$match.Success) {
        throw "PlugHub release tag must match Vx.y.z: $Tag"
    }

    return @{ Version = $match.Groups["version"].Value; ReleaseTag = $Tag.Trim() }
}

$candidateApiDirs = @(
    $RevitApiDir,
    "D:\Program Files\Autodesk\Revit 2020",
    "D:\Program Files\Autodesk\Revit",
    "C:\Program Files\Autodesk\Revit 2020",
    "C:\Program Files\Autodesk\Revit"
)

if ($Clean) {
    Remove-RepoPath (Join-Path $Root "dist\Revit2020")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Contracts\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Contracts\obj")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Framework\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Framework\obj")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Revit2020\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Revit2020\obj")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Updater\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.Updater\obj")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.StaticValidation\bin")
    Remove-RepoPath (Join-Path $Root "src\PlugHub.StaticValidation\obj")
}

if ($CleanAddin) {
    Remove-InstalledAddin
}

$resolvedApiDir = ""
if (!$UseRevitApiNuGet) {
    $resolvedApiDir = $candidateApiDirs | Where-Object { Test-RevitApiDir $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($resolvedApiDir)) {
        throw "Revit 2020 API DLLs were not found. Pass -RevitApiDir with a folder containing RevitAPI.dll and RevitAPIUI.dll, or pass -UseRevitApiNuGet for CI compile references."
    }
    $RevitApiDir = (Resolve-Path $resolvedApiDir).Path
}

Assert-PathInsideRoot $OutputDir
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
$PlugHubReleaseVersion = Resolve-PlugHubReleaseVersion $PlugHubReleaseTag

$RemovedModuleName = "PlugHub." + "Sample" + "Module"
$StaleOutputPaths = @(
    (Join-Path $OutputDir ($RemovedModuleName + ".dll")),
    (Join-Path $OutputDir ($RemovedModuleName + ".pdb")),
    (Join-Path $OutputDir "PlugHub.BuiltinModule.dll"),
    (Join-Path $OutputDir "PlugHub.BuiltinModule.pdb"),
    (Join-Path $OutputDir "config\modules.json"),
    (Join-Path $OutputDir "config\plugin-sources.json"),
    (Join-Path $OutputDir ("packages\" + "dropins")),
    (Join-Path $OutputDir ("packages\" + "github")),
    (Join-Path $OutputDir ("modules\" + "samples")),
    (Join-Path $OutputDir ("modules\" + "dropins")),
    (Join-Path $OutputDir "modules")
)
foreach ($StaleOutputPath in $StaleOutputPaths) {
    Remove-StaleOutputPath $StaleOutputPath
}

$buildArguments = @(
    "build",
    $Project,
    "-c",
    $Configuration,
    "/p:RevitVersion=2020",
    "/p:PlugHubOutputDir=$OutputDir",
    "/p:PlugHubVersion=$($PlugHubReleaseVersion.Version)",
    "/p:PlugHubReleaseTag=$($PlugHubReleaseVersion.ReleaseTag)"
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

if ($NoStage) {
    $buildArguments += "/p:StagePlugHubOutput=false"
}

& dotnet $buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if ($NoStage) {
    if ($InstallAddin) {
        throw "-InstallAddin cannot be used with -NoStage because no addin manifest is staged."
    }

    Write-Host "PlugHub build completed without staging."
    return
}

& dotnet build $UpdaterProject -c $Configuration -t:Rebuild "/p:OutDir=$OutputDir\" "/p:PlugHubVersion=$($PlugHubReleaseVersion.Version)" "/p:PlugHubReleaseTag=$($PlugHubReleaseVersion.ReleaseTag)"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build PlugHub.Updater failed with exit code $LASTEXITCODE."
}

$Addin = Join-Path $OutputDir "PlugHub.addin"
$Dll = Join-Path $OutputDir "PlugHub.Revit2020.dll"
$Updater = Join-Path $OutputDir "PlugHub.Updater.exe"
if (!(Test-Path $Dll)) { throw "Build finished but $Dll was not found." }
if (!(Test-Path $Addin)) { throw "Build finished but $Addin was not found." }
if (!(Test-Path $Updater)) { throw "Build finished but $Updater was not found." }

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
$assemblyNode.InnerText = if ($UseRelativeAddinAssembly) { "PlugHub.Revit2020.dll" } else { $Dll }
$xml.Save($Addin)

if ($InstallAddin) {
    & (Join-Path $PSScriptRoot "install-addin.ps1") -BuiltDir $OutputDir -Silent
    Write-Host "Installed addin manifest to $(Get-Revit2020AddinManifestPath)"
}

Write-Host "PlugHub build output: $OutputDir"
Write-Host "DLL: $Dll"
Write-Host "ADDIN: $Addin"
