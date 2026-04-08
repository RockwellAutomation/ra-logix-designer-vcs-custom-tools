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

# ─────────────────────────────────────────────────────────────────────────────
# AOI dependency ordering — explicit <Dependencies> elements
# ─────────────────────────────────────────────────────────────────────────────
Describe 'AOI dependency ordering with explicit Dependencies' {

    Context 'round-trip sorts AOIs by dependency order' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'aoi_explicit'
            $l5xFile = Join-Path $fixturesDir 'sample_with_aoi_dependencies.L5X'

            # Explode (AOIs are in reverse order in the source file: TopAOI, IndependentAOI, MiddleAOI, ZBaseAOI)
            $explodeResult = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $script:tempDir, '--force')
            if ($explodeResult.ExitCode -ne 0) {
                throw "Explode failed: $($explodeResult.StdErr)"
            }

            # Implode back
            $script:outputL5x = Join-Path $script:tempDir 'sorted_output.L5X'
            $implodeResult = Invoke-L5xplode @('implode', '--dir', $script:tempDir, '--l5x', $script:outputL5x, '--force')
            if ($implodeResult.ExitCode -ne 0) {
                throw "Implode failed: $($implodeResult.StdErr)"
            }

            [xml]$script:xml = Get-Content $script:outputL5x
            $script:aoiNames = @($script:xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions.AddOnInstruction | ForEach-Object { $_.Name })
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'produces all four AOIs' {
            $script:aoiNames.Count | Should -Be 4
        }

        It 'places ZBaseAOI before MiddleAOI' {
            $baseIdx   = [array]::IndexOf($script:aoiNames, 'ZBaseAOI')
            $middleIdx = [array]::IndexOf($script:aoiNames, 'MiddleAOI')
            $baseIdx | Should -BeLessThan $middleIdx
        }

        It 'places MiddleAOI before TopAOI' {
            $middleIdx = [array]::IndexOf($script:aoiNames, 'MiddleAOI')
            $topIdx    = [array]::IndexOf($script:aoiNames, 'TopAOI')
            $middleIdx | Should -BeLessThan $topIdx
        }

        It 'does not include any L5XGitPrevAOI elements (Dependencies mode does not inject hints)' {
            $content = Get-Content $script:outputL5x -Raw
            $content | Should -Not -Match 'L5XGitPrevAOI'
        }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# AOI ordering with --unsafe-skip-dependency-check (implicit deps, no <Dependencies>)
# ─────────────────────────────────────────────────────────────────────────────
Describe 'AOI ordering with --unsafe-skip-dependency-check' {

    Context 'no-deps fixture without encoded AOIs succeeds without --unsafe flag' {
        It 'succeeds because there are no encrypted/encoded AOIs' {
            $tempDir = New-TestTempDir -Prefix 'aoi_noexport'
            try {
                $l5xFile = Join-Path $fixturesDir 'sample_implicit_deps_no_export_option.L5X'
                $result = Invoke-L5xplode @('explode', '--l5x', $l5xFile, '--dir', $tempDir, '--force')

                $result.ExitCode | Should -Be 0
            }
            finally {
                $ProgressPreference = 'SilentlyContinue'
                if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
            }
        }
    }

    Context 'explode with --unsafe-skip-dependency-check adds ordering hints' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'aoi_unsafe'
            $l5xFile = Join-Path $fixturesDir 'sample_implicit_deps_no_export_option.L5X'

            $script:result = Invoke-L5xplode @(
                'explode', '--l5x', $l5xFile, '--dir', $script:tempDir,
                '--force', '--unsafe-skip-dependency-check'
            )
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'succeeds' {
            $script:result.ExitCode | Should -Be 0
        }

        It 'persists unsafe_skip_dependency_check as true' {
            $optionsFile = Join-Path $script:tempDir 'RSLogix5000Content/export-options.yaml'
            $content = Get-Content $optionsFile -Raw
            $content | Should -Match 'unsafe_skip_dependency_check:\s*true'
        }

        It 'adds L5XGitPrevAOI hints to AOI files' {
            # The fixture has 3 AOIs: TopAOI, MiddleAOI, ZBaseAOI (in that order).
            # After explode, the 2nd and 3rd AOIs should have L5XGitPrevAOI hints.
            $aoiDir = Join-Path $script:tempDir 'RSLogix5000Content/AddOnInstructionDefinitions'

            # MiddleAOI should have a hint pointing to TopAOI (the AOI before it in the source)
            $middleFile = Join-Path $aoiDir 'MiddleAOI/MiddleAOI.xml'
            $middleFile | Should -Exist
            $middleContent = Get-Content $middleFile -Raw
            $middleContent | Should -Match 'L5XGitPrevAOI'

            # ZBaseAOI should have a hint pointing to MiddleAOI
            $baseFile = Join-Path $aoiDir 'ZBaseAOI/ZBaseAOI.xml'
            $baseFile | Should -Exist
            $baseContent = Get-Content $baseFile -Raw
            $baseContent | Should -Match 'L5XGitPrevAOI'
        }

        It 'does NOT add L5XGitPrevAOI to the first AOI' {
            $aoiDir = Join-Path $script:tempDir 'RSLogix5000Content/AddOnInstructionDefinitions'
            $topFile = Join-Path $aoiDir 'TopAOI/TopAOI.xml'
            $topFile | Should -Exist
            $topContent = Get-Content $topFile -Raw
            $topContent | Should -Not -Match 'L5XGitPrevAOI'
        }
    }

    Context 'round-trip with implicit deps sorts AOIs and strips hints' {
        BeforeAll {
            $script:tempDir = New-TestTempDir -Prefix 'aoi_roundtrip'
            $l5xFile = Join-Path $fixturesDir 'sample_implicit_deps_no_export_option.L5X'

            # Explode with unsafe flag
            $explodeResult = Invoke-L5xplode @(
                'explode', '--l5x', $l5xFile, '--dir', $script:tempDir,
                '--force', '--unsafe-skip-dependency-check'
            )
            if ($explodeResult.ExitCode -ne 0) {
                throw "Explode failed: $($explodeResult.StdErr)"
            }

            # Implode back
            $script:outputL5x = Join-Path $script:tempDir 'implicit_sorted.L5X'
            $implodeResult = Invoke-L5xplode @('implode', '--dir', $script:tempDir, '--l5x', $script:outputL5x, '--force')
            if ($implodeResult.ExitCode -ne 0) {
                throw "Implode failed: $($implodeResult.StdErr)"
            }

            [xml]$script:xml = Get-Content $script:outputL5x
            $script:aoiNames = @($script:xml.RSLogix5000Content.Controller.AddOnInstructionDefinitions.AddOnInstruction | ForEach-Object { $_.Name })
        }

        AfterAll {
            $ProgressPreference = 'SilentlyContinue'
            if (Test-Path $script:tempDir) { Remove-Item $script:tempDir -Recurse -Force }
        }

        It 'produces all three AOIs' {
            $script:aoiNames.Count | Should -Be 3
        }

        It 'places ZBaseAOI before MiddleAOI (implicit dep via Parameter DataType)' {
            $baseIdx   = [array]::IndexOf($script:aoiNames, 'ZBaseAOI')
            $middleIdx = [array]::IndexOf($script:aoiNames, 'MiddleAOI')
            $baseIdx | Should -BeLessThan $middleIdx
        }

        It 'places MiddleAOI before TopAOI (implicit dep via Parameter DataType)' {
            $middleIdx = [array]::IndexOf($script:aoiNames, 'MiddleAOI')
            $topIdx    = [array]::IndexOf($script:aoiNames, 'TopAOI')
            $middleIdx | Should -BeLessThan $topIdx
        }

        It 'strips L5XGitPrevAOI ordering hints from the imploded output' {
            $content = Get-Content $script:outputL5x -Raw
            $content | Should -Not -Match 'L5XGitPrevAOI'
        }
    }
}
