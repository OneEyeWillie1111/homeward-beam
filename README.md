# Homeward Beam

*An original turn-based, first-person dungeon crawler by KrabbierGames, made in the style of early-1980s maze RPGs.*

You were abducted from your backyard and woke up on an alien ship. Fight your way down **10 decks**. On each deck, find the glowing **teleporter pad** and beam down to the next one. On deck 10, **Ixmorrhan, the Coiled Sovereign** guards the **teleportation machine home**. Beat him, step onto the machine and tap **BEAM**.

All names, creatures, sprites and sounds are original. The sprites are drawn in code and the sounds are synthesised in code, so the project contains no third-party art.

## How to play
- **War health** (your body) and **Spiritual health** (your mind) both run from 0 to 99 and show at the top of the HUD. If either one reaches 0, you die.
- **War aliens** attack your body. Weapons (Pipe Wrench, Ion Pistol and so on) do full damage to them.
- **Spirit aliens** attack your mind. Psionic items (Psi Crystal, Mind Shard and so on) do full damage to them, and each cast costs 1 Spirit. Using the wrong kind of item does half damage.
- **Alien tiers, weakest to strongest:**
  - Brood Drudge and Shade Thrall
  - Husk Stalker and Pale Vessel
  - Mantis Lancer and Mantis Oracle
  - Half-Scale Sentinel and Gilded Herald
  - **Grays**, keepers of the symbiote hosts (Gray Hostwarden and Gray Symbiote-Tender)
  - **Saurians**, the Serpent Lords (lizard people): Saurian Fang-Guard (warrior caste) and Saurian Brood-Mother (queen mother)
- **Combat:** walking into a chamber's alien starts a turn-based fight. You can ATTACK with the selected ring item, USE an item (for example, eat food), or FLEE (60% chance, and impossible against the boss). The alien strikes back after each of your actions.
- **Ring inventory:** a circle of 8 slots is always on screen (below the view in portrait, to the right of it in landscape). Each slot shows its item's icon and name. The item you're holding is highlighted in gold and shown large in the middle of the ring. **Tap any slot** to hold that item, or use ‹RING and RING› to rotate through weapons, armor, psionic items, food and access cards. Armor protects you while it's anywhere in the ring.
- **Supply lockers:** these need an **Access Card**. Face a locker and tap OPEN to get a "Prime" item.
- **Difficulty** (chosen at the start): Stargazer, Abductee, Specimen or Doomed. Harder settings give you less starting health and stronger aliens.
- **Re-cloning (reincarnation):** when you die, you wake on deck 1 with your maximum War and Spirit cut to 85% and a fresh starter kit.
- **Levels:** decks are procedurally generated from a seed and built from chambers, corridors, hatches (doors) and teleporter pads. Every run gets a new seed.

## Controls
The on-screen touch pad uses a portrait layout (view on top) or a landscape layout (view on the left), and it re-arranges itself when you rotate the phone.

| Button | Action |
|---|---|
| FWD / BACK | Step one tile |
| LEFT / RIGHT | Turn 90° |
| OPEN | Open the hatch or locker in front of you |
| ‹RING / RING› | Cycle the ring inventory |
| USE | Use the selected item (food heals; outside a fight, psionics heal for 2 Spirit) |
| DROP | Drop the selected item |
| BEAM | Use the teleporter or the home machine you're standing on |
| ATTACK / FLEE | Shown during combat |

The keyboard also works in the editor or on desktop: WASD or the arrow keys to move, Space to open or attack, F to flee, E to use, Q/R to cycle the ring and B to beam.

## Project facts
- Unity **6000.3.27f1** with the **Built-in Render Pipeline** (no URP or HDRP). The only packages are uGUI and built-in modules.
- No manual setup is needed. `Assets/Scenes/Main.unity` is listed in Build Settings and holds a single `Bootstrap` object. At runtime, `HomewardBeamApp` builds the camera, the UI, the audio and the game. If the scene is missing, it creates itself.
- **Android settings:**
  - Package: `com.krabbiergames.homewardbeam`
  - IL2CPP, ARM64 only
  - Minimum API **25** (Android 7.1). Unity 6000.3 marks API 24 as obsolete and treats 25 as the lowest supported level.
  - Target API: highest installed
  - Auto-rotation
- `Assets/Editor/BuildAndroid.cs` re-applies all of these settings. Its `BuildAndroid.PreExport` can be used as the Build Automation pre-export method.
- The game rules live in `Assets/Scripts/Core`, which is plain C# with no UnityEngine dependency. `Tests.DotNet` runs NUnit tests on them with `dotnet test`. Unity ignores that folder because it's outside `Assets`.

## Build the APK with Unity Build Automation (no PC needed)
1. **GitHub:** create a free account, then create a **New repository** named `homeward-beam` and set it to **Private**. You can skip the .gitignore template, because this project already includes one.
2. Unzip `unity-dungeon-crawler.zip`. In the repo, choose **Add file → Upload files** and drag in everything *inside* the folder: `Assets`, `Packages`, `ProjectSettings`, `README.md`, `.gitignore` and `Tests.DotNet`. Then commit to `main`.
3. In GitHub, go to **Settings → Developer settings → Personal access tokens → Tokens (classic)** and generate a token with **repo** scope. Copy it somewhere safe.
4. Sign in to **https://cloud.unity.com** with your Unity ID. Create or choose a project, then open **DevOps → Build Automation** and turn it on.
5. For **Source control**, choose **GitHub**, connect with the token from step 3, and pick the `homeward-beam` repo.
6. **Add a build target:**
   - Platform: **Android**
   - Unity version: **6000.3.27f1**
   - Branch: **main**
   - Project subfolder: leave it blank
   - Optional: under Advanced, set the pre-export method to `BuildAndroid.PreExport`
7. Tap **Start build**. When it succeeds, open the build and choose **Download** to get the `.apk`.
8. **On the phone:** open the APK. If Android blocks it, allow **Install unknown apps** for your browser or My Files, then tap **Install**.

**Signing:** unless you upload a keystore in the build target's Android signing settings, Build Automation makes a **debug-signed** APK. That's fine for testing on your own phone, but it can't be published to Google Play.
