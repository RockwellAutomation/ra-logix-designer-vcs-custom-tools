using L5xGitLib;
using RockwellAutomation.LogixDesigner.Logging;
using L5xploderLib;

namespace L5xCommands;

internal static class UserPrompts
{
    public static string GetCommitMessagePromptIfNeeded(L5xGitConfig config)
    {
        return config.PromptForCommitMessage
            ? PromptForCommitMessage()
            : $"l5xcommit User: {Environment.UserName} Workstation: {Environment.MachineName}";
    }

    public static L5xGitConfig PromptUserForConfig(string configFilePath)
    {
        Console.WriteLine("Configuration file not found.");

        var config = new L5xGitConfig
        {
            DestinationPath = PromptForCommitRestoreDirectory(),
            PromptForCommitMessage = PromptForCommitMessageRequiredOnEachCommit(),
        };

        return config;
    }

    public static L5xGitConfig InitializeConfigPromptIfNeeded(string acdPath, StdOutEventLogger? logger)
    {
        var configFilePath = Paths.GetL5xConfigFilePathFromAcdPath(acdPath);

        logger?.Status(configFilePath, "Loading configuration file...");
        var config = L5xGitConfig.LoadFromFile(configFilePath);
        if (config is null)
        {
            logger?.Status(configFilePath, "Configuration file not found...");
            config = PromptUserForConfig(configFilePath);

            logger?.Status(configFilePath, "Saving configuration file...");
            config.Save(configFilePath);
        }

        return config;
    }

    public static string PromptForAcdFilePath()
    {
        Console.WriteLine("");
        Console.WriteLine("Please choose an ACD file to create or overwrite if existing");

        string? acdFilePath = null;
        do
        {
            acdFilePath = PromptForFilePath("AcdFilePath: ").Trim();
        }
        while (string.IsNullOrWhiteSpace(acdFilePath));

        // Ensure the destination path is absolute before saving the config.
        if (!Path.IsPathRooted(acdFilePath))
        {
            acdFilePath = Path.GetFullPath(acdFilePath);
        }

        return acdFilePath;
    }

    public static string PromptForCommitRestoreDirectory()
    {
        Console.WriteLine("");
        Console.WriteLine("Please provide the directory within your Git repo to commit/restore this project to/from");

        string? destPath = null;
        do
        {
            destPath = PromptForDirectory("Directory: ").Trim();

        }
        while (string.IsNullOrWhiteSpace(destPath));

        // Ensure the destination path is absolute before saving the config.
        if (!Path.IsPathRooted(destPath))
        {
            destPath = Path.GetFullPath(destPath);
        }

        return destPath;
    }

    public static bool PromptForCommitMessageRequiredOnEachCommit()
    {
        Console.WriteLine("");
        return PromptYesNo("Would you like to be prompted for a commit message for each commit?");
    }

    public static string PromptForCommitMessage()
    {
        Console.WriteLine("");
        Console.WriteLine("Please enter a commit message for the changes. Press Enter twice to finish:");

        var commitMessageLines = new List<string>();
        string? line;

        do
        {
            Console.Write("Commit message: ");
            line = Console.ReadLine()?.Trim();

            if (!string.IsNullOrWhiteSpace(line))
            {
                commitMessageLines.Add(line);
            }
        } while (!string.IsNullOrWhiteSpace(line)); // Stop when the user presses Enter twice

        return string.Join(Environment.NewLine, commitMessageLines);
    }

    public static bool PromptForFileOverwriteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            Console.WriteLine("");
            Console.WriteLine($"The file '{filePath}' already exists.");
            return PromptYesNo($"Do you want to overwrite it?");
        }

        return true;
    }

    public static bool  PromptForDirectoryOverwriteIfExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Console.WriteLine("");
            Console.WriteLine($"Directory '{directory}' already exists. This operation deletes any existing files recursively in that folder.");
            return PromptYesNo("Do you want to overwrite it?");
        }

        return true;
    }

    private static bool PromptYesNo(string prompt)
    {
        string? response;
        do
        {
            Console.Write($"{prompt} (y/n): ");
            response = Console.ReadLine();
            if (response == null)
                continue;
            response = response.Trim();
            if (response.Equals("y", StringComparison.OrdinalIgnoreCase) || response.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (response.Equals("n", StringComparison.OrdinalIgnoreCase) || response.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        } while (true);
    }

    private static string PromptForFilePath(string prompt, bool mustExist = false)
    {
        string? path;
        do
        {
            Console.Write(prompt);
            path = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (mustExist && !File.Exists(path))
            {
                Console.WriteLine($"'{path}' does not exist");
                continue;
            }

            return path;
        } while (true);
    }

    private static string PromptForDirectory(string prompt, bool mustExist = false)
    {
        string? path;
        do
        {
            Console.Write(prompt);
            path = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (mustExist && !Directory.Exists(path))
            {
                Console.WriteLine($"'{path}' does not exist");
                continue;
            }

            return path;
        } while (true);
    }
}