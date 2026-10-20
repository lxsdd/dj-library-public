param(
  [Parameter(Mandatory=$true)][string]$PortableFolder
)
$ErrorActionPreference = 'Stop'

# This runs on a fresh disposable public Windows Actions VM only.
# Refuse to run if ANY pre-existing DJ Library profile could be read/written.
$roaming = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
if ([String]::IsNullOrWhiteSpace($roaming) -or
    -not $roaming.StartsWith($env:USERPROFILE, [StringComparison]::OrdinalIgnoreCase)) {
  throw "Unexpected Windows profile directory: $roaming; refuse GUI startup"
}
$profile = Join-Path $roaming 'DJ Library'
if (Test-Path -LiteralPath $profile) {
  throw "Pre-existing DJ Library runner profile at $profile; refuse GUI startup"
}
$exe = Join-Path $PortableFolder 'DJLibrary.PortableProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Portable full-source GUI executable missing' }

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public static class DjLibraryVisibleWindowProbe
{
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc fn, IntPtr user);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int size);
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    public static string FindMainWindow(int processId)
    {
        string found = null;
        EnumWindows((hwnd, ignored) =>
        {
            uint owner;
            GetWindowThreadProcessId(hwnd, out owner);
            if (owner != processId || !IsWindowVisible(hwnd)) return true;
            var name = new StringBuilder(512);
            GetWindowText(hwnd, name, name.Capacity);
            string title = name.ToString();
            if (!title.StartsWith("DJ Library", StringComparison.Ordinal) ||
                !title.Contains("Native v0.4.0")) return true;
            var cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);
            if (!cls.ToString().StartsWith("HwndWrapper[", StringComparison.Ordinal))
                return true;
            Rect r;
            if (!GetWindowRect(hwnd, out r)) return true;
            int width = r.Right - r.Left, height = r.Bottom - r.Top;
            if (width < 950 || height < 550) return true;
            found = title + "; class=" + cls + "; size=" + width + "x" + height;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}
'@

$process = $null
try {
  # No --self-test arguments: actually initialize a fresh EMPTY catalog
  # and display the real, unmodified product WPF MainWindow.
  $process = Start-Process -FilePath $exe -WorkingDirectory $PortableFolder -PassThru
  $observed = $null
  for ($i=0; $i -lt 80; $i++) {
    if ($process.HasExited) {
      throw "Real WPF GUI startup exited prematurely: $($process.ExitCode)"
    }
    $observed = [DjLibraryVisibleWindowProbe]::FindMainWindow($process.Id)
    if (-not [String]::IsNullOrWhiteSpace($observed)) { break }
    Start-Sleep -Milliseconds 250
  }
  if ([String]::IsNullOrWhiteSpace($observed)) {
    throw 'No real visible WPF main HWND on CI desktop within 20 seconds'
  }
  $freshDb = Join-Path $profile 'catalog-v0.4.sqlite'
  if (-not (Test-Path -LiteralPath $freshDb) -or
      (Get-Item -LiteralPath $freshDb).Length -lt 1024) {
    throw 'Real product GUI did not initialize its isolated EMPTY schema-v4 SQLite catalog'
  }
  Write-Host "PASS actual full-product WPF GUI window visible on isolated public runner: $observed"
  Write-Host "PASS fresh empty CI runner SQLite initialized only under $profile"
  # External process uses genuine UI Automation + foreground hardware-style
  # keys. It does not write to the runner's disposable catalog or profile.
  $probe = 'experiments/wpf-portable/bin/Release/net10.0-windows/GuiInteractionProbe.dll'
  if (-not (Test-Path -LiteralPath $probe)) {
    throw "Missing compiled WPF UI interaction probe: $probe"
  }
  & dotnet $probe $process.Id
  if ($LASTEXITCODE -ne 0) {
    throw "Real WPF GUI interaction gate failed, exit $LASTEXITCODE"
  }
  Write-Host "PASS real WPF two-tab UIA, Ctrl shortcuts, search and native resize"

  # The real MainWindow Closing callback must persist tab and column state.
  # On the ephemeral runner only: inspect XML and re-open this same dummy profile.
  if (-not $process.WaitForExit(12000)) {
    throw 'First WPF instance did not exit after normal UI Automation close'
  }
  if ($process.ExitCode -ne 0) { throw "Normal product GUI close exited $($process.ExitCode)" }
  $settingsPath = Join-Path $profile 'settings.xml'
  if (-not (Test-Path -LiteralPath $settingsPath)) {
    throw 'Real WPF close did not persist settings.xml under disposable CI profile'
  }
  [xml]$settings = Get-Content -LiteralPath $settingsPath -Raw -ErrorAction Stop
  if ($settings.AppSettings.SelectedTab -ne '1') {
    throw 'Real SettingsManager failed to save selected CDs tab'
  }
  if (-not $settings.AppSettings.TrackColumns.ColumnSetting -or
      -not $settings.AppSettings.CdColumns.ColumnSetting) {
    throw 'Real MainWindow close did not save actual Tracks/CDs column layouts'
  }
  Write-Host 'PASS real .NET10 MainClosing saved synthetic selected tab and both grid layouts'

  $process.Dispose()
  $process = $null
  $process = Start-Process -FilePath $exe -WorkingDirectory $PortableFolder -PassThru
  $reopenedWindow = $null
  for ($i=0; $i -lt 80; $i++) {
    if ($process.HasExited) { throw "Real settings-reopen GUI exited $($process.ExitCode)" }
    $reopenedWindow = [DjLibraryVisibleWindowProbe]::FindMainWindow($process.Id)
    if ($reopenedWindow) { break }
    Start-Sleep -Milliseconds 250
  }
  if (-not $reopenedWindow) { throw 'Second WPF MainWindow not visible' }
  & dotnet $probe $process.Id --restored
  if ($LASTEXITCODE -ne 0) { throw 'Real WPF second-start settings restoration failed' }
  if (-not $process.WaitForExit(12000)) { throw 'Restored WPF GUI did not close normally' }
  if ($process.ExitCode -ne 0) { throw "Restored WPF GUI exited $($process.ExitCode)" }
  Write-Host 'PASS original WPF settings.xml saved on close, then restored CDs selection on second normal startup'

  # Populate only the freshly created, disposable runner catalog. The sidecar
  # refuses any nonempty DB, non-APPDATA path or non-GitHub environment.
  $bootstrap = 'experiments/wpf-portable/bin/Release/net10.0-windows/DJLibrary.SyntheticCatalogUiBootstrap.dll'
  if (-not (Test-Path -LiteralPath $bootstrap)) {
    throw "Missing isolated synthetic catalog source bootstrap: $bootstrap"
  }
  & dotnet $bootstrap $freshDb
  if ($LASTEXITCODE -ne 0) { throw 'Disposable-only synthetic populated GUI fixture creation failed' }
  $process.Dispose()
  $process = $null
  $process = Start-Process -FilePath $exe -WorkingDirectory $PortableFolder -PassThru
  $fixtureWindow = $null
  for ($i=0; $i -lt 80; $i++) {
    if ($process.HasExited) { throw "Synthetic populated GUI exited early: $($process.ExitCode)" }
    $fixtureWindow = [DjLibraryVisibleWindowProbe]::FindMainWindow($process.Id)
    if ($fixtureWindow) { break }
    Start-Sleep -Milliseconds 250
  }
  if (-not $fixtureWindow) { throw 'Populated real WPF GUI window not visible' }
  & dotnet $probe $process.Id --fixture
  if ($LASTEXITCODE -ne 0) { throw 'Real WPF populated catalog UIA/filter gates failed' }
  if (-not $process.WaitForExit(12000)) { throw 'Populated GUI did not close normally' }
  if ($process.ExitCode -ne 0) { throw "Populated GUI exit $($process.ExitCode)" }
  Write-Host 'PASS real WPF populated synthetic 2-track/2-CD tab and search filters, no user data'
}
finally {
  if ($process -ne $null) {
    if (-not $process.HasExited) {
      Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
      $process.WaitForExit(3000) | Out-Null
    }
    $process.Dispose()
  }
  # The folder was proven absent before startup; it was created only by this
  # disposable runner smoke, not a user's installed DJ Library profile.
  if (Test-Path -LiteralPath $profile) {
    Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction Stop
  }
}
