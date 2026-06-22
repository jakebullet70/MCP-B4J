Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports Newtonsoft.Json
Imports B4jMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class ProjectTools

        <McpServerTool, Description("Reads a B4J project file (.b4j) and returns its metadata: app type, libraries, modules, build config")>
        Public Shared Function B4jReadProject(
            <Description("Full path to the .b4j project file")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4j extension"
            End If
            Try
                Dim proj = B4jParser.Parse(projectPath)
                Return JsonConvert.SerializeObject(New With {
                    .appType = proj.AppType,
                    .appLabel = proj.AppLabel,
                    .versionCode = proj.VersionCode,
                    .versionName = proj.VersionName,
                    .mainFormWidth = proj.MainFormWidth,
                    .mainFormHeight = proj.MainFormHeight,
                    .libraries = proj.Libraries,
                    .modules = proj.Modules.Select(Function(m) Path.GetFileName(m)).ToList(),
                    .layouts = proj.Layouts.Select(Function(l) Path.GetFileName(l)).ToList(),
                    .buildConfigs = proj.BuildConfigs
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error parsing project: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Lists all source files, layouts, and assets in a B4J project")>
        Public Shared Function B4jListProjectFiles(
            <Description("Full path to the .b4j project file")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            Try
                Dim proj = B4jParser.Parse(projectPath)
                Dim projectDir = Path.GetDirectoryName(projectPath)
                If String.IsNullOrEmpty(projectDir) Then projectDir = "."

                Dim assets As New List(Of String)
                Dim filesDir = Path.Combine(projectDir, "Files")
                If Directory.Exists(filesDir) Then
                    For Each f In Directory.GetFiles(filesDir, "*", SearchOption.AllDirectories)
                        assets.Add(f.Substring(filesDir.Length).TrimStart(Path.DirectorySeparatorChar))
                    Next
                End If

                Return JsonConvert.SerializeObject(New With {
                    .projectFile = projectPath,
                    .sourceModules = proj.Modules,
                    .layouts = proj.Layouts.Select(Function(l) Path.GetFileName(l)).ToList(),
                    .assets = assets
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns full project context in one call: app info, libraries, modules, layouts, and last build error if any")>
        Public Shared Function B4jProjectContext(
            <Description("Full path to the .b4j project file")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            Try
                Dim proj = B4jParser.Parse(projectPath)

                Dim lastError As String = ""
                Dim cachedLog As String = Nothing
                If CacheManager.TryGet(Of String)("lastBuildLog", cachedLog) Then
                    Dim errorLine = cachedLog _
                        .Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries) _
                        .FirstOrDefault(Function(l) l.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                    If errorLine IsNot Nothing Then lastError = errorLine.Trim()
                End If

                Return JsonConvert.SerializeObject(New With {
                    .appType = proj.AppType,
                    .appLabel = proj.AppLabel,
                    .versionCode = proj.VersionCode,
                    .versionName = proj.VersionName,
                    .libraries = proj.Libraries,
                    .modules = proj.Modules.Select(Function(m) Path.GetFileName(m)).ToList(),
                    .layouts = proj.Layouts.Select(Function(l) Path.GetFileName(l)).ToList(),
                    .lastBuildError = If(String.IsNullOrEmpty(lastError), Nothing, CObj(lastError))
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns a list of critical B4J language gotchas and pitfalls that frequently cause hard-to-debug bugs. Call this when starting work on a B4J project or when encountering unexpected behavior.")>
        Public Shared Function B4jLanguageGotchas() As String
            Dim gotchas As New List(Of Object) From {
                New With {
                    .title = "B4J is completely case-insensitive",
                    .severity = "CRITICAL",
                    .description = "Variable names differing only in capitalization are THE SAME variable. A local Dim with the same name as a module global (even different case) overwrites the global.",
                    .example = "In a module, 'Dim itemList As List' collides with module global 'ItemList'. Calling itemList.Initialize destroys ItemList content.",
                    .fix = "Always use clearly distinct names for local variables vs module globals."
                },
                New With {
                    .title = "Application_Error returning True suppresses all exceptions",
                    .severity = "CRITICAL",
                    .description = "If Application_Error (in the Main module) returns True, ALL unhandled runtime exceptions are silently swallowed. Bugs become invisible.",
                    .example = "A NullPointerException in an event handler never shows — Application_Error eats it.",
                    .fix = "During debugging, temporarily set Application_Error to return False (or log the error before returning True)."
                },
                New With {
                    .title = "Wait For must reference a live object",
                    .severity = "HIGH",
                    .description = "Wait For on a Resumable Sub whose sender object has gone out of scope (e.g. a local declared inside the calling Sub) silently never resumes.",
                    .example = "A local 'Dim j As HttpJob' that the Sub returns from before the response arrives — the Wait For never fires.",
                    .fix = "Keep the awaited object alive in a Process_Global, or structure the resumable so its sender outlives the Wait For."
                },
                New With {
                    .title = "AppType determines the available API surface",
                    .severity = "HIGH",
                    .description = "A 'Console'/'Server' B4J app has no Form/JavaFX UI; a 'JavaFX'/'UI' app does. Referencing UI classes (Form, Pane, xui) in a non-UI app fails to compile.",
                    .example = "Using 'Dim MainForm As Form' in a Console app → compile error.",
                    .fix = "Check the project's AppType (b4j_read_project) before adding UI code."
                },
                New With {
                    .title = "Main entry point differs by AppType",
                    .severity = "MEDIUM",
                    .description = "JavaFX/UI apps start in Sub AppStart(Form1 As Form, Args() As String). Console/Server apps start in Sub AppStart(Args() As String) — no Form parameter.",
                    .example = "Copying an AppStart with a Form parameter into a Console app breaks the build.",
                    .fix = "Match the AppStart signature to the AppType."
                },
                New With {
                    .title = "Reserved keywords cannot be used as variable/sub names",
                    .severity = "MEDIUM",
                    .description = "B4J has keywords that look like valid identifiers but are reserved: 'Is', 'ATan2', 'Rnd'.",
                    .example = "Sub IsReady() or Dim IsActive As Boolean causes compile errors. 'Rnd' as a variable name conflicts with the built-in random function.",
                    .fix = "Avoid Is*, ATan2, Rnd as sub or variable names."
                },
                New With {
                    .title = "Colors.R/G/B/A component extraction does not exist",
                    .severity = "MEDIUM",
                    .description = "B4J does not have Colors.R(), Colors.G(), Colors.B(), Colors.A() functions to extract color components.",
                    .example = "Dim r As Int = Colors.R(someColor) — compile error.",
                    .fix = "Use bit operations: R = Bit.And(Bit.ShiftRight(color, 16), 0xFF), etc."
                },
                New With {
                    .title = "Build config first token must be 'Default'",
                    .severity = "LOW",
                    .description = "In the B4J project file, Build1 (and other build configs) must start with 'Default' as the first token.",
                    .example = "Build1=release,myapp → build fails. Correct: Build1=Default,myapp",
                    .fix = "Ensure Build1=Default,<name> in the .b4j project file."
                }
            }
            Return JsonConvert.SerializeObject(New With {
                .count = gotchas.Count,
                .gotchas = gotchas
            }, Formatting.Indented)
        End Function

        <McpServerTool, Description("Returns the list of recently opened B4J projects from the B4J IDE history (b4xV5.ini RecentFile entries).")>
        Public Shared Function B4jListRecentProjects() As String
            Try
                Dim ini = AppConfig.GetB4jIniValues()
                Dim recentFiles As New List(Of Object)
                Dim i = 1
                Do
                    Dim value As String = Nothing
                    If ini.TryGetValue($"RecentFile{i}", value) AndAlso Not String.IsNullOrEmpty(value) Then
                        recentFiles.Add(New With {
                            .index = i,
                            .path = value,
                            .exists = File.Exists(value),
                            .name = Path.GetFileNameWithoutExtension(value)
                        })
                        i += 1
                    Else
                        Exit Do
                    End If
                Loop
                Return JsonConvert.SerializeObject(New With {
                    .count = recentFiles.Count,
                    .recentProjects = recentFiles
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

    End Class
End Namespace
