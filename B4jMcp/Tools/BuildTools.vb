Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports Newtonsoft.Json
Imports B4jMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class BuildTools
        Private Const LastBuildLogKey As String = "lastBuildLog"

        <McpServerTool, Description("Compiles a B4J project using B4JBuilder.exe (Release-style compile). Returns the full build log and the output .jar path on success.")>
        Public Shared Async Function B4jBuild(
            <Description("Full path to the .b4j project file")> projectPath As String,
            <Description("Optional build configuration name (maps to B4JBuilder -Configuration). Leave empty for the default.")> Optional configuration As String = "",
            <Description("If true, obfuscate the compiled jar (default False)")> Optional obfuscate As Boolean = False
        ) As Task(Of String)
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4j extension"
            End If

            Dim cfg = AppConfig.Load()
            If String.IsNullOrEmpty(cfg.B4jPath) Then
                Return "Error: b4jPath is not configured. Use b4j_set_config(key='b4jPath', value='C:\\Program Files\\Anywhere Software\\B4J')"
            End If

            Dim builderPath = Path.Combine(cfg.B4jPath, "B4JBuilder.exe")
            If Not File.Exists(builderPath) Then
                Return $"Error: B4JBuilder.exe not found at {builderPath}"
            End If

            Dim baseFolder = Path.GetDirectoryName(projectPath)
            Dim projectFile = Path.GetFileName(projectPath)
            Dim projectName = Path.GetFileNameWithoutExtension(projectPath)
            Dim outputName = projectName & ".jar"

            Dim configArg As String = ""
            If Not String.IsNullOrEmpty(configuration) Then configArg = $" -Configuration=""{configuration}"""

            Dim obfArg As String = If(obfuscate, "True", "False")

            Dim args = $"-Task=Build -BaseFolder=""{baseFolder}"" -Project=""{projectFile}"" -Obfuscate={obfArg} -ShowWarnings=True -Output=""{outputName}""{configArg}"

            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = builderPath,
                    .Arguments = args,
                    .WorkingDirectory = baseFolder,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }

                Dim output As New System.Text.StringBuilder()
                Dim exitCode As Integer = -1
                Using proc As New Process() With {.StartInfo = psi}
                    AddHandler proc.OutputDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine(e.Data)
                    AddHandler proc.ErrorDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine(e.Data)
                    proc.Start()
                    proc.BeginOutputReadLine()
                    proc.BeginErrorReadLine()
                    Await Task.Run(Sub() proc.WaitForExit(300_000))
                    exitCode = proc.ExitCode
                End Using

                Dim log = output.ToString()
                CacheManager.Store(LastBuildLogKey, log)

                Dim result As New System.Text.StringBuilder()
                result.AppendLine($"Build completed (exit code {exitCode}):")
                result.AppendLine(log)

                ' Append output jar path if build succeeded
                If exitCode = 0 Then
                    Dim jarPath = ResolveJarPath(baseFolder, outputName)
                    If jarPath IsNot Nothing Then
                        result.AppendLine($"Output: {jarPath}")
                    End If
                End If

                Return result.ToString().TrimEnd()
            Catch ex As Exception
                Return $"Error starting build: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Runs a compiled B4J project. Builds first if no jar exists, then launches it with the configured Java. For UI/JavaFX apps that keep running, returns the PID and any startup output; for Console/Server apps it waits up to timeoutMs and returns the captured output.")>
        Public Shared Async Function B4jRun(
            <Description("Full path to the .b4j project file")> projectPath As String,
            <Description("Optional arguments passed to the B4J app")> Optional appArgs As String = "",
            <Description("How long to wait for output before returning while the app is still running, in ms (default 4000)")> Optional timeoutMs As Integer = 4000,
            <Description("Build configuration name passed through to the build step (when a build is needed). Leave empty for the default.")> Optional configuration As String = "",
            <Description("Obfuscate when building (default False)")> Optional obfuscate As Boolean = False,
            <Description("Force a rebuild before running even if a jar already exists (default False)")> Optional forceBuild As Boolean = False
        ) As Task(Of String)
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4j extension"
            End If

            Dim cfg = AppConfig.Load()
            Dim baseFolder = Path.GetDirectoryName(projectPath)
            Dim outputName = Path.GetFileNameWithoutExtension(projectPath) & ".jar"

            ' Locate the jar; build if missing or a rebuild was requested.
            Dim jarPath = ResolveJarPath(baseFolder, outputName)
            If jarPath Is Nothing OrElse forceBuild Then
                Dim buildResult = Await B4jBuild(projectPath, configuration, obfuscate)
                jarPath = ResolveJarPath(baseFolder, outputName)
                If jarPath Is Nothing Then
                    Return $"No jar found and build did not produce one.{Environment.NewLine}{buildResult}"
                End If
            End If

            ' Resolve java.exe
            Dim javaExe As String = "java.exe"
            If Not String.IsNullOrEmpty(cfg.JavaBin) Then
                Dim candidate = Path.Combine(cfg.JavaBin, "java.exe")
                If File.Exists(candidate) Then javaExe = candidate
            End If

            ' JavaFX/UI apps need the JavaFX modules on the module path; a plain `java -jar` fails with
            ' "JavaFX runtime components are missing". The B4J JDK ships them at <javaHome>\javafx\lib.
            Dim runArgs As String = ""
            Try
                Dim appType = B4jParser.Parse(projectPath).AppType
                Dim isUi = appType IsNot Nothing AndAlso
                           (appType.Equals("JavaFX", StringComparison.OrdinalIgnoreCase) OrElse
                            appType.Equals("UI", StringComparison.OrdinalIgnoreCase))
                If isUi AndAlso Not String.IsNullOrEmpty(cfg.JavaBin) Then
                    Dim javaHome = Path.GetDirectoryName(cfg.JavaBin.TrimEnd("\"c, "/"c))
                    Dim fxLib = Path.Combine(javaHome, "javafx", "lib")
                    If Directory.Exists(fxLib) Then
                        runArgs = $"--module-path ""{fxLib}"" --add-modules javafx.controls,javafx.fxml,javafx.web,javafx.media,javafx.swing "
                    End If
                End If
            Catch
                ' If app-type detection fails, fall back to a plain launch.
            End Try

            runArgs &= $"-jar ""{jarPath}"""
            If Not String.IsNullOrEmpty(appArgs) Then runArgs &= " " & appArgs

            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = javaExe,
                    .Arguments = runArgs,
                    .WorkingDirectory = Path.GetDirectoryName(jarPath),
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }

                Dim proc As New Process() With {.StartInfo = psi}
                proc.EnableRaisingEvents = True
                proc.Start()

                ' Register so the app can be stopped / tailed after the startup window.
                Dim app = ProcessRegistry.Register(proc, Path.GetFileNameWithoutExtension(jarPath))
                AddHandler proc.OutputDataReceived, Sub(s, e) If e.Data IsNot Nothing Then app.Append(e.Data)
                AddHandler proc.ErrorDataReceived, Sub(s, e) If e.Data IsNot Nothing Then app.Append(e.Data)
                AddHandler proc.Exited, Sub(s, e)
                                            app.HasExited = True
                                            Try
                                                app.ExitCode = proc.ExitCode
                                            Catch
                                            End Try
                                        End Sub
                proc.BeginOutputReadLine()
                proc.BeginErrorReadLine()

                Dim exited = Await Task.Run(Function() proc.WaitForExit(Math.Max(0, timeoutMs)))

                If exited Then
                    proc.WaitForExit()  ' flush async output buffers
                    Dim code = proc.ExitCode
                    Dim outText = app.ReadAll()
                    ProcessRegistry.Remove(app.Pid)
                    proc.Dispose()
                    Return $"App exited (exit code {code}):{Environment.NewLine}{outText.TrimEnd()}"
                Else
                    ' Still running — leave it alive (GUI/server) and report. Tracked in the registry.
                    Dim pid = proc.Id
                    Return $"App is running (PID {pid}). Use b4j_tail_log({pid}) to read output, b4j_stop({pid}) to stop it.{Environment.NewLine}Startup output:{Environment.NewLine}{app.ReadAll().TrimEnd()}"
                End If
            Catch ex As Exception
                Return $"Error launching app: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Stops a running B4J app previously launched by b4j_run, identified by its process ID. Kills the entire process tree and returns the final captured output.")>
        Public Shared Function B4jStop(
            <Description("Process ID returned by b4j_run")> pid As Integer
        ) As String
            Dim app As ProcessRegistry.RunningApp = Nothing
            If Not ProcessRegistry.TryGet(pid, app) Then
                Return $"Error: No tracked B4J app with PID {pid}. Use b4j_list_processes to see running apps."
            End If
            Try
                If Not app.Proc.HasExited Then
                    app.Proc.Kill(entireProcessTree:=True)
                    app.Proc.WaitForExit(5000)
                End If
                Dim outText = app.ReadAll()
                ProcessRegistry.Remove(pid)
                app.Proc.Dispose()
                Return $"Stopped PID {pid}.{Environment.NewLine}Final output:{Environment.NewLine}{outText.TrimEnd()}"
            Catch ex As Exception
                Return $"Error stopping PID {pid}: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns captured stdout/stderr (including Log() output) from a B4J app launched by b4j_run. Set onlyNew=true to return only output since the previous tail call.")>
        Public Shared Function B4jTailLog(
            <Description("Process ID returned by b4j_run")> pid As Integer,
            <Description("If true, return only output appended since the last tail call. Default false (full output).")> Optional onlyNew As Boolean = False
        ) As String
            Dim app As ProcessRegistry.RunningApp = Nothing
            If Not ProcessRegistry.TryGet(pid, app) Then
                Return $"Error: No tracked B4J app with PID {pid}. Use b4j_list_processes to see running apps."
            End If
            Dim text = If(onlyNew, app.ReadNew(), app.ReadAll())
            Dim status = If(app.HasExited, $"exited (code {If(app.ExitCode.HasValue, app.ExitCode.Value.ToString(), "?")})", "running")
            Return $"PID {pid} [{status}]:{Environment.NewLine}{text.TrimEnd()}"
        End Function

        <McpServerTool, Description("Lists B4J apps currently tracked (launched via b4j_run), with PID, project name, status, and start time.")>
        Public Shared Function B4jListProcesses() As String
            Dim apps = ProcessRegistry.ListApps()
            If apps.Count = 0 Then Return "No tracked B4J apps. Launch one with b4j_run."
            Dim list = apps.Select(Function(a) New With {
                .pid = a.Pid,
                .project = a.ProjectName,
                .status = If(a.HasExited, "exited", "running"),
                .exitCode = If(a.ExitCode.HasValue, CObj(a.ExitCode.Value), Nothing),
                .startedUtc = a.StartedUtc.ToString("o")
            })
            Return JsonConvert.SerializeObject(New With {.count = apps.Count, .processes = list}, Formatting.Indented)
        End Function

        <McpServerTool, Description("Opens a B4J project in the B4J IDE (B4J.exe) for interactive debugging. The IDE provides breakpoints, step-through and variable inspection that the command-line builder cannot. Launches the IDE and returns immediately; the IDE stays open.")>
        Public Shared Function B4jOpenIde(
            <Description("Full path to the .b4j project file to open")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4j extension"
            End If

            Dim cfg = AppConfig.Load()
            If String.IsNullOrEmpty(cfg.B4jPath) Then
                Return "Error: b4jPath is not configured. Use b4j_set_config(key='b4jPath', value='C:\\Program Files\\Anywhere Software\\B4J')"
            End If

            Dim idePath = Path.Combine(cfg.B4jPath, "B4J.exe")
            If Not File.Exists(idePath) Then
                Return $"Error: B4J.exe not found at {idePath}"
            End If

            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = idePath,
                    .Arguments = $"""{projectPath}""",
                    .WorkingDirectory = Path.GetDirectoryName(projectPath),
                    .UseShellExecute = True
                }
                Dim proc = Process.Start(psi)
                Dim pid = If(proc IsNot Nothing, proc.Id, -1)
                Return $"B4J IDE launched (PID {pid}) with project: {projectPath}{Environment.NewLine}Use the IDE to set breakpoints and run in debug mode (F5/F8)."
            Catch ex As Exception
                Return $"Error launching B4J IDE: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Deletes a B4J project's Objects build folder to force a clean rebuild (clears stale generated code/jars). Optionally rebuilds afterwards and returns the build log.")>
        Public Shared Async Function B4jClean(
            <Description("Full path to the .b4j project file")> projectPath As String,
            <Description("If true, run a fresh build after cleaning (default False)")> Optional rebuild As Boolean = False
        ) As Task(Of String)
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4j extension"
            End If

            Dim baseFolder = Path.GetDirectoryName(projectPath)
            Dim objectsDir = Path.Combine(baseFolder, "Objects")
            Dim msg As String

            If Directory.Exists(objectsDir) Then
                Try
                    Directory.Delete(objectsDir, recursive:=True)
                    msg = $"Cleaned: deleted {objectsDir}"
                Catch ex As Exception
                    Return $"Error deleting {objectsDir}: {ex.Message} (is the app still running? stop it with b4j_stop first)"
                End Try
            Else
                msg = $"Nothing to clean: {objectsDir} does not exist"
            End If

            If rebuild Then
                Dim buildResult = Await B4jBuild(projectPath)
                Return $"{msg}{Environment.NewLine}{buildResult}"
            End If
            Return msg
        End Function

        <McpServerTool, Description("Returns the log from the last b4j_build call")>
        Public Shared Function B4jGetBuildLog() As String
            Dim log As String = Nothing
            If CacheManager.TryGet(Of String)(LastBuildLogKey, log) Then Return log
            Return "No build log available. Run b4j_build first."
        End Function

        ''' <summary>Finds the compiled jar in the project's Objects folder.</summary>
        Private Shared Function ResolveJarPath(baseFolder As String, outputName As String) As String
            Dim objectsDir = Path.Combine(baseFolder, "Objects")
            Dim candidate = Path.Combine(objectsDir, outputName)
            If File.Exists(candidate) Then Return candidate
            If Directory.Exists(objectsDir) Then
                Dim newest = Directory.GetFiles(objectsDir, "*.jar") _
                    .OrderByDescending(Function(f) File.GetLastWriteTimeUtc(f)) _
                    .FirstOrDefault()
                If newest IsNot Nothing Then Return newest
            End If
            Return Nothing
        End Function

    End Class
End Namespace
