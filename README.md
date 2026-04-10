# ra-logix-designer-vcs-custom-tools

A collection of .NET 10.0 command-line utilities and libraries for working with Rockwell Automation Studio 
5000 `.L5X` project files.

The `l5xplode` command enables you to "explode" (decompose) `.L5X` files into a structured directory of XML and text files, 
making them easier to version, diff, and review in source control systems like Git. You can also "implode" (recompose) the directory 
structure back into a valid `.L5X` file.

The `l5xgit` command contains everything l5xplode does but also additional commands to assist with importing/exporting ACD
files to/from a more git-appropriate format, and even commit / diff those changes.

## Features

- **Explode** `.L5X` files into a logical folder structure of XML and structured text files.
- **Implode** a folder structure back into a single `.L5X` file.
- **Convert** between `.L5X` and `.ACD` (binary project) formats (requires Studio 5000 Logix Designer and SDK libraries).
- **Git integration**: Commit, diff, and restore project files using Git-friendly workflows.
- **Custom serialization**: Allows for adding custom serialization formats and options.
- **Cross-platform**: Runs on Windows and Linux (limited features) with .NET 10.0.

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Studio 5000 Logix Designer and the Logix Designer SDK for `.ACD` file operations

### Build

Clone the repository and build:

```sh
git clone https://github.com/RockwellAutomation/ra-logix-designer-vcs-custom-tools.git
cd ra-logix-designer-vcs-custom-tools
dotnet build -c Release
```

### Usage

There are two command-line tools with built-in help for commands provided.  l5xgit-exclusive commands may only function on Windows.
Some commands may be available in both utilities.

```sh
l5xplode -h
l5xgit -h
```

#### Common Commands

- **Explode an L5X file:**
  ```sh
  l5xplode explode --l5x path/to/project.L5X --dir path/to/output/dir
  ```

- **Implode a directory back to L5X:**
  ```sh
  l5xplode implode --dir path/to/output/dir --l5x path/to/project.L5X
  ```

- **Convert L5X to ACD:**
  ```sh
  l5xgit l5x2acd --l5x path/to/project.L5X --acd path/to/project.ACD
  ```

- **Commit exploded project to Git:**
  ```sh
  l5xgit commit --acd path/to/project.ACD
  ```

- **Show diff with previous commit:**
  ```sh
  l5xgit difftool --acd path/to/project.ACD
  ```

- **Restore from Git:**
  ```sh
  l5xgit restoreacd --acd path/to/project.ACD
  ```

## Dependency Check and `--unsafe-skip-dependency-check`

When an L5X file contains encrypted/encoded Add-On Instructions (AOIs) and was exported
without the `Dependencies` export option, the tools will refuse to explode it because
dependency ordering cannot be guaranteed for encoded AOIs. If you need to proceed anyway,
pass `--unsafe-skip-dependency-check` to bypass this check:

```sh
l5xplode explode --l5x path/to/project.L5X --dir path/to/output/dir --unsafe-skip-dependency-check
l5xgit commit --acd path/to/project.ACD --unsafe-skip-dependency-check
```

> **Note:** This flag is **not needed** if the L5X was exported via the **Logix Designer SDK 2.2+**
> (which always includes dependency metadata), or if the project contains **no encrypted/encoded AOIs**.

> **Warning:** Using this flag without proper dependency information means code merges may produce an L5X
> with incorrect AOI ordering, potentially causing import errors in Logix Designer.
>
> Even with this flag set, a best-effort attempt is made to preserve dependency ordering of AOIs,
> so in cases where no manual merging / removal / addition of AOIs is taking place outside
> of this tool, skipping this check should not cause import errors after the implode operation.

## Integration with Logix Designer

Some level of integration with Logix Designer can be achieved by using Custom Tools xml which is produced during the build
configured to point to the build output path.

`artifacts\bin\release\Assets\CustomToolsMenu.xml` can be copied to: `C:\Program Files (x86)\Rockwell Software\RSLogix 5000\Common\CustomToolsMenu.xml`
to install the custom tools globally within Logix Designer.

A script is provided which self-elevates to Administrator and performs the copy:

```powershell
.\Install-CustomToolsMenu.ps1
```


## Testing

End-to-end tests are written in PowerShell using [Pester](https://pester.dev/) and live under the `e2e_tests/` directory.
Tests are organized into subdirectories by tool (`l5xplode/` and `l5xgit/`).

### Prerequisites

- **Pester 5.7.1+** — install with: `Install-Module Pester -RequiredVersion 5.7.1 -Scope CurrentUser`
- **Logix Designer V38** or newer
- **Logix Designer SDK 2.2** or higher
- Build the solution first: `dotnet build -c Release`

### Running Tests

Run all tests for a tool subdirectory:

```powershell
Import-Module Pester -RequiredVersion 5.7.1 -Force
Invoke-Pester -Path .\e2e_tests\l5xplode\*.Tests.ps1 -Output Detailed
Invoke-Pester -Path .\e2e_tests\l5xgit\*.Tests.ps1 -Output Detailed
```

Run all tests across both tools:

```powershell
Invoke-Pester -Path .\e2e_tests\l5xplode.Tests.ps1, .\e2e_tests\l5xgit.Tests.ps1 -Output Detailed
```

### Debug Mode

Each test file accepts a `-Debug` switch that prints every tool invocation, its stdout/stderr,
exit codes, and pauses on assertion failures so you can inspect temp directories.

Run all l5xgit tests with debug output:

```powershell
Get-ChildItem e2e_tests\l5xgit *.ps1 | ForEach-Object { & $_.FullName -Debug }
```

> **Note:** The SDK-dependent tests (tagged `SDK`) such as `CommitRestore.roundtrip.Tests.ps1`
> require the Logix Designer SDK to be installed and will fail on machines without it.

## Project Structure

- `L5xploderLib/` – Library for exploding/imploding and serialization logic
- `L5xGitLib/` - Library for interacting with git repos.
- `L5xCommands/` – Command-line interface and command implementations
- `l5xgit/` – CLI executable exposing commands defined in L5xCommands
- `l5xplode/` – CLI executable exposing subset of commands defined in L5xCommands

## Limitations
- Source-protected content is non-human readable in XML the encoded content mutates with
  each export causing files in git to change when no source changes were made.
- If the project does not verify / export without error, it cannot be converted
  to L5X from ACD and vice-versa.
- Output formats may change and may not function in future versions
- Encoded content can cause problems with dependency resolution and AOI ordering.
  To minimize issues, either avoid encoded content in the L5X, or use Logix Designer
  V38 or newer with the LD SDK 2.2 or higher for exporting to L5X.  Exports through
  The Logix Designer GUI will not contain all of the dependency relationship metadata
  that the SDK provides, and may not preserve proper AOI ordering, potentially causing
  dependency resolution issues on reimport.

## Contributing
We are not accepting contributions at this time.

## License
Permission to modify and redistribute is granted under the terms of the MIT License.  
See the [LICENSE](LICENSE) file for the full license.
