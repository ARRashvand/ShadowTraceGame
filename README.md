# Shadow Trace — رد من

> A minimalist 2D time-loop puzzle game where every move you make returns as part of the next attempt.

**Shadow Trace** is an early Android game prototype built around a simple idea: your previous movement is recorded, then replayed by a ghost in the next loop. Future puzzles will require the player to cooperate with these past versions to open paths, activate mechanisms, and reach the exit.

The project is currently in active prototyping. It is not yet a complete game or a production-ready release.

## Current prototype

Version `0.2.0` currently includes:

- Native Android rendering with a fixed top-down 2D arena
- Drag-anywhere virtual joystick controls
- Player movement and collision with walls
- A 12-second gameplay loop
- Recording the player's first route
- Replaying the recorded route as a synchronized ghost
- Responsive scaling for different portrait phone screens
- ARM64 Android packaging

The ghost is currently visual only. Switches, doors, hazards, multiple ghosts, level completion, audio, and progression will be introduced incrementally.

## Core concept

Each level is composed of several short time loops:

1. The player performs an action during a limited time window.
2. The loop resets and the player's previous actions return as a ghost.
3. The player uses that ghost to reach a new mechanism or area.
4. Additional ghosts form a chain of coordinated actions.
5. The final run uses all previous ghosts to unlock the exit.

The long-term goal is to combine accessible one-finger controls with short puzzle stages that reward planning, timing, and experimentation.

## Technology

- C#
- .NET 10 for Android
- Native Android `View` and `Canvas` rendering
- Minimum Android version: Android 8.0 / API 26
- Primary device architecture: ARM64

The prototype intentionally avoids external game engines and third-party runtime dependencies while the core mechanic is being validated.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- .NET Android workload
- Android SDK

Install the Android workload if it is not already available:

```powershell
dotnet workload install android
```

## Build

Clone the repository and enter the project directory:

```powershell
git clone https://github.com/ARRashvand/ShadowTraceGame.git
Set-Location ShadowTraceGame
```

Build a debug APK:

```powershell
dotnet build ShadowTraceGame.csproj -c Debug -m:1
```

The signed debug APK is generated at:

```text
bin/Debug/net10.0-android/ir.shadowtrace.game-Signed.apk
```

Build outputs and APK files are intentionally excluded from source control.

## Controls

- Touch and drag anywhere on the screen to move.
- Release your finger to stop.
- The timer begins with the player's first movement.
- After the first 12-second loop, move again to start playback of the recorded ghost.

## Roadmap

- [x] Player movement and wall collision
- [x] Timed route recording
- [x] First ghost playback
- [ ] Pressure switches and controlled doors
- [ ] Multiple simultaneous ghosts
- [ ] First complete puzzle level
- [ ] Hazards and reset feedback
- [ ] Sound effects, music, and haptics
- [ ] Level selection and saved progress
- [ ] Release builds and store-ready packaging

## Project status

This repository follows a small-step development process: each mechanic is implemented and tested on a physical Android device before the next system is introduced. APIs, visuals, and level rules may change while the prototype evolves.

Bug reports and focused suggestions are welcome through GitHub Issues.

## License

No open-source license has been granted at this stage. The source is publicly visible for development and evaluation, but all rights remain reserved unless a license is added later.
