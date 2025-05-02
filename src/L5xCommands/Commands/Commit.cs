using L5xGitLib;
using L5xGitLib.Services;
using L5xploderLib;
using L5xploderLib.Enum;
using L5xploderLib.Services;
using RockwellAutomation.LogixDesigner;
using RockwellAutomation.LogixDesigner.Logging;
using System.CommandLine;

namespace L5xCommands.Commands;

public static class Commit
{
    public static Command Command {
        get
        {
            var acdOption = new Option<string>(
                aliases: ["--acd", "-a"],
                description: "The path to the ACD file")
            {
                IsRequired = true
            };

            acdOption.AddValidator(result => OptionValidator.FileExtension(result, ".acd"));
            acdOption.AddValidator(OptionValidator.FileExists);

            var command = new Command("commit", "A command to Copy/Export/l5xplode/commit a representation of a Logix Designer ACD file.")
            {
                acdOption,
            };

            command.SetHandler(Execute, acdOption);
            return command;
        }
    }


    private static async Task Execute(string acdPath)
    {
        var logger = new StdOutEventLogger();
        var config = UserPrompts.InitializeConfigPromptIfNeeded(acdPath, logger);

        await PathGuard.Guard(
            path: config.DestinationPath,
            millisecondsTimeout: 0,
            timeoutExceptionText: $"Unable to acquire lock on \"{config.DestinationPath}\". Another instance of this process may be running.",
            action: async () =>
            {
                var commitMessage = UserPrompts.GetCommitMessagePromptIfNeeded(config);
                await CommitFromAcd(acdPath, config, commitMessage, logger);
            });
    }

    private static async Task<bool> CommitFromAcd(string acdPath, L5xGitConfig config, string commitMessage, StdOutEventLogger? logger)
    {
        logger?.Status(config.DestinationPath, "Creating destination directory...");
        Directory.CreateDirectory(config.DestinationPath);

        logger?.Status(acdPath, "Copying ACD to temp path...");
        var tempAcdFile = TempFile.CopyToTempPath(acdPath);
        var tempL5xFile = TempFile.FromTempFileWithNewExtension(tempAcdFile, ".L5X");
        await ConvertAcdToL5x(tempAcdFile.Path, tempL5xFile.Path);

        logger?.Status(tempL5xFile.Path, "l5xploding L5X...");
        ExplodeL5x(tempL5xFile.Path, config.DestinationPath);

        logger?.Status(config.DestinationPath, "Committing to Git repository...");

        logger?.Status(config.DestinationPath, "Commit message is:" + System.Environment.NewLine + commitMessage);
        var gitService = GitService.Create(config.DestinationPath);
        if (gitService is null)
        {
            logger?.Error(config.DestinationPath, "No Git repository found. Please ensure you are targetting a valid Git repository.  Unable to commit changes.");
            return false;
        }

        var staged = gitService.Stage(config.DestinationPath);
        if (!staged)
        {
            logger?.Error(config.DestinationPath, "Repository is not in an appropriate state to stage changes.");
            return false;
        }
        var commit = gitService.Commit(commitMessage);
        if (commit is null)
        {
            logger?.Error(config.DestinationPath, "No changes to commit.");
        }
        else
        {
            logger?.Status(config.DestinationPath, $"Changes committed successfully with commit ID: {commit.Sha}");
            logger?.Status(config.DestinationPath, "You can now push your changes to the remote repository.");
        }

        return true;
    }

    static void ExplodeL5x(string l5xFilePath, string destinationPath)
    {
        var defaultOptions = new L5xSerializationOptions()
        {
            PrettyXmlAttributes = false,
            Format = L5xSerializationFormat.Xml,
            OmitExportDate = true,
        };

        var config = L5xDefaultConfig.DefaultConfig;
        var persistenceHandler = PersistenceServiceFactory.Create(
            explodedDir: destinationPath,
            options: L5xSerializationOptions.LoadFromFile(Paths.GetOptionsFilePath(destinationPath)) ?? defaultOptions);

        using var inputStream = new FileStream(l5xFilePath, FileMode.Open, FileAccess.Read);
        L5xExploder.Explode(inputStream, config, persistenceHandler);
    }

    static async Task ConvertAcdToL5x(string acdFilePath, string l5xFilePath)
    {
        using LogixProject project = await LogixProject.OpenLogixProjectAsync(acdFilePath, new StdOutEventLogger());
        await project.SaveAsAsync(l5xFilePath);

        var fileBytes = new FileInfo(l5xFilePath).Length;
        if (fileBytes == 0)
        {
            throw new OperationFailedException("Unable to save project: An unknown error has occured", l5xFilePath);
        }
    }
}