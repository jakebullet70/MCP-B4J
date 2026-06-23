# MCP Server for B4J

Bridges [Claude Code](https://claude.ai/claude-code) (and any MCP-compatible client) with the [B4J](https://www.b4x.com/b4j.html) (B4X) ecosystem.

Exposes tools for compiling and running B4J projects, opening them in the IDE for interactive debugging, reading/editing source modules, reading/modifying layouts, and exploring libraries (both compiled `.jar`+`.xml` and `.b4xlib` source libraries) — all without leaving your AI coding assistant.

> ## 🍴 This is a fork
>
> **MCP-B4J is a fork of [unmateria/MCP-B4A](https://github.com/unmateria/MCP-B4A)** — the original MCP server for B4A (Basic4Android). All credit for the architecture, the `BalConverter` port, and the tooling design goes to the upstream project.
>
> This fork retargets the server from **B4A (Android)** to **B4J (Java / JVM desktop & server apps)**. The Android-only pieces — ADB device control, screenshots/tap/swipe, APK install, manifest editor, keystore signing, and sprite cleanup — are removed. Build/run targets `B4JBuilder.exe` and the JVM instead of `B4ABuilder.exe` and the APK toolchain. See [Differences from upstream](#differences-from-upstream) below.

---

## Tools

### Configuration

| Tool | Description |
|------|-------------|
| `b4j_get_config` | Returns current paths and config sources (auto-detected vs explicit) |
| `b4j_set_config` | Updates a configuration value |
| `b4j_doctor` | Verifies `B4JBuilder.exe`, `B4J.exe`, `java.exe` and the libraries folder resolve, reports the Java version, and counts libraries. Run after setup or when builds/runs fail unexpectedly |

### Build & Run

| Tool | Description |
|------|-------------|
| `b4j_build` | Compiles a B4J project via `B4JBuilder.exe`. Returns the full build log and the output `.jar` path on success |
| `b4j_run` | Runs a compiled B4J project with the configured Java (builds first if no jar exists). Optional `configuration`/`obfuscate` pass through to the build; `forceBuild` rebuilds even if a jar exists. Returns the PID + startup output for long-running UI/server apps, or the full output for console apps that exit |
| `b4j_clean` | Deletes the project's `Objects` build folder to force a clean rebuild; optionally rebuilds afterwards |
| `b4j_open_ide` | Opens a project in the B4J IDE (`B4J.exe`) for interactive debugging — breakpoints, step-through and variable inspection that the command-line builder cannot provide. Launches the IDE and returns immediately |
| `b4j_stop` | Stops a running app (launched by `b4j_run`) by PID — kills the process tree and returns its final output |
| `b4j_tail_log` | Returns captured stdout/stderr (incl. `Log()`) from a running/exited app; `onlyNew=true` returns just the output since the last tail |
| `b4j_list_processes` | Lists tracked apps launched via `b4j_run` (PID, project, status, start time) |
| `b4j_get_build_log` | Returns the log from the last build |

### Project

| Tool | Description |
|------|-------------|
| `b4j_read_project` | Reads project metadata: app type, libraries, modules, build configs, main-form size, `#AdditionalJar`/`#MergeLibraries`/`#PackagerProperty`, and a missing-library check (referenced libs not found on disk) |
| `b4j_list_project_files` | Lists source modules, layouts, and asset files |
| `b4j_project_context` | Single-call overview: app info, libraries, modules, layouts, and last build error |
| `b4j_list_recent_projects` | Lists recently opened projects from the B4J IDE history |
| `b4j_language_gotchas` | Critical B4J pitfalls (case-insensitivity, AppType API surface, AppStart signatures, Wait For lifetime, …) |

### Source Modules

| Tool | Description |
|------|-------------|
| `b4j_read_bas` | Reads a `.bas`/`.b4j` source module with line numbers. Optional `offset`/`limit` read just a slice of large modules (pair with `b4j_outline`) |
| `b4j_edit_bas` | Search-and-replace edit on a `.bas` file. Matches exact text (including indentation), normalises line endings, creates a `.bak` backup. Rejects ambiguous matches unless `replace_all=true` |
| `b4j_multi_edit_bas` | Applies several search/replace edits to one file **atomically** (all-or-nothing), in order, with a single `.bak`. Edits passed as a JSON array |
| `b4j_create_module` | Creates a new module `.bas` (class or code) **and registers it** in the `.b4j` (`NumberOfModules`/`Module{N}`). Backs up the project file |

### Code Navigation

| Tool | Description |
|------|-------------|
| `b4j_outline` | Outlines a module: every `Sub` (with signature), `Type`, and `Process_Globals`/`Globals` variable, each with its line number — understand a module without reading the whole file |
| `b4j_find_symbol` | Finds a Sub/Type/global across the project (`.b4j` main module + all `.bas` files). Returns definitions and references; matching is case-insensitive like B4J itself |

### Libraries

| Tool | Description |
|------|-------------|
| `b4j_list_libraries` | Lists available B4J libraries — both compiled `.jar`+`.xml` pairs and `.b4xlib` source libraries — with version and source folder |
| `b4j_get_library_docs` | Returns method/property/event docs. For `.jar` libraries this uses the XML docs; for `.b4xlib` source libraries it lists each module's `Sub` signatures |
| `b4j_search_library` | Searches library docs — compiled (`.jar`+`.xml`) methods/properties/events **and** `.b4xlib` source Sub names |

### Layouts

| Tool | Description |
|------|-------------|
| `b4j_read_layout` | Converts a binary `.bjl` layout to JSON (LayoutHeader, Variants, Data view-tree) |
| `b4j_write_layout` | Writes JSON back to `.bjl` (validates structure, lints views, creates a `.bak` backup) |
| `b4j_add_view` | Adds a fully-formed view (Label/Button/TextField/CheckBox/ComboBox/ScrollPane/ImageView/Pane) with all required default properties, registers it in `ControlsHeaders`, and backs up the file |
| `b4j_clone_layout` | Clones an existing `.bjl` to a new file in the same folder, round-tripped through the converter to validate |
| `b4j_list_layouts` | Lists all `.bjl` files in a project directory |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build) or the .NET 8 Runtime (for a framework-dependent run; the default build is self-contained)
- [B4J IDE](https://www.b4x.com/b4j.html) installed (required for compilation — provides `B4JBuilder.exe`, and `B4J.exe` for `b4j_open_ide`)
- A JDK — B4J ships with one; the server auto-detects `JavaBin` from the IDE settings

---

## Installation

### 1. Build

```powershell
cd B4jMcp
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ../publish
```

This produces `publish/B4jMcp.exe`.

### 2. Register with Claude Code

```bash
claude mcp add b4j C:\path\to\publish\B4jMcp.exe
```

Or add manually to your MCP settings:

```json
{
  "mcpServers": {
    "b4j": {
      "command": "C:\\path\\to\\publish\\B4jMcp.exe",
      "args": []
    }
  }
}
```

### 3. First-time setup

Run `b4j_get_config` to check the auto-detected paths. The server auto-detects them from the B4J IDE settings file (`%APPDATA%\Anywhere Software\B4J\b4xV5.ini`) when possible. If B4J is installed in a non-standard location, configure manually:

```text
b4j_set_config(key="b4jPath", value="C:\\Program Files\\Anywhere Software\\B4J")
b4j_set_config(key="additionalLibrariesPath", value="C:\\dev\\b4x\\b4j\\ext_libs")
b4j_set_config(key="javaBin", value="C:\\dev\\b4x\\java19\\bin")
```

Config is stored at `%APPDATA%\mcp-b4j\config.json`. Explicit values set here always override the auto-detected ones.

#### Configuration keys

| Key | Auto-detected from | Notes |
|-----|--------------------|-------|
| `b4jPath` | `Program Files\Anywhere Software\B4J` | B4J install dir (contains `B4JBuilder.exe`, `B4J.exe`, `Libraries\`) |
| `additionalLibrariesPath` | `AdditionalLibrariesFolder` in `b4xV5.ini` | Your shared/external libraries folder |
| `javaBin` | `JavaBin` in `b4xV5.ini` | `bin` folder of the JDK used to run jars |
| `projectsRoot` | — | Optional default projects folder |
| `sharedModulesFolder` | `SharedModulesFolder` in `b4xV5.ini` | Optional |

---

## Build, Run & Debug notes

- `b4j_build` invokes `B4JBuilder.exe -Task=Build …`, which is the Release-style compile. There is no debug/bundle/sign mode on the command line (debug runs happen inside the IDE; B4J jars are not APK-signed). The compiled jar lands at `<project>\Objects\<ProjectName>.jar`.
- `b4j_run` launches `java -jar <jar>` using `javaBin`. UI/JavaFX and Server apps keep running, so the tool returns once it has captured startup output (configurable via `timeoutMs`); the process is left alive. Console apps that exit within the window return their full output and exit code.
- **Interactive debugging** (breakpoints, step-through, variable inspection, hot code-swap) only exists inside the B4J IDE — it is *not* exposed by `B4JBuilder.exe`. Use `b4j_open_ide` to open the project in `B4J.exe`, then run in debug mode (F5/F8) and set breakpoints there.
- **In-chat run loop:** for long-running UI/server apps, `b4j_run` returns a PID and leaves the app alive. Use `b4j_tail_log(pid)` to read accumulated `Log()`/stdout output (`onlyNew=true` for just the latest), `b4j_list_processes` to see what's running, and `b4j_stop(pid)` to kill it. Together with `b4j_edit_bas` + `b4j_build` this gives a tight build → run → read-log → edit loop without leaving the assistant.
- **Windows only.** `B4JBuilder.exe` and the B4J IDE are Windows-only, so this server is too (it ships as a `win-x64` binary). macOS/Linux is not supported.

---

## Layout Files (`.bjl`)

B4J stores UI layouts in `.bjl` files — a binary `B4XSerializator` format. This server includes an in-process VB.NET port of the official [B4X BalConverter](https://www.b4x.com/android/forum/threads/b4x-balconverter-convert-the-layouts-files-to-json-and-vice-versa.41623/) (no JVM round-trip), providing lossless binary ↔ JSON conversion via `b4j_read_layout` / `b4j_write_layout`.

The JSON has three parts:

- **`LayoutHeader`** — `Version`, `GridSize`, `ControlsHeaders` (view name / JavaType / DesignerType), `Files`, and the `DesignerScript` blocks.
- **`Variants`** — the layout variants (`Scale`, `Width`, `Height`).
- **`Data`** — the hierarchical view tree and properties. Typed values are wrapped as `{ "ValueType": <code>, "Value": … }` (string, float, color `0xAARRGGBB`, RECT32, CNULL).

**Round-trip safety:** `b4j_write_layout` validates the JSON structure and always writes a `.bak` backup first. Conversion is lossless — verified on real B4J designer layouts (read → write → re-read yields identical JSON). The re-written binary is byte-length-identical to the original; exact bytes may differ only in the internal string-cache ordering and gzip encoding of the designer script, which carry no semantic meaning.

**View linting (beyond the binary round-trip):** a clean read→write round-trip only proves the *binary* is well-formed — it does **not** prove each view has the properties its runtime wrapper / the Abstract Designer require, so a hand-authored view can pass the round-trip yet throw at `LoadLayout` or fail to open in the Designer. `b4j_write_layout` now also lints each view and returns warnings (non-fatal) for the common traps:

- **ImageView** drawable must be a `BitmapDrawable` (a `ColorDrawable` makes the Designer fail with *"Unable to cast ColorDrawable to BitmapDrawable"*).
- **ComboBox** must have `editable`; **ScrollPane** must have `hbar` / `vbar` / `pannable` — missing them throws an NPE in the wrapper's `build` at `LoadLayout`.
- Control views must carry the base `contextMenu` / `toolTip` / `eventName`.
- `ControlsHeaders` ↔ view-tree name consistency, and contiguous child indices (`0..n-1`).

**Authoring views:** prefer `b4j_add_view` over hand-writing view JSON for `b4j_write_layout` — it emits the complete, lint-clean default property set for each supported type (Label, Button, TextField, CheckBox, ComboBox, ScrollPane, ImageView, Pane), so the above traps can't happen.

---

## Security

- `b4j_build`, `b4j_run`, and `b4j_open_ide` only accept paths to existing `.b4j` files (extension + existence checked) — no arbitrary command execution.
- All file writes (`b4j_edit_bas`, `b4j_write_layout`) create a `.bak` backup first.
- The server only touches the local filesystem, the configured JDK, and the B4J toolchain — there is no device control, no network listener, and no remote interaction.

---

## Caching

The server caches results to avoid redundant I/O ([`CacheManager.vb`](B4jMcp/Utils/CacheManager.vb)):

- **File-based cache** (mtime-invalidated): layout conversions, project parsing, library docs — auto-invalidated when the source file's modification time changes.
- **TTL cache**: library listings and other short-lived data.
- **Simple store**: last build log (no expiry, replaced on each build).

---

## Development

```powershell
cd B4jMcp
dotnet build
dotnet run
```

The server communicates via **stdio** (MCP standard). It does not open any network ports.

### Project Structure

```
B4jMcp/
├── Program.vb              # Server entry point
├── Models/
│   ├── B4jProject.vb       # Project metadata model
│   └── McpConfig.vb        # Configuration model
├── Tools/
│   ├── BuildTools.vb       # build, run, open IDE, stop/tail/list processes, build log
│   ├── ConfigTools.vb      # get/set configuration, doctor (environment diagnostics)
│   ├── LayoutTools.vb      # read/write/list .bjl layouts
│   ├── LibraryTools.vb     # list, docs, search libraries
│   ├── BasTools.vb         # read/edit .bas source modules
│   ├── NavTools.vb         # outline + find symbol across modules
│   └── ProjectTools.vb     # project metadata, file listing, context, language gotchas
└── Utils/
    ├── AppConfig.vb        # Config management + B4J IDE auto-detection
    ├── B4jParser.vb        # .b4j project file parser
    ├── B4jSymbolParser.vb  # Sub/Type/Global extraction for code navigation
    ├── BalConverter.vb     # Binary .bjl ↔ JSON converter
    ├── CacheManager.vb     # Mtime-based + TTL caching
    └── ProcessRegistry.vb  # Tracks launched apps for stop/tail
```

### Tech Stack

- **VB.NET** targeting **.NET 8**
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) SDK (0.9.0-preview.2)
- [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) for the host/stdio plumbing
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) for serialization

---

## Differences from upstream

Relative to [unmateria/MCP-B4A](https://github.com/unmateria/MCP-B4A):

| Area | MCP-B4A (upstream) | MCP-B4J (this fork) |
|------|--------------------|---------------------|
| Target platform | B4A — Android APKs | B4J — Java / JVM desktop & server apps |
| Compiler | `B4ABuilder.exe` (release/debug/bundle) | `B4JBuilder.exe` (release-style build) |
| Run / deploy | APK install + launch via ADB | `java -jar` via the configured JDK; `b4j_open_ide` for IDE debugging |
| Layout format | `.bal` / `.bil` (+ EditText auto-fix) | `.bjl` (JavaFX views — no EditText auto-fix needed) |
| Removed | ADB device control, screenshots/tap/swipe, APK install, manifest editor, keystore signing, sprite cleanup | — |

> The Android-specific EditText auto-fix logic from the upstream converter (injecting `password`/`inputType` to avoid `EditTextWrapper` NPEs) is intentionally **omitted** — those are JavaFX views in B4J and have no such requirement.

---

## License

Adapted from [MCP-B4A](https://github.com/unmateria/MCP-B4A). See the upstream repository for license terms.
