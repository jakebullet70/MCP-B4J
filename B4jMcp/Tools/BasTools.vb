Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json

Namespace Tools
    <McpServerToolType>
    Public Class BasTools

        Private Const Separator As String = "@EndOfDesignText@"

        Private Shared Function IsB4jSource(path As String) As Boolean
            Return path.EndsWith(".bas", StringComparison.OrdinalIgnoreCase) OrElse
                   path.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase)
        End Function

        <McpServerTool, Description("Reads a B4J source file (.bas or .b4j) and returns its content with line numbers. Use offset/limit to read only a slice of a large module (pair with b4j_outline to jump straight to a Sub).")>
        Public Shared Function B4jReadBas(
            <Description("Full path to the .bas or .b4j file")> basPath As String,
            <Description("1-based line number to start reading from. 0 (default) = start of file.")> Optional offset As Integer = 0,
            <Description("Maximum number of lines to return. 0 (default) = to end of file.")> Optional limit As Integer = 0
        ) As String
            If Not File.Exists(basPath) Then Return $"Error: File not found: {basPath}"
            If Not IsB4jSource(basPath) Then
                Return "Error: File must have .bas or .b4j extension"
            End If
            Try
                Dim lines = File.ReadAllLines(basPath)
                Dim startIdx = If(offset > 0, offset - 1, 0)
                If startIdx >= lines.Length Then
                    Return $"(file has {lines.Length} lines; offset {offset} is past the end)"
                End If
                Dim endIdx = If(limit > 0, Math.Min(lines.Length, startIdx + limit), lines.Length)

                Dim sb As New Text.StringBuilder()
                If startIdx > 0 OrElse endIdx < lines.Length Then
                    sb.AppendLine($"# showing lines {startIdx + 1}-{endIdx} of {lines.Length}")
                End If
                For i = startIdx To endIdx - 1
                    sb.AppendLine($"{i + 1,5}| {lines(i)}")
                Next
                Return sb.ToString()
            Catch ex As Exception
                Return $"Error reading file: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Performs a search-and-replace edit on a B4J source file (.bas or .b4j). Creates .bak backup first. The old_text must match exactly (including indentation).")>
        Public Shared Function B4jEditBas(
            <Description("Full path to the .bas or .b4j file")> basPath As String,
            <Description("Exact text to find (must match including whitespace/indentation)")> old_text As String,
            <Description("Replacement text")> new_text As String,
            <Description("If true, replace ALL occurrences; if false (default), the old_text must be unique")> Optional replace_all As Boolean = False
        ) As String
            If Not File.Exists(basPath) Then Return $"Error: File not found: {basPath}"
            If Not IsB4jSource(basPath) Then
                Return "Error: File must have .bas or .b4j extension"
            End If
            If String.IsNullOrEmpty(old_text) Then Return "Error: old_text cannot be empty"
            If old_text = new_text Then Return "Error: old_text and new_text are identical"

            Try
                Dim content = File.ReadAllText(basPath)

                ' Normalise line endings for matching
                Dim normContent = content.Replace(vbCrLf, vbLf)
                Dim normOld = old_text.Replace(vbCrLf, vbLf)
                Dim normNew = new_text.Replace(vbCrLf, vbLf)

                Dim count = CountOccurrences(normContent, normOld)

                If count = 0 Then
                    Return "Error: old_text not found in file. Make sure whitespace and indentation match exactly."
                End If

                If Not replace_all AndAlso count > 1 Then
                    Return $"Error: old_text found {count} times. Provide more context to make it unique, or set replace_all=true."
                End If

                ' Create backup
                File.Copy(basPath, basPath & ".bak", overwrite:=True)

                ' Apply replacement (preserve original line endings)
                Dim result As String
                If replace_all Then
                    result = normContent.Replace(normOld, normNew)
                Else
                    Dim idx = normContent.IndexOf(normOld, StringComparison.Ordinal)
                    result = normContent.Substring(0, idx) & normNew & normContent.Substring(idx + normOld.Length)
                End If

                ' Restore CRLF if original used it
                If content.Contains(vbCrLf) Then
                    result = result.Replace(vbLf, vbCrLf)
                End If

                File.WriteAllText(basPath, result)

                Dim replacements = If(replace_all, count, 1)
                Return $"OK: {replacements} replacement(s) made. Backup saved as {basPath}.bak"
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Applies multiple search-and-replace edits to a B4J source file (.bas or .b4j) atomically — all edits must apply or none are written. Edits run in order; one .bak backup is created. editsJson is a JSON array of {old_text, new_text, replace_all?}.")>
        Public Shared Function B4jMultiEditBas(
            <Description("Full path to the .bas or .b4j file")> basPath As String,
            <Description("JSON array of edits, e.g. [{""old_text"":""foo"",""new_text"":""bar"",""replace_all"":false}]")> editsJson As String
        ) As String
            If Not File.Exists(basPath) Then Return $"Error: File not found: {basPath}"
            If Not IsB4jSource(basPath) Then
                Return "Error: File must have .bas or .b4j extension"
            End If

            Dim edits As List(Of EditSpec)
            Try
                edits = JsonConvert.DeserializeObject(Of List(Of EditSpec))(editsJson)
            Catch ex As Exception
                Return $"Error: could not parse editsJson as a JSON array of edits: {ex.Message}"
            End Try
            If edits Is Nothing OrElse edits.Count = 0 Then Return "Error: editsJson contained no edits"

            Try
                Dim content = File.ReadAllText(basPath)
                Dim hadCrLf = content.Contains(vbCrLf)
                Dim work = content.Replace(vbCrLf, vbLf)

                ' Apply all edits in memory first; abort entirely on any failure (atomic).
                Dim applied As New List(Of String)()
                For idx = 0 To edits.Count - 1
                    Dim e = edits(idx)
                    If e Is Nothing OrElse String.IsNullOrEmpty(e.old_text) Then
                        Return $"Error: edit #{idx + 1} has empty old_text (no edits written)."
                    End If
                    Dim oldN = e.old_text.Replace(vbCrLf, vbLf)
                    Dim newN = If(e.new_text, "").Replace(vbCrLf, vbLf)
                    Dim count = CountOccurrences(work, oldN)
                    If count = 0 Then
                        Return $"Error: edit #{idx + 1} old_text not found (no edits written). Ensure whitespace and indentation match exactly."
                    End If
                    If Not e.replace_all AndAlso count > 1 Then
                        Return $"Error: edit #{idx + 1} old_text found {count} times. Add context or set replace_all=true (no edits written)."
                    End If
                    If e.replace_all Then
                        work = work.Replace(oldN, newN)
                        applied.Add($"  #{idx + 1}: {count} replacement(s)")
                    Else
                        Dim p = work.IndexOf(oldN, StringComparison.Ordinal)
                        work = work.Substring(0, p) & newN & work.Substring(p + oldN.Length)
                        applied.Add($"  #{idx + 1}: 1 replacement")
                    End If
                Next

                File.Copy(basPath, basPath & ".bak", overwrite:=True)
                If hadCrLf Then work = work.Replace(vbLf, vbCrLf)
                File.WriteAllText(basPath, work)

                Return $"OK: {edits.Count} edit(s) applied atomically. Backup saved as {basPath}.bak{Environment.NewLine}{String.Join(Environment.NewLine, applied)}"
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Creates a new B4J source module (.bas) in the project folder and registers it in the .b4j project file (updates NumberOfModules and adds Module{N}). moduleType is 'class' (Class_Globals + Initialize) or 'code' (static module with Process_Globals). Backs up the project file first.")>
        Public Shared Function B4jCreateModule(
            <Description("Full path to the .b4j project file")> projectPath As String,
            <Description("New module name — a valid B4J identifier, without extension")> moduleName As String,
            <Description("'class' for a class module (default) or 'code' for a static code module")> Optional moduleType As String = "class"
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4j", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: projectPath must have .b4j extension"
            End If
            If Not Regex.IsMatch(moduleName, "^[A-Za-z_]\w*$") Then
                Return $"Error: invalid module name '{moduleName}'. Use letters, digits and underscore; it must not start with a digit."
            End If

            Dim typeVal As String
            Select Case moduleType.Trim().ToLowerInvariant()
                Case "class", "" : typeVal = "Class"
                Case "code", "static", "staticcode" : typeVal = "StaticCode"
                Case Else : Return $"Error: moduleType must be 'class' or 'code' (got '{moduleType}')"
            End Select

            Dim projectDir = Path.GetDirectoryName(projectPath)
            If String.IsNullOrEmpty(projectDir) Then projectDir = "."
            Dim basPath = Path.Combine(projectDir, moduleName & ".bas")
            If File.Exists(basPath) Then Return $"Error: module file already exists: {basPath}"

            Try
                Dim lines = File.ReadAllLines(projectPath).ToList()
                Dim sep = lines.FindIndex(Function(l) l.Trim() = Separator)
                Dim headerEnd = If(sep >= 0, sep, lines.Count)

                Dim version As String = "10.5"
                Dim numModules As Integer = 0
                Dim numModulesLineIdx As Integer = -1
                Dim lastModuleLineIdx As Integer = -1
                Dim moduleRx As New Regex("^\s*Module(\d+)\s*=\s*(.+?)\s*$", RegexOptions.IgnoreCase)

                For i = 0 To headerEnd - 1
                    Dim ln = lines(i)
                    Dim mm = moduleRx.Match(ln)
                    If mm.Success Then
                        lastModuleLineIdx = i
                        If mm.Groups(2).Value.Trim().Equals(moduleName, StringComparison.OrdinalIgnoreCase) Then
                            Return $"Error: a module named '{moduleName}' is already registered in the project."
                        End If
                    ElseIf ln.TrimStart().StartsWith("NumberOfModules", StringComparison.OrdinalIgnoreCase) AndAlso ln.Contains("="c) Then
                        numModulesLineIdx = i
                        Integer.TryParse(ln.Substring(ln.IndexOf("="c) + 1).Trim(), numModules)
                    ElseIf ln.TrimStart().StartsWith("Version", StringComparison.OrdinalIgnoreCase) AndAlso ln.Contains("="c) Then
                        version = ln.Substring(ln.IndexOf("="c) + 1).Trim()
                    End If
                Next

                Dim newN = numModules + 1

                ' Back up the project file before modifying it.
                File.Copy(projectPath, projectPath & ".bak", overwrite:=True)

                If numModulesLineIdx >= 0 Then
                    lines(numModulesLineIdx) = $"NumberOfModules={newN}"
                Else
                    lines.Insert(headerEnd, $"NumberOfModules={newN}")
                    If lastModuleLineIdx >= headerEnd Then lastModuleLineIdx += 1
                    headerEnd += 1
                End If

                Dim insertAt = If(lastModuleLineIdx >= 0, lastModuleLineIdx + 1, headerEnd)
                lines.Insert(insertAt, $"Module{newN}={moduleName}")
                File.WriteAllLines(projectPath, lines)

                ' Write the new module file.
                Dim nl = Environment.NewLine
                Dim sb As New Text.StringBuilder()
                sb.Append("B4J=true").Append(nl)
                sb.Append("Group=Default Group").Append(nl)
                sb.Append("ModulesStructureVersion=1").Append(nl)
                sb.Append($"Type={typeVal}").Append(nl)
                sb.Append($"Version={version}").Append(nl)
                sb.Append(Separator).Append(nl)
                If typeVal = "Class" Then
                    sb.Append("Sub Class_Globals").Append(nl).Append(vbTab).Append(nl).Append("End Sub").Append(nl).Append(nl)
                    sb.Append("Public Sub Initialize").Append(nl).Append(vbTab).Append(nl).Append("End Sub").Append(nl)
                Else
                    sb.Append("Sub Process_Globals").Append(nl).Append(vbTab).Append(nl).Append("End Sub").Append(nl)
                End If
                File.WriteAllText(basPath, sb.ToString())

                Return $"OK: created {typeVal} module '{moduleName}' at {basPath} and registered it as Module{newN} (NumberOfModules={newN}). Project backup: {projectPath}.bak"
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        Private Shared Function CountOccurrences(source As String, search As String) As Integer
            Dim count = 0
            Dim idx = 0
            Do
                idx = source.IndexOf(search, idx, StringComparison.Ordinal)
                If idx < 0 Then Exit Do
                count += 1
                idx += search.Length
            Loop
            Return count
        End Function

        Private Class EditSpec
            Public Property old_text As String
            Public Property new_text As String
            Public Property replace_all As Boolean
        End Class

    End Class
End Namespace
