param(
  [Parameter(Mandatory=$true)][string]$OriginalFolder
)
$ErrorActionPreference = 'Stop'
$required = @(
  'DJLibrary.PortableProbe.exe', 'coreclr.dll', 'hostfxr.dll',
  'PresentationFramework.dll', 'DJLibrary.PortableProbe.runtimeconfig.json',
  'djmeta_native.dll', 'rules/default-rules.json'
)
foreach ($name in $required) {
  if (-not (Test-Path -LiteralPath (Join-Path $OriginalFolder $name))) {
    throw "Missing self-contained WPF/native dependency: $name"
  }
}

# Verify that the pinned native DLL actually loads and has the expected ABI,
# not merely that a file with its name exists.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate uint DjMetaAbiVersion();
'@
$dll = Join-Path $OriginalFolder 'djmeta_native.dll'
$handle = [System.Runtime.InteropServices.NativeLibrary]::Load($dll)
try {
  $function = [System.Runtime.InteropServices.NativeLibrary]::GetExport(
    $handle, 'djmeta_abi_version')
  $abi = [System.Runtime.InteropServices.Marshal]::GetDelegateForFunctionPointer(
    $function, [type][DjMetaAbiVersion]).Invoke()
  if ($abi -ne 1) { throw "Metadata normalizer ABI mismatch: $abi" }
  Write-Host "PASS x64 native DLL loaded with metadata ABI=$abi"
}
finally {
  [System.Runtime.InteropServices.NativeLibrary]::Free($handle)
}

# Copy the *complete* self-contained directory, then call the program from
# an unrelated working directory. Never use normal GUI startup here.
$relocated = Join-Path $env:RUNNER_TEMP 'DJ Library WPF .NET 10 relocated'
Copy-Item -LiteralPath $OriginalFolder -Destination $relocated -Recurse -ErrorAction Stop
$exe = Join-Path $relocated 'DJLibrary.PortableProbe.exe'
if (-not (Test-Path -LiteralPath $exe)) {
  throw 'Relocated self-contained executable missing'
}
$foreign = Join-Path $env:RUNNER_TEMP 'unrelated process working directory'
New-Item -ItemType Directory -Path $foreign -Force | Out-Null
$proc = Start-Process -FilePath $exe -ArgumentList '--self-test-public' -WorkingDirectory $foreign -PassThru
try {
  if (-not $proc.WaitForExit(100000)) {
    throw 'Relocated full-source selftest timed out'
  }
  if ($proc.ExitCode -ne 0) {
    $err = Join-Path $relocated 'SELF-TEST-ERROR.txt'
    if (Test-Path -LiteralPath $err) { Get-Content -LiteralPath $err }
    throw "Relocated full-source synthetic selftest failed, exit=$($proc.ExitCode)"
  }
}
finally {
  if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
  }
  $proc.Dispose()
}
$reportFile = Join-Path $relocated 'SELF-TEST.txt'
if (-not (Test-Path -LiteralPath $reportFile)) {
  throw 'Relocated EXE failed to write its synthetic selftest report'
}
$report = Get-Content -LiteralPath $reportFile -Raw
foreach ($phrase in @('PASS_PUBLIC_CONTRACT', 'SQLite WAL/gzip/base64', 'JSON v1 roundtrip')) {
  if (-not $report.Contains($phrase)) {
    throw "Full source portable selftest did not pass '$phrase': $report"
  }
}
if (Test-Path -LiteralPath (Join-Path $foreign 'SELF-TEST.txt')) {
  throw 'Report incorrectly written to the caller working directory'
}
$count = @(Get-ChildItem -LiteralPath $relocated -File -Recurse).Count
$total = (Get-ChildItem -LiteralPath $relocated -File -Recurse |
  Measure-Object -Property Length -Sum).Sum
Write-Host "PASS relocated self-contained WPF .NET10: $count files; $([Math]::Round($total/1MB,1)) MiB"
Write-Host $report
