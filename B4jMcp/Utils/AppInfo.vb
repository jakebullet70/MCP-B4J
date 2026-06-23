Imports System.Reflection

Namespace Utils
    ''' <summary>
    ''' Runtime access to the B4jMcp server's own version (stamped at build time
    ''' from Version.props / BuildNumber.props as Major.Minor.Patch.Build).
    ''' </summary>
    Public Module AppInfo
        Public ReadOnly Property Version As String
            Get
                Dim asm = Assembly.GetExecutingAssembly()
                Dim info = asm.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)()
                If info IsNot Nothing AndAlso Not String.IsNullOrEmpty(info.InformationalVersion) Then
                    Return info.InformationalVersion
                End If
                Return asm.GetName().Version?.ToString()
            End Get
        End Property
    End Module
End Namespace
