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
            var acdOption = new Option<string>(
                aliases: ["--acd", "-a"],
                description: "Path to the ACD file to write.")
            {
                IsRequired = true,
            };

            var l5xOption = new Option<string>(
                aliases: ["--l5x", "-l"],
                description: "Path to the L5X file to read.")
            {
                IsRequired = true
            };

            acdOption.AddValidator(result => OptionValidator.FileExtension(result, ".acd"));
            acdOption.AddValidator(OptionValidator.FileExists);
            l5xOption.AddValidator(result => OptionValidator.FileExtension(result, ".l5x"));

            var command = new Command("l5x2acd", "Converts a given ACD to an L5X file.")
            {
                acdOption,
                l5xOption,
            };

            command.SetHandler(Execute, acdOption, l5xOption);
            return command;
        }
    }


    private static async Task Execute(string acdPath, string l5xPath)
    {
        var logger = new StdOutEventLogger();

        var acdFullPath = Path.GetFullPath(acdPath);
        var l5xFullPath = Path.GetFullPath(l5xPath);
        
        await Convert(l5xFullPath, acdFullPath);
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