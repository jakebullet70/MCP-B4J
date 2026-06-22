Imports System.IO
Imports System.Text.RegularExpressions
Imports B4jMcp.Models

Namespace Utils
    Public Class B4jParser
        Private Const Separator As String = "@EndOfDesignText@"

        Public Shared Function Parse(projectFilePath As String) As B4jProject
            Dim proj As B4jProject = Nothing
            If CacheManager.TryGetByMtime(Of B4jProject)(projectFilePath, proj) Then Return proj

            proj = New B4jProject()
            proj.ProjectFile = projectFilePath
            Dim lines = File.ReadAllLines(projectFilePath)
            Dim sepIndex = Array.IndexOf(lines, Separator)
            If sepIndex < 0 Then sepIndex = lines.Length

            ' Parse header key=value pairs
            Dim header As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For i = 0 To sepIndex - 1
                Dim line = lines(i).Trim()
                Dim eq = line.IndexOf("="c)
                If eq > 0 Then
                    header(line.Substring(0, eq).Trim()) = line.Substring(eq + 1).Trim()
                End If
            Next

            ' App type (JavaFX / Console / Server / UI)
            Dim appType As String = Nothing
            If header.TryGetValue("AppType", appType) Then proj.AppType = appType

            ' Extract libraries
            Dim libCount As Integer
            If header.ContainsKey("NumberOfLibraries") Then Integer.TryParse(header("NumberOfLibraries"), libCount)
            For i = 1 To libCount
                If header.ContainsKey($"Library{i}") Then proj.Libraries.Add(header($"Library{i}"))
            Next

            ' Extract modules (source files). B4J counts these with NumberOfModules;
            ' NumberOfFiles is the asset-file count. Fall back to NumberOfFiles for older formats.
            Dim modCount As Integer
            If header.ContainsKey("NumberOfModules") Then
                Integer.TryParse(header("NumberOfModules"), modCount)
            ElseIf header.ContainsKey("NumberOfFiles") Then
                Integer.TryParse(header("NumberOfFiles"), modCount)
            End If
            For i = 1 To modCount
                If header.ContainsKey($"Module{i}") Then proj.Modules.Add(header($"Module{i}"))
            Next

            ' Remaining header entries as build configs
            Dim knownKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "NumberOfLibraries", "NumberOfFiles", "NumberOfModules", "AppType"
            }
            For i = 1 To Math.Max(libCount, modCount) + 1
                knownKeys.Add($"Library{i}")
                knownKeys.Add($"Module{i}")
            Next
            For Each kv In header
                If Not knownKeys.Contains(kv.Key) Then proj.BuildConfigs(kv.Key) = kv.Value
            Next

            ' Parse code section (project attributes)
            If sepIndex < lines.Length - 1 Then
                ParseCodeSection(proj, lines.Skip(sepIndex + 1).ToArray())
            End If

            ' Find layouts next to project or in Files subfolder (.bjl = B4J layout)
            Dim projectDir = Path.GetDirectoryName(projectFilePath)
            If String.IsNullOrEmpty(projectDir) Then projectDir = "."
            Dim filesDir = Path.Combine(projectDir, "Files")
            For Each searchDir In {projectDir, filesDir}
                If Directory.Exists(searchDir) Then
                    For Each f In Directory.GetFiles(searchDir, "*.bjl")
                        proj.Layouts.Add(f)
                    Next
                End If
            Next

            CacheManager.SetByMtime(projectFilePath, proj)
            Return proj
        End Function

        Private Shared Sub ParseCodeSection(proj As B4jProject, lines As String())
            Dim inAttributes = False

            For Each line In lines
                Dim trimmed = line.TrimStart()

                If trimmed.StartsWith("#Region") AndAlso line.Contains("Project Attributes") Then
                    inAttributes = True
                    Continue For
                End If
                If inAttributes AndAlso trimmed.StartsWith("#End Region") Then
                    inAttributes = False
                    Continue For
                End If

                If inAttributes Then
                    Dim m = Regex.Match(line, "#ApplicationLabel:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.AppLabel = m.Groups(1).Value.Trim() : Continue For
                    m = Regex.Match(line, "#VersionCode:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.VersionCode = m.Groups(1).Value.Trim() : Continue For
                    m = Regex.Match(line, "#VersionName:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.VersionName = m.Groups(1).Value.Trim() : Continue For
                    m = Regex.Match(line, "#MainFormWidth:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.MainFormWidth = m.Groups(1).Value.Trim() : Continue For
                    m = Regex.Match(line, "#MainFormHeight:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.MainFormHeight = m.Groups(1).Value.Trim() : Continue For
                End If
            Next
        End Sub
    End Class
End Namespace
