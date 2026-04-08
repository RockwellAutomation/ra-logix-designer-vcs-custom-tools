<#
.SYNOPSIS
    Shared helper functions for l5xplode / l5xgit e2e tests.

.DESCRIPTION
    Dot-source this file from individual test files to get access to common
    path resolution, process invocation, and temp-directory helpers.

    Debug mode
    ----------
    Set $env:E2E_DEBUG = '1' (or pass -Debug when invoking the test script)
    to get verbose output for every tool invocation: the exact command line,
    stdout, stderr, and exit code.  When an assertion fails in debug mode the
    test run will pause so you can inspect the temp directories before they
    are cleaned up.
#>

$repoRoot    = (Resolve-Path "$PSScriptRoot/..").Path
$fixturesDir = Join-Path $PSScriptRoot 'fixtures'
$l5xplode    = Join-Path $repoRoot 'artifacts/bin/Release/l5xplode.exe'
$l5xgit      = Join-Path $repoRoot 'artifacts/bin/Release/l5xgit.exe'

# Debug flag — set via  $env:E2E_DEBUG = '1'  or by the test file itself.
$DebugTests = $env:E2E_DEBUG -eq '1'

function Invoke-Tool {
    param(
        [string]$ExePath,
        [string[]]$Arguments
    )
    $pinfo = [System.Diagnostics.ProcessStartInfo]::new()
    $pinfo.FileName  = $ExePath
    $pinfo.Arguments = ($Arguments -join ' ')
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError  = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow  = $true

    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo = $pinfo
    $proc.Start() | Out-Null
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()

    $result = [PSCustomObject]@{
        ExitCode = $proc.ExitCode
        StdOut   = $stdout
        StdErr   = $stderr
        Command  = "$ExePath $($Arguments -join ' ')"
    }

    if ($DebugTests) {
        Write-Host "`n┌─── COMMAND ───────────────────────────────────────" -ForegroundColor Cyan
        Write-Host "│ $($result.Command)" -ForegroundColor Cyan
        Write-Host "├─── EXIT CODE: $($result.ExitCode) ───" -ForegroundColor $(if ($result.ExitCode -eq 0) { 'Green' } else { 'Red' })
        if ($result.StdOut.Trim()) {
            Write-Host "├─── STDOUT ────────────────────────────────────────" -ForegroundColor DarkGray
            $result.StdOut.TrimEnd() -split "`n" | ForEach-Object { Write-Host "│ $_" -ForegroundColor DarkGray }
        }
        if ($result.StdErr.Trim()) {
            Write-Host "├─── STDERR ────────────────────────────────────────" -ForegroundColor Yellow
            $result.StdErr.TrimEnd() -split "`n" | ForEach-Object { Write-Host "│ $_" -ForegroundColor Yellow }
        }
        Write-Host "└───────────────────────────────────────────────────" -ForegroundColor Cyan
    }

    return $result
}

function Invoke-L5xplode {
    param([string[]]$Arguments)
    Invoke-Tool -ExePath $l5xplode -Arguments $Arguments
}

function Invoke-L5xgit {
    param([string[]]$Arguments)
    Invoke-Tool -ExePath $l5xgit -Arguments $Arguments
}

function New-TestTempDir {
    param([string]$Prefix = 'e2e')
    $tempRoot = Join-Path $PSScriptRoot 'temp'
    $dir = Join-Path $tempRoot "${Prefix}_$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    return $dir
}

<#
.SYNOPSIS
    Pause the test run when an assertion fails (debug mode only).
.DESCRIPTION
    Call this inside a Pester It block's catch or after a Should that may fail.
    When $DebugTests is true it prints the relevant paths and waits for a
    keypress so you can explore the temp directories.  In normal mode it is a
    no-op.
#>
function Wait-IfDebug {
    param(
        [string]$Message = 'Assertion failed.',
        [string[]]$Paths = @()
    )
    if (-not $DebugTests) { return }
    Write-Host "`n╔══ DEBUG PAUSE ════════════════════════════════════" -ForegroundColor Magenta
    Write-Host "║ $Message" -ForegroundColor Magenta
    foreach ($p in $Paths) {
        Write-Host "║   $p" -ForegroundColor Magenta
    }
    Write-Host "║ Press any key to continue..." -ForegroundColor Magenta
    Write-Host "╚══════════════════════════════════════════════════=" -ForegroundColor Magenta
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}
