using LibGit2Sharp;

namespace L5xGitLib.Services;

public sealed class GitService : IGitService
{
    public string RepoRoot => repo.Info.WorkingDirectory ?? string.Empty;
    private readonly Repository repo;
    private bool disposed = false;

    private GitService(string repoPath)
    {
        repo = new Repository(repoPath);
    }

    public static IGitService? Create(string folderPath)
    {
        var repoPath = Repository.Discover(folderPath);
        if (repoPath == null || !Repository.IsValid(repoPath))
        {
            return null;
        }

        return new GitService(repoPath);
    }

    /// <summary>
    /// Initializes a new Git repository at the specified folder path.
    /// Creates the directory if it does not exist.
    /// </summary>
    public static IGitService Init(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        var repoPath = Repository.Init(folderPath);
        return new GitService(repoPath);
    }

    public bool Stage(string folderPath)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(GitService));
        }

        if (!repo.Index.IsFullyMerged)
        {
            return false;
        }

        var normalizedFolderPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        LibGit2Sharp.Commands.Stage(repo, normalizedFolderPath);

        return true;
    }

    public async Task AddAsync(string folderPath)
    {
        await Task.Run(() => Stage(folderPath));
    }

    public GitCommitResult? Commit(string commitMessage)
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(GitService));
        }

        if (!repo.Index.IsFullyMerged)
        {
            return null;
        }

        //  This check guards agaisnt empty commits, but it causes issues with large initial commits,
        //  so we are going to allow empty commits for now.
        //
        // var status = repo.RetrieveStatus();
        // if (!status.Staged.Any())
        // {
        //     return null;
        // }

        var config = repo.Config;
        var name = config.Get<string>("user.name")?.Value ?? "Default User";
        var email = config.Get<string>("user.email")?.Value ?? "default@example.com";

        var author = new Signature(name, email, DateTimeOffset.Now);
        var committer = author;

        var commit = repo.Commit(commitMessage, author, committer);
        return new GitCommitResult(commit.Sha);
    }

    public async Task<GitCommitResult?> CommitAsync(string commitMessage)
    {
        return await Task.Run(() => Commit(commitMessage));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                repo.Dispose();
            }

            disposed = true;
        }
    }

    ~GitService()
    {
        Dispose(false);
    }
}