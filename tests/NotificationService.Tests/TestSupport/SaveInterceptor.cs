using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NotificationService.Tests.TestSupport;

/// <summary>
/// Runs an action once, just before the first save on the context it is attached to. Used to simulate
/// a competing request writing the same row first, or a save that fails.
/// </summary>
public sealed class BeforeFirstSaveInterceptor(Func<Task> action) : SaveChangesInterceptor
{
    private bool _done;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!_done)
        {
            _done = true;
            await action();
        }
        return result;
    }
}
