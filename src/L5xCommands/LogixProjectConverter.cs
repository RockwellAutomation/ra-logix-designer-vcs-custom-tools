using RockwellAutomation.LogixDesigner;
using RockwellAutomation.LogixDesigner.Logging;

namespace L5xCommands;

internal static class LogixProjectConverter
{
    internal static async Task ConvertAsync(string sourceFilePath, string destinationFilePath, IOperationEvent? logger = null, bool overwrite = true)
    {
        using LogixProject project = await LogixProject.OpenLogixProjectAsync(sourceFilePath, logger ?? new StdOutEventLogger());

        logger?.Status(sourceFilePath, $"Converting to '{destinationFilePath}'...");
        await project.SaveAsAsync(destinationFilePath, overwrite);

        var fileBytes = new FileInfo(destinationFilePath).Length;
        if (fileBytes == 0)
        {
            throw new OperationFailedException("Unable to save project: An unknown error has occured", destinationFilePath);
        }
    }
}
