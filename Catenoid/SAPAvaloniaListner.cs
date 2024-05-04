using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using SilverAudioPlayer.Core;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekonrensis.Avalonia;
using SilverCraft.Ekorensis;
using SilverMagicBytes;

namespace Catenoid;

public class SapAvaloniaPlayerEnviroment(Logic<MainWindowContext> Logic) : BaseEnvironment<MainWindowContext>(Logic)
{
    public override string Name => "Catenoid";

    public override string Description => "A modular hackable audio player.";

    public override WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SapAvaloniaPlayerEnviroment).Assembly,
        "Catenoid.icon.svg");

    public override Version? Version => typeof(SapAvaloniaPlayerEnviroment).Assembly.GetName().Version;

    public override List<Tuple<Uri, URLType>>? Links =>
    [
        new Tuple<Uri, URLType>(
            new Uri("https://gitlab.com/silvercraft/SilverAudioPlayer/tree/master/Catenoid"),
            URLType.Code),
        new Tuple<Uri, URLType>(
            new Uri($"https://www.nuget.org/packages/Avalonia/{typeof(Window).Assembly.GetName().Version}"),
            URLType.PackageManager),
        new Tuple<Uri, URLType>(new Uri("https://github.com/AvaloniaUI/Avalonia"), URLType.LibraryCode),
        new Tuple<Uri, URLType>(new Uri("https://docs.avaloniaui.net/"), URLType.LibraryDocumentation)
    ];
    public override string Licenses => @"Avalonia
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors All Rights Reserved

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
Catenoid
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
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SilverCraftAvaloniav1Shared", "dotfile.json")
    ];

    public override IConfigurableWindow? TryGetWindow(IEnumerable<IConfigurableElement> elements)
    {
        EkonrensisWindow window = new();
        window.HandleConfiguration(elements);
        return window;
    }
}