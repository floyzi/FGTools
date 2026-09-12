# FGTools
Fall Guys mod that adds additional features to the game

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
- And much more minor changes and improvements, see them yourself!
### In Development ™...
- LAN multiplayer

## Installation
### Installation Via FGLauncher
1. Get [the latest release of FG Launcher](https://www.mediafire.com/file/hz04riz2gtmbljh/FGLauncherV1.1.2.zip/file)
2. Extract it into any directory
3. Launch FGLauncher executable and follow instructions you see
### Manual Installation
> [!NOTE]
> If you already have BepInEx installed you can skip steps from 2 to 5
1. Download the Latest Release of the mod
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
> Instructions below are for BepInEx deinstallation! If you only need to remove FGTools but keep other mods you have in BepInEx/plugins delete FGTools directory
### Deinstallation Via FGLauncher
- On your Fall Guys slot click on the Toggle Mods button. Worth to say that it only disables BepInEx, not removes it completely, if you need to completely remove BepInEx look at manual section
### Manual Deinstallation
1. In Fall Guys directory open `FallGuys_client.ini` in any text editor like notepad (check if you have file extensions enabled if you don't see it)
    - Change first line of the file (`TargetApplicationPath`) from `FallGuys_client_game.exe` to `start_protected_game.exe` then save the file (in notepad: File -> Save or `CTRL+S`)
2. Delete BepInEx entry file `winhttp.dll` from the Fall Guys directly (deletion of other folders isn't necessary)

## Still need help?
- Check out my [Usage Tutorial](https://youtube.com/watch?v=eShQOpZkjFw), it covers installation and deinstallation of the mod and provides basic usage instructions. Even though it may be a bit outdated in general everything is almost the same as it was
- If you still need help ask for it in the [Discord Server](https://dsc.gg/obedguys)

## Acknowledgements
### FGTools Development
- Kota - General help with code, some Creative Expansion Pack code
- [RRM1](https://github.com/RRM101) - General help with code, some [fallguyloadr](https://github.com/RRM101/fallguyloadr) code
### FGTools Localizators
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
Default hotkey for menu is `F2`. If pressing it does nothing try pressing `FN + F2`. If you don't have `F2` key at all check [this](https://technicskeyboard.com/how-to-press-an-f-key-that-keyboard-doesnt-have/) or change hotkey in config that you can find in `BepInEx/config/` directory as `flz.fgt.cfg`

### How do I edit config
In FGTools UI go to `Config` tab, change settings you need. If you need to edit config file manually it can be found in `BepInEx/config/` directory as `flz.fgt.cfg`, shortcut to open config file can be found at the bottom of `Misc` tab

### I see "Application Error" when I launch the game
If you see an Application Error when you launch the game you didn't complete .ini edit, follow step 5 [here](#manual-installation)

### Is there any mobile analog of the FGTools
Yes! Check out [FGTools Mobile](https://gitlab.com/floyzi/fgtoolsmobile)

### After entering Main Menu the only thing I see is a grey window that covers the whole screen
This happens because you're using incompatible version of Universe Lib. To fix this issue look for .dll files that have UniverseLib in it's name and delete them, once deleted copy UniverseLib that comes with FGTools into plugins directory. You may also need to delete old UnityExplorer version as well

### How do I install FGTools
See [installation instructions](#installation)

### How do I uninstall FGTools
See [deinstallation instructions](#deinstallation)

### My question is not on the list...
You can always ask it in the [Discord Server](https://dsc.gg/obedguys). Just don't ask really dumb questions, please

## License
[![GNU GPLv3 Image](https://www.gnu.org/graphics/gplv3-127x51.png)](http://www.gnu.org/licenses/gpl-3.0.en.html)