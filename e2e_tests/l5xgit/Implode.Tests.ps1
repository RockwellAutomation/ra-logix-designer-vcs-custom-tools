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

Describe 'l5xgit implode' {

    Context 'round-trip: explode then implode' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xgit_implode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'

            Invoke-L5xgit @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force') | Out-Null

            $script:outputL5x = Join-Path $script:tempDir 'round_trip.L5X'
            $script:result = Invoke-L5xgit @('implode', '--dir', $script:tempDir, '--l5x', $script:outputL5x, '--force')
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'implode exits with code 0' {
            $script:result.ExitCode | Should -Be 0
        }

        It 'produces a valid L5X file' {
            $script:outputL5x | Should -Exist
            [xml]$xml = Get-Content $script:outputL5x
            $xml.RSLogix5000Content | Should -Not -BeNullOrEmpty
        }
    }
}
