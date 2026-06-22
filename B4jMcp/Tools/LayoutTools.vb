Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports B4jMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class LayoutTools

        <McpServerTool, Description("Reads a B4J layout file (.bjl) and returns its content as JSON. The JSON has LayoutHeader (Version, GridSize, ControlsHeaders, Files, DesignerScript), Variants, and Data (the view tree).")>
        Public Shared Function B4jReadLayout(
            <Description("Full path to the .bjl layout file")> layoutPath As String
        ) As String
            If Not File.Exists(layoutPath) Then Return $"Error: File not found: {layoutPath}"
            If Not layoutPath.EndsWith(".bjl", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .bjl extension"
            End If
            Try
                Dim cached As String = Nothing
                If CacheManager.TryGetByMtime(Of String)(layoutPath, cached) Then Return cached

                Dim converter = New BalConverter(stripNullRect:=False)
                Dim dir = Path.GetDirectoryName(layoutPath)
                If String.IsNullOrEmpty(dir) Then dir = "."
                Dim json = converter.ConvertBalToJson(dir, Path.GetFileName(layoutPath))
                CacheManager.SetByMtime(layoutPath, json)
                Return json
            Catch ex As Exception
                Return $"Error reading layout: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Writes JSON data to a B4J layout file (.bjl). Creates a .bak backup first. The JSON must contain LayoutHeader, Variants, and Data (as returned by b4j_read_layout).")>
        Public Shared Function B4jWriteLayout(
            <Description("Full path to the .bjl layout file to write")> layoutPath As String,
            <Description("JSON layout data (as returned by b4j_read_layout)")> jsonData As String
        ) As String
            If Not layoutPath.EndsWith(".bjl", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .bjl extension"
            End If
            Try
                Dim json As JObject
                Try
                    json = JObject.Parse(jsonData)
                Catch ex As JsonException
                    Return $"Error: Invalid JSON — {ex.Message}"
                End Try

                ' Validate required structure
                If json("LayoutHeader") Is Nothing Then Return "Error: Missing 'LayoutHeader' in JSON"
                If json("Variants") Is Nothing Then Return "Error: Missing 'Variants' in JSON"
                If json("Data") Is Nothing Then Return "Error: Missing 'Data' in JSON"

                ' Backup
                If File.Exists(layoutPath) Then
                    File.Copy(layoutPath, layoutPath & ".bak", overwrite:=True)
                End If

                Dim converter = New BalConverter(stripNullRect:=False)
                Using stream = File.Create(layoutPath)
                    converter.ConvertJsonToBalInMemory(json, stream)
                End Using
                CacheManager.Invalidate(layoutPath)
                Return $"OK: backup saved as {layoutPath}.bak"
            Catch ex As Exception
                Return $"Error writing layout: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Clones an existing .bjl layout to a new file in the same folder, round-tripping through the converter to validate the result. Note: this creates the file only — register it in the project's Files via the B4J IDE to use it.")>
        Public Shared Function B4jCloneLayout(
            <Description("Full path to the source .bjl layout file")> sourcePath As String,
            <Description("Name for the new layout (a bare file name; '.bjl' is added if missing)")> newName As String
        ) As String
            If Not File.Exists(sourcePath) Then Return $"Error: File not found: {sourcePath}"
            If Not sourcePath.EndsWith(".bjl", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: source must have .bjl extension"
            End If
            If String.IsNullOrWhiteSpace(newName) Then Return "Error: newName cannot be empty"

            Try
                Dim dir = Path.GetDirectoryName(sourcePath)
                If String.IsNullOrEmpty(dir) Then dir = "."

                ' Strip any path components from newName and ensure the .bjl extension.
                Dim destFileName = Path.GetFileName(newName.Trim())
                If Not destFileName.EndsWith(".bjl", StringComparison.OrdinalIgnoreCase) Then destFileName &= ".bjl"
                Dim destPath = Path.Combine(dir, destFileName)
                If File.Exists(destPath) Then Return $"Error: a layout named '{destFileName}' already exists in {dir}"

                ' Round-trip through the converter so we only write a validated layout.
                Dim converter = New BalConverter(stripNullRect:=False)
                Dim json = converter.ConvertBalToJson(dir, Path.GetFileName(sourcePath))
                Dim jobj = JObject.Parse(json)
                Using stream = File.Create(destPath)
                    converter.ConvertJsonToBalInMemory(jobj, stream)
                End Using

                Return $"OK: cloned {Path.GetFileName(sourcePath)} -> {destFileName} in {dir}. Add it to the project's Files in the B4J IDE to use it."
            Catch ex As Exception
                Return $"Error cloning layout: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Lists all .bjl layout files in a B4J project directory")>
        Public Shared Function B4jListLayouts(
            <Description("Path to the B4J project directory (or .b4j file path)")> projectDir As String
        ) As String
            If projectDir.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase) Then
                projectDir = If(Path.GetDirectoryName(projectDir), ".")
            End If
            If Not Directory.Exists(projectDir) Then Return $"Error: Directory not found: {projectDir}"
            Try
                Dim layouts = Directory.GetFiles(projectDir, "*.bjl", SearchOption.AllDirectories) _
                    .OrderBy(Function(f) f) _
                    .Select(Function(f) New With {
                        .name = Path.GetFileName(f),
                        .path = f,
                        .sizeKb = Math.Round(New FileInfo(f).Length / 1024.0, 1)
                    }).ToList()
                Return JsonConvert.SerializeObject(New With {
                    .count = layouts.Count,
                    .layouts = layouts
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

    End Class
End Namespace
