Imports System.IO
Imports System.Text.RegularExpressions

Namespace Utils
    ''' <summary>A declaration found in a B4J source module.</summary>
    Public Class B4jSymbol
        Public Property Kind As String      ' "Sub", "Type", or "Global"
        Public Property Name As String
        Public Property Signature As String
        Public Property Line As Integer
    End Class

    ''' <summary>
    ''' Lightweight structural parser for B4J source (.bas / .b4j). Extracts Sub declarations,
    ''' Type declarations, and variables declared inside Process_Globals / Class_Globals / Globals.
    ''' B4J has no LSP, so this powers b4j_outline and b4j_find_symbol.
    ''' </summary>
    Public Class B4jSymbolParser
        Private Shared ReadOnly SubRegex As New Regex("^\s*(?:Public\s+|Private\s+)?Sub\s+(\w+)\s*(\([^)]*\))?", RegexOptions.IgnoreCase)
        Private Shared ReadOnly TypeRegex As New Regex("^\s*Type\s+(\w+)\s*(\(.*\))?", RegexOptions.IgnoreCase)
        Private Shared ReadOnly DeclRegex As New Regex("^\s*(?:Dim|Public|Private)\s+(.+?)\s+As\s+", RegexOptions.IgnoreCase)
        Private Shared ReadOnly GlobalsStartRegex As New Regex("^\s*Sub\s+(?:Process_Globals|Class_Globals|Globals)\b", RegexOptions.IgnoreCase)
        Private Shared ReadOnly EndSubRegex As New Regex("^\s*End\s+Sub\b", RegexOptions.IgnoreCase)

        Public Shared Function ParseFile(path As String) As List(Of B4jSymbol)
            Return Parse(File.ReadAllLines(path))
        End Function

        Public Shared Function Parse(lines As String()) As List(Of B4jSymbol)
            Dim symbols As New List(Of B4jSymbol)()
            Dim inGlobals As Boolean = False

            For i = 0 To lines.Length - 1
                Dim line = lines(i)
                Dim lineNo = i + 1

                If GlobalsStartRegex.IsMatch(line) Then
                    inGlobals = True
                ElseIf inGlobals AndAlso EndSubRegex.IsMatch(line) Then
                    inGlobals = False
                End If

                ' Global variable declarations (only inside Process_Globals / Globals)
                If inGlobals Then
                    Dim dm = DeclRegex.Match(line)
                    If dm.Success Then
                        For Each rawName In dm.Groups(1).Value.Split(","c)
                            Dim nm = rawName.Trim()
                            Dim paren = nm.IndexOf("("c)        ' strip array suffix e.g. arr(10)
                            If paren >= 0 Then nm = nm.Substring(0, paren).Trim()
                            If nm.Length > 0 Then
                                symbols.Add(New B4jSymbol With {.Kind = "Global", .Name = nm, .Signature = line.Trim(), .Line = lineNo})
                            End If
                        Next
                        Continue For
                    End If
                End If

                Dim sm = SubRegex.Match(line)
                If sm.Success Then
                    symbols.Add(New B4jSymbol With {.Kind = "Sub", .Name = sm.Groups(1).Value, .Signature = line.Trim(), .Line = lineNo})
                    Continue For
                End If

                Dim tm = TypeRegex.Match(line)
                If tm.Success Then
                    symbols.Add(New B4jSymbol With {.Kind = "Type", .Name = tm.Groups(1).Value, .Signature = line.Trim(), .Line = lineNo})
                End If
            Next

            Return symbols
        End Function
    End Class
End Namespace
