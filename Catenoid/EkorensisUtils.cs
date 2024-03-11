using SilverAudioPlayer.Shared;
using SilverCraft.Ekonrensis.Avalonia;
using SilverCraft.Ekorensis;
namespace Catenoid;
public static class EkorensisUtils
{
    public static void LaunchConfigurableWindow(IAmConfigurable configurable)
    {
        EkonrensisWindow cw = new();
        cw.HandleConfiguration(configurable);
        if (configurable is ICodeInformation info)
        {
            cw.SetTitle($"Configuring {info.Name}");
        }
        cw.Show();
    }
}