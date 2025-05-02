# ra-logix-designer-vcs-custom-tools

A collection of .NET 8.0 command-line utilities and libraries for working with Rockwell Automation Studio 
5000 `.L5X` project files.

The `l5xplode` command enables you to "explode" (decompose) `.L5X` files into a structured directory of XML and text files, 
making them easier to version, diff, and review in source control systems like Git. You can also "implode" (recompose) the directory 
structure back into a valid `.L5X` file.

The `l5xgit` command contains everything l5zxplode does but also additional commands to assist with importing/exporting ACD
files to/from a more git-appropriate format, and even commit / diff those changes.

## Features

- **Explode** `.L5X` files into a logical folder structure of XML and structured text files.
- **Implode** a folder structure back into a single `.L5X` file.
- **Convert** between `.L5X` and `.ACD` (binary project) formats (requires Studio 5000 Logix Designer and SDK libraries).
- **Git integration**: Commit, diff, and restore project files using Git-friendly workflows.
- **Custom serialization**: Allows for adding custom serialization formats and options.
- **Cross-platform**: Runs on Windows and Linux (limited features) with .NET 8.0.

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Studio 5000 Logix Designer and the Logix Designer SDK for `.ACD` file operations

### Build

Clone the repository and build:

```sh
git clone <your-repo-url>
cd <clone_dir>
dotnet build ./src -c Release
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

## Integration with Logix Designer

Some level of integration with Logix Designer can be achieved by using Custom Tools xml which is produced during the build
configured to point to the build output path.

`bin/Release/net8.0/Assets/CustomToolsMenu.xml` can be coped to: `C:\Program Files (x86)\Rockwell Software\RSLogix 5000\Common\CustomToolsMenu.xml`
to install the custom tools globally within Logix Designer.


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

## Contributing
We are not accepting contributions at this time.

## License
Permission to modify and redistribute is granted under the terms of the MIT License.  
See the [LICENSE](LICENSE) file for the full license.