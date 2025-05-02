using L5xCommands.Commands;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace L5xplode;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("l5xplode - A tool to transform L5X files into an organized XML file structure and back");

        rootCommand.AddCommand(Explode.Command);
        rootCommand.AddCommand(Implode.Command);

        try
        {
            await rootCommand.InvokeAsync(args);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return -1;
        }
    }
}