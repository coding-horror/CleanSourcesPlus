Imports System
Imports System.IO
Imports System.Collections.Specialized
Imports System.Text.RegularExpressions
Imports ICSharpCode.SharpZipLib.Zip

''' <summary>
'''
''' This is a console application to clean .net projects by removing the following:
'''
'''   * bin, obj and setup directories 
'''   * all source bindings
'''   * any user settings
'''
''' This is useful when sharing projects with other developers.
'''
''' Code based on Omar Shahine's "Clean Sources"
'''    http://wiki.shahine.com/default.aspx/MyWiki/CleanSources.html
'''
''' Jeff Atwood
''' http://www.codinghorror.com/blog/
''' 
''' </summary>
Module Clean

    Private _HasError As Boolean = False

    Private _DirectoryDeletionRegex As Regex = _
        New Regex(AppSettings.GetString("DirectoryDeletionPattern"), RegexOptions.IgnoreCase)
    Private _FileDeletionRegex As Regex = _
        New Regex(AppSettings.GetString("FileDeletionPattern"), RegexOptions.IgnoreCase)
    Private _FileBindingRegex As Regex = _
        New Regex(AppSettings.GetString("FileBindingPattern"), RegexOptions.IgnoreCase)

    Public Sub Main(ByVal args As String())
        If (args.Length = 0) Then
            Console.WriteLine("No solution folder path was provided. Please provide a folder path as the first parameter to this executable.")
            Return
        End If

        Dim path As String = args(0)
        If Not Directory.Exists(path) Then
            Console.WriteLine("Solution folder path '" & path & " doesn't exist.")
            Return
        End If

        CleanSolutionFolder(path)

        If args.Length > 1 Then
            If args(1).ToLower() = "-zip" Then
                ZipDirectory(path)
            End If
        End If

        If _HasError Then
            Console.WriteLine("Press ENTER to continue...")
            Console.ReadLine()
        End If
    End Sub

    ''' <summary>
    ''' Creates a ZIP file containing the contents of the folder
    ''' </summary>
    Public Sub ZipDirectory(ByVal directoryPath As String)

        If Not directoryPath.EndsWith("\") Then
            directoryPath += "\"
        End If

        Dim ZipFilePath As String = Path.Combine(directoryPath, _
            Path.GetFileName(Path.GetDirectoryName(directoryPath)) + ".zip")
        If File.Exists(ZipFilePath) Then
            DeletePath(ZipFilePath)
        End If

        Dim ParentDirectory As String = Directory.GetParent(directoryPath).ToString + "\"
        Dim s As FileStream = Nothing
        Dim buf() As Byte
        Dim zs As ZipOutputStream = Nothing
        Dim ze As ZipEntry

        Try
            Dim fileList As StringCollection = GenerateFileList(directoryPath)

            zs = New ZipOutputStream(File.Create(ZipFilePath))
            zs.SetLevel(9)

            For Each CurrentPath As String In fileList
                ze = New ZipEntry(CurrentPath.Replace(ParentDirectory, ""))
                zs.PutNextEntry(ze)
                If Not CurrentPath.EndsWith("/") Then
                    s = File.OpenRead(CurrentPath)
                    ReDim buf(Convert.ToInt32(s.Length) - 1)
                    s.Read(buf, 0, buf.Length)
                    zs.Write(buf, 0, buf.Length)
                    s.Close()
                    Console.Write(".")
                End If
            Next
        Catch ex As Exception
            _HasError = True
            DumpException(ex, "create zip file", ZipFilePath)
        Finally
            If Not s Is Nothing Then
                s.Close()
            End If
            If Not zs Is Nothing Then
                zs.Finish()
                zs.Close()
            End If
        End Try
    End Sub

    ''' <summary>
    ''' Builds an string collection containing all the files under a specific path
    ''' </summary>
    Private Function GenerateFileList(ByVal path As String) As StringCollection
        Dim fileList As StringCollection = New StringCollection
        Dim isEmpty As Boolean = True

        For Each file As String In Directory.GetFiles(path)
            fileList.Add(file)
            isEmpty = False
        Next
        If isEmpty Then
            If Directory.GetDirectories(path).Length = 0 Then
                fileList.Add(path + "/")
            End If
        End If

        For Each dir As String In Directory.GetDirectories(path)
            For Each s As String In GenerateFileList(dir)
                fileList.Add(s)
            Next
        Next

        Return fileList
    End Function

    Private Sub CleanSolutionFolder(ByVal solutionFolderPath As String)
        Dim PathCollection As StringCollection = Nothing
        '-- build a collection of paths to delete
        CleanDirectory(solutionFolderPath, PathCollection)
        '-- now delete them
        DeletePaths(PathCollection)
    End Sub

    ''' <summary>
    ''' Recursively builds a collection of paths to be deleted. 
    ''' Also remove source control bindings while we're at it.
    ''' </summary>
    Private Sub CleanDirectory(ByVal path As String, ByRef pathCollection As StringCollection)
        Dim tdi As New DirectoryInfo(path)

        If pathCollection Is Nothing Then pathCollection = New StringCollection

        For Each fi As FileInfo In tdi.GetFiles()
            If _FileDeletionRegex.IsMatch(fi.Name) Then
                pathCollection.Add(fi.FullName)
            End If
            If _FileBindingRegex.IsMatch(fi.Name) Then
                RemoveSourceBindings(fi.FullName)
            End If
        Next

        For Each di As DirectoryInfo In tdi.GetDirectories()
            If _DirectoryDeletionRegex.IsMatch(di.Name) Then
                pathCollection.Add(di.FullName & "\")
            Else
                CleanDirectory(di.FullName, pathCollection)
            End If
        Next
    End Sub

    ''' <summary>
    ''' Returns true if the provided path is a directory
    ''' </summary>
    Private Function IsDirectory(ByVal path As String) As Boolean
        If path.EndsWith("\") Then Return True
        If File.Exists(path) OrElse Directory.Exists(path) Then
            Return ((File.GetAttributes(path) And FileAttributes.Directory) = FileAttributes.Directory)
        Else
            Return False
        End If
    End Function

    ''' <summary>
    ''' Delete the provided collection of files/directories
    ''' </summary>
    Private Sub DeletePaths(ByVal pathCollection As StringCollection)
        For Each path As String In pathCollection
            DeletePath(path)
        Next
    End Sub

    ''' <summary>
    ''' Returns true if a file is marked Read-Only
    ''' </summary>
    Private Function GetFileReadOnly(ByVal filePath As String) As Boolean
        Dim fi As New FileInfo(filePath)
        Return (fi.Attributes And FileAttributes.ReadOnly) = FileAttributes.ReadOnly
    End Function

    ''' <summary>
    ''' Toggles the Read-Only attribute on a file
    ''' </summary>
    Private Sub SetFileReadOnly(ByVal filePath As String, ByVal isReadOnly As Boolean)
        Dim fi As New FileInfo(filePath)
        If Not isReadOnly Then
            fi.Attributes = fi.Attributes And FileAttributes.Normal
        Else
            fi.Attributes = fi.Attributes And FileAttributes.ReadOnly
        End If
    End Sub

    ''' <summary>
    ''' Deletes a file or directory, with exception trapping
    ''' </summary>
    Private Sub DeletePath(ByVal path As String)
        Try
            If IsDirectory(path) Then
                Directory.Delete(path, True)
            Else
                If GetFileReadOnly(path) Then
                    SetFileReadOnly(path, False)
                End If
                File.Delete(path)
            End If
        Catch ex As Exception
            DumpException(ex, "delete file or path", path)
        End Try
    End Sub

    ''' <summary>
    ''' Dumps an exception to the console
    ''' </summary>
    Private Sub DumpException(ByVal ex As Exception, ByVal taskDescription As String, ByVal path As String)
        Console.WriteLine("Failed to " & taskDescription)
        Console.WriteLine("  " & path)
        Console.WriteLine("Exception message:")
        Console.WriteLine(ex.ToString())
        _HasError = True
    End Sub

    ''' <summary>
    ''' Removes source control bindings, if present, from the text file. 
    ''' This is hard-coded to work only against VS.NET solution files.
    ''' </summary>
    Private Sub RemoveSourceBindings(ByVal path As String)
        Dim oldFileContents As String = ReadFile(path)
        Dim newFileContents As String = oldFileContents

        '-- remove any GlobalSection(SourceCodeControl) block
        newFileContents = Regex.Replace(newFileContents, "\s+GlobalSection\(SourceCodeControl\)[\w|\W]+?EndGlobalSection", "")
        '-- remove any remaining lines that have keys beginning with 'Scc'
        newFileContents = Regex.Replace(newFileContents, "^\s+Scc.*[\n\r\f]", "", RegexOptions.Multiline)

        If newFileContents <> oldFileContents Then
            WriteFile(path, newFileContents)
        End If
    End Sub

    ''' <summary>
    ''' Reads a text file from disk
    ''' </summary>
    Private Function ReadFile(ByVal path As String) As String
        Dim reader As StreamReader = Nothing
        Dim content As String = ""
        Try
            reader = File.OpenText(path)
            content = reader.ReadToEnd
        Catch ex As Exception
            DumpException(ex, "read contents of file", path)
        Finally
            If Not reader Is Nothing Then reader.Close()
        End Try
        Return content
    End Function

    ''' <summary>
    ''' Writes a text file to disk
    ''' </summary>
    Private Sub WriteFile(ByVal path As String, ByVal fileContents As String)
        SetFileReadOnly(path, False)
        Dim writer As StreamWriter = Nothing
        Try
            writer = New StreamWriter(path, False)
            writer.Write(fileContents)
        Catch ex As Exception
            DumpException(ex, "write contents to file", path)
        Finally
            If Not writer Is Nothing Then writer.Close()
        End Try
    End Sub

End Module