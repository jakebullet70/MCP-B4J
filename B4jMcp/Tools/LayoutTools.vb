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

                Dim warnings = ViewTemplates.ValidateLayout(json)
                Dim result = $"OK: backup saved as {layoutPath}.bak"
                If warnings.Count > 0 Then
                    result &= Environment.NewLine &
                              "Warnings (the file was written anyway — these survive the binary round-trip but break at LoadLayout/Designer):" &
                              Environment.NewLine &
                              String.Join(Environment.NewLine, warnings.Select(Function(w) " - " & w))
                End If
                Return result
            Catch ex As Exception
                Return $"Error writing layout: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Adds a fully-formed view to a .bjl layout with all the default properties its runtime wrapper and the Abstract Designer require. Supported types: Label, Button, TextField, CheckBox, ComboBox, ScrollPane, ImageView, Pane. Inserts the view into the parent's child list (keeping indices contiguous), registers it in ControlsHeaders, backs up the file (.bak), and returns any validation warnings. Prefer this over hand-authoring view JSON for b4j_write_layout.")>
        Public Shared Function B4jAddView(
            <Description("Full path to the .bjl layout file")> layoutPath As String,
            <Description("View type: Label, Button, TextField, CheckBox, ComboBox, ScrollPane, ImageView, or Pane")> viewType As String,
            <Description("View name. Also used as the event name, so it must match the page's Class_Globals field and event subs.")> name As String,
            <Description("Left position in dip")> left As Integer,
            <Description("Top position in dip")> top As Integer,
            <Description("Width in dip")> width As Integer,
            <Description("Height in dip")> height As Integer,
            <Description("Optional caption for Label/Button/CheckBox/TextField. Default empty.")> Optional text As String = "",
            <Description("Optional parent view name. Default 'Main' (the root pane).")> Optional parent As String = "Main"
        ) As String
            If Not File.Exists(layoutPath) Then Return $"Error: File not found: {layoutPath}"
            If Not layoutPath.EndsWith(".bjl", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .bjl extension"
            End If
            If String.IsNullOrWhiteSpace(name) Then Return "Error: name cannot be empty"

            Dim javaType = ViewTemplates.JavaTypeFor(viewType)
            If javaType Is Nothing Then
                Return $"Error: unsupported viewType '{viewType}'. Supported: Label, Button, TextField, CheckBox, ComboBox, ScrollPane, ImageView, Pane."
            End If
            If String.IsNullOrWhiteSpace(parent) Then parent = "Main"

            Try
                Dim converter = New BalConverter(stripNullRect:=False)
                Dim dir = Path.GetDirectoryName(layoutPath)
                If String.IsNullOrEmpty(dir) Then dir = "."
                Dim layout = JObject.Parse(converter.ConvertBalToJson(dir, Path.GetFileName(layoutPath)))

                Dim data = TryCast(layout("Data"), JObject)
                If data Is Nothing Then Return "Error: layout has no 'Data' root."

                ' Reject a duplicate name anywhere in the tree.
                If NameExists(data, name) Then Return $"Error: a view named '{name}' already exists in this layout."

                ' Find the parent node (the root pane is named 'Main').
                Dim parentNode As JObject
                If String.Equals(If(data("name") IsNot Nothing, data("name").ToString(), ""), parent, StringComparison.OrdinalIgnoreCase) Then
                    parentNode = data
                Else
                    parentNode = FindByName(data, parent)
                End If
                If parentNode Is Nothing Then Return $"Error: parent view '{parent}' not found."

                Dim kids = TryCast(parentNode(":kids"), JObject)
                If kids Is Nothing Then
                    kids = New JObject()
                    parentNode(":kids") = kids
                End If

                Dim view = ViewTemplates.BuildView(viewType, name, parent, left, top, width, height, text)
                kids(kids.Count.ToString()) = view

                ' Register in ControlsHeaders.
                Dim lh = TryCast(layout("LayoutHeader"), JObject)
                If lh Is Nothing Then Return "Error: layout has no 'LayoutHeader'."
                Dim ch = TryCast(lh("ControlsHeaders"), JArray)
                If ch Is Nothing Then
                    ch = New JArray()
                    lh("ControlsHeaders") = ch
                End If
                Dim header As New JObject()
                header.Add("Name", New JValue(name))
                header.Add("JavaType", New JValue(javaType))
                header.Add("DesignerType", New JValue(ViewTemplates.CanonicalDesignerType(viewType)))
                ch.Add(header)

                ' Backup + write.
                File.Copy(layoutPath, layoutPath & ".bak", overwrite:=True)
                Using stream = File.Create(layoutPath)
                    converter.ConvertJsonToBalInMemory(layout, stream)
                End Using
                CacheManager.Invalidate(layoutPath)

                Dim warnings = ViewTemplates.ValidateLayout(layout)
                Dim result = $"OK: added {ViewTemplates.CanonicalDesignerType(viewType)} '{name}' to {Path.GetFileName(layoutPath)} (backup: .bak)."
                If warnings.Count > 0 Then
                    result &= Environment.NewLine & "Warnings:" & Environment.NewLine &
                              String.Join(Environment.NewLine, warnings.Select(Function(w) " - " & w))
                End If
                Return result
            Catch ex As Exception
                Return $"Error adding view: {ex.Message}"
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

        ' Returns True if any view in the tree (including node itself) is named `name`.
        Private Shared Function NameExists(node As JObject, name As String) As Boolean
            Dim nm = If(node("name") IsNot Nothing, node("name").ToString(), Nothing)
            If nm IsNot Nothing AndAlso String.Equals(nm, name, StringComparison.OrdinalIgnoreCase) Then Return True
            Return FindByName(node, name) IsNot Nothing
        End Function

        ' Finds a descendant view by name (recurses into :kids); Nothing if not found.
        Private Shared Function FindByName(node As JObject, name As String) As JObject
            Dim kids = TryCast(node(":kids"), JObject)
            If kids Is Nothing Then Return Nothing
            For Each p In kids.Properties()
                Dim view = TryCast(p.Value, JObject)
                If view Is Nothing Then Continue For
                Dim nm = If(view("name") IsNot Nothing, view("name").ToString(), Nothing)
                If nm IsNot Nothing AndAlso String.Equals(nm, name, StringComparison.OrdinalIgnoreCase) Then Return view
                Dim nested = FindByName(view, name)
                If nested IsNot Nothing Then Return nested
            Next
            Return Nothing
        End Function

    End Class
End Namespace
