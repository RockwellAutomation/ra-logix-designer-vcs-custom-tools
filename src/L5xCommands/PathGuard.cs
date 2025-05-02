using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace L5xCommands;

internal sealed class PathGuard : IDisposable
{
    private readonly Semaphore semaphore;

    private PathGuard(string path)
    {
        semaphore = new Semaphore(1, 1, GetSemaphoreName(path));
    }

    private bool Acquire(int millisecondsTimeout) => semaphore.WaitOne(millisecondsTimeout);
    private void Release() => semaphore.Release();
    public void Dispose() => semaphore.Dispose();

    private static string GetSemaphoreName(string path)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path)));
        var uniqueString = Convert.ToHexString(hashBytes);
        return @$"Global\L5xSemaphore_{uniqueString}";
    }

    public static async Task Guard(string path, int millisecondsTimeout, string timeoutExceptionText, Func<Task> action)
    {
        using var mutex = new PathGuard(path);
        bool acquired = mutex.Acquire(millisecondsTimeout);

        if (!acquired)
            throw new TimeoutException(timeoutExceptionText);

        try
        {
            await action();
        }
        finally
        {
            mutex.Release();
        }
    }

    public static async Task<T> Guard<T>(string path, int millisecondsTimeout, string timeoutExceptionText, Func<Task<T>> func)
    {
        using var mutex = new PathGuard(path);
        bool acquired = mutex.Acquire(millisecondsTimeout);

        if (!acquired)
            throw new TimeoutException(timeoutExceptionText);

        try
        {
            return await func();
        }
        finally
        {
            mutex.Release();
        }
    }

    public static void Guard(string path, int millisecondsTimeout, string timeoutExceptionText, Action action)
    {
        using var mutex = new PathGuard(path);
        bool acquired = mutex.Acquire(millisecondsTimeout);

        if (!acquired)
            throw new TimeoutException(timeoutExceptionText);

        try
        {
            action();
        }
        finally
        {
            mutex.Release();
        }
    }

    public static T Guard<T>(string path, int millisecondsTimeout, string timeoutExceptionText, Func<T> func)
    {
        using var mutex = new PathGuard(path);
        bool acquired = mutex.Acquire(millisecondsTimeout);

        if (!acquired)
            throw new TimeoutException(timeoutExceptionText);

        try
        {
            return func();
        }
        finally
        {
            mutex.Release();
        }
    }
}