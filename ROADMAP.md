# MCP-B4J Roadmap

Prioritized feature backlog. Tier 1 is complete; Tier 2/3 are open.

## Tier 1 — close the run/debug loop & code navigation ✅ DONE

- [x] **Process registry + `b4j_stop` / `b4j_tail_log` / `b4j_list_processes`**
  Track launched apps, stop them, and tail accumulated `Log()`/stdout. Also fixed the
  undisposed-process / unbounded-buffer leak in `b4j_run`.
  → `Utils/ProcessRegistry.vb`, `Tools/BuildTools.vb`
- [x] **`b4j_outline` / `b4j_find_symbol`**
  Parse `Sub`/`Type`/`Globals` per module; find definitions + references across the project
  (case-insensitive, matching B4J).
  → `Utils/B4jSymbolParser.vb`, `Tools/NavTools.vb`
- [x] **`b4j_doctor`**
  Verify `B4JBuilder.exe`, `B4J.exe`, `java.exe`, libraries folder resolve; report Java version.
  → `Tools/ConfigTools.vb`

## Tier 2 — workflow depth ✅ DONE

- [x] **Module management (`b4j_create_module`)** — creates a `.bas` (class or code) *and* registers it
  in the `.b4j` (`NumberOfModules` / `Module{N}`), with a project-file backup. → `Tools/BasTools.vb`
- [x] **Line-range `b4j_read_bas`** — optional `offset` / `limit` params; defaults to whole-file.
  → `Tools/BasTools.vb`
- [x] **Multi-edit (`b4j_multi_edit_bas`)** — applies a JSON array of search/replace edits atomically
  (all-or-nothing) with a single backup. → `Tools/BasTools.vb`
- [x] **Index `.b4xlib` in `b4j_search_library`** — now also matches Sub names inside `.b4xlib`
  source libraries. → `Tools/LibraryTools.vb`
- [x] **Richer project parse** — `#AdditionalJar`, `#PackagerProperty`, `#MergeLibraries` surfaced in
  `b4j_read_project`, plus a missing-library check (referenced libs not found on disk; no false
  positives when no library dirs are configured). → `Models/B4jProject.vb`, `Utils/B4jParser.vb`, `Tools/ProjectTools.vb`

## Tier 3 — polish / future ✅ DONE

- [x] **Build passthrough on `b4j_run`** — `configuration` / `obfuscate` flow into the build step, and
  `forceBuild` rebuilds even when a jar exists. → `Tools/BuildTools.vb`
- [x] **`b4j_clean`** — deletes the `Objects/` folder to force a clean rebuild; optional `rebuild`.
  → `Tools/BuildTools.vb`
- [x] **`b4j_clone_layout`** — clones a `.bjl` via the converter (validated round-trip).
  → `Tools/LayoutTools.vb`
- [x] ~~Cross-platform~~ **Won't do — Windows only.** `B4JBuilder.exe` and the B4J IDE are
  Windows-only tools, so the server is inherently Windows-only. The earlier OS-aware executable
  resolution was removed as misleading; tool names are hardcoded to `.exe`.

## Tier 4 — layout authoring ✅ DONE

- [x] **`b4j_add_view`** — inserts a fully-formed view (Label / Button / TextField / CheckBox /
  ComboBox / ScrollPane / ImageView / Pane) with the complete default property set its runtime
  wrapper *and* the Abstract Designer require, registers it in `ControlsHeaders`, keeps child
  indices contiguous, and backs up the file. → `Utils/ViewTemplates.vb`, `Tools/LayoutTools.vb`
- [x] **View lint in `b4j_write_layout`** — a clean binary round-trip does not prove a view is
  loadable. `b4j_write_layout` now returns non-fatal warnings for the traps that pass the
  round-trip but break at `LoadLayout` / in the Designer: ImageView with a non-`BitmapDrawable`
  drawable; ComboBox missing `editable`; ScrollPane missing `hbar`/`vbar`/`pannable`; control
  views missing base `contextMenu`/`toolTip`/`eventName`; `ControlsHeaders` ↔ view-tree name
  mismatches; non-contiguous child indices. → `Utils/ViewTemplates.vb`

## Known follow-ups / tech debt

- `b4j_find_symbol` scans all top-level `.bas` files in the project dir (plus the `.b4j`), including
  files **not registered** in the project (verified: surfaced an orphan `TitleBar.bas`). Consider an
  option to restrict to project-registered modules. Subfolder/shared-module folders aren't covered.
- `b4j_edit_bas` overwrites `.bak` on each edit (no history).
- Path handling allows reads/writes to any `.bas`/`.b4j`/`.bjl` on disk (acceptable for a local
  dev tool; revisit if ever exposed remotely).

## Fixed during testing

- **`b4j_run` JavaFX launch.** UI/JavaFX apps failed with "JavaFX runtime components are missing"
  because they were launched with a bare `java -jar`. `b4j_run` now detects `JavaFX`/`UI` app types
  and adds `--module-path <javaHome>\javafx\lib --add-modules javafx.controls,…`. (Verified against
  the GUIHelpers sample project.)
- **`b4j_outline` / `b4j_find_symbol` now handle `Class_Globals`.** `GlobalsStartRegex` includes
  `Class_Globals`, so class-module globals are extracted alongside `Process_Globals`/`Globals`.
- **`b4j_find_symbol` reports module-name definitions.** A class/code module whose filename matches
  the query is now returned as a definition (`kind` = `ClassModule` / `Module`), so type names like
  `TitleBarHelper` resolve instead of returning `definitionCount: 0`.
