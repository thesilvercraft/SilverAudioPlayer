# SilverAudioPlayer
A modular audio player, written in c# using dotnet 6, avalonia, naudio, drywetmidi and a lot of other dependancies.  
![image](https://user-images.githubusercontent.com/46320280/199335292-e41cf205-1484-4f92-8da3-2964c0bda517.png)
Modular means different things for different people but in this case modular would be defined as having code devided up into multiple optional components all housing important code but as previously mentioned are fully optional.
You don't want the awful Jellyfin integration code? Remove its module  
You don't want to be able to play midis? Remove the midi module  
You don't want to be able to play flacs? Remove the flac decoder  
You don't want to be able to do anything with the player? Remove the player  
Component/Plugin development would be welcome but semi-difficult since I introduce breaking changes with each update, since v4 I have been attempting to follow some sort of semver so plugin development would actually make sense but I make no promises. To sum it up where 1.2.3.4 are the version digits, if 1 is changed something major API breaking has happened (YOU WILL MOST DEFINETLY NEED TO RECOMPILE/FIX YOUR MODULE FOR THE NEW UPDATE), if 2 is changed something might break your code but it probably won't, if 3 is changed you probably don't have to worry at all, you won't even notice the changes when 4 changes.  
Shortcuts for the app are F1 - Info/About this app menu, F3 - Lyrics, and thats it.
