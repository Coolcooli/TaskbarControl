# Taskbar Control

Taskbar Control keeps the Windows taskbar from popping up over a borderless game when your mouse reaches the bottom of the screen. Choose which games or apps to watch. While any of them is running, the taskbar stays hidden. Press the **Windows key** to open Start and use the taskbar, then switch back to your game.

[Download Taskbar Control for Windows](https://github.com/Coolcooli/TaskbarControl/releases/latest/download/TaskbarControl.exe)

## Playing a 16:9 game on an ultrawide monitor

Say you play at **2560 × 1440 (16:9)** in a borderless window centered on a **3440 × 1440 ultrawide**. The bottom edge of the game lines up with the bottom of the screen. When you move your mouse down to a menu button, Windows can reveal the taskbar right over the button you meant to click. Add the game's executable to Taskbar Control and you can use that edge of the window without bringing up the taskbar.

## Other times it comes in handy

- **If you already use taskbar auto-hide**, you can block accidental reveals just for certain apps. For example, add your video editor so you can scrub a timeline along the bottom edge. Close the last watched app and your usual taskbar behavior returns.
- **On a multi-monitor setup**, the rule applies to every screen where Windows shows a taskbar. You can Alt+Tab to a chat window on your second monitor while the game stays open and the taskbars stay hidden.
- **If you play regularly**, leave **Start in the background when I sign in** enabled. Taskbar Control remembers your choices and activates when you launch a watched game from its usual shortcut.

## Get started

1. Save `TaskbarControl.exe` in the folder where you want to keep it, then run it.
2. Select **Choose running app…** to pick an open app, or **Add files…** to browse for an executable. For a game with a separate launcher, choose the executable that runs during gameplay.
3. Select **Done**. Your choices are saved. To try it, open a watched app, move the pointer to the screen edge, then press the Windows key to bring up Start and the taskbar.

## Settings and exit

Open the exe again or double-click its system tray icon to edit your list or turn off automatic startup at sign-in. Apps are matched by their full executable paths. Choose **Exit Taskbar Control** to restore the taskbar immediately and stop watching for this session.

## Build from source

The project uses Windows Forms and targets .NET Framework 4.8. From a Visual Studio Developer Command Prompt with the .NET Framework 4.8 targeting pack installed, run the following in the project directory.

```bat
msbuild TaskbarControl.csproj /p:Configuration=Release
```

The executable is written to `bin\Release\TaskbarControl.exe`.
