# Roblox-Launcher

A minimal, portable Windows launcher with exactly two actions:

- Launch Roblox
- Launch Roblox Studio

## What it does

On first launch, the program creates a `Roblox` directory beside the launcher. It redirects the standard Windows Roblox data location (`%LOCALAPPDATA%\Roblox`) to that directory using a Windows directory junction, then downloads and runs the official Roblox Player and Roblox Studio installers when either product is missing.

The existing Roblox folder is copied into the portable folder before the junction is switched over. The old folder is kept as a temporary backup until the junction is created successfully. If Roblox is running, migration is stopped and you are asked to close it first.

The downloaded installer executables are checked for a valid Authenticode signature from Roblox Corporation before they are run. Downloads come only from Roblox's official `setup.rbxcdn.com` host.

## Portable folder

After setup, the main files are kept together:

```text
Roblox-Launcher/
├── Roblox-Launcher.exe
└── Roblox/
    └── Versions/
        ├── version-.../
        └── version-.../
```

Windows keeps a directory-junction entry at `%LOCALAPPDATA%\Roblox` because the official Roblox installers and clients expect that standard location. The actual Roblox directory contents are stored beside the launcher. This is a portable data layout, not a fully self-contained Roblox distribution: Roblox may still use normal Windows temporary folders and registry protocol registration.

Keep the launcher and its `Roblox` folder together when moving it to another drive or PC. Close Roblox Player and Studio before moving the folder.

## Build

The GitHub Actions workflow builds a self-contained Windows x64 executable using .NET 8. It does not require .NET to be installed on the target PC.

Run the workflow from the Actions tab, then download the `Roblox-Launcher-Windows-x64` artifact.

## Requirements

- Windows 10 or Windows 11, x64
- Internet connection for first-time installation and Roblox updates
- Enough free disk space for Roblox Player and Roblox Studio

This project is an independent launcher and is not affiliated with or endorsed by Roblox Corporation. Roblox trademarks and software remain the property of their respective owners.
