using L5xGitLib;
using L5xGitLib.Services;
using L5xploderLib;
using L5xploderLib.Enum;
using L5xploderLib.Services;
using RockwellAutomation.LogixDesigner.Logging;
using System.CommandLine;

namespace L5xCommands.Commands;

public static class Commit
{
    public static Command Command {
        get
        {
            var command = new Command("commit", "A command to Copy/Export/l5xplode/commit a representation of a Logix Designer ACD file.");

            var acdOption = CommandOptions.AcdInputFile();
            acdOption.Required = true;
            var unsafeSkipDependencyCheckOption = CommandOptions.UnsafeSkipDependencyCheck();

            command.Options.Add(acdOption);
            command.Options.Add(unsafeSkipDependencyCheckOption);

            command.SetAction(parseResult => 
            {
                var acdPath = parseResult.GetValue(acdOption) ?? throw new ArgumentNullException(nameof(acdOption));
                var unsafeSkipDependencyCheck = parseResult.GetValue(unsafeSkipDependencyCheckOption);

                return Execute(acdPath, unsafeSkipDependencyCheck);
            });

            return command;
        }
    }

    private static async Task Execute(string acdPath, bool unsafeSkipDependencyCheck)
    {
        var logger = new StdOutEventLogger();
        var configFilePath = Paths.GetL5xConfigFilePathFromAcdPath(acdPath);

        logger?.Status(configFilePath, "Loading configuration file...");
        var config = L5xGitConfig.LoadFromFile(configFilePath);

        if (config is null)
        {
            logger?.Status(configFilePath, "Configuration file not found...");
            Console.WriteLine("Configuration file not found.");

            var destPath = UserPrompts.PromptForCommitRestoreDirectory();

            var gitService = EnsureGitRepo(destPath, logger);
            if (gitService is null)
                return;

            var promptForCommitMessage = UserPrompts.PromptForCommitMessageRequiredOnEachCommit();

            config = new L5xGitConfig
            {
                DestinationPath = destPath,
                PromptForCommitMessage = promptForCommitMessage,
            };

            logger?.Status(configFilePath, "Saving configuration file...");
            config.Save(configFilePath);

            await RunCommit(acdPath, config, gitService, logger, unsafeSkipDependencyCheck);
        }
        else
        {
            // Existing config — ensure git repo still exists.
            var gitService = EnsureGitRepo(config.DestinationPath, logger);
            if (gitService is null)
                return;

            await RunCommit(acdPath, config, gitService, logger, unsafeSkipDependencyCheck);
        }
    }

    private static IGitService? EnsureGitRepo(string destinationPath, IOperationEvent? logger)
    {
        var gitService = GitService.Create(destinationPath);
        if (gitService is null)
        {
            if (UserPrompts.PromptToCreateAndInitGitRepo(destinationPath))
            {
                logger?.Status(destinationPath, "Initializing new Git repository...");
                gitService = GitService.Init(destinationPath);
                logger?.Status(destinationPath, "Git repository initialized.");
            }
            else
            {
                logger?.Error(destinationPath, "No Git repository found. Unable to commit changes.");
                return null;
            }
        }
        return gitService;
    }

    private static async Task RunCommit(string acdPath, L5xGitConfig config, IGitService gitService, IOperationEvent? logger, bool unsafeSkipDependencyCheck)
    {
        await PathGuard.Guard(
            path: config.DestinationPath,
            millisecondsTimeout: 0,
            timeoutExceptionText: $"Unable to acquire lock on \"{config.DestinationPath}\". Another instance of this process may be running.",
            action: async () =>
            {
                var commitMessage = UserPrompts.GetCommitMessagePromptIfNeeded(config);
                await CommitFromAcd(acdPath, config, commitMessage, gitService, logger, unsafeSkipDependencyCheck);
            });
    }

    private static async Task<bool> CommitFromAcd(string acdPath, L5xGitConfig config, string commitMessage, IGitService gitService, IOperationEvent? logger, bool unsafeSkipDependencyCheck)
    {
        logger?.Status(acdPath, "Copying ACD to temp path...");
        var tempAcdFile = TempFile.CopyToTempPath(acdPath);
        var tempL5xFile = TempFile.FromTempFileWithNewExtension(tempAcdFile, ".L5X");

        await LogixProjectConverter.ConvertAsync(tempAcdFile.Path, tempL5xFile.Path, logger: logger, overwrite: false);

        logger?.Status(tempL5xFile.Path, "l5xploding L5X...");
        ExplodeL5x(tempL5xFile.Path, config.DestinationPath, unsafeSkipDependencyCheck);

        logger?.Status(config.DestinationPath, "Committing to Git repository...");

        logger?.Status(config.DestinationPath, "Commit message is:" + System.Environment.NewLine + commitMessage);

        var staged = gitService.Stage(config.DestinationPath);
        if (!staged)
        {
            logger?.Error(config.DestinationPath, "Repository is not in an appropriate state to stage changes.");
            return false;
        }
        var commit = await gitService.CommitAsync(commitMessage);
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

    static void ExplodeL5x(string l5xFilePath, string destinationPath, bool unsafeSkipDependencyCheck)
    {
        var defaultOptions = new L5xSerializationOptions()
        {
            PrettyXmlAttributes = false,
            Format = L5xSerializationFormat.Xml,
            OmitExportDate = true,
            UnsafeSkipDependencyCheck = unsafeSkipDependencyCheck,
        };

        var savedOptions = L5xSerializationOptions.LoadFromFile(Paths.GetOptionsFilePath(destinationPath));

        // If we have previously saved options, inherit UnsafeSkipDependencyCheck from them unless
        // the user is explicitly providing the flag now.
        var options = savedOptions != null
            ? new L5xSerializationOptions
            {
                PrettyXmlAttributes = savedOptions.PrettyXmlAttributes,
                Format = savedOptions.Format,
                OmitExportDate = savedOptions.OmitExportDate,
                UnsafeSkipDependencyCheck = unsafeSkipDependencyCheck || savedOptions.UnsafeSkipDependencyCheck,
            }
            : defaultOptions;

        var config = L5xDefaultConfig.DefaultConfig;
        var persistenceHandler = PersistenceServiceFactory.Create(
            explodedDir: destinationPath,
            options: options);

        using var inputStream = new FileStream(l5xFilePath, FileMode.Open, FileAccess.Read);
        L5xExploder.Explode(inputStream, config, persistenceHandler);
    }
}