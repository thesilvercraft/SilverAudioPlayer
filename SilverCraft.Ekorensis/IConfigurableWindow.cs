namespace SilverCraft.Ekorensis;

public interface IConfigurableWindow
{
    /// <summary>
    /// Close and dispose of this window, once closed this window cannot be reopened
    /// </summary>
    void Close();
    /// <summary>
    /// Opens and shows the window
    /// </summary>
    void Show();
}