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
            var dirOption = new Option<string>(
                aliases: ["--dir", "-d"],
                description: "The directory containing the XML files to reconstitute the L5X file")
            {
                IsRequired = true
            };

            var l5xOption = new Option<string>(
                aliases: ["--l5x", "-l"],
                description: "The output L5X file path")
            {
                IsRequired = true
            };

            var forceOption = new Option<bool>(
                aliases: ["--force", "-f"],
                description: "Force overwrite of existing files without prompting");

            l5xOption.AddValidator(result => OptionValidator.FileExtension(result, ".l5x"));
            
            var command = new Command("implode", "Reconstitute an equivalent L5X file from the output of the explode command")
            {
                dirOption,
                l5xOption,
                forceOption
            };

            command.SetHandler(Execute, l5xOption, dirOption, forceOption);

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