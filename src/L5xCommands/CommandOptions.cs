using L5xploderLib.Enum;
using System.CommandLine;

namespace L5xCommands;

/// <summary>
/// Factory methods for common command-line options shared across multiple commands.
/// Each method returns a new Option instance (required by System.CommandLine, which
/// does not allow the same Option instance to be added to multiple commands).
/// </summary>
internal static class CommandOptions
{
    /// <summary>
    /// Creates an --acd / -a option for specifying a path to an existing ACD file to read.
    /// The file must exist and have a .acd extension.
    /// </summary>
    public static Option<string> AcdInputFile() => new("--acd", "-a")
    {
        Description = "Path to the ACD input file",
        Validators =
        {
            optionValue => OptionValidator.FileExtension(optionValue, ".acd"),
            OptionValidator.FileExists,
        }
    };

    /// <summary>
    /// Creates an --acd / -a option for specifying a path to an ACD output file.
    /// The file does not need to exist but must have a .acd extension.
    /// </summary>
    public static Option<string> AcdOutputFile() => new("--acd", "-a")
    {
        Description = "Path to the ACD output file",
        Validators =
        {
            optionValue => OptionValidator.FileExtension(optionValue, ".acd"),
        }
    };

    /// <summary>
    /// Creates an --l5x / -l option for specifying a path to an L5X file that must exist.
    /// </summary>
    public static Option<string> L5xInputFile() => new("--l5x", "-l")
    {
        Description = "Path to the L5X input file",
        Validators =
        {
            optionValue => OptionValidator.FileExtension(optionValue, ".l5x"),
            OptionValidator.FileExists,
        }
    };

    /// <summary>
    /// Creates an --l5x / -l option for specifying a path to an L5X output file.
    /// The file does not need to exist but must have a .l5x extension.
    /// </summary>
    public static Option<string> L5xOutputFile() => new("--l5x", "-l")
    {
        Description = "Path to the L5X output file",
        Validators =
        {
            optionValue => OptionValidator.FileExtension(optionValue, ".l5x"),
        }
    };

    /// <summary>
    /// Creates a --dir / -d option for specifying a directory path.
    /// </summary>
    public static Option<string> Directory() => new("--dir", "-d")
    {
        Description = "Path to the exploded L5X directory",
    };

    /// <summary>
    /// Creates a --force / -f option for forcing overwrite without prompting.
    /// </summary>
    public static Option<bool> Force() => new("--force", "-f")
    {
        Description = "Force overwrite of existing files without prompting",
    };

    /// <summary>
    /// Creates a --pretty-attributes / -p option for formatting XML attributes on separate lines.
    /// </summary>
    public static Option<bool> PrettyAttributes() => new("--pretty-attributes", "-p")
    {
        Description = "Format XML attributes by placing each attribute on a separate line for readability",
    };

    /// <summary>
    /// Creates a --format option for specifying the serialization format.
    /// </summary>
    public static Option<L5xSerializationFormat> Format() => new("--format")
    {
        Description = "The serialization format to use.",
        DefaultValueFactory = _ => L5xSerializationFormat.Xml,
    };

    /// <summary>
    /// Creates an --unsafe-skip-dependency-check option to bypass the safety check
    /// for missing AOI dependency information in the L5X file.
    /// </summary>
    public static Option<bool> UnsafeSkipDependencyCheck() => new("--unsafe-skip-dependency-check", "-skipdeps")
    {
        Description = "Bypass the safety check for missing AOI dependency information. " +
                      "Required when the L5X was exported without the 'Dependencies' option. " +
                      "WARNING: The resulting exploded representation may not implode correctly " +
                      "if encoded AOIs have hidden inter-AOI dependencies.",
    };
}
