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
}

Describe 'l5xgit CLI' {

    Context 'help output' {
        It 'shows help text listing available subcommands' {
            $result = Invoke-L5xgit @('--help')
            $result.StdOut | Should -Match 'l5xgit'
            $result.StdOut | Should -Match 'explode'
            $result.StdOut | Should -Match 'implode'
            $result.StdOut | Should -Match 'commit'
        }
    }

    Context 'unknown subcommand' {
        It 'reports an error on stderr' {
            $result = Invoke-L5xgit @('bogus')
            $result.StdErr | Should -Match 'Unrecognized command or argument'
        }
    }

    Context 'explode missing required options' {
        It 'reports error when --l5x is missing' {
            $tempDir = New-TestTempDir -Prefix 'l5xgit_cli'
            try {
                $result = Invoke-L5xgit @('explode', '--dir', $tempDir)
                $result.StdErr | Should -Match "--l5x.*required|required.*--l5x"
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }

        It 'reports error when --dir is missing' {
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            $result = Invoke-L5xgit @('explode', '--l5x', $l5xFile)
            $result.StdErr | Should -Match "--dir.*required|required.*--dir"
        }
    }

    Context 'implode missing required options' {
        It 'reports error when --l5x is missing' {
            $tempDir = New-TestTempDir -Prefix 'l5xgit_cli'
            try {
                $result = Invoke-L5xgit @('implode', '--dir', $tempDir)
                $result.StdErr | Should -Match "--l5x.*required|required.*--l5x"
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }

        It 'reports error when --dir is missing' {
            $result = Invoke-L5xgit @('implode', '--l5x', 'output.L5X')
            $result.StdErr | Should -Match "--dir.*required|required.*--dir"
        }
    }
}
