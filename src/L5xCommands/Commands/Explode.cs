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

            var l5xOption = CommandOptions.L5xInputFile();
            l5xOption.Required = true;
            var dirOption = CommandOptions.Directory();
            dirOption.Required = true;
            var forceOption = CommandOptions.Force();
            var prettyAttributesOption = CommandOptions.PrettyAttributes();
            var formatOption = CommandOptions.Format();
            var unsafeSkipDependencyCheckOption = CommandOptions.UnsafeSkipDependencyCheck();

            command.Options.Add(l5xOption);
            command.Options.Add(dirOption);
            command.Options.Add(prettyAttributesOption);
            command.Options.Add(forceOption);
            command.Options.Add(unsafeSkipDependencyCheckOption);
            
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
                var unsafeSkipDependencyCheck = parseResult.GetValue(unsafeSkipDependencyCheckOption);

                try
                {
                    Execute(l5xPath, dirPath, force, prettyAttributes, format, unsafeSkipDependencyCheck);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            });

            return command;
        }
    }

    private static void Execute(string l5xFile, string directory, bool force, bool prettyAttributes, L5xSerializationFormat format, bool unsafeSkipDependencyCheck)
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
                UnsafeSkipDependencyCheck = unsafeSkipDependencyCheck,
            });

        using var inputStream = new FileStream(l5xFile, FileMode.Open, FileAccess.Read);
        L5xExploder.Explode(inputStream, config, persistenceHandler);

        Console.WriteLine($"Exploded L5X file '{l5xFile}' into multiple {format} files at '{directory}'.");
    }
}