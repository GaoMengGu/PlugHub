param(
    [string]$BuiltDir = "$PSScriptRoot\..\dist\Revit2020",
    [string]$Thumbprint = "",
    [string]$CertificatePath = "",
    [string]$CertificatePassword = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [switch]$CreateSelfSignedDevCertificate,
    [string]$SelfSignedSubject = "CN=PlugHub Local Dev"
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    $command = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $candidate = Get-ChildItem -LiteralPath $kitsRoot -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($null -ne $candidate) {
            return $candidate.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK or add signtool.exe to PATH."
}

$BuiltDir = (Resolve-Path $BuiltDir).Path
$SignTool = Find-SignTool

if ($CreateSelfSignedDevCertificate) {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $SelfSignedSubject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256
    $Thumbprint = $certificate.Thumbprint
    Write-Warning "Created a self-signed development code-signing certificate. It is free but not publicly trusted."
}

if ([string]::IsNullOrWhiteSpace($Thumbprint) -and [string]::IsNullOrWhiteSpace($CertificatePath)) {
    throw "Provide -Thumbprint, provide -CertificatePath, or use -CreateSelfSignedDevCertificate."
}

$targets = Get-ChildItem -LiteralPath $BuiltDir -Filter "*.dll" -File |
    Where-Object { $_.Name -like "PlugHub*.dll" } |
    Sort-Object Name
if ($targets.Count -eq 0) {
    throw "No PlugHub DLLs were found in $BuiltDir."
}

foreach ($target in $targets) {
    # signtool sign /fd SHA256 /tr <timestamp-url> /td SHA256 ...
    $arguments = @("sign", "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256")
    if (![string]::IsNullOrWhiteSpace($CertificatePath)) {
        $arguments += @("/f", (Resolve-Path $CertificatePath).Path)
        if (![string]::IsNullOrWhiteSpace($CertificatePassword)) {
            $arguments += @("/p", $CertificatePassword)
        }
    }
    else {
        $arguments += @("/sha1", $Thumbprint)
    }

    $arguments += $target.FullName
    & $SignTool @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $($target.FullName) with exit code $LASTEXITCODE."
    }
}

Write-Host "Signed $($targets.Count) PlugHub DLL(s) in $BuiltDir."
