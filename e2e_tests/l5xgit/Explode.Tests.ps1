#Requires -Modules Pester
param(
    [switch]$Debug
)

$env:E2E_DEBUG = if ($Debug) { '1' } else { '0' }

BeforeAll {
    . "$PSScriptRoot/../Helpers.ps1"

    if (-not (Test-Path $l5xgit)) {
        throw "l5xgit.exe not found at '$l5xgit'. Run 'dotnet build -c Release' first."
    }
    if (-not (Test-Path $l5xplode)) {
        throw "l5xplode.exe not found at '$l5xplode'. Run 'dotnet build -c Release' first."
    }

    $script:noDepsFixture = Join-Path $fixturesDir 'raC_Opr_HTTP_Client_no_deps.L5X'

    function Assert-WithPause {
        param(
            [Parameter(Mandatory)][scriptblock]$Assertion,
            [string[]]$Paths = @()
        )
        try {
            & $Assertion
        }
        catch {
            Wait-IfDebug -Message $_.Exception.Message -Paths $Paths
            throw
        }
    }
}

Describe 'l5xgit explode' {

    Context 'basic explode with Dependencies fixture' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xgit_explode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            $script:result = Invoke-L5xgit @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force')
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'exits with code 0' {
            $script:result.ExitCode | Should -Be 0
        }

        It 'creates the RSLogix5000Content subdirectory' {
            Join-Path $script:tempDir 'RSLogix5000Content' | Should -Exist
        }

        It 'creates export-options.yaml' {
            Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml' | Should -Exist
        }

        It 'creates the root document XML' {
            Join-Path $script:tempDir 'RSLogix5000Content/RSLogix5000Content.xml' | Should -Exist
        }
    }

    Context 'l5xgit explode matches l5xplode output structure' {
        BeforeAll {
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'

            $script:gitDir   = New-TestTempDir -Prefix 'l5xgit_explode'
            $script:plodeDir = New-TestTempDir -Prefix 'l5xgit_explode'

            Invoke-L5xgit   @('explode', '--l5x', $l5xFile, '--dir', $script:gitDir,   '--force') | Out-Null
            Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:plodeDir, '--force') | Out-Null

            # Get relative file lists from both
            $script:gitFiles   = Get-ChildItem $script:gitDir   -Recurse -File | ForEach-Object { $_.FullName.Substring($script:gitDir.Length)   } | Sort-Object
            $script:plodeFiles = Get-ChildItem $script:plodeDir -Recurse -File | ForEach-Object { $_.FullName.Substring($script:plodeDir.Length) } | Sort-Object
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:gitDir)   { Remove-Item $script:gitDir   -Recurse -Force }
            if (Test-Path $script:plodeDir) { Remove-Item $script:plodeDir -Recurse -Force }
        }

        It 'produces the same set of files' {
            $script:gitFiles | Should -Be $script:plodeFiles
        }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Direct explode of the no-deps fixture (no SDK round-trip)
# ─────────────────────────────────────────────────────────────────────────────
Describe 'l5xgit explode with no-deps fixture rejects missing Dependencies when encoded AOIs present' -Tag 'NoDeps' {

    Context 'explode without --unsafe-skip-dependency-check fails' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'explode_nodeps_fail'
            $script:result = Invoke-L5xgit @(
                'explode', '--l5x', $script:noDepsFixture,
                '--dir', $script:tempDir, '--force'
            )
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'exits with a non-zero exit code' {
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $script:result.ExitCode | Should -Not -Be 0
            }
        }

        It 'reports the missing Dependencies export option' {
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $script:result.StdErr | Should -Match 'Dependencies'
            }
        }

        It 'tells the user about --unsafe-skip-dependency-check' {
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $script:result.StdErr | Should -Match 'unsafe-skip-dependency-check'
            }
        }

        It 'does NOT create the exploded directory structure' {
            $p = Join-Path $script:tempDir 'RSLogix5000Content'
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $p | Should -Not -Exist
            }
        }
    }

    Context 'explode with --unsafe-skip-dependency-check succeeds' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'explode_nodeps_ok'
            $script:result = Invoke-L5xgit @(
                'explode', '--l5x', $script:noDepsFixture,
                '--dir', $script:tempDir, '--force',
                '--unsafe-skip-dependency-check'
            )
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'exits with code 0' {
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $script:result.ExitCode | Should -Be 0
            }
        }

        It 'creates the exploded RSLogix5000Content directory' {
            $p = Join-Path $script:tempDir 'RSLogix5000Content'
            Assert-WithPause -Paths @($p) -Assertion { $p | Should -Exist }
        }

        It 'persists unsafe_skip_dependency_check in export-options.yaml' {
            $optionsFile = Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml'
            Assert-WithPause -Paths @($optionsFile) -Assertion {
                $optionsFile | Should -Exist
                $content = Get-Content $optionsFile -Raw
                $content | Should -Match 'unsafe_skip_dependency_check:\s*true'
            }
        }

        It 'does not produce errors on stderr' {
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $script:result.StdErr | Should -BeNullOrEmpty
            }
        }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# No-deps fixture WITHOUT encoded AOIs — should succeed without --unsafe flag
# ─────────────────────────────────────────────────────────────────────────────
Describe 'l5xgit explode with no-deps fixture succeeds when no encoded AOIs' -Tag 'NoDeps' {

    Context 'explode without --unsafe-skip-dependency-check succeeds (no encoded AOIs)' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'explode_nodeps_plain'
            $script:plainNoDepsFixture = Join-Path $fixturesDir 'sample_no_dependencies.L5X'
            $script:result = Invoke-L5xgit @(
                'explode', '--l5x', $script:plainNoDepsFixture,
                '--dir', $script:tempDir, '--force'
            )
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'exits with code 0' {
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $script:result.ExitCode | Should -Be 0
            }
        }

        It 'creates the exploded RSLogix5000Content directory' {
            $p = Join-Path $script:tempDir 'RSLogix5000Content'
            Assert-WithPause -Paths @($p) -Assertion { $p | Should -Exist }
        }

        It 'does not produce errors on stderr' {
            Assert-WithPause -Paths @($script:tempDir) -Assertion {
                $script:result.StdErr | Should -BeNullOrEmpty
            }
        }

        It 'does not set unsafe_skip_dependency_check in export-options.yaml' {
            $optionsFile = Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml'
            Assert-WithPause -Paths @($optionsFile) -Assertion {
                $optionsFile | Should -Exist
                $content = Get-Content $optionsFile -Raw
                $content | Should -Match 'unsafe_skip_dependency_check:\s*false'
            }
        }
    }
}
