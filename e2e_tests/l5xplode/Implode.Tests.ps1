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

Describe 'l5xplode implode' {

    Context 'round-trip: explode then implode' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_implode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'

            # Explode first
            $explodeResult = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force')
            if ($explodeResult.ExitCode -ne 0) {
                throw "Explode failed: $($explodeResult.StdErr)"
            }

            # Implode back
            $script:outputL5x = Join-Path $script:tempDir 'round_trip_output.L5X'
            $script:result = Invoke-L5xplode @('implode', '--dir', $script:tempDir, '--l5x', $script:outputL5x, '--force')
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'implode exits with code 0' {
            $script:result.ExitCode | Should -Be 0
        }

        It 'produces an output L5X file' {
            $script:outputL5x | Should -Exist
        }

        It 'output L5X contains RSLogix5000Content root element' {
            $content = Get-Content $script:outputL5x -Raw
            $content | Should -Match '<RSLogix5000Content'
        }

        It 'output L5X contains the Controller element' {
            [xml]$xml = Get-Content $script:outputL5x
            $xml.RSLogix5000Content.Controller | Should -Not -BeNullOrEmpty
        }

        It 'output L5X contains the TestTag' {
            [xml]$xml = Get-Content $script:outputL5x
            $tags = $xml.RSLogix5000Content.Controller.Tags.Tag
            ($tags | Where-Object { $_.Name -eq 'TestTag' }) | Should -Not -BeNullOrEmpty
        }

        It 'output L5X contains the SimpleType data type' {
            [xml]$xml = Get-Content $script:outputL5x
            $dataTypes = $xml.RSLogix5000Content.Controller.DataTypes.DataType
            ($dataTypes | Where-Object { $_.Name -eq 'SimpleType' }) | Should -Not -BeNullOrEmpty
        }

        It 'output L5X contains the SampleAOI add-on instruction' {
            [xml]$xml = Get-Content $script:outputL5x
            $aois = $xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions.AddOnInstruction
            ($aois | Where-Object { $_.Name -eq 'SampleAOI' }) | Should -Not -BeNullOrEmpty
        }

        It 'output L5X contains the MainProgram' {
            [xml]$xml = Get-Content $script:outputL5x
            $programs = $xml.RSLogix5000Content.Controller.Programs.Program
            ($programs | Where-Object { $_.Name -eq 'MainProgram' }) | Should -Not -BeNullOrEmpty
        }

        It 'output L5X contains the MainTask' {
            [xml]$xml = Get-Content $script:outputL5x
            $tasks = $xml.RSLogix5000Content.Controller.Tasks.Task
            ($tasks | Where-Object { $_.Name -eq 'MainTask' }) | Should -Not -BeNullOrEmpty
        }

        It 'output L5X omits ExportDate (default behavior)' {
            [xml]$xml = Get-Content $script:outputL5x
            $xml.RSLogix5000Content.ExportDate | Should -BeNullOrEmpty
        }
    }

    Context 'round-trip with minimal L5X' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_implode'
            $l5xFile = Join-Path $fixturesDir 'minimal.L5X'

            Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force') | Out-Null

            $script:outputL5x = Join-Path $script:tempDir 'minimal_round_trip.L5X'
            $script:result = Invoke-L5xplode @('implode', '--dir', $script:tempDir, '--l5x', $script:outputL5x, '--force')
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
            $content = Get-Content $script:outputL5x -Raw
            $content | Should -Match '<RSLogix5000Content'
        }
    }

    Context 'round-trip with --unsafe-skip-dependency-check preserves options' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_implode'
            $l5xFile = Join-Path $fixturesDir 'sample_no_dependencies.L5X'

            # Explode with the unsafe flag
            $explodeResult = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force', '--unsafe-skip-dependency-check')
            if ($explodeResult.ExitCode -ne 0) {
                throw "Explode failed: $($explodeResult.StdErr)"
            }

            # Implode back
            $script:outputL5x = Join-Path $script:tempDir 'unsafe_round_trip.L5X'
            $script:result = Invoke-L5xplode @('implode', '--dir', $script:tempDir, '--l5x', $script:outputL5x, '--force')
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
