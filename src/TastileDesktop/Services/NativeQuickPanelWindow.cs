using Windows.Graphics;

namespace TastileDesktop.Services;

public sealed class NativeQuickPanelWindow
{
    private readonly Func<string, Task> _actionHandler;
    
    public NativeQuickPanelWindow(Func<string, Task> actionHandler)
    {
        _actionHandler = actionHandler;
    }
    
    public void Show() { }
    public void Hide() { }
    public void Dispose() { }
}
