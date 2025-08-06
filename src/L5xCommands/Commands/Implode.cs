using L5xploderLib;
using L5xploderLib.Services;
using System.CommandLine;

namespace L5xCommands.Commands;

public static class Implode
{
    public static Command Command
    {
        get
        {
            var command = new Command("implode", "Reconstitute an equivalent L5X file from the output of the explode command");

            var dirOption = new Option<string>("--dir", "-d")
            {
                Description = "The directory containing the XML files to reconstitute the L5X file",
                Required = true
            };

            var l5xOption = new Option<string>("--l5x", "-l")
            {
                Description = "The output L5X file path",
                Required = true,
                Validators = 
                {
                    optionValue => OptionValidator.FileExtension(optionValue, ".l5x"),
                }
            };

            var forceOption = new Option<bool>("--force", "-f")
            {
                Description = "Force overwrite of existing files without prompting"
            };

            command.Options.Add(dirOption);
            command.Options.Add(l5xOption);
            command.Options.Add(forceOption);

            command.SetAction(parseResult => 
            {
                var l5xPath = parseResult.GetValue(l5xOption) ?? throw new ArgumentNullException(nameof(l5xOption));
                var dirPath = parseResult.GetValue(dirOption) ?? throw new ArgumentNullException(nameof(dirOption));
                var force = parseResult.GetValue(forceOption);

                Execute(l5xPath, dirPath, force);
            });

            return command;
        }
    }

    private static void Execute(string l5xFile, string directory, bool force)
    {
        bool confirmed = ConfirmL5xOverwrite(l5xFile, force);
        if (!confirmed)
        {
            return;
        }

        var config = L5xDefaultConfig.DefaultConfig;
        var persistenceHandler = PersistenceServiceFactory.Create(
            explodedDir: directory,
            options: L5xSerializationOptions.LoadFromFile(Paths.GetOptionsFilePath(directory)) ?? L5xSerializationOptions.DefaultOptions);

        L5xImploder.Implode(
            outputFilePath: l5xFile,
            configs: config,
            persistenceService: persistenceHandler);

        Console.WriteLine($"Reassembled L5X file '{l5xFile}' from '{directory}'.");
    }

    private static bool ConfirmL5xOverwrite(string l5xFile, bool force)
    {
        if (!force && File.Exists(l5xFile))
        {
            Console.Write($"File '{l5xFile}' already exists. Overwrite? (y/n): ");
            var response = Console.ReadLine()?.Trim().ToLower();
            if (response != "y" && response != "yes")
            {
                Console.WriteLine("Operation canceled.");
                return false;
            }
        }

        return true;
    }
}