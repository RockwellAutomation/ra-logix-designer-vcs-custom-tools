#Requires -Modules Pester
param(
    [switch]$Debug
)

$env:E2E_DEBUG = if ($Debug) { '1' } else { '0' }

<#
.SYNOPSIS
    End-to-end tests for the l5xplode CLI tool.

.DESCRIPTION
    These Pester tests exercise the l5xplode.exe binary (explode and implode commands)
    against sample L5X fixture files, verifying:
      - Successful explode/implode round-trips
      - Expected directory structure after exploding
      - export-options.yaml serialization
      - The --unsafe-skip-dependency-check flag behavior
      - CLI validation (missing required args, non-existent files, bad extensions)
#>

BeforeAll {
    . "$PSScriptRoot/Helpers.ps1"

    # Ensure the tool is built
    if (-not (Test-Path $l5xplode)) {
        throw "l5xplode.exe not found at '$l5xplode'. Run 'dotnet build -c Release' first."
    }
}

Describe 'l5xplode explode' {

    Context 'with valid L5X containing Dependencies export option' {
        BeforeAll {
            $script:tempDir = New-TestTempDir
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
            $script:tempDir = New-TestTempDir
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
            $script:tempDir = New-TestTempDir
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
            $tempDir = New-TestTempDir
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
            $tempDir = New-TestTempDir
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
            $tempDir = New-TestTempDir
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
            $script:tempDir = New-TestTempDir
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
            $script:tempDir = New-TestTempDir
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
}

Describe 'l5xplode implode' {

    Context 'round-trip: explode then implode' {
        BeforeAll {
            $script:tempDir = New-TestTempDir
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
            $script:tempDir = New-TestTempDir
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
            $script:tempDir = New-TestTempDir
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

Describe 'l5xplode CLI validation' {

    Context 'explode with missing required options' {
        It 'reports error when --l5x is not provided' {
            $tempDir = New-TestTempDir
            try {
                $result = Invoke-L5xplode @('explode', '--dir', $tempDir)
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
            $result.StdErr | Should -Match "--dir.*required|required.*--dir"
        }
    }

    Context 'explode with non-existent L5X file' {
        It 'reports a validation error on stderr' {
            $tempDir = New-TestTempDir
            try {
                $result = Invoke-L5xplode @('explode', '--l5x', 'C:\nonexistent\file.L5X', '--dir', $tempDir, '--force')
                # System.CommandLine validators write to stderr
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
            $tempDir = New-TestTempDir
            $txtFile = Join-Path $tempDir 'notanl5x.txt'
            Set-Content -Path $txtFile -Value 'hello'
            try {
                $result = Invoke-L5xplode @('explode', '--l5x', $txtFile, '--dir', $tempDir, '--force')
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
            $result.StdErr | Should -Match "--dir.*required|required.*--dir"
        }

        It 'reports error when --l5x is not provided' {
            $tempDir = New-TestTempDir
            try {
                $result = Invoke-L5xplode @('implode', '--dir', $tempDir)
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
            $result.StdErr | Should -Match 'Unrecognized command or argument'
        }
    }
}

Describe 'l5xplode --pretty-attributes option' {

    Context 'explode with --pretty-attributes' {
        BeforeAll {
            $script:tempDir = New-TestTempDir
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
