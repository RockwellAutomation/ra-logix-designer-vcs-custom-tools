using L5xCommands.Commands;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace L5xplode;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("l5xplode - A tool to transform L5X files into an organized XML file structure and back");

        rootCommand.Subcommands.Add(Explode.Command);
        rootCommand.Subcommands.Add(Implode.Command);

        int result;
        try
        {
            var parseResult = rootCommand.Parse(args);
            result = await parseResult.InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            result = 1;
        }

        return result;
    }
}