using System;
using System.Threading.Tasks;

namespace QuickFix.Util;

internal static class Extensions
{
    /// <summary>
    /// Allows us to get rid of UnobservedTaskException for Fire and Forget Task.<br/>
    /// The exception becomes observed if we see it, await it or Wait it, and retrieve the Result property (we can also retrieve Exception, but this will only work for the IsFaulted state).
    /// </summary>
    /// <param name="task"></param>
    public static async void ForgetTask(this Task task)
    {
        var t = task;
        if (t is null || t.IsCompletedSuccessfully)
            return;

        if (t.IsFaulted)
        {
            try { _ = t.Exception; } catch { }
            return;
        }

        await t.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <summary>
    /// Gets the task from <see cref="IAsyncResult"/>.
    /// </summary>
    /// <param name="asyncResult">The asynchronous result.</param>
    /// <returns><see langword="null"/> if not found.</returns>
    public static Task GetTask(this IAsyncResult asyncResult)
    {
        var asyncRes = asyncResult;
        if (asyncRes is null)
            return null;

        if (asyncRes is Task task)
            return task;

        try
        {
            return TaskToAsyncResult.Unwrap(asyncRes);
        }
        catch
        {
            return null;
        }
    }
}
