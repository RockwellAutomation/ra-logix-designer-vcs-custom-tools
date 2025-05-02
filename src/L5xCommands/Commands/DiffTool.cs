
using System.CommandLine;
using L5xploderLib;
using L5xGitLib;
using L5xGitLib.Services;

namespace L5xCommands.Commands;

public static class Difftool
{
    public static Command Command
    {
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

            var command = new Command("difftool", "A command to show the diff of HEAD with the previous commit.")
            {
                acdOption,
            };

            command.SetHandler(Execute, acdOption);
            return command;
        }
    }


    private static void Execute(string acdPath)
    {
        var configFilePath = Paths.GetL5xConfigFilePathFromAcdPath(acdPath);
        var config = L5xGitConfig.LoadFromFile(configFilePath);
        if (config == null)
        {
            Console.Error.WriteLine($"Configuration file not found at {configFilePath}. Please run 'l5xgit commit' first.");
            return;
        }

        using var gitService = GitService.Create(config.DestinationPath);
        if (gitService == null)
        {
            Console.Error.WriteLine($"Failed to initialize Git service for path: {config.DestinationPath}");
            return;
        }
        var repoRoot = gitService.RepoRoot;

        // run a git difftool --dir-diff HEAD~1 command, unfortunately lib2git does not help us here
        // we just use the git CLI
        var diffCommand = $"difftool --dir-diff HEAD~1 --no-prompt";
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = diffCommand,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start the git difftool process.");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error while running git difftool: {ex.Message}");
        }
    }
}