using System.CommandLine;

namespace L5xCommands.Commands;

public static class L5x2Acd
{
    public static Command Command
    {
        get
        {
            var command = new Command("l5x2acd", "Converts a given L5x to an ACD file.");

            var acdOption = new Option<string>("--acd", "-a")
            {
                Description = "Path to the ACD file to write.",
                Required = true,
                Validators = 
                {
                    optionValue => OptionValidator.FileExtension(optionValue, ".acd"),
                    OptionValidator.FileExists,
                }
            };

            var l5xOption = new Option<string>("--l5x", "-l")
            {
                Description = "Path to the L5X file to read.",
                Required = true,
                Validators = 
                {
                    optionValue => OptionValidator.FileExtension(optionValue, ".l5x"),
                }
            };

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

        await LogixProjectConverter.ConvertAsync(l5xFullPath, acdFullPath);
    }
}