param(
  [Parameter(Mandatory=$true)][string]$ModernFolder
)
$ErrorActionPreference = 'Stop'

# HARD safety guard: this script is exclusively for a freshly provisioned,
# ephemeral, standard public GitHub-hosted Windows runner.
if ($env:GITHUB_ACTIONS -ne 'true' -or
    $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or
    -not $env:RUNNER_TEMP) {
  throw 'Refuse normal device or self-hosted runner: cross-runtime profile test is GitHub-hosted only'
}
$roaming = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
if (-not $roaming.StartsWith($env:USERPROFILE, [StringComparison]::OrdinalIgnoreCase)) {
  throw 'APPDATA does not resolve inside the disposable hosted user profile'
}
$profile = Join-Path $roaming 'DJ Library'
if (Test-Path -LiteralPath $profile) {
  throw 'Existing DJ Library user profile found: DO NOT modify it'
}
$legacyRoot = Join-Path $env:RUNNER_TEMP 'DJLibrary Framework 4.8 OLD'
$backup = Join-Path $env:RUNNER_TEMP ('djlib-rollback-' + [Guid]::NewGuid().ToString('N'))
$legacy = Join-Path $legacyRoot 'DJLibrary.exe'
$modern = Join-Path $ModernFolder 'DJLibrary.PortableProbe.exe'
$probe = 'experiments/wpf-portable/bin/Release/net10.0-windows/GuiInteractionProbe.dll'
$bootstrap = 'experiments/wpf-portable/bin/Release/net10.0-windows/DJLibrary.SyntheticCatalogUiBootstrap.dll'
foreach ($needed in @('DJLibrary.exe', 'djmeta_native.dll', 'rules/default-rules.json')) {
  if ($needed -eq 'DJLibrary.exe') {
    if (-not (Test-Path -LiteralPath $needed)) { throw "Original Framework binary missing: $needed" }
  }
}
foreach ($needed in @($modern, $probe, $bootstrap)) {
  if (-not (Test-Path -LiteralPath $needed)) {
    throw "Cross-runtime gate file missing: $needed"
  }
}

# Only runner-local generated product binaries. No artifact upload.
New-Item -ItemType Directory -Path (Join-Path $legacyRoot 'rules') -Force | Out-Null
Copy-Item -LiteralPath 'DJLibrary.exe' -Destination $legacy -ErrorAction Stop
Copy-Item -LiteralPath (Join-Path $ModernFolder 'djmeta_native.dll') -Destination (Join-Path $legacyRoot 'djmeta_native.dll') -ErrorAction Stop
Copy-Item -LiteralPath (Join-Path $ModernFolder 'rules/default-rules.json') -Destination (Join-Path $legacyRoot 'rules/default-rules.json') -ErrorAction Stop

# Do not compare SQLite file hashes while an app is running / a WAL is open.
# Compare stable, closed-process copies, then validate actual behavior in both.
function Sha256([string]$path) {
  if (-not (Test-Path -LiteralPath $path)) { throw "Missing snapshot: $path" }
  return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function OpenRealGui([string]$exe, [string]$mode) {
  $proc = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru
  try {
    $found = $false
    for ($i = 0; $i -lt 80; $i++) {
      if ($proc.HasExited) { throw "WPF $mode exited unexpectedly: $($proc.ExitCode)" }
      $proc.Refresh()
      if ($proc.MainWindowHandle -ne [IntPtr]::Zero -and
          $proc.MainWindowTitle.StartsWith('DJ Library')) {
        $found = $true
        break
      }
      Start-Sleep -Milliseconds 250
    }
    if (-not $found) { throw "No actual WPF main HWND in $mode" }
    $argsList = @("$($proc.Id)")
    if ($mode -eq 'fixture') { $argsList += '--fixture' }
    & dotnet $probe @argsList
    if ($LASTEXITCODE -ne 0) {
      throw "WPF $mode external keyboard/tab/search/UIA failed (exit=$LASTEXITCODE)"
    }
    if (-not $proc.WaitForExit(12000)) {
      throw "WPF $mode did not close through its normal Window Closing hook"
    }
    if ($proc.ExitCode -ne 0) { throw "WPF $mode exited $($proc.ExitCode)" }
  }
  finally {
    if (-not $proc.HasExited) {
      Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
      $proc.WaitForExit(3000) | Out-Null
    }
    $proc.Dispose()
  }
}

try {
  # Framework first creates and closes a fresh, empty schema-v4 installation.
  OpenRealGui $legacy 'first-framework'
  $db = Join-Path $profile 'catalog-v0.4.sqlite'
  $settings = Join-Path $profile 'settings.xml'
  if (-not (Test-Path -LiteralPath $db) -or
      -not (Test-Path -LiteralPath $settings)) {
    throw 'Legacy WPF did not create BOTH empty catalog and settings.xml'
  }
  [xml]$xml = Get-Content -LiteralPath $settings -Raw
  if ($xml.AppSettings.SelectedTab -ne '1') {
    throw 'Legacy Framework MainClosing did not persist synthetic selected CDs tab'
  }
  # The same previously verified catalog code inserts 1/2/2 synthetic records.
  & dotnet $bootstrap $db
  if ($LASTEXITCODE -ne 0) { throw 'Synthetic populated catalog bootstrap failed' }
  OpenRealGui $legacy 'fixture'
  Write-Host 'PASS real Framework 4.8 WPF GUI: 2 Tracks, 2 CDs and 2->1->0->2 filtering'

  # Snapshot both after all Framework processes have exited. A rollback must
  # restore these exact bytes, independently of any migration internals.
  New-Item -ItemType Directory -Path $backup -ErrorAction Stop | Out-Null
  Copy-Item -Path (Join-Path $profile '*') -Destination $backup -Recurse -Force -ErrorAction Stop
  $originalCatalog = Sha256 $db
  $originalSettings = Sha256 $settings
  $catalogSnapshot = Sha256 (Join-Path $backup 'catalog-v0.4.sqlite')
  $settingsSnapshot = Sha256 (Join-Path $backup 'settings.xml')
  if ($originalCatalog -cne $catalogSnapshot -or $originalSettings -cne $settingsSnapshot) {
    throw 'Unchanged Framework profile backup integrity FAILED before modernization'
  }
  Write-Host 'PASS SHA256 preflight verified Framework profile/cat/settings backup'

  OpenRealGui $modern 'fixture'
  Write-Host 'PASS same saved legacy Framework profile opened, filtered and normally closed under real .NET 10 WPF'

  # Restore old profile from the verified, fully cold backup. Profile has been
  # proven absent before this CI test; no owner profile can be targeted here.
  Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction Stop
  Copy-Item -LiteralPath $backup -Destination $profile -Recurse -Force -ErrorAction Stop
  if ((Sha256 $db) -cne $originalCatalog -or
      (Sha256 $settings) -cne $originalSettings) {
    throw 'Rollback SHA256 exact restore mismatch'
  }
  Write-Host 'PASS rollback restored original Framework SQLite and XML byte-for-byte'

  OpenRealGui $legacy 'fixture'
  Write-Host 'PASS rolled-back profile reopens in ORIGINAL Framework WPF: 2 CDs, 2 Tracks and functioning search'
  Write-Host 'PASS full actual WPF compatibility: Framework -> .NET 10 -> SHA256 restore -> Framework'
}
finally {
  # Only ever remove the directory verified initially absent, and a fresh
  # GUID-named temporary backup. No other runner or user data directories.
  if (Test-Path -LiteralPath $profile) {
    Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction Stop
  }
  if (Test-Path -LiteralPath $backup) {
    Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction Stop
  }
}
