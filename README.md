# Abduction

*An original turn-based 3D dungeon crawler for Android by KrabbierGames, built in Unity 6000.3.27f1 with the Built-in Render Pipeline.*

> You were taken from your backyard. Fight down the decks of the ship to the teleportation machine home.

## Starting a run
1. **Pick who you were:**
   - **Human:** balanced.
   - **Cat:** +6 Spirit, −4 War.
   - **Dog:** +6 War, −4 Spirit.

   You can be just a little pet.
2. **Pick a role (difficulty):**

   | Role | Decks |
   |---|---|
   | Stargazer | 10 |
   | Abductee | 20 |
   | Specimen | 35 |
   | Doomed | 50 |

   Harder roles start you with less health and face stronger aliens.
3. You begin in your **backyard** at night as a ship hovers overhead. Tap **BEAM** to be taken, and you wake on deck 1.

## Goal
On every deck, find the glowing **teleporter pad**, stand on it and tap **BEAM** to go down. On the **last deck**, **Ixmorrhan, the Coiled Sovereign** stands on the **teleportation machine home**. Defeat him, step onto the machine and tap **BEAM**.

## Screen
- **The view:** a third-person look over your shoulder at the **back of your character's head**, fixed at the bottom centre (cats show pointed ears, dogs floppy ears). It stays perfectly still: no bobbing.
- **Ring inventory:** your 8 items orbit your head. Tap a slot (or use **< RING** and **RING >**) to hold that item.
  - A **red ring** marks your equipped weapon, which ATTACK uses. Selecting a weapon or psionic item equips it.
  - A **blue ring** marks the armor you're wearing. Your best armor is worn automatically.
  - **Gold** marks the item you're holding, ready to USE.
- **Corner map (top-left):** fills in as you explore. The arrow is you. Cyan is a teleporter, gold is the machine home, orange is a hatch and red dots are aliens.
- **Prompt banner:** always tells you what you can do right now, for example "Hatch ahead: tap OPEN." or "Teleporter pad: tap BEAM to go down a deck."
- **HELP (top-right):** opens a paged guide covering the goal, moving, fighting, health, the ring and the aliens.
- **Bars:** **WAR** (red) is your body and **SPIRIT** (blue) is your mind; both run from 0 to 99. An enemy health bar appears during fights.

## Controls
| Button | Action |
|---|---|
| FORWARD / BACK | Step one tile (smooth glide) |
| TURN L / TURN R | Turn 90° (smooth turn) |
| OPEN | Open the hatch or supply locker in front of you |
| < RING / RING >, or tap a slot | Choose an item |
| USE | Use the held item (food heals; outside fights, psionics heal) |
| BEAM | Get abducted from the backyard, use a teleporter, or use the machine home |
| ATTACK / FLEE | Shown during fights. You can't flee from the boss. |
| DROP | Drop the held item |

The keyboard also works in the editor: WASD or the arrow keys to move, Space to open or attack, F to flee, E to use, Q/R to cycle the ring, B to beam and H for help.

## Rules
- **Fights:** walking into an alien starts a turn-based fight, and the alien strikes back after each of your actions.
  - **War aliens** have red eyes and hurt War. Weapons do full damage to them.
  - **Spirit aliens** have cyan eyes and hurt Spirit. Psionic items do full damage to them and cost 1 Spirit per cast.
  - The wrong kind of attack does half damage.
- **Re-cloning:** if War or Spirit hits 0, you wake on deck 1 with 85% of your maximum health and a fresh starter kit.
- **Alien castes, weakest to strongest:**
  - Brood Drudge and Shade Thrall
  - Husk Stalker and Pale Vessel
  - Mantis Lancer and Mantis Oracle
  - Half-Scale Sentinel (a symbiote host) and Gilded Herald (Nordic-style)
  - **Grays**: Gray Hostwarden and Gray Symbiote-Tender
  - **Serpent Lords (Saurians)**: Saurian Fang-Guard and Saurian Brood-Mother

  Castes are spread across however many decks your role has, so the Serpent Lords always appear near the end.
- **Decks:** every deck is procedurally generated from a seed, with chambers, corridors, hatches, supply lockers (opened with an access card) and a teleporter.

## Look (everything is generated in code; no imported art)
- **Ship corridors:** real 3D geometry using Unity's Standard (PBR) shader, with silvery brushed-metal walls and floors.
  - Their textures and surface-detail (normal) maps are generated in code.
  - A live reflection probe, glowing cyan light strips, exponential fog, a head-lamp light that moves with you, and glowing teleporters with their own lights.
- **Motion:** stepping and turning are eased; hatch doors slide up into the ceiling; items float and spin.
- **Aliens:** smooth 3D figures with glowing eyes and caste-specific shapes. They turn to face you, breathe, and recoil when hit.
- **Backyard opening:** a lawn, picket fence and tree under a starry sky, with a hovering ship and its tractor beam. A white flash plays when you're abducted.
- **Interface:** rounded panels with gradients and soft shadows, health bars that animate smoothly, buttons that shrink slightly when pressed, and messages and overlays that fade.

## Project facts
- Unity **6000.3.27f1**, **Built-in Render Pipeline** (no URP or HDRP). The only packages are uGUI and built-in modules.
- **No manual setup:** `Assets/Scenes/Main.unity`, which is listed in Build Settings, holds one `Bootstrap` object. `AbductionApp` builds everything at runtime and also creates itself if the scene is missing.
- **Android settings:**
  - Package: `com.krabbiergames.abduction`
  - Product name: "Abduction"
  - IL2CPP, ARM64 only
  - Minimum API **25** (Unity 6000.3 marks API 24 as obsolete)
  - Auto-rotation
- `Assets/Editor/BuildAndroid.cs` re-applies all of these settings. `BuildAndroid.PreExport` can be used as the Build Automation pre-export method.
- **Material templates:** `Assets/Resources/*.mat` are the Standard-shader templates, so the cloud build compiles only the shader variants the game needs.
- **Tests:** the rules, the map, the help text and the layout live in `Assets/Scripts/Core`, which is plain C#. `Tests.DotNet` runs them with `dotnet test`. `Tests.DotNet/UnityCompileCheck` compiles all of the game's code against the Unity 6000.3.27f1 libraries without needing a Unity license.

## Build the APK with Unity Build Automation (no PC needed)
1. **GitHub:** create a free account, then create a **New repository** and set it to **Private** (the existing `homeward-beam` repo works fine). You can skip the .gitignore template, because this project already includes one.
2. Unzip `unity-dungeon-crawler.zip`. In the repo, choose **Add file → Upload files** and drag in everything *inside* the folder: `Assets`, `Packages`, `ProjectSettings`, `README.md`, `.gitignore` and `Tests.DotNet`. Then commit to `main`.
3. In GitHub, go to **Settings → Developer settings → Personal access tokens → Tokens (classic)** and generate a token with **repo** scope. Copy it somewhere safe.
4. Sign in to **https://cloud.unity.com** with your Unity ID. Create or choose a project, then open **DevOps → Build Automation** and turn it on.
5. For **Source control**, choose **GitHub**, connect with the token from step 3, and pick your repo.
6. **Add a build target:**
   - Platform: **Android**
   - Unity version: **6000.3.27f1**
   - Branch: **main**
   - Project subfolder: leave it blank
   - Optional: under Advanced, set the pre-export method to `BuildAndroid.PreExport`
7. Tap **Start build**. When it succeeds, open the build and choose **Download** to get the `.apk`.
8. **On the phone:** open the APK. If Android blocks it, allow **Install unknown apps** for your browser or My Files, then tap **Install**.

**Signing:** unless you upload a keystore in the build target's Android signing settings, Build Automation makes a **debug-signed** APK. That's fine for testing on your own phone, but it can't be published to Google Play.
