Imports Newtonsoft.Json.Linq

Namespace Utils
    ''' <summary>
    ''' Builds complete, runtime- and Designer-safe view JSON blocks for the common B4J
    ''' view types, and validates a layout for the property mistakes that pass the binary
    ''' round-trip but fail at LoadLayout / in the Abstract Designer.
    '''
    ''' Property encoding matches BalConverter:
    '''   - plain JSON string  -> cached string (name, eventName, text, tag, ...)
    '''   - plain JSON int/bool -> int / bool
    '''   - {ValueType:6,Value:"0xAARRGGBB"} -> color
    '''   - {ValueType:7,Value:n}            -> float
    '''   - nested object without ValueType  -> sub-map (drawable, font, shadow, variant0)
    ''' </summary>
    Public Module ViewTemplates

        ' ── value-block helpers (mirror BalConverter type codes) ─────────────────
        Private Function ColorVal(hex As String) As JObject
            Dim o As New JObject()
            o.Add("ValueType", New JValue(6))
            o.Add("Value", New JValue(hex))
            Return o
        End Function

        Private Function FloatVal(n As Double) As JObject
            Dim o As New JObject()
            o.Add("ValueType", New JValue(7))
            o.Add("Value", New JValue(n))
            Return o
        End Function

        Private Function FontBlock(size As Double, bold As Boolean) As JObject
            Dim o As New JObject()
            o.Add("csType", New JValue("Dbasic.Designer.FontGrid"))
            o.Add("type", New JValue("B4IFontWrapper"))
            o.Add("bold", New JValue(bold))
            o.Add("fontName", New JValue("DEFAULT"))
            o.Add("fontSize", FloatVal(size))
            o.Add("italic", New JValue(False))
            Return o
        End Function

        Private Function ShadowBlock() As JObject
            Dim o As New JObject()
            o.Add("csType", New JValue("Dbasic.Designer.ShadowGrid"))
            o.Add("type", New JValue(""))
            o.Add("offsetX", FloatVal(0))
            o.Add("offsetY", FloatVal(0))
            o.Add("radius", FloatVal(10))
            o.Add("shadowColor", ColorVal("0xFF000000"))
            o.Add("stype", New JValue(0))
            Return o
        End Function

        Private Function ColorDrawableBlock(colorKey As String, hex As String) As JObject
            Dim o As New JObject()
            o.Add("csType", New JValue("Dbasic.Designer.Drawable.ColorDrawable"))
            o.Add("type", New JValue("ColorDrawable"))
            o.Add("color", ColorVal(hex))
            o.Add("colorKey", New JValue(colorKey))
            Return o
        End Function

        Private Function BitmapDrawableBlock(file As String) As JObject
            Dim o As New JObject()
            o.Add("csType", New JValue("Dbasic.Designer.Drawable.BitmapDrawable"))
            o.Add("type", New JValue("BitmapDrawable"))
            o.Add("file", New JValue(file))
            Return o
        End Function

        Private Function RectBlock(left As Integer, top As Integer, w As Integer, h As Integer) As JObject
            Dim o As New JObject()
            o.Add("left", New JValue(left))
            o.Add("top", New JValue(top))
            o.Add("width", New JValue(w))
            o.Add("height", New JValue(h))
            o.Add("hanchor", New JValue(0))
            o.Add("vanchor", New JValue(0))
            Return o
        End Function

        ' ── type maps ────────────────────────────────────────────────────────────
        ''' <summary>Returns the B4J wrapper JavaType for a DesignerType, or Nothing if unsupported.</summary>
        Public Function JavaTypeFor(designerType As String) As String
            Select Case designerType.Trim().ToLowerInvariant()
                Case "label" : Return ".LabelWrapper"
                Case "button" : Return ".ButtonWrapper"
                Case "textfield" : Return ".TextInputControlWrapper$TextFieldWrapper"
                Case "checkbox" : Return ".CheckboxWrapper"
                Case "combobox" : Return ".ComboBoxWrapper"
                Case "scrollpane" : Return ".ScrollPaneWrapper"
                Case "imageview" : Return ".ImageViewWrapper"
                Case "pane" : Return ".PaneWrapper$ConcretePaneWrapper"
                Case Else : Return Nothing
            End Select
        End Function

        Public Function CanonicalDesignerType(designerType As String) As String
            Select Case designerType.Trim().ToLowerInvariant()
                Case "label" : Return "Label"
                Case "button" : Return "Button"
                Case "textfield" : Return "TextField"
                Case "checkbox" : Return "CheckBox"
                Case "combobox" : Return "ComboBox"
                Case "scrollpane" : Return "ScrollPane"
                Case "imageview" : Return "ImageView"
                Case "pane" : Return "Pane"
                Case Else : Return designerType
            End Select
        End Function

        Private Function CsTypeFor(designerType As String) As String
            Select Case designerType.Trim().ToLowerInvariant()
                Case "label" : Return "Dbasic.Designer.MetaLabel"
                Case "button" : Return "Dbasic.Designer.MetaButton"
                Case "textfield" : Return "Dbasic.Designer.MetaTextField"
                Case "checkbox" : Return "Dbasic.Designer.MetaCheckBox"
                Case "combobox" : Return "Dbasic.Designer.MetaComboBox"
                Case "scrollpane" : Return "Dbasic.Designer.MetaScrollPane"
                Case "imageview" : Return "Dbasic.Designer.MetaImageView"
                Case "pane" : Return "Dbasic.Designer.MetaPane"
                Case Else : Return Nothing
            End Select
        End Function

        ' Adds the properties shared by every non-image view.
        Private Sub AddControlBase(v As JObject, jt As String, name As String, parent As String,
                                   left As Integer, top As Integer, width As Integer, height As Integer)
            v.Add("alpha", FloatVal(1))
            v.Add("borderColor", ColorVal("0xFF000000"))
            v.Add("borderWidth", FloatVal(0))
            v.Add("contextMenu", New JValue(""))
            v.Add("cornerRadius", FloatVal(0))
            v.Add("enabled", New JValue(True))
            v.Add("eventName", New JValue(name))
            v.Add("extraCss", New JValue(""))
            v.Add("hanchor", New JValue(0))
            v.Add("height", New JValue(height))
            v.Add("javaType", New JValue(jt))
            v.Add("left", New JValue(left))
            v.Add("name", New JValue(name))
            v.Add("parent", New JValue(parent))
            v.Add("shadow", ShadowBlock())
            v.Add("tag", New JValue(""))
            v.Add("toolTip", New JValue(""))
            v.Add("top", New JValue(top))
            v.Add("vanchor", New JValue(0))
            v.Add("visible", New JValue(True))
            v.Add("width", New JValue(width))
            v.Add("variant0", RectBlock(left, top, width, height))
        End Sub

        ''' <summary>
        ''' Builds a complete view JObject for the given DesignerType with all properties
        ''' its runtime wrapper and the Designer require. Throws on an unsupported type.
        ''' </summary>
        Public Function BuildView(designerType As String, name As String, parent As String,
                                  left As Integer, top As Integer, width As Integer, height As Integer,
                                  text As String) As JObject
            Dim dt = designerType.Trim().ToLowerInvariant()
            Dim cs = CsTypeFor(dt)
            Dim jt = JavaTypeFor(dt)
            If cs Is Nothing OrElse jt Is Nothing Then
                Throw New ArgumentException($"Unsupported view type: {designerType}")
            End If
            If String.IsNullOrEmpty(parent) Then parent = "Main"

            Dim v As New JObject()
            v.Add("csType", New JValue(cs))
            v.Add("type", New JValue(jt))

            ' ImageView is a Node (not a Control): minimal set, BitmapDrawable required.
            If dt = "imageview" Then
                v.Add("alpha", FloatVal(1))
                v.Add("drawable", BitmapDrawableBlock(""))
                v.Add("enabled", New JValue(True))
                v.Add("eventName", New JValue(name))
                v.Add("extraCss", New JValue(""))
                v.Add("hanchor", New JValue(0))
                v.Add("height", New JValue(height))
                v.Add("javaType", New JValue(jt))
                v.Add("left", New JValue(left))
                v.Add("name", New JValue(name))
                v.Add("parent", New JValue(parent))
                v.Add("preserveRatio", New JValue(True))
                v.Add("shadow", ShadowBlock())
                v.Add("tag", New JValue(""))
                v.Add("top", New JValue(top))
                v.Add("vanchor", New JValue(0))
                v.Add("visible", New JValue(True))
                v.Add("width", New JValue(width))
                v.Add("variant0", RectBlock(left, top, width, height))
                Return v
            End If

            AddControlBase(v, jt, name, parent, left, top, width, height)

            Select Case dt
                Case "label"
                    v.Add("alignment", New JValue("CENTER_LEFT"))
                    v.Add("drawable", ColorDrawableBlock("-fx-background-color", "0xFFF0F8FF"))
                    v.Add("font", FontBlock(14, False))
                    v.Add("fontAwesome", New JValue(""))
                    v.Add("materialIcons", New JValue(""))
                    v.Add("text", New JValue(text))
                    v.Add("textColor", ColorVal("0xFF000000"))
                    v.Add("wrapText", New JValue(False))
                Case "button"
                    v.Add("alignment", New JValue("CENTER"))
                    v.Add("drawable", ColorDrawableBlock("-fx-base", "0xFFF0F8FF"))
                    v.Add("font", FontBlock(14, False))
                    v.Add("fontAwesome", New JValue(""))
                    v.Add("materialIcons", New JValue(""))
                    v.Add("text", New JValue(text))
                    v.Add("textColor", ColorVal("0xFF000000"))
                    v.Add("wrapText", New JValue(False))
                Case "checkbox"
                    v.Add("alignment", New JValue("CENTER_LEFT"))
                    v.Add("checked", New JValue(False))
                    v.Add("drawable", ColorDrawableBlock("-fx-base", "0xFFF0F8FF"))
                    v.Add("font", FontBlock(14, False))
                    v.Add("fontAwesome", New JValue(""))
                    v.Add("materialIcons", New JValue(""))
                    v.Add("text", New JValue(text))
                    v.Add("textColor", ColorVal("0xFF000000"))
                    v.Add("wrapText", New JValue(False))
                Case "textfield"
                    v.Add("drawable", ColorDrawableBlock("-fx-base", "0xFFF0F8FF"))
                    v.Add("editable", New JValue(True))
                    v.Add("font", FontBlock(14, False))
                    v.Add("password", New JValue(False))
                    v.Add("prompt", New JValue(""))
                    v.Add("text", New JValue(text))
                Case "combobox"
                    v.Add("drawable", ColorDrawableBlock("-fx-base", "0xFFF0F8FF"))
                    v.Add("editable", New JValue(False))
                    v.Add("font", FontBlock(14, False))
                Case "scrollpane"
                    v.Add("drawable", ColorDrawableBlock("-fx-background-color", "0xFFFFFFFF"))
                    v.Add("hbar", New JValue("AS_NEEDED"))
                    v.Add("vbar", New JValue("AS_NEEDED"))
                    v.Add("pannable", New JValue(False))
                Case "pane"
                    v.Add("drawable", ColorDrawableBlock("-fx-background-color", "0xFFF0F8FF"))
                Case Else
                    Throw New ArgumentException($"Unsupported view type: {designerType}")
            End Select

            Return v
        End Function

        ' ── validation ────────────────────────────────────────────────────────────
        ''' <summary>
        ''' Returns warnings for the mistakes that survive the binary round-trip but break
        ''' at runtime (missing wrapper props) or in the Designer (ImageView drawable type),
        ''' plus ControlsHeaders / :kids consistency. Empty list = no problems found.
        ''' </summary>
        Public Function ValidateLayout(layout As JObject) As List(Of String)
            Dim warnings As New List(Of String)()
            Dim data = TryCast(layout("Data"), JObject)
            If data Is Nothing Then
                warnings.Add("No 'Data' object found.")
                Return warnings
            End If

            Dim kidNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            ValidateNode(data, warnings, kidNames)

            Dim headerNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim lh = TryCast(layout("LayoutHeader"), JObject)
            If lh IsNot Nothing Then
                Dim ch = TryCast(lh("ControlsHeaders"), JArray)
                If ch IsNot Nothing Then
                    For Each c In ch
                        Dim nm = If(c("Name") IsNot Nothing, c("Name").ToString(), Nothing)
                        If nm IsNot Nothing Then headerNames.Add(nm)
                    Next
                End If
            End If

            For Each nm In kidNames
                If Not headerNames.Contains(nm) Then
                    warnings.Add($"View '{nm}' is in the layout tree but missing from ControlsHeaders (the Designer/runtime may not bind it).")
                End If
            Next
            For Each nm In headerNames
                If Not nm.Equals("Main", StringComparison.OrdinalIgnoreCase) AndAlso Not kidNames.Contains(nm) Then
                    warnings.Add($"ControlsHeaders lists '{nm}' but there is no matching view in the layout tree.")
                End If
            Next
            Return warnings
        End Function

        Private Sub ValidateNode(node As JObject, warnings As List(Of String), kidNames As HashSet(Of String))
            Dim kids = TryCast(node(":kids"), JObject)
            If kids Is Nothing Then Return

            ' contiguity: numeric keys must be 0..n-1
            Dim numeric As New List(Of Integer)()
            For Each p In kids.Properties()
                Dim n As Integer
                If Integer.TryParse(p.Name, n) Then numeric.Add(n)
            Next
            numeric.Sort()
            For i = 0 To numeric.Count - 1
                If numeric(i) <> i Then
                    warnings.Add($"Child view indices are not contiguous from 0 (expected '{i}', gap found) — DynamicBuilder iterates 0..n-1 and will drop later views.")
                    Exit For
                End If
            Next

            For Each p In kids.Properties()
                Dim view = TryCast(p.Value, JObject)
                If view Is Nothing Then Continue For
                ValidateView(view, warnings, kidNames)
                ValidateNode(view, warnings, kidNames)
            Next
        End Sub

        Private Sub ValidateView(view As JObject, warnings As List(Of String), kidNames As HashSet(Of String))
            Dim name = If(view("name") IsNot Nothing, view("name").ToString(), Nothing)
            If name IsNot Nothing Then kidNames.Add(name)
            Dim cs = If(view("csType") IsNot Nothing, view("csType").ToString(), "")
            Dim label = If(name, "(unnamed)")

            Select Case cs
                Case "Dbasic.Designer.MetaImageView"
                    Dim dr = TryCast(view("drawable"), JObject)
                    Dim drType = If(dr IsNot Nothing AndAlso dr("type") IsNot Nothing, dr("type").ToString(), Nothing)
                    If drType IsNot Nothing AndAlso Not drType.Equals("BitmapDrawable", StringComparison.OrdinalIgnoreCase) Then
                        warnings.Add($"ImageView '{label}': drawable is '{drType}' but the Abstract Designer requires a BitmapDrawable (it will fail to open the layout with 'Unable to cast ColorDrawable to BitmapDrawable'). Use {{csType:'Dbasic.Designer.Drawable.BitmapDrawable', type:'BitmapDrawable', file:''}}.")
                    End If
                Case "Dbasic.Designer.MetaComboBox"
                    If view("editable") Is Nothing Then
                        warnings.Add($"ComboBox '{label}': missing 'editable' (bool) — LoadLayout will throw NullPointerException in ComboBoxWrapper.build.")
                    End If
                Case "Dbasic.Designer.MetaScrollPane"
                    For Each k In {"hbar", "vbar", "pannable"}
                        If view(k) Is Nothing Then
                            warnings.Add($"ScrollPane '{label}': missing '{k}' — LoadLayout will throw in ScrollPaneWrapper.build (hbar/vbar = AS_NEEDED|NEVER|ALWAYS, pannable = bool).")
                        End If
                    Next
            End Select

            ' Base ControlWrapper.build reads contextMenu / toolTip / eventName.
            If IsControlCsType(cs) Then
                For Each k In {"contextMenu", "toolTip", "eventName"}
                    If view(k) Is Nothing Then
                        warnings.Add($"{ShortName(cs)} '{label}': missing base property '{k}' — LoadLayout may throw in ControlWrapper.build.")
                    End If
                Next
            End If
        End Sub

        Private Function IsControlCsType(cs As String) As Boolean
            Select Case cs
                Case "Dbasic.Designer.MetaLabel", "Dbasic.Designer.MetaButton",
                     "Dbasic.Designer.MetaCheckBox", "Dbasic.Designer.MetaTextField",
                     "Dbasic.Designer.MetaComboBox", "Dbasic.Designer.MetaScrollPane"
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Function ShortName(cs As String) As String
            Const prefix = "Dbasic.Designer.Meta"
            If cs.StartsWith(prefix) Then Return cs.Substring(prefix.Length)
            Return cs
        End Function

    End Module
End Namespace
