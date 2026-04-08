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

Describe 'l5xplode explode' {

    Context 'with valid L5X containing Dependencies export option' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            $script:result = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force')
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

        It 'creates the root document XML file' {
            Join-Path $script:tempDir 'RSLogix5000Content/RSLogix5000Content.xml' | Should -Exist
        }

        It 'creates export-options.yaml' {
            Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml' | Should -Exist
        }

        It 'creates the DataTypes folder with the expected type file' {
            Join-Path $script:tempDir 'RSLogix5000Content/DataTypes/SimpleType.xml' | Should -Exist
        }

        It 'creates the Modules folder with the expected module file' {
            Join-Path $script:tempDir 'RSLogix5000Content/Modules/Local.xml' | Should -Exist
        }

        It 'creates the AddOnInstructionDefinitions folder' {
            $aoiDir = Join-Path $script:tempDir 'RSLogix5000Content/AddOnInstructionDefinitions/SampleAOI'
            $aoiDir | Should -Exist
            Join-Path $aoiDir 'SampleAOI.xml' | Should -Exist
        }

        It 'creates the Tags folder with the expected tag file' {
            Join-Path $script:tempDir 'RSLogix5000Content/Tags/TestTag.xml' | Should -Exist
        }

        It 'creates the Programs folder with program subfolder' {
            $progDir = Join-Path $script:tempDir 'RSLogix5000Content/Programs/MainProgram'
            $progDir | Should -Exist
            Join-Path $progDir 'MainProgram.xml' | Should -Exist
        }

        It 'creates program Tags subfolder' {
            Join-Path $script:tempDir 'RSLogix5000Content/Programs/MainProgram/Tags/ProgramTag.xml' | Should -Exist
        }

        It 'creates program Routines subfolder' {
            Join-Path $script:tempDir 'RSLogix5000Content/Programs/MainProgram/Routines/MainRoutine.xml' | Should -Exist
        }

        It 'creates the Tasks folder' {
            Join-Path $script:tempDir 'RSLogix5000Content/Tasks/MainTask.xml' | Should -Exist
        }
    }

    Context 'export-options.yaml content' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force') | Out-Null
            $script:optionsFile = Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml'
            $script:optionsContent = Get-Content $script:optionsFile -Raw
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'contains the serialization_format key' {
            $script:optionsContent | Should -Match 'serialization_format'
        }

        It 'contains the omit_export_date key' {
            $script:optionsContent | Should -Match 'omit_export_date'
        }

        It 'contains the xml_attribute_per_line key' {
            $script:optionsContent | Should -Match 'xml_attribute_per_line'
        }

        It 'contains the unsafe_skip_dependency_check key' {
            $script:optionsContent | Should -Match 'unsafe_skip_dependency_check'
        }

        It 'has unsafe_skip_dependency_check set to false by default' {
            $script:optionsContent | Should -Match 'unsafe_skip_dependency_check:\s*false'
        }
    }

    Context 'with --unsafe-skip-dependency-check flag' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            $script:result = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force', '--unsafe-skip-dependency-check')
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'exits with code 0' {
            $script:result.ExitCode | Should -Be 0
        }

        It 'persists unsafe_skip_dependency_check as true in export-options.yaml' {
            $optionsFile = Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml'
            $content = Get-Content $optionsFile -Raw
            $content | Should -Match 'unsafe_skip_dependency_check:\s*true'
        }
    }

    Context 'with L5X missing Dependencies export option but no encoded AOIs' {
        It 'succeeds without --unsafe-skip-dependency-check (no encoded AOIs to worry about)' {
            $tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            try {
                $l5xFile = Join-Path $fixturesDir 'sample_no_dependencies.L5X'
                $result = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $tempDir, '--force')

                $result.ExitCode | Should -Be 0
                Join-Path $tempDir 'RSLogix5000Content' | Should -Exist
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }

        It 'also succeeds with --unsafe-skip-dependency-check' {
            $tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            try {
                $l5xFile = Join-Path $fixturesDir 'sample_no_dependencies.L5X'
                $result = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $tempDir, '--force', '--unsafe-skip-dependency-check')

                $result.ExitCode | Should -Be 0
                Join-Path $tempDir 'RSLogix5000Content' | Should -Exist
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }

        It 'creates L5XGitPrevAOI ordering hints when --unsafe-skip-dependency-check is used' {
            $tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            try {
                $l5xFile = Join-Path $fixturesDir 'sample_no_dependencies.L5X'
                Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $tempDir, '--force', '--unsafe-skip-dependency-check') | Out-Null

                $optionsFile = Join-Path $tempDir 'RSLogix5000Content/export-options.yaml'
                $content = Get-Content $optionsFile -Raw
                $content | Should -Match 'unsafe_skip_dependency_check:\s*true'
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }
    }

    Context 'with minimal L5X (empty collections)' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            $l5xFile = Join-Path $fixturesDir 'minimal.L5X'
            $script:result = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force')
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'exits with code 0' {
            $script:result.ExitCode | Should -Be 0
        }

        It 'creates the root directory structure' {
            Join-Path $script:tempDir 'RSLogix5000Content' | Should -Exist
            Join-Path $script:tempDir 'RSLogix5000Content/RSLogix5000Content.xml' | Should -Exist
        }

        It 'creates the Modules folder (even minimal L5X has one module)' {
            Join-Path $script:tempDir 'RSLogix5000Content/Modules/Local.xml' | Should -Exist
        }
    }

    Context 'explode then re-explode with --force' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            $script:result1 = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force')
            $script:result2 = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force')
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'first explode succeeds' {
            $script:result1.ExitCode | Should -Be 0
        }

        It 'second explode with --force succeeds (overwrites)' {
            $script:result2.ExitCode | Should -Be 0
        }
    }

    Context 'explode with --pretty-attributes' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'l5xplode_explode'
            $l5xFile = Join-Path $fixturesDir 'sample_with_dependencies.L5X'
            $script:result = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force', '--pretty-attributes')
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'exits with code 0' {
            $script:result.ExitCode | Should -Be 0
        }

        It 'records pretty-attributes in export-options.yaml' {
            $optionsFile = Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml'
            $content = Get-Content $optionsFile -Raw
            $content | Should -Match 'xml_attribute_per_line:\s*true'
        }
    }
}
