Namespace Models
    Public Class B4jProject
        Public Property ProjectFile As String = ""
        Public Property AppType As String = ""          ' JavaFX / Console / Server / UI
        Public Property AppLabel As String = ""
        Public Property VersionCode As String = "1"
        Public Property VersionName As String = "1.0"
        Public Property MainFormWidth As String = ""
        Public Property MainFormHeight As String = ""
        Public Property Libraries As New List(Of String)
        Public Property Modules As New List(Of String)
        Public Property Layouts As New List(Of String)
        Public Property Assets As New List(Of String)
        Public Property BuildConfigs As New Dictionary(Of String, String)
        Public Property AdditionalJars As New List(Of String)       ' #AdditionalJar entries
        Public Property PackagerProperties As New List(Of String)   ' #PackagerProperty entries
        Public Property MergeLibraries As String = ""               ' #MergeLibraries value
    End Class
End Namespace
