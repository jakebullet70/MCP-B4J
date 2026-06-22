Imports ModelContextProtocol.Server
Imports System.ComponentModel
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

    End Class
End Namespace
