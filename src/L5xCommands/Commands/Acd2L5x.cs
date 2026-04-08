using System.CommandLine;

namespace L5xCommands.Commands;

public static class Acd2L5x
{
    public static Command Command
    {
        get
        {
            var command = new Command("acd2l5x", "Converts a given ACD to an L5X file.");

            var acdOption = CommandOptions.AcdInputFile();
            acdOption.Required = true;
            var l5xOption = CommandOptions.L5xOutputFile();
            l5xOption.Required = true;

            command.Options.Add(acdOption);
            command.Options.Add(l5xOption);

            command.SetAction(parseResult => 
            {
                var acdPath = parseResult.GetValue(acdOption) ?? throw new ArgumentNullException(nameof(acdOption));
                var l5xPath = parseResult.GetValue(l5xOption) ?? throw new ArgumentNullException(nameof(l5xOption));

                return Execute(acdPath, l5xPath);
            });

            return command;
        }
    }

    private static async Task Execute(string acdPath, string l5xPath)
    {
        var acdFullPath = Path.GetFullPath(acdPath);
        var l5xFullPath = Path.GetFullPath(l5xPath);

        await LogixProjectConverter.ConvertAsync(acdFullPath, l5xFullPath);
    }
}