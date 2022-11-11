![SilverAudioPlayer logo](https://raw.githubusercontent.com/thesilvercraft/SilverAudioPlayer/master/SilverAudioPlayer.Avalonia/textandlogo.svg)
![subtext](https://user-images.githubusercontent.com/46320280/200084291-c9700996-cd9f-4e65-ad0f-ee4dd190e905.svg)
![image](https://user-images.githubusercontent.com/46320280/199335292-e41cf205-1484-4f92-8da3-2964c0bda517.png)
<p>
  <a href="https://github.com/thesilvercraft/SilverAudioPlayer/releases">
    <image src="https://user-images.githubusercontent.com/46320280/201438261-4431164b-f762-4f72-be7c-91e0e5c429c9.svg" width="24%"/>
  </a>
  <a href="https://thesilvercraft.github.io/InstallSAPSilverCraftBucket">
    <image src="https://user-images.githubusercontent.com/46320280/201438267-5e054497-718e-4dbe-af1d-59851c4dcbf7.svg" width="24%"/>
  </a>
   <a href="https://thesilvercraft.github.io/CompileSAPYourself">
    <image src="https://user-images.githubusercontent.com/46320280/201438275-411bc16b-e225-472f-9cbc-aa4d051dd592.svg" width="24%"/>
   </a>

  <a href="https://discord.gg/hM6euqAtsB">
    <image src="https://user-images.githubusercontent.com/46320280/201440304-b865ff0e-a405-4848-861b-2c015e1390bf.svg" width="24%"/>
  </a>
</p>


## Supported enviroments
Windows 7 and above (windows 10 recommended, use windows 11 if you want the ability to enable the Mica effect)

## Enviroments not supported as could be
Linux distributions (the ones dotnet and avalonia run on, with libasound)  
if you know how to implement a better linux IWavePlayer for naudio please consider improving `SilverAudioPlayer.Unix.PlayProviderExtension.Naudio.ASound` as the current implementation handles playing via conversion to WAV and writing the WAV file in its entirety to libasound the moment it can
> **Warning**
> The current as is implementation of `SilverAudioPlayer.Unix.PlayProviderExtension.Naudio.ASound` may lead to hearing loss, please consider lowering your system's output volume before extensive testing on your part. YOU HAVE BEEN WARNED  

## Ways to install
### Github releases
You could grab the latest release from [github releases](https://github.com/thesilvercraft/SilverAudioPlayer/releases) and write it down in a directory that is writable by the program (UI/Plugin settings are stored where the program is stored right now, maybe next release that will be changed)  
### Scoop
You could also install it via [the silvercraft scoop bucket](https://github.com/thesilvercraft/SilverCraftBucket) by using:  
`scoop bucket add silvercraft https://github.com/thesilvercraft/SilverCraftBucket`  
`scoop install silveraudioplayer`  
### Compile it yourself (compiles quickly compared to webbrowsers)
Or if you're feeling daring grab the sources from github by:
`git clone https://github.com/thesilvercraft/SilverAudioPlayer.git`  
And compile it yourself (that's why open source is good)  
`dotnet build`
Before compiling it yourself consider editing `SilverAudioPlayer.Avalonia.csproj`  
In visual studio you can comment out features you dont want by selecting their `<DefineConstraints>` line and using CTRL+K+C  
## What do you mean modular?
Modular means different things for different people but in this case modular would be defined as having code devided up into multiple optional components all housing important code but as previously mentioned are fully optional.
You don't want the awful Jellyfin integration code (I'm critising my code, jellyfin for the most part is awsome)? Remove its module  
You don't want to be able to play midis? Remove the midi module  
You don't want to be able to play flacs? Remove the flac decoder  
You don't want to be able to do anything with the player? Remove the player  

## Versioning
I introduce breaking changes with each update, since v4 I have been attempting to follow some sort of semver.
I make no promises. 
To sum it up where a.b.c.d are the version digits, if a is changed something major API breaking has happened you will have to modify your code to a large extent,  
if b is changed something might break your code but it probably won't update regardless,  
if c is changed you probably don't have to worry at all,   
changes in d shouldn't be noticable at all.  

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
