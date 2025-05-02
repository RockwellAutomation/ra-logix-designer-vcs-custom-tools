namespace L5xploderLib;

public static class Paths
{
    public static string GetExplodedSubDir(string explodedDir) => Path.Combine(explodedDir, Constants.ExplodedSubDirName);
    public static string GetOptionsFilePath(string explodedDir) => Path.Combine(GetExplodedSubDir(explodedDir), Constants.SerializationOptionsFileName);    
    public static string GetL5xConfigFilePathFromAcdPath(string acdPath) => Path.Combine(Path.GetDirectoryName(acdPath) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(acdPath)}_L5xGit.yml");
}