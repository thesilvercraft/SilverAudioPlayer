namespace SilverAudioPlayer.Core;

/// <summary>
/// Abstraction for UI-specific notifications and dispatching to decouple Core logic from UI frameworks.
/// </summary>
public interface IPlayerUiBridge
{
    void ResetUiScrollBar();
    void SetScrollBarTextTo(TimeSpan totalDuration);
    void HandleLateStageMetadataAndScrollBar();
    void ShowMessageBox(string title, string message);
    void PostToUi(Action action);
}

/// <summary>
/// Bridge that adapts <see cref="PlayerContext"/> actions to <see cref="IPlayerUiBridge"/>.
/// </summary>
public class PlayerContextUiBridge(PlayerContext context) : IPlayerUiBridge
{
    private readonly PlayerContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public void ResetUiScrollBar()
    {
        _context.ResetUIScrollBar?.Invoke();
    }

    public void SetScrollBarTextTo(TimeSpan totalDuration)
    {
        _context.SetScrollBarTextTo?.Invoke(totalDuration);
    }

    public void HandleLateStageMetadataAndScrollBar()
    {
        _context.HandleLateStageMetadataAndScrollBar?.Invoke();
    }

    public void ShowMessageBox(string title, string message)
    {
        _context.ShowMessageBox?.Invoke(title, message);
    }

    public void PostToUi(Action action)
    {
        // By default, queue manipulation / UI posting can be executed directly or scheduled
        action();
    }
}
