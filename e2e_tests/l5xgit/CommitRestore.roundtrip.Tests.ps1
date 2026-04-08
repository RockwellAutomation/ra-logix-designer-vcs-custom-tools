#Requires -Modules Pester
param(
    [switch]$Debug
)

$env:E2E_DEBUG = if ($Debug) { '1' } else { '0' }

<#
.SYNOPSIS
    End-to-end round-trip tests for the l5xgit commit and restoreacd commands
    using the raC_Opr_HTTP_Client fixture files.

.DESCRIPTION
    These tests exercise the full commit/restore lifecycle:
      1. Convert an L5X fixture to ACD  (l5xgit l5x2acd)
      2. Commit the ACD to a temp git repo  (l5xgit commit)
      3. Restore the ACD from the git repo  (l5xgit restoreacd)
      4. Convert the restored ACD back to L5X  (l5xgit acd2l5x)
      5. Verify the round-tripped L5X content

    Two fixtures are used:
      - raC_Opr_HTTP_Client_deps.L5X      (exported WITH Dependencies)
      - raC_Opr_HTTP_Client_no_deps.L5X   (exported WITHOUT Dependencies)

    IMPORTANT: These tests require the Rockwell LogixDesigner SDK to be installed
    on the machine.  Each l5x2acd / acd2l5x conversion takes ~30s, so the full
    suite is intentionally slow.

    Debug mode
    ----------
    Set  $env:E2E_DEBUG = '1'  before running to see every command invocation,
    its stdout/stderr, and exit code.  On assertion failure the test run will
    pause so you can inspect the temp directories before they are cleaned up.

    Example:
      $env:E2E_DEBUG = '1'
      Invoke-Pester -Path .\e2e_tests\l5xgit\CommitRestore.roundtrip.Tests.ps1 -Output Detailed
#>

BeforeAll {
    . "$PSScriptRoot/../Helpers.ps1"

    if (-not (Test-Path $l5xgit)) {
        throw "l5xgit.exe not found at '$l5xgit'. Run 'dotnet build -c Release' first."
    }

    $script:depsFixture   = Join-Path $fixturesDir 'raC_Opr_HTTP_Client_deps.L5X'
    $script:noDepsFixture = Join-Path $fixturesDir 'raC_Opr_HTTP_Client_no_deps.L5X'

    function New-CommitTestEnvironment {
        param(
            [Parameter(Mandatory)][string]$L5xFixture,
            [string]$Prefix = 'commit_rt'
        )

        $rootDir    = New-TestTempDir -Prefix $Prefix
        $acdDir     = Join-Path $rootDir 'acd'
        $gitRepoDir = Join-Path $rootDir 'repo'

        New-Item -ItemType Directory -Path $acdDir     -Force | Out-Null
        New-Item -ItemType Directory -Path $gitRepoDir -Force | Out-Null

        Push-Location $gitRepoDir
        try {
            git init --quiet 2>&1 | Out-Null
            git config user.name  'Test User'
            git config user.email 'test@example.com'
        }
        finally { Pop-Location }

        if ($DebugTests) {
            Write-Host "`n[DEBUG] Git repo initialized at: $gitRepoDir" -ForegroundColor DarkCyan
        }

        $acdBaseName = [System.IO.Path]::GetFileNameWithoutExtension($L5xFixture)
        $acdPath     = Join-Path $acdDir "$acdBaseName.acd"

        $convertResult = Invoke-L5xgit @('l5x2acd', '--l5x', $L5xFixture, '--acd', $acdPath)
        if ($convertResult.ExitCode -ne 0) {
            throw "l5x2acd failed (exit $($convertResult.ExitCode)): $($convertResult.StdErr)"
        }

        $configPath = Join-Path $acdDir "${acdBaseName}_L5xGit.yml"
        "destination_path: $gitRepoDir`nprompt_for_commit_message: false" | Set-Content -Path $configPath -Encoding UTF8

        if ($DebugTests) {
            Write-Host "[DEBUG] ACD: $acdPath" -ForegroundColor DarkCyan
            Write-Host "[DEBUG] Config: $configPath" -ForegroundColor DarkCyan
        }

        return @{
            RootDir    = $rootDir
            AcdDir     = $acdDir
            AcdPath    = $acdPath
            ConfigPath = $configPath
            GitRepoDir = $gitRepoDir
        }
    }

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

Describe 'l5xgit commit with Dependencies fixture' -Tag 'SDK' {

    Context 'commit raC_Opr_HTTP_Client_deps to a git repo' {
        BeforeAll {
            $script:env = New-CommitTestEnvironment -L5xFixture $script:depsFixture -Prefix 'commit_deps'
            $script:result = Invoke-L5xgit @('commit', '--acd', $script:env.AcdPath)
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:env.RootDir) { Remove-Item $script:env.RootDir -Recurse -Force }
        }

        It 'exits with code 0' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.ExitCode | Should -Be 0
            }
        }

        It 'creates the exploded RSLogix5000Content directory' {
            $p = Join-Path $script:env.GitRepoDir 'RSLogix5000Content'
            Assert-WithPause -Paths @($script:env.GitRepoDir) -Assertion { $p | Should -Exist }
        }

        It 'creates export-options.yaml' {
            $p = Join-Path $script:env.GitRepoDir 'RSLogix5000Content/export-options.yaml'
            Assert-WithPause -Paths @($p) -Assertion { $p | Should -Exist }
        }

        It 'creates the root document XML' {
            $p = Join-Path $script:env.GitRepoDir 'RSLogix5000Content/RSLogix5000Content.xml'
            Assert-WithPause -Paths @($p) -Assertion { $p | Should -Exist }
        }

        It 'creates the AddOnInstructionDefinitions folder' {
            $p = Join-Path $script:env.GitRepoDir 'RSLogix5000Content/AddOnInstructionDefinitions'
            Assert-WithPause -Paths @($p) -Assertion { $p | Should -Exist }
        }

        It 'writes a git commit' {
            Push-Location $script:env.GitRepoDir
            try {
                $commitLog = git --no-pager log --oneline
                if ($DebugTests) {
                    Write-Host "[DEBUG] Git log:" -ForegroundColor DarkCyan
                    $commitLog | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkCyan }
                }
                Assert-WithPause -Paths @($script:env.GitRepoDir) -Assertion {
                    $commitLog.Count | Should -BeGreaterOrEqual 1
                }
            }
            finally { Pop-Location }
        }

        It 'stdout mentions successful commit' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.StdOut | Should -Match 'committed successfully|commit ID'
            }
        }

        It 'does not produce dependency warnings on stderr' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.StdErr | Should -BeNullOrEmpty
            }
        }
    }
}

Describe 'l5xgit commit with no-deps fixture' -Tag 'SDK' {

    # NOTE: The LogixDesigner SDK re-export (ACD → L5X via SaveAs) always
    # includes Dependencies in ExportOptions regardless of the original L5X.
    # Therefore a commit without --unsafe-skip-dependency-check succeeds even
    # when the original fixture was exported without Dependencies.

    Context 'commit without --unsafe-skip-dependency-check succeeds (SDK re-export includes Dependencies)' {
        BeforeAll {
            $script:env = New-CommitTestEnvironment -L5xFixture $script:noDepsFixture -Prefix 'commit_nodeps_sdk'
            $script:result = Invoke-L5xgit @('commit', '--acd', $script:env.AcdPath)
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:env.RootDir) { Remove-Item $script:env.RootDir -Recurse -Force }
        }

        It 'exits with code 0 because SDK re-export adds Dependencies' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.ExitCode | Should -Be 0
            }
        }

        It 'creates the exploded directory structure' {
            $p = Join-Path $script:env.GitRepoDir 'RSLogix5000Content'
            Assert-WithPause -Paths @($p) -Assertion { $p | Should -Exist }
        }

        It 'creates a git commit' {
            Push-Location $script:env.GitRepoDir
            try {
                $commitCount = (git --no-pager log --oneline | Measure-Object).Count
                if ($DebugTests) { Write-Host "[DEBUG] Commit count: $commitCount" -ForegroundColor DarkCyan }
                Assert-WithPause -Paths @($script:env.GitRepoDir) -Assertion {
                    $commitCount | Should -BeGreaterOrEqual 1
                }
            }
            finally { Pop-Location }
        }

        It 'does not produce errors on stderr' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.StdErr | Should -BeNullOrEmpty
            }
        }
    }

    Context 'commit with --unsafe-skip-dependency-check succeeds' {
        BeforeAll {
            $script:env = New-CommitTestEnvironment -L5xFixture $script:noDepsFixture -Prefix 'commit_nodeps_ok'
            $script:result = Invoke-L5xgit @(
                'commit', '--acd', $script:env.AcdPath, '--unsafe-skip-dependency-check'
            )
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:env.RootDir) { Remove-Item $script:env.RootDir -Recurse -Force }
        }

        It 'exits with code 0' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.ExitCode | Should -Be 0
            }
        }

        It 'creates the exploded directory structure' {
            $p = Join-Path $script:env.GitRepoDir 'RSLogix5000Content'
            Assert-WithPause -Paths @($p) -Assertion { $p | Should -Exist }
        }

        It 'persists unsafe_skip_dependency_check in export-options.yaml' {
            $optionsFile = Join-Path $script:env.GitRepoDir 'RSLogix5000Content/export-options.yaml'
            Assert-WithPause -Paths @($optionsFile) -Assertion {
                $optionsFile | Should -Exist
                $content = Get-Content $optionsFile -Raw
                $content | Should -Match 'unsafe_skip_dependency_check:\s*true'
            }
        }

        It 'creates a git commit' {
            Push-Location $script:env.GitRepoDir
            try {
                $commitCount = (git --no-pager log --oneline | Measure-Object).Count
                Assert-WithPause -Paths @($script:env.GitRepoDir) -Assertion {
                    $commitCount | Should -BeGreaterOrEqual 1
                }
            }
            finally { Pop-Location }
        }

        It 'stdout mentions successful commit' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.StdOut | Should -Match 'committed successfully|commit ID'
            }
        }
    }
}

Describe 'l5xgit commit then restoreacd round-trip (deps)' -Tag 'SDK' {

    Context 'restore ACD from a committed repo' {
        BeforeAll {
            $script:env = New-CommitTestEnvironment -L5xFixture $script:depsFixture -Prefix 'restore_deps'
            $commitResult = Invoke-L5xgit @('commit', '--acd', $script:env.AcdPath)
            if ($commitResult.ExitCode -ne 0) { throw "commit failed: $($commitResult.StdErr)" }

            $script:restoredAcd = Join-Path $script:env.AcdDir 'restored.acd'
            $restoredConfigPath = Join-Path $script:env.AcdDir 'restored_L5xGit.yml'
            "destination_path: $($script:env.GitRepoDir)`nprompt_for_commit_message: false" |
                Set-Content -Path $restoredConfigPath -Encoding UTF8

            $script:restoreResult = Invoke-L5xgit @('restoreacd', '--acd', $script:restoredAcd)
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:env.RootDir) { Remove-Item $script:env.RootDir -Recurse -Force }
        }

        It 'restoreacd exits with code 0' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:restoreResult.ExitCode | Should -Be 0
            }
        }

        It 'produces an ACD file' {
            Assert-WithPause -Paths @($script:env.AcdDir) -Assertion {
                $script:restoredAcd | Should -Exist
            }
        }

        It 'restored ACD is a non-trivial file (> 1 KB)' {
            Assert-WithPause -Paths @($script:restoredAcd) -Assertion {
                (Get-Item $script:restoredAcd).Length | Should -BeGreaterThan 1024
            }
        }
    }
}

Describe 'l5xgit full round-trip L5X-ACD-commit-restoreacd-ACD-L5X (deps)' -Tag 'SDK' {

    Context 'round-tripped L5X preserves structure' {
        BeforeAll {
            $script:env = New-CommitTestEnvironment -L5xFixture $script:depsFixture -Prefix 'fullrt_deps'
            $commitResult = Invoke-L5xgit @('commit', '--acd', $script:env.AcdPath)
            if ($commitResult.ExitCode -ne 0) { throw "commit failed: $($commitResult.StdErr)" }

            $script:restoredAcd = Join-Path $script:env.AcdDir 'roundtrip.acd'
            $rtConfigPath = Join-Path $script:env.AcdDir 'roundtrip_L5xGit.yml'
            "destination_path: $($script:env.GitRepoDir)`nprompt_for_commit_message: false" |
                Set-Content -Path $rtConfigPath -Encoding UTF8

            $restoreResult = Invoke-L5xgit @('restoreacd', '--acd', $script:restoredAcd)
            if ($restoreResult.ExitCode -ne 0) { throw "restoreacd failed: $($restoreResult.StdErr)" }

            $script:roundTrippedL5x = Join-Path $script:env.AcdDir 'roundtrip_output.L5X'
            $acd2l5xResult = Invoke-L5xgit @('acd2l5x', '--acd', $script:restoredAcd, '--l5x', $script:roundTrippedL5x)
            if ($acd2l5xResult.ExitCode -ne 0) { throw "acd2l5x failed: $($acd2l5xResult.StdErr)" }

            [xml]$script:xml = Get-Content $script:roundTrippedL5x

            if ($DebugTests) {
                Write-Host "[DEBUG] Round-tripped L5X: $($script:roundTrippedL5x)" -ForegroundColor DarkCyan
                $aoiContainer = $script:xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions
                $allAois = @($aoiContainer.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
                Write-Host "[DEBUG] AOI/EncodedData count: $($allAois.Count)" -ForegroundColor DarkCyan
                $allAois | ForEach-Object { Write-Host "  $($_.LocalName): $($_.Name)" -ForegroundColor DarkCyan }
            }
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:env.RootDir) { Remove-Item $script:env.RootDir -Recurse -Force }
        }

        It 'produces a valid L5X file' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:roundTrippedL5x | Should -Exist
                $script:xml.RSLogix5000Content | Should -Not -BeNullOrEmpty
            }
        }

        It 'round-tripped L5X contains the Controller element' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:xml.RSLogix5000Content.Controller | Should -Not -BeNullOrEmpty
            }
        }

        It 'round-tripped L5X contains AddOnInstructionDefinitions' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions | Should -Not -BeNullOrEmpty
            }
        }

        It 'round-tripped L5X preserves all four encoded AOIs' {
            Assert-WithPause -Paths @($script:roundTrippedL5x, $script:env.GitRepoDir) -Assertion {
                # Encrypted AOIs are EncodedData elements, not AddOnInstruction elements
                $aoiContainer = $script:xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions
                $allAois = @($aoiContainer.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
                $allAois.Count | Should -Be 4
            }
        }

        It 'preserves DataTypes' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:xml.RSLogix5000Content.Controller.DataTypes | Should -Not -BeNullOrEmpty
            }
        }

        It 'preserves Programs' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:xml.RSLogix5000Content.Controller.Programs | Should -Not -BeNullOrEmpty
            }
        }

        It 'preserves Tasks' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:xml.RSLogix5000Content.Controller.Tasks | Should -Not -BeNullOrEmpty
            }
        }
    }
}

Describe 'l5xgit full round-trip with no-deps and --unsafe-skip-dependency-check' -Tag 'SDK' {

    Context 'round-tripped L5X preserves structure when committed with unsafe flag' {
        BeforeAll {
            $script:env = New-CommitTestEnvironment -L5xFixture $script:noDepsFixture -Prefix 'fullrt_nodeps'
            $commitResult = Invoke-L5xgit @(
                'commit', '--acd', $script:env.AcdPath, '--unsafe-skip-dependency-check'
            )
            if ($commitResult.ExitCode -ne 0) { throw "commit failed: $($commitResult.StdErr)" }

            $script:restoredAcd = Join-Path $script:env.AcdDir 'roundtrip_nodeps.acd'
            $rtConfigPath = Join-Path $script:env.AcdDir 'roundtrip_nodeps_L5xGit.yml'
            "destination_path: $($script:env.GitRepoDir)`nprompt_for_commit_message: false" |
                Set-Content -Path $rtConfigPath -Encoding UTF8

            $restoreResult = Invoke-L5xgit @('restoreacd', '--acd', $script:restoredAcd)
            if ($restoreResult.ExitCode -ne 0) { throw "restoreacd failed: $($restoreResult.StdErr)" }

            $script:roundTrippedL5x = Join-Path $script:env.AcdDir 'roundtrip_nodeps_output.L5X'
            $acd2l5xResult = Invoke-L5xgit @('acd2l5x', '--acd', $script:restoredAcd, '--l5x', $script:roundTrippedL5x)
            if ($acd2l5xResult.ExitCode -ne 0) { throw "acd2l5x failed: $($acd2l5xResult.StdErr)" }

            [xml]$script:xml = Get-Content $script:roundTrippedL5x

            if ($DebugTests) {
                Write-Host "[DEBUG] Round-tripped L5X (no-deps): $($script:roundTrippedL5x)" -ForegroundColor DarkCyan
                $aoiContainer = $script:xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions
                $allAois = @($aoiContainer.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
                Write-Host "[DEBUG] AOI/EncodedData count: $($allAois.Count)" -ForegroundColor DarkCyan
                $allAois | ForEach-Object { Write-Host "  $($_.LocalName): $($_.Name)" -ForegroundColor DarkCyan }
            }
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:env.RootDir) { Remove-Item $script:env.RootDir -Recurse -Force }
        }

        It 'produces a valid L5X file' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:roundTrippedL5x | Should -Exist
                $script:xml.RSLogix5000Content | Should -Not -BeNullOrEmpty
            }
        }

        It 'round-tripped L5X contains the Controller element' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $script:xml.RSLogix5000Content.Controller | Should -Not -BeNullOrEmpty
            }
        }

        It 'round-tripped L5X preserves all four encoded AOIs' {
            Assert-WithPause -Paths @($script:roundTrippedL5x, $script:env.GitRepoDir) -Assertion {
                # Encrypted AOIs are EncodedData elements, not AddOnInstruction elements
                $aoiContainer = $script:xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions
                $allAois = @($aoiContainer.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
                $allAois.Count | Should -Be 4
            }
        }

        It 'round-tripped L5X does not contain L5XGitPrevAOI hints' {
            Assert-WithPause -Paths @($script:roundTrippedL5x) -Assertion {
                $content = Get-Content $script:roundTrippedL5x -Raw
                $content | Should -Not -Match 'L5XGitPrevAOI'
            }
        }
    }
}

Describe 'l5xgit subsequent commit inherits saved unsafe flag' -Tag 'SDK' {

    Context 'second commit without explicit --unsafe-skip-dependency-check succeeds' {
        BeforeAll {
            $script:env = New-CommitTestEnvironment -L5xFixture $script:noDepsFixture -Prefix 'inherit_unsafe'
            $first = Invoke-L5xgit @(
                'commit', '--acd', $script:env.AcdPath, '--unsafe-skip-dependency-check'
            )
            if ($first.ExitCode -ne 0) { throw "first commit failed: $($first.StdErr)" }

            if ($DebugTests) {
                Write-Host "[DEBUG] First commit done. Running second without --unsafe..." -ForegroundColor DarkCyan
            }

            $script:result = Invoke-L5xgit @('commit', '--acd', $script:env.AcdPath)
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:env.RootDir) { Remove-Item $script:env.RootDir -Recurse -Force }
        }

        It 'second commit exits with code 0' {
            Assert-WithPause -Paths @($script:env.RootDir) -Assertion {
                $script:result.ExitCode | Should -Be 0
            }
        }

        It 'export-options.yaml still has unsafe_skip_dependency_check true' {
            $optionsFile = Join-Path $script:env.GitRepoDir 'RSLogix5000Content/export-options.yaml'
            Assert-WithPause -Paths @($optionsFile) -Assertion {
                $content = Get-Content $optionsFile -Raw
                $content | Should -Match 'unsafe_skip_dependency_check:\s*true'
            }
        }
    }
}
