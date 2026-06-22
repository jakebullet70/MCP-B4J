Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports System.IO.Compression
Imports System.Text.RegularExpressions
Imports System.Xml.Linq
Imports Newtonsoft.Json
Imports B4jMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class LibraryTools

        <McpServerTool, Description("Lists all available B4J libraries: compiled .jar+.xml pairs and .b4xlib source libraries, from the B4J installation and additional libraries path")>
        Public Shared Function B4jListLibraries(
            <Description("Include built-in libraries from b4jPath. Default true.")> Optional includeBuiltIn As Boolean = True
        ) As String
            Try
                Dim cfg = AppConfig.Load()
                Dim cacheKey = $"libs:list:{includeBuiltIn}:{cfg.B4jPath}:{cfg.AdditionalLibrariesPath}"
                Dim cached As String = Nothing
                If CacheManager.TryGetByTtl(Of String)(cacheKey, cached) Then Return cached

                Dim dirs = LibraryDirs(cfg, includeBuiltIn)
                If dirs.Count = 0 Then
                    Return "Error: No library directories configured. Set b4jPath and/or additionalLibrariesPath."
                End If

                Dim libs As New List(Of Object)()
                For Each searchDir In dirs
                    ' Compiled libraries: .jar + .xml pairs
                    For Each xmlFile In Directory.GetFiles(searchDir, "*.xml")
                        Dim jarFile = Path.ChangeExtension(xmlFile, ".jar")
                        If Not File.Exists(jarFile) Then Continue For
                        Dim libName = Path.GetFileNameWithoutExtension(xmlFile)
                        Dim libVersion = "?"
                        Try
                            Dim doc = XDocument.Load(xmlFile)
                            Dim nameEl = doc.Root.Element("name")
                            Dim versionEl = doc.Root.Element("version")
                            If nameEl IsNot Nothing Then libName = nameEl.Value
                            If versionEl IsNot Nothing Then libVersion = versionEl.Value
                        Catch
                        End Try
                        libs.Add(New With {.name = libName, .version = libVersion, .type = "jar", .source = searchDir})
                    Next

                    ' Source libraries: .b4xlib (zip with manifest.txt)
                    For Each libFile In Directory.GetFiles(searchDir, "*.b4xlib")
                        Dim manifest = ReadB4xlibManifest(libFile)
                        Dim version As String = Nothing
                        manifest.TryGetValue("Version", version)
                        libs.Add(New With {
                            .name = Path.GetFileNameWithoutExtension(libFile),
                            .version = If(String.IsNullOrEmpty(version), "?", version),
                            .type = "b4xlib",
                            .source = searchDir
                        })
                    Next
                Next

                Dim result = JsonConvert.SerializeObject(New With {
                    .count = libs.Count,
                    .libraries = libs.OrderBy(Function(l) DirectCast(l, Object).GetType().GetProperty("name").GetValue(l))
                }, Formatting.Indented)
                CacheManager.SetByTtl(cacheKey, result, 60)
                Return result
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns the documented methods, properties, and events of a B4J library in compact format. For .jar libraries this uses the XML docs; for .b4xlib source libraries it lists each module's Sub signatures.")>
        Public Shared Function B4jGetLibraryDocs(
            <Description("Library name (e.g. 'jSQL', 'jFX', 'b4xpages')")> libraryName As String
        ) As String
            Try
                Dim xmlPath = FindLibraryXml(libraryName)
                If xmlPath IsNot Nothing Then
                    Dim cached As String = Nothing
                    If CacheManager.TryGetByMtime(Of String)(xmlPath, cached) Then Return cached
                    Dim rendered = RenderXmlDocs(xmlPath, libraryName)
                    CacheManager.SetByMtime(xmlPath, rendered)
                    Return rendered
                End If

                Dim libPath = FindB4xlib(libraryName)
                If libPath IsNot Nothing Then
                    Dim cached As String = Nothing
                    If CacheManager.TryGetByMtime(Of String)(libPath, cached) Then Return cached
                    Dim rendered = RenderB4xlibDocs(libPath, libraryName)
                    CacheManager.SetByMtime(libPath, rendered)
                    Return rendered
                End If

                Return $"Error: No .xml or .b4xlib found for library '{libraryName}'"
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Searches compiled library (.jar+.xml) documentation for methods, properties, or events matching a query. Note: .b4xlib source libraries are not indexed here — use b4j_get_library_docs for those.")>
        Public Shared Function B4jSearchLibrary(
            <Description("Search query (method name, keyword, or description text)")> query As String,
            <Description("Optional: limit search to a specific library name")> Optional libraryName As String = ""
        ) As String
            Try
                Dim cfg = AppConfig.Load()
                Dim dirs = LibraryDirs(cfg, includeBuiltIn:=True)
                If dirs.Count = 0 Then Return "Error: No library directories configured."

                Dim matches As New List(Of Object)()
                Dim queryLower = query.ToLowerInvariant()

                For Each searchDir In dirs
                    For Each xmlFile In Directory.GetFiles(searchDir, "*.xml")
                        Dim libBaseName = Path.GetFileNameWithoutExtension(xmlFile)
                        If Not String.IsNullOrEmpty(libraryName) AndAlso
                           Not libBaseName.Equals(libraryName, StringComparison.OrdinalIgnoreCase) Then
                            Continue For
                        End If
                        If Not File.Exists(Path.ChangeExtension(xmlFile, ".jar")) Then Continue For
                        Try
                            Dim doc = XDocument.Load(xmlFile)
                            Dim nameEl = doc.Root.Element("name")
                            Dim libNameVal = If(nameEl IsNot Nothing, nameEl.Value, libBaseName)
                            For Each cls In doc.Root.Elements("class")
                                Dim typeNameAttr = cls.Attribute("typeName")
                                Dim typeName = If(typeNameAttr IsNot Nothing, typeNameAttr.Value, "")
                                Dim allElems = cls.Elements("method").Concat(cls.Elements("property")).Concat(cls.Elements("event"))
                                For Each elem In allElems
                                    Dim mNameAttr = elem.Attribute("name")
                                    Dim mName = If(mNameAttr IsNot Nothing, mNameAttr.Value, "")
                                    Dim commentEl = elem.Element("comment")
                                    Dim comment = If(commentEl IsNot Nothing, commentEl.Value, "")
                                    If mName.ToLowerInvariant().Contains(queryLower) OrElse
                                       comment.ToLowerInvariant().Contains(queryLower) OrElse
                                       typeName.ToLowerInvariant().Contains(queryLower) Then
                                        matches.Add(New With {
                                            .library = libNameVal,
                                            .typeName = typeName,
                                            .kind = elem.Name.LocalName,
                                            .name = mName,
                                            .description = TruncateComment(comment.Trim())
                                        })
                                    End If
                                Next
                            Next
                        Catch
                            ' Skip malformed XMLs
                        End Try
                    Next
                Next

                Return JsonConvert.SerializeObject(New With {
                    .query = query,
                    .count = matches.Count,
                    .results = matches.Take(50)
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        ' ── Helpers ──────────────────────────────────────────────────────────────

        Private Shared Function LibraryDirs(cfg As Models.McpConfig, includeBuiltIn As Boolean) As List(Of String)
            Dim dirs As New List(Of String)()
            If Not String.IsNullOrEmpty(cfg.AdditionalLibrariesPath) AndAlso
               Directory.Exists(cfg.AdditionalLibrariesPath) Then
                dirs.Add(cfg.AdditionalLibrariesPath)
            End If
            If includeBuiltIn AndAlso Not String.IsNullOrEmpty(cfg.B4jPath) Then
                Dim libDir = Path.Combine(cfg.B4jPath, "Libraries")
                If Directory.Exists(libDir) Then dirs.Add(libDir)
            End If
            Return dirs
        End Function

        Private Shared Function RenderXmlDocs(xmlPath As String, libraryName As String) As String
            Dim doc = XDocument.Load(xmlPath)
            Dim sb As New System.Text.StringBuilder()

            Dim rootNameEl = doc.Root.Element("name")
            Dim rootVersionEl = doc.Root.Element("version")
            Dim rootName = If(rootNameEl IsNot Nothing, rootNameEl.Value, libraryName)
            Dim rootVersion = If(rootVersionEl IsNot Nothing, rootVersionEl.Value, "?")
            sb.AppendLine($"Library: {rootName} v{rootVersion} (jar)")
            sb.AppendLine()

            For Each cls In doc.Root.Elements("class")
                Dim typeNameAttr = cls.Attribute("typeName")
                Dim typeName = If(typeNameAttr IsNot Nothing, typeNameAttr.Value, "?")
                sb.AppendLine($"[{typeName}]")

                For Each m In cls.Elements("method")
                    Dim mNameAttr = m.Attribute("name")
                    Dim mName = If(mNameAttr IsNot Nothing, mNameAttr.Value, "?")
                    Dim params = String.Join(", ", m.Elements("parameter").Select(
                        Function(p)
                            Dim pName = If(p.Attribute("name") IsNot Nothing, p.Attribute("name").Value, "")
                            Dim pType = ShortType(If(p.Attribute("type") IsNot Nothing, p.Attribute("type").Value, ""))
                            Return $"{pName}: {pType}"
                        End Function))
                    Dim retTypeAttr = m.Attribute("returnType")
                    Dim retType = ShortType(If(retTypeAttr IsNot Nothing, retTypeAttr.Value, ""))
                    Dim commentEl = m.Element("comment")
                    Dim comment = If(commentEl IsNot Nothing, commentEl.Value.Trim(), "")
                    Dim line = $"  .{mName}({params})"
                    If Not String.IsNullOrEmpty(retType) AndAlso retType <> "void" Then line &= $" → {retType}"
                    If Not String.IsNullOrEmpty(comment) Then line &= $" — {TruncateComment(comment)}"
                    sb.AppendLine(line)
                Next

                For Each p In cls.Elements("property")
                    Dim pNameAttr = p.Attribute("name")
                    Dim pName = If(pNameAttr IsNot Nothing, pNameAttr.Value, "?")
                    Dim pTypeAttr = p.Attribute("type")
                    Dim pType = ShortType(If(pTypeAttr IsNot Nothing, pTypeAttr.Value, ""))
                    Dim commentEl = p.Element("comment")
                    Dim comment = If(commentEl IsNot Nothing, commentEl.Value.Trim(), "")
                    Dim line = $"  .{pName}: {pType}"
                    If Not String.IsNullOrEmpty(comment) Then line &= $" — {TruncateComment(comment)}"
                    sb.AppendLine(line)
                Next

                For Each ev In cls.Elements("event")
                    Dim evNameAttr = ev.Attribute("name")
                    Dim evName = If(evNameAttr IsNot Nothing, evNameAttr.Value, "?")
                    Dim params = String.Join(", ", ev.Elements("parameter").Select(
                        Function(p)
                            Dim pName = If(p.Attribute("name") IsNot Nothing, p.Attribute("name").Value, "")
                            Dim pType = ShortType(If(p.Attribute("type") IsNot Nothing, p.Attribute("type").Value, ""))
                            Return $"{pName}: {pType}"
                        End Function))
                    Dim commentEl = ev.Element("comment")
                    Dim comment = If(commentEl IsNot Nothing, commentEl.Value.Trim(), "")
                    Dim line = $"  [event] {evName}({params})"
                    If Not String.IsNullOrEmpty(comment) Then line &= $" — {TruncateComment(comment)}"
                    sb.AppendLine(line)
                Next
                sb.AppendLine()
            Next

            Return sb.ToString()
        End Function

        Private Shared Function RenderB4xlibDocs(libPath As String, libraryName As String) As String
            Dim sb As New System.Text.StringBuilder()
            Dim manifest = ReadB4xlibManifest(libPath)
            Dim version As String = Nothing
            manifest.TryGetValue("Version", version)
            sb.AppendLine($"Library: {Path.GetFileNameWithoutExtension(libPath)} v{If(String.IsNullOrEmpty(version), "?", version)} (b4xlib)")
            Dim dependsOn As String = Nothing
            If manifest.TryGetValue("B4J.DependsOn", dependsOn) AndAlso Not String.IsNullOrEmpty(dependsOn) Then
                sb.AppendLine($"Depends on (B4J): {dependsOn}")
            End If
            sb.AppendLine()

            Dim subRegex As New Regex("^\s*(?:Public\s+|Private\s+)?Sub\s+(\w+)\s*(\(.*?\))?", RegexOptions.IgnoreCase)
            Using zip = ZipFile.OpenRead(libPath)
                For Each entry In zip.Entries.Where(Function(e) e.FullName.EndsWith(".bas", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"[Module: {Path.GetFileNameWithoutExtension(entry.Name)}]")
                    Using reader As New StreamReader(entry.Open())
                        Dim line As String
                        Do
                            line = reader.ReadLine()
                            If line Is Nothing Then Exit Do
                            If line.TrimStart().StartsWith("Private Sub", StringComparison.OrdinalIgnoreCase) Then Continue Do
                            Dim m = subRegex.Match(line)
                            If m.Success Then
                                Dim params = If(m.Groups(2).Success, m.Groups(2).Value, "()")
                                sb.AppendLine($"  Sub {m.Groups(1).Value}{params}")
                            End If
                        Loop
                    End Using
                    sb.AppendLine()
                Next
            End Using

            Return sb.ToString()
        End Function

        Private Shared Function ReadB4xlibManifest(libPath As String) As Dictionary(Of String, String)
            Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Try
                Using zip = ZipFile.OpenRead(libPath)
                    Dim entry = zip.GetEntry("manifest.txt")
                    If entry Is Nothing Then Return result
                    Using reader As New StreamReader(entry.Open())
                        Dim line As String
                        Do
                            line = reader.ReadLine()
                            If line Is Nothing Then Exit Do
                            Dim eq = line.IndexOf("="c)
                            If eq > 0 Then result(line.Substring(0, eq).Trim()) = line.Substring(eq + 1).Trim()
                        Loop
                    End Using
                End Using
            Catch
            End Try
            Return result
        End Function

        Private Shared Function FindLibraryXml(name As String) As String
            Return FindLibraryFile(name, ".xml")
        End Function

        Private Shared Function FindB4xlib(name As String) As String
            Return FindLibraryFile(name, ".b4xlib")
        End Function

        Private Shared Function FindLibraryFile(name As String, extension As String) As String
            Dim cfg = AppConfig.Load()
            For Each searchDir In LibraryDirs(cfg, includeBuiltIn:=True)
                Dim candidate = Path.Combine(searchDir, name & extension)
                If File.Exists(candidate) Then Return candidate
                Dim found = Directory.GetFiles(searchDir, "*" & extension).FirstOrDefault(
                    Function(f) Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                If found IsNot Nothing Then Return found
            Next
            Return Nothing
        End Function

        Private Shared Function ShortType(fullType As String) As String
            If String.IsNullOrEmpty(fullType) Then Return ""
            Dim dot = fullType.LastIndexOf("."c)
            Return If(dot >= 0, fullType.Substring(dot + 1), fullType)
        End Function

        Private Shared Function TruncateComment(comment As String) As String
            If String.IsNullOrEmpty(comment) Then Return ""
            Dim firstLine = comment.Split(New Char() {Chr(10), Chr(13)}).FirstOrDefault()
            If firstLine Is Nothing Then Return ""
            firstLine = firstLine.Trim()
            Return If(firstLine.Length > 100, firstLine.Substring(0, 100) & "…", firstLine)
        End Function

    End Class
End Namespace
