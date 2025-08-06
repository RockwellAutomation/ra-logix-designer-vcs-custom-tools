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
            var command = new Command("explode", "Expand an L5X file into a multi-file XML representation");

            var l5xOption = new Option<string>("--l5x", "-l")
            {
                Description = "The L5X file to expand into multiple XML files",
                Required = true,
                Validators = 
                {
                    optionValue => OptionValidator.FileExtension(optionValue, ".l5x"),
                    OptionValidator.FileExists,
                }
            };

            var dirOption = new Option<string>("--dir", "-d")
            {
                Description = "The directory to write the resultant XML files and folder structure to",
                Required = true
            };

            var forceOption = new Option<bool>("--force", "-f")
            {
                Description = "Force overwrite of existing files without prompting"
            };

            var prettyAttributesOption = new Option<bool>("--pretty-attributes", "-p")
            {
                Description = "Format XML attributes by placing each attribute on a separate line for readability"
            };

            var formatOption = new Option<L5xSerializationFormat>("--format")
            {
                Description = $"The serialization format to use.",
                DefaultValueFactory = _ => L5xSerializationFormat.Xml
            };

            command.Options.Add(l5xOption);
            command.Options.Add(dirOption);
            command.Options.Add(forceOption);
            command.Options.Add(prettyAttributesOption);
            
            if (Enum.GetNames(typeof(L5xSerializationFormat)).Length > 1)
            {
                command.Options.Add(formatOption);
            }

            command.SetAction(parseResult => 
            {
                var l5xPath = parseResult.GetValue(l5xOption) ?? throw new ArgumentNullException(nameof(l5xOption));
                var dirPath = parseResult.GetValue(dirOption) ?? throw new ArgumentNullException(nameof(dirOption));
                var force = parseResult.GetValue(forceOption);
                var prettyAttributes = parseResult.GetValue(prettyAttributesOption);
                var format = parseResult.GetValue(formatOption);

                Execute(l5xPath, dirPath, force, prettyAttributes, format);
            });

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