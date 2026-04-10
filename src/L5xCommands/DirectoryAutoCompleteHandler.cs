namespace L5xCommands;

/// <summary>
/// Provides Tab-completion for directory paths in console prompts.
/// Used by the ReadLine library to suggest matching directories as the user types.
/// Separators is empty so ReadLine passes the full input to GetSuggestions
/// rather than splitting on path separators.
/// </summary>
internal class DirectoryAutoCompleteHandler : IAutoCompleteHandler
{
    // Do NOT split on directory separators — we need the full path in GetSuggestions
    public char[] Separators { get; set; } = [];

    public string[] GetSuggestions(string text, int index)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Directory.GetDirectories(".")
                    .Select(d => d.TrimStart('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            var directory = Path.GetDirectoryName(text);
            var prefix = Path.GetFileName(text);

            // If text ends with a separator (e.g. "C:\"), treat it as a directory listing
            if (text.EndsWith(Path.DirectorySeparatorChar) || text.EndsWith(Path.AltDirectorySeparatorChar))
            {
                directory = text;
                prefix = "";
            }

            if (string.IsNullOrEmpty(directory))
                directory = ".";

            if (!Directory.Exists(directory))
                return [];

            var pattern = string.IsNullOrEmpty(prefix) ? "*" : prefix + "*";

            return Directory.GetDirectories(directory, pattern)
                .Select(d => d + Path.DirectorySeparatorChar)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
