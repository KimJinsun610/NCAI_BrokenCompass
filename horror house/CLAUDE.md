# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6000.3.17f1 (Universal Render Pipeline, New Input System) horror-school game project ("NCAI_BrokenCompass" / "horror house"). Working title: **야간근무 (Night Duty)** — a first-person rule-compliance horror game set in an abandoned school, referencing Napolitan-style creepypasta.

There is no build/lint/test tooling in this repo (no package.json, no test runner) — this is an in-editor Unity project. Verification happens by opening/playing the project in the Unity Editor, or via the Unity MCP tools (`mcp__UnityMCP__*`) when connected to a running editor instance (run_tests, read_console, manage_scene, etc.).

**Repo layout note:** this `horror house/` folder is the Unity project root, but it is a subfolder of the git repository root (`NCAI_BrokenCompass/`). A top-level `Docs/` folder with team docs (including the full 형상관리_매뉴얼.md / configuration-management manual) lives one level above this folder, outside the Unity project.

**Design docs** (feasibility judgement, scope cut, per-programmer dev plans, architecture) live in the Claude project *"VARCO AI_Broken Compass Napolitan Project"*, not in this repo. Ask the user for them before making system-design decisions.

### Adopted scope (as of 2026-09)

The original design covered 6 patrol spaces; this was cut to **4 spaces / 5 patrol points** to fit an 8-week schedule:

- **복도 (Corridor)** — the "self-reporting" space: lit fluorescent count (of 8) = 조도 axis, props left out = 배치 axis, door sounds = 청각 axis. This is where the player learns to read the stats. Never cut.
- **화장실 (Toilet)** — 4 stalls
- **교실 (Classroom)** — 1-1 and 1-3, same prefab, different state values (2 patrol points)
- **과학실 (Science room)** — anatomy model, single-object tracking

도서관 (Library) and 탈의실 (Locker room) are **deferred, not deleted** — their 21 rule cards are kept in a "2차 풀". Locker room is restore-priority 1 (its doors reuse the same `HingedDoor` component as toilet stalls).

Four fear axes: **청각 / 조도 / 배치** drive world presentation; **신뢰 (trust)** drives the in-game instruction document (지침록) instead — it is never drawn in the world.

## Git / Git LFS workflow (required reading before committing)

Full detail is in `../Docs/형상관리_매뉴얼.md` (Korean). Key rules future Claude sessions must follow:

- **Never commit scene/prefab edits without checking locks first.** `.unity` and `.prefab` files are LFS-lockable (not LFS-stored, just lock-tracked — they're kept as Force-Text YAML for diffability). Before editing a scene/prefab: `git lfs locks` to check nobody else holds it, then `git lfs lock "Assets/path/to/File.unity"`. After pushing your change: `git lfs unlock "Assets/path/to/File.unity"`.
- **`Assets/NOT_Lonely/` is a large third-party asset package and is gitignored** (`/Assets/NOT_Lonely/` in `.gitignore`) — it is never committed. Each teammate installs it locally at the exact same version. Do not try to `git add` anything under it, and do not delete/reinstall it without knowing why it's there (see manual §8 for the "Discard All" warning: discarding changes in a Git client can wipe this untracked folder entirely).
- **Original game content goes under `Assets/_Game/`** (Scenes/, Scripts/, Prefabs/, Materials/, Art/, Audio/, ScriptableObjects/), fully tracked by git — this keeps it separate from vendor assets. When customizing a vendor prefab, make a Prefab Variant under `Assets/_Game/Prefabs/` rather than editing the vendor original.
- Standard binary asset types (textures, models, audio, video, fonts, archives, native plugins) are tracked via Git LFS per `.gitattributes` — don't disable/bypass this by force-adding large binaries outside LFS tracking.
- Commit message prefixes in use: `feat:`, `fix:`, `art:`, `chore:`, `docs:`.
- Do not force-add `Library/`, `Temp/`, `obj/`, `Build/`, `Logs/`, `UserSettings/` — these are local Unity-regenerated caches, already gitignored.
- **Check `git status` before committing.** A `Claude outputs/` folder can appear at the project root from desktop-app sessions; it is not part of the project. Exclude it (`git reset "Claude outputs"`) rather than committing it.

## Assembly definitions and code placement

`Assets/_Game/Scripts/` is split into two assemblies with a **one-way dependency**:

```
Assets/_Game/Scripts/
├── NightDuty.Core.asmdef        references: []           ← judgement, stats, direction
│   ├── Core/         GameClock, EventBus, ServiceLocator
│   ├── Rules/        RuleWatcher, ComplianceMode, ViolationLog
│   │   └── Conditions/   ICondition — 7 primitive types
│   ├── Stats/        FearAxisSystem, BandResolver, DecayRules, PlayerProfiler
│   ├── Direction/    DayDirector, CardDrawer, ContradictionSolver
│   ├── Presentation/ ISpacePresenter, LightingBandTable, GazeTracker, OffscreenTransform
│   ├── Data/         RuleSO, SpaceProfileSO, BandTableSO, SpaceRegistry
│   └── Editor/       NightDuty.Editor.asmdef  references: [NightDuty.Core], Editor-only
└── NightDuty.Client.asmdef      references: [NightDuty.Core]   ← player, UI, presenters
```

Rules for anything added here:

- **`NightDuty.Core` must never reference presentation.** When an axis changes, Core raises an event; what gets drawn is decided on the other side. Reversing this breaks the one-way dependency and makes every scene script recompile on a system-code edit. The asmdef is what enforces it — don't add references to work around it.
- **Never reference scripts under `Assets/NOT_Lonely/` from `_Game` code.** Two reasons: (1) vendor scripts have no asmdef, so they land in `Assembly-CSharp`, which an asmdef assembly cannot reference; (2) the folder is gitignored, so any such reference breaks the build for anyone who has not installed the package. Prefabs, models and materials may be referenced from scenes (GUID references are fine) — **scripts may not.** `SimpleFPController` in particular is reference-only: the player controller is implemented from scratch under `_Game/Scripts/Player/`.
- **`.gitkeep` marks intentionally-empty folders.** Git does not track empty directories, and Unity ignores dot-prefixed files (so no `.meta` is generated and they never appear in the Project window). Each one carries a one-line note of what the folder is for. Delete a folder's `.gitkeep` once real files live there.
- A `.csproj` is only generated for an assembly that contains at least one `.cs` file. An empty assembly having no `.csproj` is normal, not a failure.

## Vendor package — required fix after installing

`Assets/NOT_Lonely/HQ_AbandonedSchool/` ships in Built-in RP form with `URP.unitypackage` alongside it. **The URP package must be imported** (per `Upgrade to HDRP or URP.txt`) — it already has been in the current working copy.

After that, one manual fix is still required on **every machine**, because the folder is gitignored and the change cannot be committed:

> **`Shaders/NOT_Lonely_LightRays.shader` — delete the two lines reading `uniform float4 _CameraDepthTexture_TexelSize;`** (originally lines 224 and 507).
>
> URP 17.3 declares this symbol itself, so the asset's 2021-era manual declaration is now a redefinition error on d3d11. The variable is declared but never used anywhere in the shader — depth fade goes through `SHADERGRAPH_SAMPLE_SCENE_DEPTH` — so removing it changes nothing functionally. Re-importing `URP.unitypackage` reverts the fix.

If a teammate reports `redefinition of '_CameraDepthTexture_TexelSize'`, this is the fix.

## Architecture / folder structure

- `Assets/_Game/` — the team's actual game content (see above). This is where new scripts and scene work should go.
- `Assets/0. Main/` and `Assets/3.1. Programmer_lee/`, `Assets/3.2 Programmer_Kim/` — per-programmer personal workspace folders (numbered subfolders like `01 Scene`, `02 Scripts`, `03 Prefebs`, etc.). **Use these for throwaway experiments and single-system test scenes only** (e.g. `_Test_GazeTracker.unity`); shared code and shipping content go in `Assets/_Game/` from the start, because moving scripts later breaks every SO and prefab reference to them. Note the inconsistent naming — `3.1. Programmer_lee` has a trailing dot after the number, `3.2 Programmer_Kim` does not; don't hard-code these paths.
- `Assets/1. Design/`, `Assets/2. Art/` — design/art team workspace folders.
- `Assets/NOT_Lonely/` — third-party asset packages (`HQ_AbandonedSchool` environment kit, `Object Placement Tool`, `SimpleFPController`), gitignored, must be installed locally by each teammate (see workflow section above). Scene/prefab GUID references into this folder work as long as the same package version is present locally, regardless of where a scene copy lives.
- `Assets/Scenes/`, `Assets/Settings/` — default URP template scenes/render settings created by the Unity template.
- Render pipeline: URP 17.3.0. Input: new Input System (`InputSystem_Actions.inputactions`) 1.19.0.
- Unity MCP (`com.coplaydev.unity-mcp`) and Unity AI Assistant packages are installed, enabling the `mcp__UnityMCP__*` tool family for editor introspection/control from Claude Code — check `mcpforunity://custom-tools` and `mcpforunity://instances` before using them, per the MCP server's own instructions.

**Heavy scene warning:** `Assets/3.1. Programmer_lee/01 Scene/DemoScene.unity` is a ~19 MB copy of the vendor demo scene. Open it to browse available props, but do not work in it — it is slow to load, can exhaust the editor's graphics ring buffer (`Ran out of Graphics Ring Buffer space`), and a merge conflict in it is effectively unresolvable. Build whitebox scenes fresh under `Assets/_Game/Scenes/`.

## System conventions

These are decided; changing them affects rule data and level layout, so raise it with the team rather than adjusting locally.

- **`ComplianceMode`** — every rule declares how compliance ends: `Attempt` (one valid interaction confirms it; the watcher then goes dormant and later world changes are ignored), `StateAtExit`, or `Continuous`. "Close the open ones" rules are always `Attempt`, because the presentation deliberately re-opens them.
- **`RED_THRESHOLD = 75`** — rules phrased "if the lighting looks red" evaluate against the 조도 axis value, independent of the flashlight. 50–74 (3200K) deliberately stays a grey zone: it looks red but does not count as red.
- **Unvisited-space penalty is a single event (배치 +22)**, never the sum of that space's individual rule violations.
- **Ordinal naming** — rules refer to "the third stall", "desk #1". Scene objects register their ordinal via `SpaceRegistry`, and the reference direction is **left-to-right as seen from the entrance**. Changing a layout that shifts ordinals changes what the rules mean; notify the systems owner.
- Fluorescent-count-and-colour-temperature per 조도 band is a single shared table; the only per-space parameter is the light count (corridor 8 / classroom 8 / toilet 4 / science 4). Science room 90–100 keeps a minimum red glow rather than going fully dark, because two of its rules require looking at the model with the flashlight off.

## Ownership

- **Programmer_Lee** — systems: judgement, stats, day direction, rule data schema. Works mostly in `.cs` and `.asset`, so rarely needs scene locks.
- **Programmer_Kim** — client: player, UI (지침록 tablet, HUD, summary), space presenters, scenes, audio. Holds scene locks most often; coordinate lock time slots with the art team and release them the same day.

The only thing crossing between them is the `BandChanged(space, axis, band)` event plus the `ISpacePresenter` / `IDocumentView` interfaces. `DebugAxisDriver` (editor-only axis sliders) lets client work proceed without the judgement system being finished — keep it working.

## Branches

Team branches observed on the remote: `main`, `Programmer_Lee` (current), `Programmer_Jinsun`, `hyunuung`, `Art`. Work tends to happen on a per-person branch before merging to `main`.
