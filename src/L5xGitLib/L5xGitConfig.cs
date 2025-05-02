using YamlDotNet.Serialization;

namespace L5xGitLib;

public sealed class L5xGitConfig
{
    [YamlMember(Alias = "destination_path")] 
    public required string DestinationPath { get; init; }

    [YamlMember(Alias = "prompt_for_commit_message")]
    public required bool PromptForCommitMessage { get; init; }

    public void Save(string filePath)
    {
        var serializer = new SerializerBuilder()
            .WithIndentedSequences()
            .Build();

        var yaml = serializer.Serialize(this);
        File.WriteAllText(filePath, yaml);
    }

    public static L5xGitConfig? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<L5xGitConfig>(yaml);
    }
}