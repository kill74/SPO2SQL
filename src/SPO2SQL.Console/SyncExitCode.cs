namespace SPO2SQL;

/// <summary>
/// Carries the synchronization outcome from the hosted service to the process
/// entry point. <see cref="Environment.ExitCode"/> cannot be used here because
/// <c>Main</c> returns an explicit integer, which always wins.
/// </summary>
public sealed class SyncExitCode
{
    private int _value;

    public int Value => Volatile.Read(ref _value);

    public void Fail() => Interlocked.CompareExchange(ref _value, 1, 0);
}
