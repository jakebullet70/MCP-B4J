Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json
Imports B4jMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class NavTools

        <McpServerTool, Description("Outlines a B4J source module: lists every Sub (with signature), Type, and Process_Globals/Globals variable, each with its line number. Use this to understand a module without reading the whole file.")>
        Public Shared Function B4jOutline(
            <Description("Full path to a .bas or .b4j file")> path As String
        ) As String
            If Not File.Exists(path) Then Return $"Error: File not found: {path}"
            If Not (path.EndsWith(".bas", StringComparison.OrdinalIgnoreCase) OrElse path.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase)) Then
                Return "Error: File must have .bas or .b4j extension"
            End If
            Try
                Dim syms = B4jSymbolParser.ParseFile(path)
                Return JsonConvert.SerializeObject(New With {
                    .file = path,
                    .count = syms.Count,
                    .symbols = syms.Select(Function(s) New With {.kind = s.Kind, .name = s.Name, .signature = s.Signature, .line = s.Line})
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Finds a symbol (Sub, Type, or global variable) across a B4J project. Returns definitions and references. Matching is case-insensitive because B4J itself is. Scans the .b4j main module plus all .bas files in the project folder.")>
        Public Shared Function B4jFindSymbol(
            <Description("Full path to the .b4j project file")> projectPath As String,
            <Description("Symbol name to find (case-insensitive)")> name As String,
            <Description("If true, return only definitions (no references). Default false.")> Optional definitionsOnly As Boolean = False
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If String.IsNullOrWhiteSpace(name) Then Return "Error: name cannot be empty"
            Try
                Dim projectDir = Path.GetDirectoryName(projectPath)
                If String.IsNullOrEmpty(projectDir) Then projectDir = "."

                Dim files As New List(Of String) From {projectPath}
                files.AddRange(Directory.GetFiles(projectDir, "*.bas"))

                Dim wordRx As New Regex("\b" & Regex.Escape(name) & "\b", RegexOptions.IgnoreCase)
                Dim defs As New List(Of Object)()
                Dim refs As New List(Of Object)()

                For Each f In files
                    Dim moduleName = Path.GetFileNameWithoutExtension(f)

                    For Each sym In B4jSymbolParser.ParseFile(f)
                        If sym.Name.Equals(name, StringComparison.OrdinalIgnoreCase) Then
                            defs.Add(New With {.module = moduleName, .kind = sym.Kind, .signature = sym.Signature, .line = sym.Line, .file = f})
                        End If
                    Next

                    If Not definitionsOnly Then
                        Dim lines = File.ReadAllLines(f)
                        For i = 0 To lines.Length - 1
                            If wordRx.IsMatch(lines(i)) Then
                                refs.Add(New With {.module = moduleName, .line = i + 1, .text = lines(i).Trim()})
                            End If
                        Next
                    End If
                Next

                Return JsonConvert.SerializeObject(New With {
                    .name = name,
                    .definitionCount = defs.Count,
                    .definitions = defs,
                    .referenceCount = refs.Count,
                    .references = If(definitionsOnly, Nothing, CObj(refs))
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

    End Class
End Namespace
