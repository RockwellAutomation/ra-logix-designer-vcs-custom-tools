using System.CommandLine;
using System.CommandLine.Parsing;

namespace L5xCommands;

internal static class OptionValidator
{
    public static void FileExtension(OptionResult result, string requiredExtension)
    {
        var value = result.GetValueOrDefault<string>();
        if (!string.IsNullOrEmpty(value) && !value.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            result.AddError($"Option \"--{result.Option.Name}\" must end with {requiredExtension}");
        }
    }

    public static void FileExists(OptionResult result)
    {
        var value = result.GetValueOrDefault<string>();
        if (!string.IsNullOrEmpty(value) && !File.Exists(value))
        {
            result.AddError($"Option \"--{result.Option.Name}\" must be a file which exists.");
        }
    }
}