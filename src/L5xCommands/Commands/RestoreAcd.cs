using L5xGitLib;
using L5xploderLib;
using L5xploderLib.Services;
using RockwellAutomation.LogixDesigner;
using RockwellAutomation.LogixDesigner.Logging;
using System.CommandLine;
using System.Text.RegularExpressions;

namespace L5xCommands.Commands;

public static class RestoreAcd
{
    public static Command Command
    {
        get
        {
            var command = new Command("restoreacd", "The inverse of the commit command, this command will overwrite the chosen ACD file with one imploded from Git.");

            var acdOption = new Option<string?>("--acd", "-a")
            {
                Description = "Path to the ACD file to overwrite. If not provided will prompt for path.",
                Validators = 
                {
                    optionValue => OptionValidator.FileExtension(optionValue, ".acd"),
                }
            };

            command.Options.Add(acdOption);

            command.SetAction(parseResult =>
            {
                var acdPath = parseResult.GetValue(acdOption);

                return Execute(acdPath);
            });

            return command;
        }
    }

    private static async Task Execute(string? acdPath)
    {
        var logger = new StdOutEventLogger();

        if (string.IsNullOrWhiteSpace(acdPath))
        {
            acdPath = UserPrompts.PromptForAcdFilePath();
        }

        acdPath = Path.GetFullPath(acdPath);

        var config = UserPrompts.InitializeConfigPromptIfNeeded(acdPath, logger);

        if (!UserPrompts.PromptForFileOverwriteIfExists(acdPath))
        {
            return;
        }

        var acdFileName = Path.GetFileName(acdPath);
        using var tempAcdFile = TempFile.FromSuggestedFileName(acdFileName);
        using var tempL5xFile = TempFile.FromTempFileWithNewExtension(tempAcdFile, ".L5X");

        logger?.Status(tempL5xFile.Path, $"Restoring L5x from {config.DestinationPath}...");
        await L5xImploder.ImplodeAsync(
            outputFilePath: tempL5xFile.Path,
            configs: L5xDefaultConfig.DefaultConfig,
            persistenceService: PersistenceServiceFactory.Create(
                explodedDir: config.DestinationPath,
                options: L5xSerializationOptions.LoadFromFile(Paths.GetOptionsFilePath(config.DestinationPath)) ?? L5xSerializationOptions.DefaultOptions));
        logger?.Status(tempL5xFile.Path, "Restoration of L5x complete.");

        await ConvertL5xToAcd(tempL5xFile.Path, tempAcdFile.Path);

        // Backup the file, same as logix designer would
        if (File.Exists(acdPath))
        {
            var backupFileName = GetAcdBackupFilePath(acdPath);
            File.Copy(acdPath, backupFileName);
        }

        // Now move the temp file to the original ACD path
        File.Move(tempAcdFile.Path, acdPath, true);
    }

    static async Task ConvertL5xToAcd(string l5xFilePath, string acdFilePath)
    {
        Console.WriteLine($"Converting L5X file '{l5xFilePath}' to ACD file '{acdFilePath}'...");
        
        using LogixProject project = await LogixProject.OpenLogixProjectAsync(l5xFilePath, new StdOutEventLogger());
        await project.SaveAsAsync(acdFilePath, true);

        var fileBytes = new FileInfo(acdFilePath).Length;
        if (fileBytes == 0)
        {
            throw new OperationFailedException("Unable to save project: An unknown error has occured", acdFilePath);
        }
    }

    static string GetAcdBackupFilePath(string acdFilePath)
    {
        var dir = Path.GetDirectoryName(acdFilePath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(acdFilePath);
        var ext = Path.GetExtension(acdFilePath).ToUpper();

        var userPart = $"{Environment.UserDomainName}.{Environment.UserName}";
        var backupPrefix = $"{baseName}.{userPart}.BAK";
        var searchPattern = $"{baseName}.{userPart}.BAK*.acd";

        // Regex to match: <baseName>.<userPart>.BAK###.acd
        var regex = new Regex($@"^{Regex.Escape(baseName)}\.{Regex.Escape(userPart)}\.BAK(\d{{3}})\.acd$", RegexOptions.IgnoreCase);

        int maxSeq = 0;
        foreach (var file in Directory.GetFiles(dir, searchPattern))
        {
            var fname = Path.GetFileName(file);
            var match = regex.Match(fname);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int seq) && seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        var nextSeq = maxSeq + 1;
        var backupFileName = $"{backupPrefix}{nextSeq:D3}{ext}";
        return Path.Combine(dir, backupFileName);
    }
}