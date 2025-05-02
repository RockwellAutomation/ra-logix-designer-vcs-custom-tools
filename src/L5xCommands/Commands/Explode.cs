using L5xploderLib;
using L5xploderLib.Enum;
using L5xploderLib.Services;
using System.CommandLine;

namespace L5xCommands.Commands;

public static class Explode
{
    public static Command Command {
        get
        {
            var l5xOption = new Option<string>(
                aliases: ["--l5x", "-l"],
                description: "The L5X file to expand into multiple XML files")
            {
                IsRequired = true 
            };

            var dirOption = new Option<string>(
                aliases: ["--dir", "-d"],
                description: "The directory to write the resultant XML files and folder structure to")
            {
                IsRequired = true
            };

            var forceOption = new Option<bool>(
                aliases: ["--force", "-f"],
                description: "Force overwrite of existing files without prompting");

            var prettyAttributesOption = new Option<bool>(
                aliases: ["--pretty-attributes", "-p"],
                description: "Format XML attributes by placing each attribute on a separate line for readability");

            l5xOption.AddValidator(result => OptionValidator.FileExtension(result, ".l5x"));
            l5xOption.AddValidator(OptionValidator.FileExists);

            var command = new Command("explode", "Expand an L5X file into a multi-file XML representation")
            {
                l5xOption,
                dirOption,
                forceOption,
                prettyAttributesOption
            };

            var formatOption = new Option<L5xSerializationFormat>(
                aliases: ["--format"],
                description: $"The serialization format to use.",
                getDefaultValue: () => L5xSerializationFormat.Xml);

            if (Enum.GetNames(typeof(L5xSerializationFormat)).Length > 1)
            {
                command.AddOption(formatOption);
            }

            command.SetHandler(Execute, l5xOption, dirOption, forceOption, prettyAttributesOption, formatOption);

            return command;
        }
    }

    private static void Execute(string l5xFile, string directory, bool force, bool prettyAttributes, L5xSerializationFormat format)
    {
        bool confirmed = force || UserPrompts.PromptForDirectoryOverwriteIfExists(Paths.GetExplodedSubDir(directory));
        if (!confirmed)
        {
            Console.WriteLine($"Exiting without l5xploding '{l5xFile}' into directory '{directory}'.");
            return;
        }

        var config = L5xDefaultConfig.DefaultConfig;
        var persistenceHandler = PersistenceServiceFactory.Create(
            explodedDir: directory,
            // Options are set based on the provided parameters during an explode
            options: new L5xSerializationOptions
            {
                Format = format,
                PrettyXmlAttributes = prettyAttributes,
                OmitExportDate = true,
            });

        using var inputStream = new FileStream(l5xFile, FileMode.Open, FileAccess.Read);
        L5xExploder.Explode(inputStream, config, persistenceHandler);

        Console.WriteLine($"Exploded L5X file '{l5xFile}' into multiple {format} files at '{directory}'.");
    }
}