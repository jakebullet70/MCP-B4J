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

## Tier 2 — workflow depth

- [ ] **Module management (`b4j_create_module`)** — create a `.bas` *and* register it in the
  `.b4j` (`NumberOfModules` / `Module{N}`). Today you can edit modules but not properly add one.
- [ ] **Line-range `b4j_read_bas`** — `offset` / `limit` params so large modules don't dump
  whole-file and burn tokens.
- [ ] **Multi-edit `b4j_edit_bas`** — apply several search/replace edits atomically with a single backup.
- [ ] **Index `.b4xlib` in `b4j_search_library`** — search currently skips source libraries.
- [ ] **Richer project parse** — `#AdditionalJar`, `#PackagerProperty`, `#MergeLibraries`, plus a
  *missing-library* check (libs referenced but not present on disk).

## Tier 3 — polish / future

- [ ] Pass build-config + obfuscate through `b4j_run`.
- [ ] `b4j_clean` — wipe `Objects/` and force a rebuild.
- [ ] Cross-platform support — B4J runs on macOS/Linux; the hardcoded `.exe` / `win-x64` blocks it.
- [ ] Layout scaffolding — create/clone a `.bjl`, or generate a starter view tree.

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
