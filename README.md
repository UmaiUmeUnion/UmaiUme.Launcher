#UmaiUme Launcher

UmaiUme Launcher (or **UULauncher** for short) is a custom Unity game launcher that allows to patch game's assemblies before running the game.

UULauncher currently supports patching ReiPatcher patches only, but more functionality may be added in the future.

### Main Features
* Run instead of the game's executable to patch and launch the game
* Easy to set up
* Automatically restores assemblies to unpatched state when the game exits
* Logs every launch
* Compatible with other patchers (Sybaris, IPA, ReiPatcher, etc.)
* Open-source!

All in all, if you are familiar with Illusion Plugin Architecture or Sybaris, this tool will feel rather familiar.

### How to install (basic guide)

#### 1. Install required tools
UULauncher requires the following assemblies before working:
* ReiPatcher.exe
* Mono.Cecil.dll
* ExIni.dll

**NOTE:** You do not need to have ReiPatcher installed.
**NOTE:** ExIni.dll must be in the same directory as UULauncher. The location of other assemblies can be configured later.

#### 2. Launch UULauncher.exe
Put UULauncher.exe into the game's root and run it once.

On the first launch, UULauncher will prompt you to create the configuration file. Press ``Yes`` to create the configuration file.

For more info, run ``UULauncher.exe -h`` which will show a help screen.

#### 3. Edit ``UULauncher.ini``
Edit ``UULauncher.ini`` with a text editor of your choice. Follow the instructions in the configuration file and edit all the required fields to your preference.

Some ReiPatcher patchers require to add some addition fields into the configuration file (e.g. UnityInjector). Refer to the guide of the specific patcher for more info.

#### 4. Launch UULauncher.exe
Finally, launch UULauncher once more. If the configuration file was edited correctly, UULauncher will patch the game and run it.

### How to use
Just run UULauncher every time you want to launch the patched version of the game.

Running the game's original executable will run the unpatched game.