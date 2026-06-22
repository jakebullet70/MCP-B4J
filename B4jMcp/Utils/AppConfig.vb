Imports System.IO
Imports Newtonsoft.Json
Imports B4jMcp.Models

Namespace Utils
    Public Class AppConfig
        Private Shared ReadOnly _configPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mcp-b4j", "config.json")

        Private Shared ReadOnly _b4jIniPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Anywhere Software", "B4J", "b4xV5.ini")

        Private Shared _stored As McpConfig   ' Explicit overrides from JSON
        Private Shared _instance As McpConfig ' Effective config (stored + auto-detected)

        Public Shared Function Load() As McpConfig
            If _instance IsNot Nothing Then Return _instance
            LoadStored()
            _instance = MergeWithB4jDefaults(_stored)
            Return _instance
        End Function

        Private Shared Sub LoadStored()
            If Not File.Exists(_configPath) Then
                _stored = New McpConfig()
                SaveStored()
            Else
                Dim json = File.ReadAllText(_configPath)
                Try
                    Dim parsed = JsonConvert.DeserializeObject(Of McpConfig)(json)
                    _stored = If(parsed IsNot Nothing, parsed, New McpConfig())
                Catch ex As Newtonsoft.Json.JsonException
                    _stored = New McpConfig()
                End Try
            End If
        End Sub

        Private Shared Function MergeWithB4jDefaults(base As McpConfig) As McpConfig
            Dim merged As New McpConfig With {
                .B4jPath = base.B4jPath,
                .AdditionalLibrariesPath = base.AdditionalLibrariesPath,
                .ProjectsRoot = base.ProjectsRoot,
                .SharedModulesFolder = base.SharedModulesFolder,
                .JavaBin = base.JavaBin
            }

            If Not File.Exists(_b4jIniPath) Then Return merged

            Dim ini = ParseIni(_b4jIniPath)

            ' Auto-detect AdditionalLibrariesPath from B4J's AdditionalLibrariesFolder
            If String.IsNullOrEmpty(merged.AdditionalLibrariesPath) Then
                Dim v As String = Nothing
                If ini.TryGetValue("AdditionalLibrariesFolder", v) AndAlso Not String.IsNullOrEmpty(v) Then
                    merged.AdditionalLibrariesPath = v
                End If
            End If

            ' Auto-detect B4jPath from standard installation folder
            If String.IsNullOrEmpty(merged.B4jPath) Then
                Dim candidate = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Anywhere Software", "B4J")
                If Directory.Exists(candidate) Then
                    merged.B4jPath = candidate
                End If
            End If

            ' Auto-detect SharedModulesFolder
            If String.IsNullOrEmpty(merged.SharedModulesFolder) Then
                Dim v As String = Nothing
                If ini.TryGetValue("SharedModulesFolder", v) AndAlso Not String.IsNullOrEmpty(v) Then
                    merged.SharedModulesFolder = v
                End If
            End If

            ' Auto-detect JavaBin
            If String.IsNullOrEmpty(merged.JavaBin) Then
                Dim v As String = Nothing
                If ini.TryGetValue("JavaBin", v) AndAlso Not String.IsNullOrEmpty(v) Then
                    merged.JavaBin = v
                End If
            End If

            Return merged
        End Function

        Private Shared Function ParseIni(path As String) As Dictionary(Of String, String)
            Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each line In File.ReadAllLines(path)
                Dim trimmed = line.Trim()
                If trimmed.StartsWith(";") OrElse trimmed.StartsWith("#") OrElse Not trimmed.Contains("=") Then Continue For
                Dim idx = trimmed.IndexOf("="c)
                Dim key = trimmed.Substring(0, idx).Trim()
                Dim value = trimmed.Substring(idx + 1).Trim()
                result(key) = value
            Next
            Return result
        End Function

        Private Shared Sub SaveStored()
            Dim dir = Path.GetDirectoryName(_configPath)
            If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
                Directory.CreateDirectory(dir)
            End If
            File.WriteAllText(_configPath, JsonConvert.SerializeObject(_stored, Formatting.Indented))
        End Sub

        Public Shared Sub Save(config As McpConfig)
            _stored = config
            SaveStored()
            _instance = Nothing  ' Invalidate cache so it's rebuilt with auto-detected values
        End Sub

        Public Shared Function SetValue(key As String, value As String) As String
            If _stored Is Nothing Then LoadStored()
            Dim prop = GetType(McpConfig).GetProperty(key,
                Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance Or
                Reflection.BindingFlags.IgnoreCase)
            If prop Is Nothing Then
                Return $"Unknown key: {key}. Valid keys: b4jPath, additionalLibrariesPath, projectsRoot, sharedModulesFolder, javaBin"
            End If
            prop.SetValue(_stored, value)
            SaveStored()
            _instance = Nothing  ' Invalidate cache
            CacheManager.InvalidateLibraries()
            Return $"OK: {key} = {value}"
        End Function

        Public Shared Function GetConfigPath() As String
            Return _configPath
        End Function

        Public Shared Function GetB4jIniPath() As String
            Return _b4jIniPath
        End Function

        ''' <summary>Exposes the parsed b4xV5.ini as a dictionary for use by other tools.</summary>
        Public Shared Function GetB4jIniValues() As Dictionary(Of String, String)
            If Not File.Exists(_b4jIniPath) Then Return New Dictionary(Of String, String)()
            Return ParseIni(_b4jIniPath)
        End Function

        ''' <summary>
        ''' Returns which keys have explicit overrides vs auto-detected from B4J ini.
        ''' </summary>
        Public Shared Function GetSources() As Dictionary(Of String, String)
            If _stored Is Nothing Then LoadStored()
            Dim sources As New Dictionary(Of String, String)
            For Each key In {"B4jPath", "AdditionalLibrariesPath", "ProjectsRoot", "SharedModulesFolder", "JavaBin"}
                Dim prop = GetType(McpConfig).GetProperty(key)
                Dim storedVal = prop.GetValue(_stored)?.ToString()
                sources(key) = If(String.IsNullOrEmpty(storedVal), "auto (b4xV5.ini)", "explicit (mcp config)")
            Next
            Return sources
        End Function
    End Class
End Namespace
