using L5xGitLib;
using L5xploderLib;
using L5xploderLib.Services;
using RockwellAutomation.LogixDesigner;
using RockwellAutomation.LogixDesigner.Logging;
using System.CommandLine;
using System.Text.RegularExpressions;

namespace L5xCommands.Commands;

public static class L5x2Acd
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
            l5xOption.AddValidator(result => OptionValidator.FileExtension(result, ".l5x"));
            l5xOption.AddValidator(OptionValidator.FileExists);

            var command = new Command("l5x2acd", "Converts a given L5x to an ACD file.")
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

    static async Task Convert(string l5xFilePath, string acdFilePath)
    {
        Console.WriteLine($"Converting L5X file '{l5xFilePath}' to ACD file '{acdFilePath}'...");
        
        using LogixProject project = await LogixProject.OpenLogixProjectAsync(l5xFilePath, new StdOutEventLogger());
        await project.SaveAsAsync(acdFilePath, true);

        var fileBytes = new FileInfo(acdFilePath).Length;
        if (fileBytes == 0)
        {
            throw new OperationFailedException("Unable to save project: An unknown error has occured", acdFilePath);
        }
    }
}