using System.CommandLine;
using L5xCommands.Commands;

namespace L5xGit;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var pauseOption = new Option<bool>(
            aliases: new[] { "--pause", "-p" },
            description: "Pause before exiting."
        );

        var rootCommand = new RootCommand("l5xgit - A tool to perform git-related operations on Logix Designer ACD files");
        rootCommand.AddGlobalOption(pauseOption);

        rootCommand.AddCommand(Commit.Command);
        rootCommand.AddCommand(RestoreAcd.Command);
        rootCommand.AddCommand(Difftool.Command);
        rootCommand.AddCommand(Explode.Command);
        rootCommand.AddCommand(Implode.Command);
        rootCommand.AddCommand(L5x2Acd.Command);


        var pause = rootCommand.Parse(args).GetValueForOption(pauseOption);

        try
        {
            int result = await rootCommand.InvokeAsync(args);

            if (pause)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (pause)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }

            return -1;
        }
    }
}
