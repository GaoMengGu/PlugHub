$ErrorActionPreference = "Stop"

function Write-ReleaseEnv {
    param(
        [string]$Tag,
        [string]$Skip
    )

    @(
        "PLUGHUB_RELEASE_TAG=$Tag",
        "PLUGHUB_SKIP_RELEASE=$Skip"
    ) | Set-Content -LiteralPath ".plughub-release.env" -Encoding utf8
}

function Remove-PathIfExists {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Resolve-ReleaseTag {
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        git fetch --tags --force origin
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to fetch tags from origin."
        }

        $tag = git tag --points-at HEAD --list "V[0-9]*.[0-9]*.[0-9]*" --sort=-v:refname |
            Select-Object -First 1
        if (![string]::IsNullOrWhiteSpace($tag)) {
            return $tag.Trim()
        }

        Write-Host "No V* release tag points at HEAD yet. Waiting for tag sync ($attempt/60)."
        Start-Sleep -Seconds 10
    }

    return ""
}

function New-ReleaseNotes {
    param([string]$Tag)

    $notesPath = "PlugHub-ReleaseNotes-$Tag.md"
    $previous = git tag --list "V[0-9]*.[0-9]*.[0-9]*" --sort=-v:refname |
        Where-Object { $_ -ne $Tag } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($previous)) {
        $commits = @(git log -1 $Tag --pretty=format:"- %s")
    }
    else {
        $commits = @(git log "$previous..$Tag" --no-merges --pretty=format:"- %s")
    }

    if ($commits.Count -eq 0) {
        $commits = @("- Framework build and release artifacts updated.")
    }

    @(
        "## Release Notes",
        "",
        $commits
    ) | Set-Content -LiteralPath $notesPath -Encoding utf8

    return $notesPath
}

function Build-ReleaseArtifacts {
    param([string]$Tag)

    dotnet run --project src\PlugHub.StaticValidation\PlugHub.StaticValidation.csproj
    if ($LASTEXITCODE -ne 0) {
        throw "Static validation failed."
    }

    .\scripts\build-revit2020.ps1 -UseRevitApiNuGet -UseRelativeAddinAssembly

    $updaterOutput = (Resolve-Path "dist\Revit2020").Path
    dotnet build src\PlugHub.Updater\PlugHub.Updater.csproj -c Release -t:Rebuild /p:OutDir="$updaterOutput\"
    if ($LASTEXITCODE -ne 0) {
        throw "Updater build failed."
    }

    $builtUpdater = Join-Path $updaterOutput "PlugHub.Updater.exe"
    if (!(Test-Path -LiteralPath $builtUpdater)) {
        throw "Updater build finished but $builtUpdater was not found."
    }

    $zipPath = "PlugHub-Revit2020-$Tag.zip"
    $sourceRoot = (Resolve-Path "dist\Revit2020").Path
    $artifactDir = Join-Path $env:TEMP "PlugHub-Revit2020-$Tag"
    Remove-PathIfExists $zipPath
    Remove-PathIfExists $artifactDir

    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -File |
        Where-Object { $_.Name -notlike "*.sigstore.json" -and $_.Name -notlike "*.pdb" } |
        ForEach-Object {
            $relativePath = $_.FullName.Substring($sourceRoot.Length).TrimStart("\", "/")
            $targetPath = Join-Path $artifactDir $relativePath
            New-Item -ItemType Directory -Force -Path (Split-Path $targetPath -Parent) | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
        }
    Compress-Archive -Path "$artifactDir\*" -DestinationPath $zipPath -Force

    $uninstallerOutput = Join-Path $env:TEMP "PlugHubUninstaller"
    Remove-PathIfExists $uninstallerOutput
    New-Item -ItemType Directory -Force -Path $uninstallerOutput | Out-Null

    dotnet build src\PlugHub.Uninstaller\PlugHub.Uninstaller.csproj -c Release -t:Rebuild /p:OutDir="$uninstallerOutput\"
    if ($LASTEXITCODE -ne 0) {
        throw "Uninstaller build failed."
    }

    $builtUninstaller = Join-Path $uninstallerOutput "PlugHub.Uninstaller.exe"
    if (!(Test-Path -LiteralPath $builtUninstaller)) {
        throw "Uninstaller build finished but $builtUninstaller was not found."
    }

    Copy-Item -LiteralPath $builtUninstaller -Destination (Join-Path $uninstallerOutput "PlugHub-Uninstall.exe") -Force

    $payloadZip = (Resolve-Path $zipPath).Path
    $uninstallerExe = (Resolve-Path (Join-Path $uninstallerOutput "PlugHub.Uninstaller.exe")).Path
    $installerOutput = Join-Path $env:TEMP "PlugHubInstaller"
    $installerExe = "PlugHub-Setup-$Tag.exe"
    Remove-PathIfExists $installerOutput
    New-Item -ItemType Directory -Force -Path $installerOutput | Out-Null

    dotnet build src\PlugHub.Installer\PlugHub.Installer.csproj -c Release -t:Rebuild /p:InstallerPayloadZip="$payloadZip" /p:InstallerUninstallerExe="$uninstallerExe" /p:OutDir="$installerOutput\"
    if ($LASTEXITCODE -ne 0) {
        throw "Installer build failed."
    }

    $builtInstaller = Join-Path $installerOutput "PlugHub.Installer.exe"
    if (!(Test-Path -LiteralPath $builtInstaller)) {
        throw "Installer build finished but $builtInstaller was not found."
    }

    Copy-Item -LiteralPath $builtInstaller -Destination $installerExe -Force

    $checksumPath = "PlugHub-SHA256-$Tag.txt"
    $assets = @(
        "PlugHub-Revit2020-$Tag.zip",
        "PlugHub-Setup-$Tag.exe"
    )
    $lines = foreach ($asset in $assets) {
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $asset
        "$($hash.Hash.ToLowerInvariant())  $asset"
    }
    $lines | Set-Content -LiteralPath $checksumPath -Encoding ascii

    return $assets + $checksumPath
}

function Publish-GiteeRelease {
    param(
        [string]$Tag,
        [string]$NotesPath,
        [string[]]$Assets
    )

    if ([string]::IsNullOrWhiteSpace($env:GITEE_TOKEN)) {
        throw "GITEE_TOKEN is required to publish the Gitee release."
    }

    $releaseBody = Get-Content -LiteralPath $NotesPath -Raw
    $releaseBaseUri = "https://gitee.com/api/v5/repos/GaoMengGu/PlugHub/releases"
    $escapedToken = [Uri]::EscapeDataString($env:GITEE_TOKEN)
    $escapedTag = [Uri]::EscapeDataString($Tag)
    $release = $null

    try {
        $release = Invoke-RestMethod -Method Post -Uri $releaseBaseUri -Body @{
            access_token = $env:GITEE_TOKEN
            tag_name = $Tag
            target_commitish = "main"
            name = "PlugHub $Tag"
            body = $releaseBody
            prerelease = "false"
        }
    }
    catch {
        $release = Invoke-RestMethod -Method Get -Uri "$releaseBaseUri/tags/$escapedTag?access_token=$escapedToken"
    }

    if ($null -eq $release -or [string]::IsNullOrWhiteSpace([string]$release.id)) {
        throw "Gitee release was not created or resolved for tag $Tag."
    }

    foreach ($asset in $Assets) {
        $fullPath = (Resolve-Path $asset).Path
        & curl.exe -sS -X POST -F "access_token=$($env:GITEE_TOKEN)" -F "file=@$fullPath" "$releaseBaseUri/$($release.id)/attach_files"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to upload Gitee release attachment: $asset"
        }
    }
}

$tag = Resolve-ReleaseTag
if ([string]::IsNullOrWhiteSpace($tag)) {
    Write-ReleaseEnv "" "true"
    Write-Host "No release tag points at the current commit. Skipping Gitee release build."
    exit 0
}

if ($tag -notmatch "^V\d+\.\d+\.\d+$") {
    throw "Release tag must match Vx.y.z: $tag"
}

Write-ReleaseEnv $tag "false"
$notesPath = New-ReleaseNotes $tag
$assets = Build-ReleaseArtifacts $tag
Publish-GiteeRelease -Tag $tag -NotesPath $notesPath -Assets $assets
