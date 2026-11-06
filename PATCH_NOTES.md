# I.G.I. 2: Covert Strike Android Port - Patch Notes

## Version 0.1.0 (Initial Setup)

### Port Setup
- **New repository**: I.G.I. 2 Android port, built from the same codebase as the [Project I.G.I. Android port](https://github.com/rayishere0/Project-IGI-Android-Port). The engine is multi-title — this repo just points the same host, renderer and input stack at I.G.I. 2.
- **Title**: The Android host runs I.G.I. 2 (`TitleCatalog.For(GameTitle.Igi2)`) instead of I.G.I. 1.
- **Launcher**: Folder verification now checks the I.G.I. 2 installation — signature file `igi2.exe`, data root is the `pc` folder (`common/`, `missions/`, `menusystem/`, `language/`, `config.qvm`).
- **Branding**: App name, launcher title, log tag and user-facing strings updated to I.G.I. 2: Covert Strike.
- **Game data**: Installed to `Project IGI 2` in the app's internal storage.

### Status
- All 19 campaign missions load and render; the world, ground, sky, forest and water draw.
- Player, enemy AI, weapons, HUD and mission objectives are in place in the engine — playability is being proven mission by mission on device.
- Known gaps: water, AI events, conveyors.

### Shared With the I.G.I. 1 Port
- **Decoupled Game Files**: No copyrighted retail game assets in the repository or APK bundle.
- **Storage Access Framework (SAF) Launcher**: Custom launcher screen with a **"SELECT GAME FOLDER"** button; verifies folder signatures before enabling **"PLAY"**.
- **Touch Pause System**: Tapping the `||` touch button on the HUD instantly pauses game simulation, AI updates, and audio playback.
- **Subtle Cheats Gear Icon**: Compact top-right gear icon on the main menu opens an on-demand modal for Infinite Health, Infinite Ammunition, and All Weapons.
- **30 FPS Target Frame Pacer**: Default frame rate capped at 30 FPS for smooth, thermal-stable, battery-friendly mobile performance.
- **Cutscene & Audio Polish**: Background mission music mutes during cutscenes for clear dialogue playback; mid-mission cutscenes have an interactive hold-to-skip prompt.
- **Graphics & Rendering**: Vulkan 1.1 RHI via native C++ JNI bridge (symbol obfuscation, `-fvisibility=hidden -Wl,--exclude-libs,ALL`); WebGPU backend available as a fallback.
