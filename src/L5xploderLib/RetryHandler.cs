
using System.Diagnostics;

namespace L5xploderLib;

internal static class RetryHandler
{
    public static T RetryOnException<T>(Func<T> func, int maxRetries = 3, int delayMilliseconds = 100, params Type[] retryOnExceptions)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return func();
            }
            catch (Exception ex) when (retryOnExceptions.Any(e => e.IsInstanceOfType(ex)) && attempt < maxRetries)
            {
                Thread.Sleep(delayMilliseconds);
            }
        }

        throw new UnreachableException($"Failed after {maxRetries} attempts.");  // This should never be reached because we either return, retry, or throw an exception above.
    }

    public static void RetryOnException(Action action, int maxRetries = 3, int delayMilliseconds = 100, params Type[] retryOnExceptions)
    {
        RetryOnException<object>(() =>
        {
            action();
            return null!;
        }, maxRetries, delayMilliseconds, retryOnExceptions);
    }
}