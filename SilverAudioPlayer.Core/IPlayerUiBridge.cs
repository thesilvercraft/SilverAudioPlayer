namespace SilverAudioPlayer.Core;

using Shared;
using Shared.StandardRequests;

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
/// Compatibility bridge for legacy context callbacks and mediator based UI notifications.
/// </summary>
public class PlayerContextUiBridge : IPlayerUiBridge
{
    private readonly PlayerContext? _context;
    private readonly IMediator? _mediator;

    public PlayerContextUiBridge(PlayerContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public PlayerContextUiBridge(IMediator mediator) =>
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    public void ResetUiScrollBar()
    {
        if (_mediator != null) _mediator.SendCommand(new ResetUiScrollBarMessage());
        else _context?.ResetUIScrollBar?.Invoke();
    }

    public void SetScrollBarTextTo(TimeSpan totalDuration)
    {
        if (_mediator != null) _mediator.SendCommand(new SetScrollBarTextMessage(totalDuration));
        else _context?.SetScrollBarTextTo?.Invoke(totalDuration);
    }

    public void HandleLateStageMetadataAndScrollBar()
    {
        if (_mediator != null) _mediator.SendCommand(new RefreshMetadataAndScrollBarMessage());
        else _context?.HandleLateStageMetadataAndScrollBar?.Invoke();
    }

    public void ShowMessageBox(string title, string message)
    {
        if (_mediator != null) _mediator.SendCommand(new ShowPlayerMessageBoxMessage(title, message));
        else _context?.ShowMessageBox?.Invoke(title, message);
    }

    public void PostToUi(Action action)
    {
        if (_mediator != null) _mediator.SendCommand(new PostToUiMessage(action));
        else action();
    }
}