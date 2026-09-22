# Shadow Trace — رد من

A small Android time-loop puzzle about cooperating with your past selves.

This is a playable prototype, not a finished game. Version **0.7.0** explores one level with three solutions. Only the first level is implemented; the proposed four-level campaign remains a design plan.

## One level, three discoveries

Reach the green exit using two ghosts, one ghost, or no ghosts. The level combines red and blue pressure switches with a movable crate. A narrow passage admits the crate but not the player. Solutions are recognized by the number of recorded ghosts at completion, not by a required sequence of coordinates.

| Discovery | Condition (without assistance) | First-time reward |
| --- | --- | --- |
| Normal | Finish with two ghosts | Badge, 100 points |
| Clever | Finish with one ghost | Badge, 250 points, one time-pause token |
| Master | Finish with no ghosts | Badge, 500 points, one rewrite token |

Each badge is independent and awards its reward once. Repeating a solution never farms tokens. The collection score is capped at 850. Badges and token balances are saved locally; uninstalling or clearing app data removes them. Runs themselves are not saved across process termination.

## Controls and rules

- Drag anywhere in the room to move. Release to stop. The timer starts on movement.
- Each loop lasts 12 seconds. The first two completed setup loops become ghosts.
- Ghosts replay both movement and recorded crate movements. After a ghost releases the crate, it can be moved by the current player. Simultaneous recorded crate moves have deterministic priority: the later ghost wins.
- Push the crate by walking into it. Use **گرفتن جعبه** near it to pull, then tap again to release. The tether can pass through the narrow crate passage, not solid walls.
- **از نو** is always free and clears recordings, the crate position and assistance status, while keeping earned discoveries and inventory.
- A time-pause token freezes the loop clock and ghosts for three seconds while the player may move. Switches held by ghosts remain active.
- A rewrite token returns to the beginning of a chosen ghost's recording. Older ghosts remain; the chosen ghost and all dependent later ghosts are removed. Confirmation is required before spending.
- Assisted completion is allowed but does not award unassisted badges. Restart freely to make an unassisted attempt.
- Tutorial, hints and feedback sharing are available in Persian. No account, analytics, advertising, payments, music or network service is built into gameplay.

## Build

Requires .NET 10 SDK, the .NET Android workload, Android SDK and a compatible JDK. The current package targets ARM64 devices with Android 8.0/API 26 or newer.

```powershell
dotnet workload install android
dotnet build ShadowTraceGame.csproj -c Release --disable-build-servers -m:1
```

The signed test APK is generated at `bin/Release/net10.0-android/ir.shadowtrace.game-Signed.apk`. This is a private playtest build signed with the local development key, not a store-production signing setup. Versioned playtest copies are placed in ignored `artifacts/` locally.

## Verification

The Android view and a dependency-free console test harness share `Puzzle.cs`:

```powershell
dotnet run --project tests/RouteTests.csproj
```

Tests drive actual player input through all three routes at 30, 60 and 120 FPS. They also cover closed doors, the crate-only passage, recorded crate actions, rewriting dependencies, time pause, free reset and one-time reward rules. Physical-device testing is still needed for touch feel, Persian layout, Android persistence, dialogs and the final APK.

## Feedback

Use **ارسال نظر** after completing the level to share a short prompt through an installed messaging app. It does not send anything automatically. Useful feedback: which solutions you discovered, crate control, confusion points and whether you wanted another attempt.

## Roadmap

- [x] Movement, walls, synchronized ghost playback
- [x] Red/blue switches, crate pushing and pulling
- [x] First level with three independently rewarded solutions
- [x] Local discoveries and consumable assistance
- [x] Exit animation, tutorial and private feedback sharing
- [ ] Test and tune first-level discovery with players
- [ ] Design and build the remaining three levels
- [ ] Sound, accessibility refinements, campaign and store packaging

## License

No open-source license has been granted. The repository is publicly visible, but no additional reuse rights are granted unless a license is added later.
