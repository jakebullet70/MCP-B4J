Imports System.Diagnostics
Imports System.Text

Namespace Utils
    ''' <summary>
    ''' Tracks B4J app processes launched by b4j_run so they can be stopped (b4j_stop)
    ''' and have their output tailed (b4j_tail_log) after the initial startup window.
    ''' </summary>
    Public Class ProcessRegistry
        Private Shared ReadOnly _apps As New Dictionary(Of Integer, RunningApp)
        Private Shared ReadOnly _syncRoot As New Object()

        Public Class RunningApp
            Public Property Pid As Integer
            Public Property Proc As Process
            Public Property ProjectName As String
            Public Property StartedUtc As DateTime
            Public Property HasExited As Boolean
            Public Property ExitCode As Integer?

            Private ReadOnly _output As New StringBuilder()
            Private ReadOnly _outLock As New Object()
            Private _readPos As Integer = 0

            Public Sub Append(text As String)
                SyncLock _outLock
                    _output.AppendLine(text)
                End SyncLock
            End Sub

            ''' <summary>Returns the full captured output so far.</summary>
            Public Function ReadAll() As String
                SyncLock _outLock
                    Return _output.ToString()
                End SyncLock
            End Function

            ''' <summary>Returns only the output appended since the previous ReadNew call.</summary>
            Public Function ReadNew() As String
                SyncLock _outLock
                    Dim full = _output.ToString()
                    If _readPos > full.Length Then _readPos = 0
                    Dim chunk = full.Substring(_readPos)
                    _readPos = full.Length
                    Return chunk
                End SyncLock
            End Function
        End Class

        Public Shared Function Register(proc As Process, projectName As String) As RunningApp
            Dim app As New RunningApp With {
                .Pid = proc.Id,
                .Proc = proc,
                .ProjectName = projectName,
                .StartedUtc = DateTime.UtcNow,
                .HasExited = False
            }
            SyncLock _syncRoot
                _apps(proc.Id) = app
            End SyncLock
            Return app
        End Function

        Public Shared Function TryGet(pid As Integer, ByRef app As RunningApp) As Boolean
            SyncLock _syncRoot
                Return _apps.TryGetValue(pid, app)
            End SyncLock
        End Function

        Public Shared Function ListApps() As List(Of RunningApp)
            SyncLock _syncRoot
                Return _apps.Values.ToList()
            End SyncLock
        End Function

        Public Shared Sub Remove(pid As Integer)
            SyncLock _syncRoot
                _apps.Remove(pid)
            End SyncLock
        End Sub
    End Class
End Namespace
