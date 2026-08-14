# DaemonElite

DaemonElite is a modern WPF voice workstation recreated from the uploaded SAW.IO production package. It keeps the original signal-first spirit while giving it a more deliberate desktop experience: a live FFT monitor, 15 built-in voice profiles, safe recording/playback lifecycle management, processed WAV export, and an on-screen system console.

## Included voice profiles

Normal, Deep Male, Female, Child, Chipmunk, Robot, Alien, Demon, Giant, Radio, Underwater, Telephone, Cathedral, Stadium, and Tiny.

## Requirements

- Windows 10 or Windows 11
- .NET 8 SDK
- A working microphone and audio output device

## Build

```powershell
dotnet restore
dotnet build DaemonElite.sln -c Release
dotnet run --project DaemonElite.csproj
```

Create a self-contained Windows build:

```powershell
dotnet publish DaemonElite.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Notes

- Capture sessions are kept in the current user's local application data folder.
- WAV exports are written wherever you choose in the export dialog; the default music folder is `Music\DaemonElite`.
- Logs are stored under `LocalAppData\Black Star Labs\DaemonElite\Logs`.
- Microphone permission is controlled by Windows privacy settings.

## Technology

- C# / .NET 8
- WPF
- NAudio 2.2.1
- Nullable reference types and strict resource cleanup

Copyright © 2026 Black Star Labs.