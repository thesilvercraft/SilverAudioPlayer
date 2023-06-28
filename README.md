![SilverAudioPlayer logo](https://raw.githubusercontent.com/thesilvercraft/SilverAudioPlayer/master/SilverAudioPlayer.Avalonia/textandlogo.svg)
![subtext](https://user-images.githubusercontent.com/46320280/204018020-6b0204a0-5b2d-4306-b6d1-488a7e621faf.svg)
![mockup of silveraudioplayer, interactive version on silverwebsiterepo soon™️](https://user-images.githubusercontent.com/46320280/204107142-3bd7a10a-0f94-4c1f-80b9-593ece8041a2.svg)
![image showing silver audio playing wanna be starting something by micheal jackson with the background gradiant present on brave's start page #4d54d1,#a51c7b,#ee4a37 and a SilverCraftBlue progress bar thank me later](https://user-images.githubusercontent.com/46320280/199335292-e41cf205-1484-4f92-8da3-2964c0bda517.png)
<p>
  <a href="https://github.com/thesilvercraft/SilverAudioPlayer/releases">
    <image src="https://user-images.githubusercontent.com/46320280/204018118-16c4caf3-dcbf-40b0-8f2a-e2c8229d2f59.svg" width="24%"/>
  </a>
  <a href="https://thesilvercraft.github.io/InstallSAPSilverCraftBucket">
    <image src="https://user-images.githubusercontent.com/46320280/204018232-45b66ab6-7080-4212-b366-d046e0542820.svg" width="24%"/>
  </a>
   <a href="https://thesilvercraft.github.io/CompileSAPYourself">
    <image src="https://user-images.githubusercontent.com/46320280/204018350-4ac114f4-096d-4dd6-8e15-4c5d266e09a6.svg" width="24%"/>
   </a>
</p>


## Supported enviroments
Most platforms that can run dotnet 7, Windows 10 (1607+), Windows 11 (22000+), Most linux distributions (see https://github.com/dotnet/core/blob/main/linux-support.md)
In addition to the requirements other dependancies may be required to run SilverAudioPlayer on linux (mostly the ones required by AvaloniaUI), some of them include ALSA, DBUS, X11/Wayland(through XWayland).
SilverAudioPlayer is tested and developed on KDE on an Arch based distribution.
If you know how to implement a better linux IWavePlayer for naudio please consider improving `SilverAudioPlayer.Unix.PlayProviderExtension.Naudio.ASound` as the current implementation handles playing via conversion to WAV and writing the WAV file in its entirety to libasound the moment it can
> **Warning**
> The current as is implementation of `SilverAudioPlayer.Unix.PlayProviderExtension.Naudio.ASound` may lead to hearing loss, please consider lowering your system's output volume before extensive testing on your part. YOU HAVE BEEN WARNED  

Instead of using the NAudio playprovider you can and should use the libvlc one.

If you know how to use MPRIS / DBUS in c# please consider improving `SilverAudioPlayer.Linux.MPRIS` as the current implementation ~~does not even show up in QDBusViewer~~ does not work in playerctl

## Ways to install
### Github releases (windows builds only)
You could grab the latest release from [github releases](https://github.com/thesilvercraft/SilverAudioPlayer/releases) and write it down in a directory that is writable by the program (UI/Plugin settings are stored where the program is stored right now, maybe next release that will be changed)  
### Scoop (windows builds only)
You could also install it via [the silvercraft scoop bucket](https://github.com/thesilvercraft/SilverCraftBucket) by using:  
`scoop bucket add silvercraft https://github.com/thesilvercraft/SilverCraftBucket`  
`scoop install silveraudioplayer`
### Compile it yourself using the builder GUI
Grab the sources from github by:
`git clone https://github.com/thesilvercraft/SilverAudioPlayer.git`  
`bash GUIBuild.sh`
![image](https://user-images.githubusercontent.com/46320280/234542618-e66f588a-499f-4ddd-8cd2-71cef63522c5.png)

### Compile it yourself manually (compiles quickly compared to webbrowsers)
Grab the sources from github by:  
`git clone https://github.com/thesilvercraft/SilverAudioPlayer.git`  
`cd SilverAudioPlayer\SilverAudioPlayer.Avalonia`  
And compile it yourself (that's why open source is good)  
`dotnet build`  
Before compiling it yourself consider editing `SilverAudioPlayer.Avalonia.csproj`  
In visual studio you can comment out features you dont want by selecting their `<DefineConstraints>` line and using CTRL+K+C  
## What do you mean modular?
Modular means different things for different people but in this case modular would be defined as having code divided up into multiple optional components all housing important code but as previously mentioned are fully optional.
You don't want the awful Jellyfin integration code (I'm critising my code, jellyfin for the most part is awesome)? Remove its module  
You don't want to be able to play midis? Remove the midi module  
You don't want to be able to play flacs? Remove the flac decoder  
You don't want to indirectly support google's duopoly by having the chromecast module? Remove it
You don't want to be able to do anything with the player? Remove the player  

## Versioning
I introduce breaking changes with each update, since v4 I have been attempting to follow some sort of semver.
I make no promises. 
To sum it up where a.b.c.d are the version digits, if a is changed something major API breaking has happened you will have to modify your code to a large extent,  
if b is changed something might break your code but it probably won't update regardless,  
if c is changed you probably don't have to worry at all,   
changes in d shouldn't be noticeable at all.  

## Module creation
Follow the [plugin guide](https://github.com/thesilvercraft/SilverAudioPlayer/wiki/Create-a-new-plugin) (and let me know if any issues arise), for now attempt installing [SilverAudioPlayer.Shared](https://www.nuget.org/packages/SilverAudioPlayer.Shared/) to a class library containing a class that implements one of:
- `IPlayProvider` for implementing new players (most of the time you should just implement `INaudioWaveStreamWrapper` if a new player isn't needed to play that file)
- `IMetadataProvider` for implementing new metadata providers (the things that read metadata from files/streams)
- `IMusicStatusInterface` for implementing things that track/control playback (eg. discord rich presence/SMTC/Cd Art Display)
- `IWakeLockProvider` for implementing ways to let the OS know NOT to go to sleep
- `IPlayStreamProvider` for implementing ways of letting the user add new tracks from an external source (eg. internet radio streams, media servers (jellyfin, dlna, etc.))  
Consider looking through the already implemented modules as a reference (copy the good aspects of them, not the bad ones)  
**Don't forget to export your module by adding `[Export(typeof([YOURMODULEINTERFACETYPE]))]`**

## Goals and general TODO
Check out the [issues](https://github.com/thesilvercraft/SilverAudioPlayer/issues)
