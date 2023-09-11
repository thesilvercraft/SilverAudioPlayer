> **Warning**  
> This project is now hosted on https://gitlab.com/silvercraft/SilverAudioPlayer 


![SilverAudioPlayer logo](https://raw.githubusercontent.com/thesilvercraft/SilverAudioPlayer/master/SilverAudioPlayer.Avalonia/textandlogo.svg)
![subtext](https://user-images.githubusercontent.com/46320280/204018020-6b0204a0-5b2d-4306-b6d1-488a7e621faf.svg)
![mockup of silveraudioplayer](https://user-images.githubusercontent.com/46320280/204107142-3bd7a10a-0f94-4c1f-80b9-593ece8041a2.svg)
![image showing silver audio player playing wanna be starting something by micheal jackson with the background gradiant present on brave's start page #4d54d1,#a51c7b,#ee4a37 and a SilverCraftBlue progress bar thank me later](https://user-images.githubusercontent.com/46320280/199335292-e41cf205-1484-4f92-8da3-2964c0bda517.png)
<p>
  <a href="https://github.com/thesilvercraft/SilverAudioPlayer/releases">
    <image src="imgs/downloadlastestgithub.svg" width="24%"/>
  </a>
  <a href="https://thesilvercraft.github.io/InstallSAPSilverCraftBucket">
    <image src="imgs/installusingscoop.svg" width="24%"/>
  </a>
   <a href="https://thesilvercraft.github.io/CompileSAPYourself">
    <image src="imgs/diybuild.svg" width="24%"/>
   </a>
   <a href="https://github.com/thesilvercraft/SilverAudioPlayer/wiki/Build-and-install-using-makepkg">
    <image src="imgs/makepkg.svg" width="24%"/>
   </a>
</p>


## Supported environments
Most platforms that can run dotnet 6  
In addition to the requirements other dependencies may be required to run SilverAudioPlayer on linux: the ones required by Avalonia, ALSA, OpenAL, DBUS, X11/Wayland(through XWayland).  
SilverAudioPlayer is tested and developed on KDE on an Arch based distribution (EndeavourOS).  
If you know how to use MPRIS / DBUS in c# please consider improving `SilverAudioPlayer.Linux.MPRIS` as the current implementation does not work in playerctl, does not show up on kde plasma but does show up in kde connect.

## What do you mean modular?
Modular means different things for different people but in this case modular would be defined as having code divided up into multiple optional components all housing important code but as previously mentioned are fully optional.
You don't want the awful Jellyfin integration code (I'm criticising my code, jellyfin for the most part is awesome)? Remove its module  
You don't want to be able to play midis? Remove the midi module  
You don't want to be able to play flacs? Remove the flac decoder  
You don't want to indirectly support google's duopoly by having the chromecast module? Remove it   
You don't want to be able to do anything with the player? Remove the player  
To remove a module you just need to delete the dll file associated with it (and optionally the configuration files)  

## Versioning
I introduce breaking changes with each update, since v4 I have been attempting to follow some sort of semver.
I make no promises. 
To sum it up where a.b.c.d are the version digits, if a is changed something major API breaking has happened you will have to modify your code to a large extent,  
if b is changed something might break your code but it probably won't update regardless,  
if c is changed you probably don't have to worry at all,   
changes in d shouldn't be noticeable at all.  

### Breaking changes since last major version
v6.2.0 removes an unused method void MakeSureAllIsWell(); in IWillProvideMemory, and IAmOnceAgainAskingYouForYourMemory uses a IEnumerable<ObjectToRemember> instead of ObjectToRemember[]

## Module creation
Follow the [plugin guide](https://github.com/thesilvercraft/SilverAudioPlayer/wiki/Create-a-new-plugin) (and let me know if any issues arise), for now attempt installing [SilverAudioPlayer.Shared](https://www.nuget.org/packages/SilverAudioPlayer.Shared/) to a class library containing a class that implements one of:
- `IPlayProvider` for implementing new players (most of the time you should just implement `INaudioWaveStreamWrapper` if a new player isn't needed to play that file)
- `IMetadataProvider` for implementing new metadata providers (the things that read metadata from files/streams)
- `IMusicStatusInterface` for implementing things that track/control playback (eg. discord rich presence/SMTC/Cd Art Display)
- `IWakeLockProvider` for implementing ways to let the OS know NOT to go to sleep
- `IPlayStreamProvider` for implementing ways of letting the user add new tracks from an external source (eg. internet radio streams, media servers (jellyfin, dlna, etc.))  

Consider looking through the already implemented modules as a reference  
**Don't forget to export your module by adding `[Export(typeof([YOURMODULEINTERFACETYPE]))]`**

## Goals and general TODO
Check out the [issues](https://github.com/thesilvercraft/SilverAudioPlayer/issues)
