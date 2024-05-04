using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Console;

public class ConsoleEnvironment(Logic<Context> Logic) : BaseEnvironment<Context>(Logic)
{
    public override string Name => "SilverAudioPlayer.Consol";

    public override string Description => "console UI for SilverAudioPlayer";

    public override WrappedStream? Icon => null;

    public override Version? Version => typeof(ConsoleEnvironment).Assembly.GetName().Version;

    public override List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/SilverAudioPlayer.Console"),
            URLType.Code),
    ];
    public override string Licenses => @"SilverAudioPlayer.Console
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
";

    public override string[] KnownConfigFileLocations =>
    [
        ConfigPath.GetPath("SilverAudioPlayer.Config.xml"),
    ];
}