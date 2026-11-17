# Taskbar Control

Taskbar Control hides the Windows taskbar while any app you choose is running. Moving the pointer to the screen edge keeps the taskbar hidden. Press the Windows key to open Start and use the taskbar. After you leave Start, the taskbar hides again.

## Get started

1. Download [TaskbarControl.exe](https://github.com/Coolcooli/TaskbarControl/releases/latest/download/TaskbarControl.exe) and save it in the folder where you want to keep it.
2. Run the exe. Select **Choose running app…** to pick an open app, or **Add files…** to select one or more executables.
3. Select **Done**. Open your apps using their usual shortcuts. Taskbar Control keeps watching if you minimize an app or switch windows with Alt+Tab.

The taskbar returns to its usual behavior when the last watched app exits. Apps are matched by their full executable paths. For a game with a separate launcher, select the executable that runs during gameplay.

## Settings and exit

Open the exe again or double-click its notification-area icon to change the list. **Start in the background when I sign in** is enabled by default. Choose **Exit Taskbar Control** to restore the taskbar immediately and stop watching for this session.

## Build from source

The project uses Windows Forms and targets .NET Framework 4.8. From a Visual Studio Developer Command Prompt with the .NET Framework 4.8 targeting pack installed, run the following in the project directory.

```bat
msbuild TaskbarControl.csproj /p:Configuration=Release
```

The executable is written to `bin\Release\TaskbarControl.exe`.
