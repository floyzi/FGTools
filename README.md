# FGTools
Fall Guys mod that adds additional features to the game to improve your experience and help with level editor and rounds training

<p>
    <a href="https://github.com/floyzi/FGTools/releases/latest" title="Latest Release" target="_blank">
        <img src="https://img.shields.io/github/v/release/floyzi/FGTools?include_prereleases&display_name=release" /></a>
    <a href="https://github.com/floyzi/FGTools/releases/latest" title="Total Downloads" target="_blank">
        <img src = "https://img.shields.io/github/downloads-pre/floyzi/FGTools/total"></a>
    <a href="https://dsc.gg/obedguys" title="Obed Guys Corp Discord" target="_blank">
    <img src="https://img.shields.io/badge/Join%20The%20Discord-5865F2?logo=Discord&logoColor=fff" /></a>
</p>

## Showcase
|      |      |
| :--: | :--: |
|![FGTools UI](Misc/Images/1.png) FGTools UI | ![Locker Search](Misc/Images/2.png) Locker Search 
|![Round loader with speedrun mode](Misc/Images/3.png) Round Loader with speedrun mode | ![Creative Local Saves Browser](Misc/Images/4.png) Creative Local Saves Browser

## Features
### As of now...
- General:
    - Search in locker
    - All cosmetics unlocker (client side)
    - Menu theme selector
    - Loadout presets
    - Discord RPC
- Round Loader:
    - Speedrun mode
    - Round rules
    - Local explore
    - Show loader
    - Free camera mode
- Creative:
    - Autosave system
    - On device level backups
    - IMG to Creative level converter (deprecated)
- And more!
### In Development ™...
- LAN multiplayer

## Installation
### Installation Via FGLauncher
1. Get the [Latest Release](https://www.mediafire.com/file/hz04riz2gtmbljh/FGLauncherV1.1.2.zip/file) of FG Launcher
2. Extract it into any directory
3. Launch FGLauncher executable and follow instructions you see

### Manual Installation
> [!NOTE]
> If you already have BepInEx installed you can skip steps from 2 to 5
1. Download the [Latest Release](https://github.com/floyzi/FGTools/releases/latest) of the mod
2. Download the latest [BepInEx Bleeding Edge](https://builds.bepinex.dev/projects/bepinex_be) build for Unity Il2Cpp X64
3. Locate your Fall Guys installation directory
4. Drop everything from BepInEx release you downloaded earlier into Fall Guys folder
5. In Fall Guys directory open `FallGuys_client.ini` in any text editor like notepad (check if you have file extensions enabled if you don't see it)
    - Change first line of the file (`TargetApplicationPath`) from `start_protected_game.exe` to `FallGuys_client_game.exe` then save the file (in notepad: File -> Save or `CTRL+S`)
6. In Fall Guys directory follow this path: `BepInEx/plugins` (if `plugins` directory doesn't exist create it manually)
7. Drop everything from the Latest Release of the mod into `plugins` directory
8. You're all set! Launch the game via launcher

## Deinstallation
> [!WARNING]
> Instructions below are for BepInEx deinstallation! If you only need to remove FGTools in BepInEx/plugins delete FGTools directory. Keep in mind that FGTools directory hosts all of your presets and locally saved creative levels, if you need them later it's better for you to temporary move FGTools directory outside of plugis directory instead of deleting
### Deinstallation Via FGLauncher
- On your Fall Guys slot click on the Toggle Mods button. Worth to say that it only disables BepInEx, not removes it completely, if you need to completely remove BepInEx look at manual section
### Manual Deinstallation
1. In Fall Guys directory open `FallGuys_client.ini` in any text editor like notepad (check if you have file extensions enabled if you don't see it)
    - Change first line of the file (`TargetApplicationPath`) from `FallGuys_client_game.exe` to `start_protected_game.exe` then save the file (in notepad: File -> Save or `CTRL+S`)
2. Delete BepInEx entry file `winhttp.dll` from the Fall Guys directly (deletion of other files isn't necessary)

## Still need help?
- Check out my [Usage Tutorial](https://youtube.com/watch?v=eShQOpZkjFw), it covers installation and deinstallation of the mod and provides basic usage instructions. Even though it may be a bit outdated in general everything is almost the same as it was
- If you still need help ask for it in the [Discord Server](https://dsc.gg/obedguys)

## Acknowledgements
### FGTools Development
- Kota - General help with code, some Creative Expansion Pack code
- [RRM1](https://github.com/RRM101) - General help with code, some [fallguyloadr](https://github.com/RRM101/fallguyloadr) code
### FGTools Translation
- XiaoBai / Fall Guy 0294 - translated FGTools on Chinese
- ArenaCloser12 - translated FGTools on Korean
- Nemui_gamer - translated FGTools on Japanese
- ItzAqua! - translated FGTools on Spanish
- English & Russian were translated by me
### Other
- Special thanks to [sinai-dev](https://github.com/sinai-dev) who made [Unity Explorer](https://github.com/sinai-dev/UnityExplorer) and [Universe Lib](https://github.com/sinai-dev/UniverseLib)
- Special thanks to [yukieiji](https://github.com/yukieiji) for keeping [Unity Explorer](https://github.com/yukieiji/UnityExplorer) and [Universe Lib](https://github.com/yukieiji/UniverseLib) updated
- Special thanks to [repinek](https://github.com/repinek) for making [IMG To FGC](https://github.com/repinek/ImgToFGC)


## Building
TODO

## FAQ
### Can I play online with FGTools
Yes! FGTools alone allows you to play online matches with the mod. If you can't play online look for other mods that may interfere FGTools work or check if Fall Guys servers are up and your internet connection is fine

### Can I get banned for using FGTools
No. All bans in Fall Guys are tied up to user reports, when you're using FGTools you appear the same as if you played without mods for other players, this means there are no reason to report you. But remember that using mods is always a risk, even though Fall Guys Devs never banned anyone who used mods before the chances are never equal to zero

### Why I appear as a console player
FGTools, as any other Fall Guys mod that allow you to play online, spoofs your platform to console to prevent the server from disconnecting you from the match. Unfortunately, you can't change that

### How do I open FGTools menu
Default hotkey for menu is `F2`. If pressing it does nothing try pressing `FN + F2`. If you don't have `F2` key at all check [this](https://technicskeyboard.com/how-to-press-an-f-key-that-keyboard-doesnt-have/) or change hotkey in config that you can find in `BepInEx/config/` directory as `flz.fgt.cfg` (after you changed the hotkey you may need to restart the game for changes to apply)

### I don't see cursor, how do i get it back!?
Press `F1`, see question above if you have problems with that

### How do I edit config
In FGTools UI go to `Config` tab, change settings you need. If you need to edit config file manually it can be found in `BepInEx/config/` directory as `flz.fgt.cfg`, shortcut to open config file can be found at the bottom of `Misc` tab

### Is there any mobile analog of the FGTools
Yes! Check out [FGTools Mobile](https://gitlab.com/floyzi/fgtoolsmobile)

### Which button do I press to enable cheats online
FGTools **does not provide you any tools to cheat in online matches.** All "cheats" FGTools has are only available to use in Round Loader where you play completely alone

### How do I install FGTools
See [installation instructions](#installation)

### How do I uninstall FGTools
See [deinstallation instructions](#deinstallation)

## Troubleshooting

### I want to report a bug, how can i do it
You can either [create an issue on GitHub](https://github.com/floyzi/FGTools/issues/new) or, if you don't have a GitHub account, [create an issue on the Discord Server](https://discord.com/channels/1156450016408391750/1175624351136108596)

### I see "Application Error" when I launch the game
If you see an Application Error when you launch the game you didn't complete .ini edit, follow step 5 [here](#manual-installation)

### I see "Untrusted system file" error when I launch the game
It occurs because of the same reason as question above: you didn't install the mod correctly. To fix this see [question above](#i-see-application-error-when-i-launch-the-game)

### "No exchange code was found"
This error occurs because you launched the game directly from `FallGuys_client_game.exe`. You need to launch the game from the launcher (Steam or Epic)

### "Epic Games Account Error"
This error may occur for the same reason as ["No exchange code was found"](#no-exchange-code-was-found) error. To fix it make sure you're launching the game from the launcher. This error may also occur when Epic Games services are down, you can check their status [here](https://status.epicgames.com/)

### Antivirus flags mod as malware
This is a false positive, FGTools is a FOSS project, it's source code can be viewed in this repository and compiled releases are easy to decompile. If you still don't beleive me you can test it on [Virus Total](https://www.virustotal.com)

### After entering Main Menu the only thing I see is a grey window that covers up the whole screen
This happens because you're using incompatible version of Universe Lib. To fix this issue look for .dll files that have UniverseLib in it's name and delete them, once deleted copy UniverseLib that comes with FGTools into plugins directory. You may also need to delete old UnityExplorer version as well

### "FGTools encountered an exception on startup."
This error can occur only in two cases:
#### Fall Guys client was updated
In rare cases after Fall Guys client updates FGTools code may be no longer compatible with the new client. To fix this you need to wait for me to update the mod
#### Fall Guys client never updated
If you see this error knowing the fact that Fall Guys didn't receive any updates since latest FGTools release this error may be caused by your antivirus that can messup BepInEx files. To fix this disable antivirus you have and restart the game, in rare cases you may be needed to reinstall BepInEx

### I see UniverseLib error
UniverseLib is a library that FGTools UI is depends on, if you get errors like "Unable to find UniverseLib dll" or "Installed UniverseLib version X does not match the required version Y" this means you didn't complete installation correctly. Download the latest release of FGTools and put UniverseLib from there inside BepInEx/plugins directory

### "Failed to generate Il2Cpp interop assemblies"
This error is not related to FGTools in any way but it may occur because you haven't installed BepInEx correctly, your ISP is blocking BepInEx for whatever reason or BepInEx services may be temporary down. Try enabling VPN, changing your network and reinstalling/updating BepInEx, BepInEx releases can be found [here](https://builds.bepinex.dev/projects/bepinex_be)

### Obstacle X does not work on round Y
Not all obstacles work when you using round loader and some obstacles may behave different than in online matches. I am working on fixing more obstacles but as of now not every obstacle is ready in round loader.

### I stuck on loading screen
Normal game loading time with FGTools shouldn't really differ from loading without mods. If you beleive you actually got stuck on loading screen check BepInEx console and make a report if you see an error here. Instructions on how to make a bug report can be found [here](#i-want-to-report-a-bug-how-can-i-do-it). If there are no errors you see in console you may just need to wait a little bit more or restart the game

### "Сongratulations! Your game just crashed!"
When your game crashes you may see this popup. BepInEx and FGTools are far from being called stable things which means your game can crash from time to time. To help me with fixing this FGTools creates a proper crash reports that have needed info for me to fix this. Instructions on how to make a bug report can be found [here](#i-want-to-report-a-bug-how-can-i-do-it)

## Have a question that is not listed above?
You can always ask it in the [Discord Server](https://dsc.gg/obedguys). Just make sure your question **ACTUALLY** has not been answered above.<br>
Also, check [reports history](https://discord.com/channels/1156450016408391750/1175624351136108596) on the [Discord Server](https://dsc.gg/obedguys), there are a good chance that your question actually already has an answer!

## License
[![GNU GPLv3 Image](https://www.gnu.org/graphics/gplv3-127x51.png)](http://www.gnu.org/licenses/gpl-3.0.en.html)