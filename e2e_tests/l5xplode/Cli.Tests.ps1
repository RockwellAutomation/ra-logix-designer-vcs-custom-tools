#Requires -Modules Pester
param(
    [switch]$Debug
)

$env:E2E_DEBUG = if ($Debug) { '1' } else { '0' }

BeforeAll {
    . "$PSScriptRoot/../Helpers.ps1"

    if (-not (Test-Path $l5xplode)) {
        throw "l5xplode.exe not found at '$l5xplode'. Run 'dotnet build -c Release' first."
    }
}

Describe 'l5xplode CLI validation' {

    Context 'explode with missing required options' {
        It 'reports error when --l5x is not provided' {
            $tempDir = New-TestTempDir -Prefix 'l5xplode_cli'
            try {
                $result = Invoke-L5xplode @('explode', '--dir', $tempDir)
                $result.ExitCode | Should -Not -Be 0
                $result.StdErr | Should -Match "--l5x.*required|required.*--l5x"
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }

        It 'reports error when --dir is not provided' {
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            $result = Invoke-L5xplode @('explode', '--l5x', $l5xFile)
            $result.ExitCode | Should -Not -Be 0
            $result.StdErr | Should -Match "--dir.*required|required.*--dir"
        }
    }

    Context 'explode with non-existent L5X file' {
        It 'reports a validation error on stderr' {
            $tempDir = New-TestTempDir -Prefix 'l5xplode_cli'
            try {
                $result = Invoke-L5xplode @('explode', '--l5x', 'C:\nonexistent\file.L5X', '--dir', $tempDir, '--force')
                $result.ExitCode | Should -Not -Be 0
                $result.StdErr | Should -Not -BeNullOrEmpty
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }
    }

    Context 'explode with wrong file extension' {
        It 'reports a validation error when given a .txt file instead of .L5X' {
            $tempDir = New-TestTempDir -Prefix 'l5xplode_cli'
            $txtFile = Join-Path $tempDir 'notanl5x.txt'
            Set-Content -Path $txtFile -Value 'hello'
            try {
                $result = Invoke-L5xplode @('explode', '--l5x', $txtFile, '--dir', $tempDir, '--force')
                $result.ExitCode | Should -Not -Be 0
                $result.StdErr | Should -Not -BeNullOrEmpty
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }
    }

    Context 'implode with missing required options' {
        It 'reports error when --dir is not provided' {
            $result = Invoke-L5xplode @('implode', '--l5x', 'output.L5X')
            $result.ExitCode | Should -Not -Be 0
            $result.StdErr | Should -Match "--dir.*required|required.*--dir"
        }

        It 'reports error when --l5x is not provided' {
            $tempDir = New-TestTempDir -Prefix 'l5xplode_cli'
            try {
                $result = Invoke-L5xplode @('implode', '--dir', $tempDir)
                $result.ExitCode | Should -Not -Be 0
                $result.StdErr | Should -Match "--l5x.*required|required.*--l5x"
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }
    }

    Context 'no subcommand provided' {
        It 'shows help text on stdout' {
            $result = Invoke-L5xplode @()
            $result.StdOut | Should -Match 'l5xplode'
        }
    }

    Context 'unknown subcommand' {
        It 'reports an error on stderr' {
            $result = Invoke-L5xplode @('bogus')
            $result.ExitCode | Should -Not -Be 0
            $result.StdErr | Should -Match 'Unrecognized command or argument'
        }
    }
}
