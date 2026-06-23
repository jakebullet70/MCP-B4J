Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports Newtonsoft.Json
Imports B4jMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class ConfigTools

        <McpServerTool, Description("Returns the current MCP-B4J configuration (paths to B4J, libraries, Java, etc.)")>
        Public Shared Function B4jGetConfig() As String
            Dim cfg = AppConfig.Load()
            Dim sources = AppConfig.GetSources()
            Dim result = New With {
                .serverVersion = AppInfo.Version,
                .b4jPath = cfg.B4jPath,
                .additionalLibrariesPath = cfg.AdditionalLibrariesPath,
                .projectsRoot = cfg.ProjectsRoot,
                .sharedModulesFolder = cfg.SharedModulesFolder,
                .javaBin = cfg.JavaBin,
                .configFile = AppConfig.GetConfigPath(),
                .b4jIniFile = AppConfig.GetB4jIniPath(),
                .sources = sources
            }
            Dim needsSetup = String.IsNullOrEmpty(cfg.B4jPath)
            If needsSetup Then
                Return JsonConvert.SerializeObject(New With {
                    .config = result,
                    .warning = "b4jPath is not set. Use b4j_set_config to configure. Example: b4j_set_config(key='b4jPath', value='C:\\Program Files\\Anywhere Software\\B4J')"
                }, Formatting.Indented)
            End If
            Return JsonConvert.SerializeObject(result, Formatting.Indented)
        End Function

        <McpServerTool, Description("Updates a configuration value. Valid keys: b4jPath, additionalLibrariesPath, projectsRoot, sharedModulesFolder, javaBin")>
        Public Shared Function B4jSetConfig(
            <Description("Configuration key to set (b4jPath, additionalLibrariesPath, projectsRoot, sharedModulesFolder, javaBin)")> key As String,
            <Description("New value for the configuration key")> value As String
        ) As String
            Return AppConfig.SetValue(key, value)
        End Function

        <McpServerTool, Description("Diagnoses the B4J environment: verifies that B4JBuilder.exe, B4J.exe, java.exe and the libraries folder resolve, reports the detected Java version, and counts available libraries. Run this when builds or runs fail unexpectedly or right after first-time setup.")>
        Public Shared Function B4jDoctor() As String
            Dim cfg = AppConfig.Load()
            Dim checks As New List(Of Object)()
            Dim okCount As Integer = 0
            Dim total As Integer = 0

            Dim add =
                Sub(checkName As String, ok As Boolean, detail As String)
                    checks.Add(New With {.check = checkName, .ok = ok, .detail = detail})
                    total += 1
                    If ok Then okCount += 1
                End Sub

            ' b4jPath
            Dim b4jOk = Not String.IsNullOrEmpty(cfg.B4jPath) AndAlso Directory.Exists(cfg.B4jPath)
            add("b4jPath", b4jOk, If(String.IsNullOrEmpty(cfg.B4jPath), "not configured — run b4j_set_config(key='b4jPath', …)", If(b4jOk, cfg.B4jPath, $"directory not found: {cfg.B4jPath}")))

            ' B4JBuilder.exe and B4J.exe
            If b4jOk Then
                Dim builder = Path.Combine(cfg.B4jPath, "B4JBuilder.exe")
                add("B4JBuilder.exe", File.Exists(builder), If(File.Exists(builder), builder, $"missing at {builder}"))

                Dim ide = Path.Combine(cfg.B4jPath, "B4J.exe")
                add("B4J.exe (IDE)", File.Exists(ide), If(File.Exists(ide), ide, $"missing at {ide} — b4j_open_ide will not work"))

                Dim libDir = Path.Combine(cfg.B4jPath, "Libraries")
                Dim libDirOk = Directory.Exists(libDir)
                Dim libCount = If(libDirOk, Directory.GetFiles(libDir, "*.xml").Length + Directory.GetFiles(libDir, "*.b4xlib").Length, 0)
                add("Built-in libraries", libDirOk, If(libDirOk, $"{libCount} libraries in {libDir}", $"missing at {libDir}"))
            End If

            ' additionalLibrariesPath (only if set)
            If Not String.IsNullOrEmpty(cfg.AdditionalLibrariesPath) Then
                Dim extOk = Directory.Exists(cfg.AdditionalLibrariesPath)
                Dim extCount = If(extOk, Directory.GetFiles(cfg.AdditionalLibrariesPath, "*.xml").Length + Directory.GetFiles(cfg.AdditionalLibrariesPath, "*.b4xlib").Length, 0)
                add("additionalLibrariesPath", extOk, If(extOk, $"{extCount} libraries in {cfg.AdditionalLibrariesPath}", $"directory not found: {cfg.AdditionalLibrariesPath}"))
            End If

            ' Java
            Dim javaExe = If(Not String.IsNullOrEmpty(cfg.JavaBin), Path.Combine(cfg.JavaBin, "java.exe"), "java.exe")
            Dim javaConfigured = Not String.IsNullOrEmpty(cfg.JavaBin)
            If javaConfigured AndAlso Not File.Exists(javaExe) Then
                add("java.exe", False, $"missing at {javaExe} — b4j_run will fail")
            Else
                Dim version As String = "(could not read)"
                Dim javaFound As Boolean = True
                Try
                    Dim psi As New ProcessStartInfo() With {
                        .FileName = javaExe,
                        .Arguments = "-version",
                        .RedirectStandardError = True,
                        .UseShellExecute = False,
                        .CreateNoWindow = True
                    }
                    Using p = Process.Start(psi)
                        Dim stderr = p.StandardError.ReadToEnd()
                        p.WaitForExit(5000)
                        version = stderr.Split(New String() {vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                    End Using
                Catch ex As Exception
                    javaFound = False
                    version = $"java not runnable ({javaExe}): {ex.Message}"
                End Try
                add("java", javaFound, If(javaConfigured, $"{javaExe} — {version}", $"using PATH java.exe — {version}"))
            End If

            ' Config files
            Dim iniPath = AppConfig.GetB4jIniPath()
            add("B4J IDE settings (b4xV5.ini)", File.Exists(iniPath), If(File.Exists(iniPath), iniPath, $"not found at {iniPath} — auto-detection limited"))

            Dim allOk = okCount = total
            Return JsonConvert.SerializeObject(New With {
                .serverVersion = AppInfo.Version,
                .healthy = allOk,
                .summary = $"{okCount}/{total} checks passed",
                .checks = checks,
                .configFile = AppConfig.GetConfigPath()
            }, Formatting.Indented)
        End Function

    End Class
End Namespace
