namespace L5xGitLib.Services;

/// <summary>
/// Abstraction over Git repository operations.
/// </summary>
public interface IGitService : IDisposable
{
    /// <summary>
    /// Gets the working directory root of the repository.
    /// </summary>
    string RepoRoot { get; }

    /// <summary>
    /// Stages all changes under the specified folder path.
    /// Returns <c>false</c> if the repository is not in a state that allows staging (e.g. unresolved merge).
    /// </summary>
    bool Stage(string folderPath);

    /// <summary>
    /// Asynchronously stages all changes under the specified folder path.
    /// </summary>
    Task AddAsync(string folderPath);

    /// <summary>
    /// Creates a commit with the specified message.
    /// Returns <c>null</c> when there is nothing to commit or the repo state prevents it.
    /// </summary>
    GitCommitResult? Commit(string commitMessage);

    /// <summary>
    /// Asynchronously creates a commit with the specified message.
    /// </summary>
    Task<GitCommitResult?> CommitAsync(string commitMessage);
}
