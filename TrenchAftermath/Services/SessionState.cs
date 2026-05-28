using TrenchAftermath.Domain;

namespace TrenchAftermath.Services;

/// <summary>
/// Shared state for the currently loaded warband session.
/// Persists in memory across page navigations.
/// </summary>
public sealed class SessionState
{
    public WarbandSession? CurrentWarband { get; private set; }

    public event Action? OnChange;

    public void SetWarband(WarbandSession? warband)
    {
        CurrentWarband = warband;
        OnChange?.Invoke();
    }

    public void ClearWarband()
    {
        CurrentWarband = null;
        OnChange?.Invoke();
    }
}