using RockwellAutomation.LogixDesigner;
using RockwellAutomation.LogixDesigner.Logging;
using System.CommandLine;

namespace L5xCommands.Commands;

public static class Acd2L5x
{
    public static Command Command
    {
        get
        {
            var command = new Command("acd2l5x", "Converts a given ACD to an L5X file.");

            var acdOption = new Option<string>("--acd", "-a")
            {
                Description = "Path to the ACD file to write.",
                Required = true,
                Validators = 
                {
                    optionValue => OptionValidator.FileExtension(optionValue, ".acd"),
                    OptionValidator.FileExists,
                }
            };

            var l5xOption = new Option<string>("--l5x", "-l")
            {
                Description = "Path to the L5X file to read.",
                Required = true,
                Validators = 
                {
                    optionValue => OptionValidator.FileExtension(optionValue, ".l5x"),
                },
            };

            command.Options.Add(acdOption);
            command.Options.Add(l5xOption);

            command.SetAction(parseResult => 
            {
                var acdPath = parseResult.GetValue(acdOption) ?? throw new ArgumentNullException(nameof(acdOption));
                var l5xPath = parseResult.GetValue(l5xOption) ?? throw new ArgumentNullException(nameof(l5xOption));

                return Execute(acdPath, l5xPath);
            });

            return command;
        }
    }

    private static async Task Execute(string acdPath, string l5xPath)
    {
        var logger = new StdOutEventLogger();

        var acdFullPath = Path.GetFullPath(acdPath);
        var l5xFullPath = Path.GetFullPath(l5xPath);

        await Convert(acdFullPath, l5xFullPath);
    }

    static async Task Convert(string acdFilePath, string l5xFilePath)
    {
        Console.WriteLine($"Converting ACD file '{acdFilePath}' to L5X file '{l5xFilePath}'...");
        
        using LogixProject project = await LogixProject.OpenLogixProjectAsync(acdFilePath, new StdOutEventLogger());
        await project.SaveAsAsync(l5xFilePath, true);

        var fileBytes = new FileInfo(l5xFilePath).Length;
        if (fileBytes == 0)
        {
            throw new OperationFailedException("Unable to save project: An unknown error has occured", l5xFilePath);
        }
    }
}