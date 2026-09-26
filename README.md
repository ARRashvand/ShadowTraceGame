# Shadow Trace — رد من

A small Android time-loop puzzle about cooperating with your past selves.

This is a playable prototype, not a finished game. Version **0.9.0** includes two levels with three solution tiers each. Two further levels remain a design plan.

## Visual playtest (0.9)

The time-lab presentation adds low-contrast floor slabs, beveled walls and crates, numbered translucent ghosts with short trails, switch-to-door light signals, gate panels, and a circular memory timer. The crate action is now the central bottom button with proximity feedback. Results reveal discovery cards in sequence. All visuals are native Canvas primitives; no new image packs or dependencies are required. Puzzle rules and solution routes are unchanged from 0.8. Device testing is required for visual readability, motion and touch comfort.

## Two levels, three discoveries each

Reach the green exit using two ghosts, one ghost, or no ghosts. The level combines red and blue pressure switches with a movable crate. A narrow passage admits the crate but not the player. Solutions are recognized by the number of recorded ghosts at completion, not by a required sequence of coordinates.

| Discovery | Condition (without assistance) | First-time reward |
| --- | --- | --- |
| Normal | Finish with two ghosts | Badge, 100 points |
| Clever | Finish with one ghost | Badge, 250 points, one time-pause token |
| Master | Finish with no ghosts | Badge, 500 points, one rewrite token |

Each badge is independent per level and awards its reward once. Repeating a solution never farms tokens. Each level's collection score is capped at 850. Tokens are shared across levels. Badges, selected level, unlocks and token balances are saved locally; uninstalling or clearing app data removes them. Runs themselves are not saved across process termination.

Level two, **Short Memory / حافظهٔ کوتاه**, moves the blue switch to the right and removes the crate chute. Its blue door stays open for 1.6 seconds after the switch is released; a small blue bar shows the remaining time. The normal solution uses two ghosts; creative solutions combine the temporary opening with one ghost or a crate. Existing level-one badges are preserved on upgrade and unlock level two. Otherwise, any level-one completion (including assisted) unlocks it. Tap the top **مراحل** button to choose a level; changing an unfinished run asks for confirmation.

## Controls and rules

- Drag anywhere in the room to move. Release to stop. The timer starts on movement.
- Each loop lasts 12 seconds. The first two completed setup loops become ghosts.
- Ghosts replay both movement and recorded crate movements. After a ghost releases the crate, it can be moved by the current player. Simultaneous recorded crate moves have deterministic priority: the later ghost wins.
- Push the crate by walking into it. Use **گرفتن جعبه** near it to pull, then tap again to release. The tether can pass through the narrow crate passage, not solid walls.
- **از نو** is always free and clears recordings, the crate position and assistance status, while keeping earned discoveries and inventory.
- A time-pause token freezes the loop clock, ghosts and timed-door countdown for three seconds while the player may move. Switches held by ghosts remain active.
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

Tests drive actual player input through all three routes in both levels at 30, 60 and 120 FPS. They also cover closed doors, the crate-only passage, recorded crate actions, rewriting dependencies, time pause, timed-door expiry/reset, free reset and one-time reward rules. Physical-device testing is still needed for touch feel, Persian layout, Android persistence, upgrade migration, level-selection dialogs and the final APK.

## Feedback

Use **ارسال نظر** after completing the level to share a short prompt through an installed messaging app. It does not send anything automatically. Useful feedback: which solutions you discovered, crate control, confusion points and whether you wanted another attempt.

## Roadmap

- [x] Movement, walls, synchronized ghost playback
- [x] Red/blue switches, crate pushing and pulling
- [x] First level with three independently rewarded solutions
- [x] Local discoveries and consumable assistance
- [x] Exit animation, tutorial and private feedback sharing
- [ ] Test and tune first-level discovery with players
- [x] Second level with a timed switch, level selection and separate discoveries
- [ ] Design and build levels three and four
- [ ] Sound, accessibility refinements, campaign and store packaging

## License

No open-source license has been granted. The repository is publicly visible, but no additional reuse rights are granted unless a license is added later.
