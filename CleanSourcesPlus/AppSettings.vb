Imports System.Configuration
Imports System.IO
Imports System.Reflection
Imports System.Text.RegularExpressions
Imports System.Collections.Specialized

''' <summary>
''' Class for returning global settings related to WinForms or Console .NET applications
''' </summary>
Public Class AppSettings

    Private Shared _EntryAssemblyAttribCollection As Specialized.NameValueCollection
    Private Shared _CommandLineArgsCollection As Specialized.NameValueCollection

    Private Const _strKeyNotPresent As String = _
        "The key <{0}> is not present in the <appSettings> section of the .config file"
    Private Const _strKeyError As String = _
        "Error '{0}' retrieving key <{1}> from <appSettings> section of the .config file"
    Private Const _strSectionNotPresent As String = _
        "The <{0}> section is not present in the .config file"
    Private Const _strSectionError As String = _
        "Error '{0}' retrieving section <{1}> from the .config file"

    Private Sub New()
        ' to keep this class from being creatable as an instance.
    End Sub

#Region "  Properties"

    ''' <summary>
    ''' Returns true if a debugger is attached, or if the "debug" command line parameter was provided
    ''' </summary>
    Public Shared ReadOnly Property DebugMode() As Boolean
        Get
            '-- matches debug, /debug, debug=1, -debug:true, etc
            If MatchArgumentPattern("^.{0,1}debug$|^.{0,1}debug(=|:).+") <> "" Then
                Return True
            Else
                Return System.Diagnostics.Debugger.IsAttached
            End If
        End Get
    End Property

    ''' <summary>
    ''' Returns the build date of the application
    ''' </summary>
    ''' <remarks>
    ''' This is based on the build number or filesystem time for the entry assembly; "11/24/2004 5:46:12 PM"
    ''' </remarks>
    Public Shared ReadOnly Property BuildDate() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("BuildDate")
        End Get
    End Property

    ''' <summary>
    ''' returns product name associated with this application
    ''' </summary>
    ''' <remarks>
    ''' AssemblyProduct element in AssemblyInfo file
    ''' </remarks>
    Public Shared ReadOnly Property Product() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("Product")
        End Get
    End Property

    ''' <summary>
    ''' returns company name associated with this application
    ''' </summary>
    ''' <remarks>
    ''' AssemblyCompany element in AssemblyInfo file
    ''' </remarks>
    Public Shared ReadOnly Property Company() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("Company")
        End Get
    End Property

    ''' <summary>
    ''' returns copyright notice associated with this application
    ''' </summary>
    ''' <remarks>
    ''' AssemblyCopyright element in AssemblyInfo file
    ''' </remarks>
    Public Shared ReadOnly Property Copyright() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("Copyright")
        End Get
    End Property

    ''' <summary>
    ''' returns a description of this application
    ''' </summary>
    ''' <remarks>
    ''' AssemblyDescription element in AssemblyInfo file
    ''' </remarks>
    Public Shared ReadOnly Property Description() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("Description")
        End Get
    End Property

    ''' <summary>
    ''' returns title of this application
    ''' </summary>
    ''' <remarks>
    ''' AssemblyTitle element in AssemblyInfo file
    ''' </remarks>
    Public Shared ReadOnly Property Title() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("Title")
        End Get
    End Property

    ''' <summary>
    ''' returns the filename, without path, of the entry assembly for this application
    ''' </summary>
    ''' <remarks>
    ''' derived from CodeBase(); "myapp.exe"
    ''' </remarks>
    Public Shared ReadOnly Property CodeBaseFileName() As String
        Get
            Return Path.GetFileName(CodeBase)
        End Get
    End Property

    ''' <summary>
    ''' returns the complete path and filename of the entry assembly for this application
    ''' </summary>
    ''' <remarks>
    ''' derived from CodeBase:
    '''   "c:/myapp/myapp.exe"
    '''   "http://myapp/myapp.exe"
    ''' </remarks>
    Public Shared ReadOnly Property CodeBase() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("CodeBase")
        End Get
    End Property

    ''' <summary>
    ''' returns the root folder where the entry assembly for this application was run from
    ''' </summary>
    ''' <remarks>
    ''' derived from CodeBase(); "http://myapp/" or "c:\myapp\"
    ''' </remarks>
    Public Shared ReadOnly Property CodeBaseFolder() As String
        Get
            Return CodeBase.Replace(CodeBaseFileName, "")
        End Get
    End Property

    ''' <summary>
    ''' returns the unique Framework name for this application, including private key
    ''' </summary>
    ''' <remarks>
    ''' derived from assembly.FullName; "webapp, Version=1.0.1789.31986, Culture=neutral, PublicKeyToken=null"
    ''' </remarks>
    Public Shared ReadOnly Property FullName() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("FullName")
        End Get
    End Property

    ''' <summary>
    ''' returns internal .NET FriendlyName for this application
    ''' </summary>
    ''' <remarks>
    ''' not really so friendly; "/LM/W3SVC/1/Root/webapp-24-127458354529761533"
    ''' </remarks>
    Public Shared ReadOnly Property FriendlyName() As String
        Get
            Return AppDomain.CurrentDomain.FriendlyName
        End Get
    End Property

    ''' <summary>
    ''' returns the version number of this application
    ''' </summary>
    ''' <remarks>
    ''' calculated using the entry assembly and the AssemblyVersion element; "1.0.1789.31986"
    ''' </remarks>
    Public Shared ReadOnly Property Version() As String
        Get
            CheckEntryAssemblyAttribs()
            Return _EntryAssemblyAttribCollection("Version")
        End Get
    End Property

    ''' <summary>
    ''' returns the base folder that the application is executing from
    ''' </summary>
    ''' <remarks>
    ''' equivalent to CurrentDomain.BaseDirectory property; "c:/inetpub/wwwroot/webapp/"
    ''' </remarks>
    Public Shared ReadOnly Property BaseDirectory() As String
        Get
            Return AppDomain.CurrentDomain.BaseDirectory
        End Get
    End Property

    ''' <summary>
    ''' Returns the version of the .NET runtime currently in use by this application
    ''' </summary>
    ''' <remarks>
    ''' returned in the format "1.1.4322"
    ''' </remarks>
    Public Shared ReadOnly Property RuntimeVersion() As String
        Get
            Return Regex.Match(System.Environment.Version.ToString, "\d+.\d+.\d+").ToString
        End Get
    End Property

    ''' <summary>
    ''' Returns complete path to the currently active .config file
    ''' </summary>
    ''' <remarks>
    ''' sample "c:/inetpub/wwwroot/webapp/web.config"
    ''' </remarks>
    Public Shared ReadOnly Property ConfigPath() As String
        Get
            Return Convert.ToString(System.AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE"))
        End Get
    End Property

    ''' <summary>
    ''' returns any command line arguments passed to this app, converted to a NameValueCollection of key/value pairs
    ''' </summary>
    ''' <remarks>
    ''' Arguments can be passed via URL querystring semantics, or standard space-delimited command line style. 
    ''' If no key/value pairs are found, args are returned with generic key names such as arg0, arg1, etc.
    ''' </remarks>
    Public Shared ReadOnly Property ArgumentsCollection() As Specialized.NameValueCollection
        Get
            If _CommandLineArgsCollection Is Nothing Then
                _CommandLineArgsCollection = CommandLineArgCollection()
            End If
            Return _CommandLineArgsCollection
        End Get
    End Property

    ''' <summary>
    ''' returns any command line arguments passed to this app as a simple, raw StringCollection
    ''' </summary>
    Public Shared ReadOnly Property ArgumentsArray() As StringCollection
        Get
            Dim args As StringCollection = New StringCollection
            args.AddRange(Environment.GetCommandLineArgs())
            Return args
        End Get
    End Property

    ''' <summary>
    ''' returns true if command line help was requested
    ''' </summary>
    Public Shared ReadOnly Property HelpRequested() As Boolean
        Get
            '-- matches /?, ?, /help, help=1, -help:true, etc
            Return MatchArgumentPattern("^.{0,1}help$|^.{0,1}help(=|:).+|^.{0,1}\?") <> ""
        End Get
    End Property

    ''' <summary>
    ''' Returns the current .NET security zone this code is running under
    ''' </summary>
    ''' <remarks>
    ''' MyComputer, Internet, Intranet, NoZone, Trusted, Untrusted
    ''' </remarks>
    Public Shared ReadOnly Property SecurityZone() As String
        Get
            Return System.Security.Policy.Zone.CreateFromUrl(CodeBase).SecurityZone.ToString
        End Get
    End Property

#End Region

#Region "  GetCustomSection"

    ''' <summary>
    ''' returns custom NameValueSectionHandler .config section as an arbitrary object
    ''' </summary>
    ''' <remarks>
    ''' this is usually used in combination with a user-defined IConfigurationSectionHandler
    ''' </remarks>
    Public Shared Function GetCustomSection(ByVal name As String, _
        Optional ByVal MustBePresent As Boolean = True) As Object
        Dim o As Object
        Try
            o = ConfigurationManager.GetSection(name)
        Catch ex As Exception
            Throw New ConfigurationErrorsException(String.Format(_strSectionError, ex.Message, name), ex)
        End Try
        If o Is Nothing Then
            If MustBePresent Then
                Throw New System.Configuration.ConfigurationErrorsException( _
                    String.Format(_strSectionNotPresent, name))
            End If
        End If
        Return o
    End Function

    ''' <summary>
    ''' returns custom NameValueSectionHandler .config section, for xml key/value pairs
    ''' </summary>
    ''' <remarks>
    ''' in .config file define configSection:
    '''   &lt;section name="mySection" type="System.Configuration.NameValueSectionHandler" /&gt;
    ''' the XML should look like:
    '''   &lt;mySection&gt;
    '''     &lt;add key="nvkey" value="nvvalue" /&gt;
    '''   &lt;/mySection&gt;
    ''' </remarks>
    Public Shared Sub GetCustomSection(ByVal name As String, ByRef nvc As Specialized.NameValueCollection, _
        Optional ByVal MustBePresent As Boolean = True)
        Try
            nvc = CType(ConfigurationManager.GetSection(name), Specialized.NameValueCollection)
        Catch ex As Exception
            Throw New ConfigurationErrorsException(String.Format(_strSectionError, ex.Message, name), ex)
        End Try
        If nvc Is Nothing Then
            If MustBePresent Then
                Throw New System.Configuration.ConfigurationErrorsException( _
                    String.Format(_strSectionNotPresent, name))
            Else
                nvc = New Specialized.NameValueCollection
            End If
        End If
    End Sub

    ''' <summary>
    ''' returns custom HybridDictionary .config section, for xml key/value pairs
    ''' </summary>
    ''' <remarks>
    ''' in .config file, define configSection:
    '''   &lt;section name="mySection" type="System.Configuration.DictionarySectionHandler" /&gt;
    ''' the XML should look like:
    '''   &lt;mySection&gt;
    '''     &lt;add key="dictkey" value="dictvalue" /&gt;
    '''   &lt;/mySection&gt;
    ''' </remarks>
    Public Shared Sub GetCustomSection(ByVal name As String, ByRef d As Specialized.HybridDictionary, _
        Optional ByVal MustBePresent As Boolean = True)
        Dim h As Hashtable
        Try
            h = CType(ConfigurationManager.GetSection(name), Hashtable)
        Catch ex As Exception
            Throw New ConfigurationErrorsException(String.Format(_strSectionError, ex.Message, name), ex)
        End Try
        If h Is Nothing Then
            If MustBePresent Then
                Throw New System.Configuration.ConfigurationErrorsException( _
                    String.Format(_strSectionNotPresent, name))
            Else
                d = New Specialized.HybridDictionary
                Return
            End If
        End If

        '-- copy the hashtable into the hybriddictionary
        Dim newd As New Specialized.HybridDictionary
        For Each o As Object In h.Keys
            newd.Add(o, h.Item(o))
        Next
        d = newd
    End Sub

    ''' <summary>
    ''' returns custom HashTable .config section, for single xml tag
    ''' </summary>
    ''' <remarks>
    ''' in .config file, define configSection:
    '''   &lt;section name="mySection" type="System.Configuration.SingleTagSectionHandler" /&gt;
    ''' the XML should look like:
    '''   &lt;mySection tag1="value1" tag2="value2" /&gt;
    ''' </remarks>
    Public Shared Sub GetCustomSection(ByVal name As String, ByRef h As Hashtable, _
        Optional ByVal MustBePresent As Boolean = True)
        Try
            h = CType(ConfigurationManager.GetSection(name), Hashtable)
        Catch ex As Exception
            Throw New ConfigurationErrorsException(String.Format(_strSectionError, ex.Message, name), ex)
        End Try
        If h Is Nothing Then
            If MustBePresent Then
                Throw New System.Configuration.ConfigurationErrorsException( _
                    String.Format(_strSectionNotPresent, name))
            Else
                h = New Hashtable
            End If
        End If
    End Sub

    ''' <summary>
    ''' returns custom XmlDocument .config section, for arbitrary xml
    ''' </summary>
    ''' <remarks>
    ''' in .config file define configSection:
    '''   &lt;section name="mySection" type="MyNamespace.MyConfigurationSectionHandler, MyNamespace" /&gt;
    ''' the XML is arbitrary; 
    ''' you must define your own class that implements IConfigurationSectionHandler and returns an XmlDocument
    ''' </remarks>
    Public Shared Sub GetCustomSection(ByVal name As String, ByRef x As System.Xml.XmlDocument, _
        Optional ByVal MustBePresent As Boolean = True)
        Try
            x = CType(ConfigurationManager.GetSection(name), System.Xml.XmlDocument)
        Catch ex As Exception
            Throw New ConfigurationErrorsException(String.Format(_strSectionError, ex.Message, name), ex)
        End Try
        If x Is Nothing Then
            If MustBePresent Then
                Throw New ConfigurationErrorsException(String.Format(_strSectionNotPresent, name))
            Else
                x = New System.Xml.XmlDocument
            End If
        End If
    End Sub

#End Region

    ''' <summary>
    ''' returns true if this named argument exists anywhere in the command line arguments
    ''' </summary>
    Public Shared Function HasArgument(ByVal ArgumentName As String) As Boolean
        Return MatchArgumentPattern("^.{0,1}" & ArgumentName & "$|^.{0,1}" & ArgumentName & "(=|:).+") <> ""
    End Function

    ''' <summary>
    ''' Returns the first argument that matches the specified regular expression pattern, if any. 
    ''' If no match returns empty string.
    ''' </summary>
    Public Shared Function MatchArgumentPattern(ByVal strRegexPattern As String) As String
        Dim r As New Regex(strRegexPattern, RegexOptions.IgnoreCase)
        For Each strKey As String In ArgumentsArray
            If r.IsMatch(strKey) Then Return strKey
        Next
        Return ""
    End Function

    ''' <summary>
    ''' Returns the entry assembly for this application domain
    ''' </summary>
    Private Shared Function EntryAssembly() As Reflection.Assembly
        If System.Reflection.Assembly.GetEntryAssembly Is Nothing Then
            Return System.Reflection.Assembly.GetCallingAssembly
        Else
            Return System.Reflection.Assembly.GetEntryAssembly
        End If
    End Function

    ''' <summary>
    ''' Ensures we have retrieved the entry assembly attribs at least once
    ''' </summary>
    Private Shared Sub CheckEntryAssemblyAttribs()
        If _EntryAssemblyAttribCollection Is Nothing Then
            _EntryAssemblyAttribCollection = AssemblyAttribCollection(EntryAssembly())
        End If
    End Sub

    ''' <summary>
    ''' returns NameValueCollection of all attributes for the specified assembly
    ''' </summary>
    ''' <remarks>
    ''' note that Assembly* values are pulled from AssemblyInfo file in project folder
    '''
    ''' Product         = AssemblyProduct string
    ''' Copyright       = AssemblyCopyright string
    ''' Company         = AssemblyCompany string
    ''' Description     = AssemblyDescription string
    ''' Title           = AssemblyTitle string
    ''' 
    ''' in addition to all named AssemblyInfo items, adds custom CodeBase, BuildDate, Version, and FullName items.
    ''' </remarks>
    Private Shared Function AssemblyAttribCollection(ByVal a As Reflection.Assembly) As Specialized.NameValueCollection
        Dim attribs() As Object
        Dim attrib As Object
        Dim Name As String
        Dim Value As String
        Dim nvc As New Specialized.NameValueCollection

        attribs = a.GetCustomAttributes(False)
        For Each attrib In attribs
            Name = attrib.GetType().ToString()
            Value = ""
            Select Case Name
                Case "System.Reflection.AssemblyTrademarkAttribute"
                    Name = "Trademark"
                    Value = CType(attrib, AssemblyTrademarkAttribute).Trademark.ToString
                Case "System.Reflection.AssemblyProductAttribute"
                    Name = "Product"
                    Value = CType(attrib, AssemblyProductAttribute).Product.ToString
                Case "System.Reflection.AssemblyCopyrightAttribute"
                    Name = "Copyright"
                    Value = CType(attrib, AssemblyCopyrightAttribute).Copyright.ToString
                Case "System.Reflection.AssemblyCompanyAttribute"
                    Name = "Company"
                    Value = CType(attrib, AssemblyCompanyAttribute).Company.ToString
                Case "System.Reflection.AssemblyTitleAttribute"
                    Name = "Title"
                    Value = CType(attrib, AssemblyTitleAttribute).Title.ToString
                Case "System.Reflection.AssemblyDescriptionAttribute"
                    Name = "Description"
                    Value = CType(attrib, AssemblyDescriptionAttribute).Description.ToString
                Case Else
                    'Console.WriteLine(Name)
            End Select
            If Value <> "" Then
                If nvc.Item(Name) = "" Then
                    nvc.Add(Name, Value)
                End If
            End If
        Next

        '-- add some extra values that are not in the AssemblyInfo, but nice to have
        With nvc
            .Add("CodeBase", a.CodeBase.Replace("file:///", ""))
            .Add("BuildDate", AssemblyBuildDate(a).ToString)
            .Add("Version", a.GetName.Version.ToString)
            .Add("FullName", a.FullName)
        End With

        Return nvc
    End Function

    ''' <summary>
    ''' If an URL was used to launch this app, parses that URL for any 
    ''' QueryString style key value pairs and adds them to a NameValueCollection
    ''' </summary>
    ''' <remarks>
    ''' when you launch an exe via an URL, like this:
    '''   http://localhost/App.Website/App.Loader.exe?a=1&amp;b=2&amp;c=apple
    ''' 
    ''' you get a set of argument params that looks like this:
    '''   args() as String = {
    '''     "C:\WINDOWS\Microsoft.NET\Framework\vx.x.xxxx\IEExec", _
    '''     "http://localhost/App.Website/App.Loader.exe?a=1&amp;b=2&amp;c=apple", _
    '''     "3", "1", "86474707A3C6F63616C686F6374710000000"}
    ''' 
    ''' the resulting nvc for this app would be
    '''   a = 1
    '''   b = 2
    '''   c = apple
    ''' </remarks>
    Private Shared Sub GetURLCommandLineArgs(ByVal strURL As String, _
        ByRef nvc As Specialized.NameValueCollection)

        For Each m As Match In Regex.Matches(strURL, "(?<Key>[^=#&?]+)=(?<Value>[^=#&]*)")
            nvc.Add(m.Groups("Key").ToString, m.Groups("Value").ToString)
        Next
    End Sub

    ''' <summary>
    ''' returns true if this string is a URL
    ''' </summary>
    Private Shared Function IsURL(ByVal strAny As String) As Boolean
        Return strAny.IndexOf("&") > -1 _
            OrElse strAny.StartsWith("?") _
            OrElse strAny.ToLower.StartsWith("http://")
    End Function

    ''' <summary>
    ''' If a command line argument prefix was present in the key, remove it
    ''' </summary>
    Private Shared Function RemoveArgPrefix(ByVal strKey As String) As String
        If strKey.StartsWith("-") Or strKey.StartsWith("/") Then
            Return strKey.Substring(1)
        Else
            Return strKey
        End If
    End Function

    ''' <summary>
    ''' Parses any key=value pairs in the command line args into a NameValueCollection
    ''' </summary>
    ''' <returns>true if any key=value name pairs were found and added, otherwise false</returns>
    ''' <remarks>
    ''' App.Loader.exe -remoting=0 /sample=yes c:true
    '''   remoting = 0
    '''   sample   = yes
    '''   c        = true
    ''' </remarks>
    Private Shared Function GetKeyValueCommandLineArg(ByVal strArg As String, _
        ByRef nvc As Specialized.NameValueCollection) As Boolean

        Dim mc As MatchCollection = Regex.Matches(strArg, "(?<Key>^[^=:]+)(=|:)(?<Value>[^=: ]*$)")
        If mc.Count = 0 Then
            Return False
        Else
            For Each m As Match In mc
                nvc.Add(RemoveArgPrefix(m.Groups("Key").ToString), m.Groups("Value").ToString)
            Next
            Return True
        End If
    End Function

    ''' <summary>
    ''' parses command line arguments, handling special case when app was launched via URL
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks>
    ''' note that the default .GetCommandLineArgs array is SPACE DELIMITED!
    ''' </remarks>
    Private Shared Function CommandLineArgCollection() As Specialized.NameValueCollection
        Dim strArgs() As String = Environment.GetCommandLineArgs()
        Dim nvc As New Specialized.NameValueCollection

        If strArgs.Length = 0 Then
            Return nvc
        End If

        '--
        '-- handles typical case where app was launched via local .EXE
        '--
        Dim intArg As Integer = 0
        For Each strArg As String In strArgs
            If IsURL(strArg) Then
                GetURLCommandLineArgs(strArg, nvc)
            Else
                If Not GetKeyValueCommandLineArg(strArg, nvc) Then
                    nvc.Add("arg" & intArg, RemoveArgPrefix(strArg))
                    intArg += 1
                End If
            End If
        Next

        Return nvc
    End Function

    ''' <summary>
    ''' exception-safe retrieval of LastWriteTime for this assembly.
    ''' </summary>
    ''' <returns>File.GetLastWriteTime, or DateTime.MaxValue if exception was encountered.</returns>
    Private Shared Function AssemblyLastWriteTime(ByVal a As System.Reflection.Assembly) As DateTime
        Try
            Return File.GetLastWriteTime(a.Location)
        Catch ex As Exception
            Return DateTime.MaxValue
        End Try
    End Function

    ''' <summary>
    ''' Returns DateTime this Assembly was last built. Will attempt to calculate from build number, if possible. 
    ''' If not, the actual LastWriteTime on the assembly file will be returned.
    ''' </summary>
    ''' <param name="a">Assembly to get build date for</param>
    ''' <param name="ForceFileDate">Don't attempt to use the build number to calculate the date</param>
    ''' <returns>DateTime this assembly was last built</returns>
    Private Shared Function AssemblyBuildDate(ByVal a As System.Reflection.Assembly, _
        Optional ByVal ForceFileDate As Boolean = False) As DateTime

        Dim AssemblyVersion As System.Version = a.GetName.Version
        Dim BuildTime As DateTime

        If ForceFileDate Then
            BuildTime = AssemblyLastWriteTime(a)
        Else
            BuildTime = CType("01/01/2000", DateTime). _
                AddDays(AssemblyVersion.Build). _
                AddSeconds(AssemblyVersion.Revision * 2)
            If TimeZone.IsDaylightSavingTime(BuildTime, TimeZone.CurrentTimeZone.GetDaylightChanges(BuildTime.Year)) Then
                BuildTime = BuildTime.AddHours(1)
            End If
            If BuildTime > DateTime.Now Or AssemblyVersion.Build < 730 Or AssemblyVersion.Revision = 0 Then
                BuildTime = AssemblyLastWriteTime(a)
            End If
        End If

        Return BuildTime
    End Function

    ''' <summary>
    ''' Throws an exception if the specified Key is not present in the appSettings section of the .config file
    ''' </summary>
    Private Shared Sub EnsureKeyPresent(ByVal Key As String)
        Dim strTemp As String
        Try
            strTemp = ConfigurationManager.AppSettings.Get(Key)
        Catch ex As Exception
            Throw New System.Configuration.ConfigurationErrorsException(String.Format(_strKeyError, ex.Message, Key), ex)
        End Try
        If strTemp Is Nothing OrElse strTemp = "" Then
            Throw New System.Configuration.ConfigurationErrorsException(String.Format(_strKeyNotPresent, Key))
        End If
    End Sub

    ''' <summary>
    ''' returns the value from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>String. If key is not present, throws ConfigurationErrorsException.</returns>
    Public Shared Function GetString(ByVal Key As String) As String
        EnsureKeyPresent(Key)
        Return GetString(Key, "")
    End Function

    ''' <summary>
    ''' returns the value from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>String. If key is not present, returns specified Default.</returns>
    Public Shared Function GetString(ByVal Key As String, ByVal [Default] As String) As String
        Try
            Dim strTemp As String = ConfigurationManager.AppSettings.Get(Key)
            If strTemp = Nothing Then
                Return [Default]
            Else
                Return strTemp
            End If
        Catch ex As Exception
            Return [Default]
        End Try
    End Function

    ''' <summary>
    ''' returns the value from the specified 'KeyPrefix/Key' in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>String. If key is not present, returns Default.</returns>
    Public Shared Function GetString(ByVal Key As String, _
        ByVal KeyPrefix As String, _
        ByVal [Default] As String) As String
        Return GetString(KeyPrefix & "/" & Key, [Default])
    End Function

    ''' <summary>
    ''' returns the value from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>Boolean. If key is not present, throws ConfigurationErrorsException.</returns>
    Public Shared Function GetBoolean(ByVal Key As String) As Boolean
        EnsureKeyPresent(Key)
        Return GetBoolean(Key, False)
    End Function

    ''' <summary>
    ''' returns the value from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>Boolean. If key is not present or if the value cannot be converted to a boolean, returns specified Default.</returns>
    Public Shared Function GetBoolean(ByVal Key As String, ByVal [Default] As Boolean) As Boolean
        Dim strTemp As String
        Try
            strTemp = ConfigurationManager.AppSettings.Get(Key)
            If strTemp = Nothing Then
                Return [Default]
            Else
                Return Convert.ToBoolean(strTemp)
            End If
        Catch ex As Exception
            Return [Default]
        End Try
    End Function

    ''' <summary>
    ''' returns the value from the specified 'KeyPrefix/Key' in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>Boolean. If key is not present or if the value cannot be converted to a boolean, returns Default.</returns>
    Public Shared Function GetBoolean(ByVal Key As String, _
        ByVal KeyPrefix As String, _
        Optional ByVal [Default] As Boolean = False) As Boolean
        Return GetBoolean(KeyPrefix & "/" & Key, [Default])
    End Function

    ''' <summary>
    ''' returns the value from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>Integer. If key is not present, throws ConfigurationErrorsException.</returns>
    Public Shared Function GetInteger(ByVal Key As String) As Integer
        EnsureKeyPresent(Key)
        Return GetInteger(Key, 0)
    End Function

    ''' <summary>
    ''' returns the value from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>Integer. If key is not present or if the value cannot be converted to an integer, returns specified Default.</returns>
    Public Shared Function GetInteger(ByVal Key As String, _
        ByVal [Default] As Integer) As Integer

        Dim strTemp As String
        Try
            strTemp = ConfigurationManager.AppSettings.Get(Key)
            If strTemp = Nothing Then
                Return [Default]
            Else
                Return Convert.ToInt32(strTemp)
            End If
        Catch ex As Exception
            Return [Default]
        End Try
    End Function

    ''' <summary>
    ''' returns the value from the specified 'KeyPrefix/Key' in the appSettings section of the .config file
    ''' </summary>
    ''' <returns>Integer. If key is not present or if the value cannot be converted to an integer, returns Default.</returns>
    Public Shared Function GetInteger(ByVal Key As String, _
        ByVal KeyPrefix As String, _
        Optional ByVal [Default] As Integer = 0) As Integer
        Return GetInteger(KeyPrefix & "/" & Key, [Default])
    End Function

    ''' <summary>
    ''' forces a relative path to become rooted relative to the current app BaseFolder
    ''' </summary>
    ''' <remarks>
    ''' do not use ~/, it is unnecessary; we aren't using Server.MapPath.
    ''' </remarks>
    Private Shared Function MakePathRooted(ByVal path As String) As String
        If path.StartsWith("~/") Then path = path.Replace("~/", "")
        If IO.Path.IsPathRooted(path) Then
            Return path
        Else
            Return IO.Path.Combine(BaseDirectory, path)
        End If
    End Function

    ''' <summary>
    ''' returns a Path from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <remarks>
    ''' If the path is not rooted, it is assumed to be relative to the current web root, and will be pathed appropriately
    ''' If the key does not exist, a ConfigurationErrorsException will be thrown
    ''' </remarks>
    ''' <returns>A rooted filesystem path</returns>
    Public Shared Function GetPath(ByVal Key As String) As String
        Dim strPath As String = GetString(Key)
        Return MakePathRooted(strPath)
    End Function

    ''' <summary>
    ''' returns a Path from the specified Key in the appSettings section of the .config file
    ''' </summary>
    ''' <remarks>
    ''' If the key does not exist, the default is used. 
    ''' If the path is not rooted, it is assumed to be relative to the current web root, and will be pathed appropriately
    ''' </remarks>
    ''' <returns>A rooted filesystem path</returns>
    Public Shared Function GetPath(ByVal Key As String, ByVal [Default] As String) As String
        Dim strPath As String = GetString(Key, [Default])
        Return MakePathRooted(strPath)
    End Function

    ''' <summary>
    ''' returns a Path from the specified 'KeyPrefix/Key' in the appSettings section of the .config file
    ''' </summary>
    ''' <remarks>
    ''' If the key does not exist, the default is used. 
    ''' If the path is not rooted, it is assumed to be relative to the current web root, and will be pathed appropriately
    ''' </remarks>
    ''' <returns>A rooted filesystem path</returns>
    Public Shared Function GetPath(ByVal Key As String, ByVal KeyPrefix As String, ByVal [Default] As String) As String
        Dim strPath As String = GetString(KeyPrefix & "/" & Key, [Default])
        Return MakePathRooted(strPath)
    End Function

End Class