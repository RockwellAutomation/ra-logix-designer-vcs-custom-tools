using System.CommandLine;
using L5xCommands.Commands;

namespace L5xGit;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var pauseOption = new Option<bool>("--pause", "-p")
        {
            Description = "Pause before exiting."
        };

        var rootCommand = new RootCommand("l5xgit - A tool to perform git-related operations on Logix Designer ACD files");
        new List<Command> {
            Commit.Command,
            RestoreAcd.Command,
            Difftool.Command,
            Explode.Command,
            Implode.Command,
            L5x2Acd.Command,
        }.ForEach(subCommand => {
            subCommand.Options.Add(pauseOption);
            rootCommand.Subcommands.Add(subCommand);
        });

        var parseResult = rootCommand.Parse(args);
        var pause = parseResult.GetValue(pauseOption);

        int result;
        try
        {
            result = await parseResult.InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            result = -1;
        }

        if (pause)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        return result;
    }
}
