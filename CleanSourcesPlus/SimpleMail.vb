Imports System.Net
Imports System.Net.Sockets
Imports System.Threading
Imports System.Text
Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports System.IO

''' <summary>
''' a simple class for trivial SMTP mail support, with no COM dependencies. Defaults to correct GSK settings.
''' </summary>
''' <remarks>
'''   - plain text or HTML body
'''   - one optional file attachment
'''   - basic retry mechanism
''' </remarks>
Public Class SimpleMail

    ''' <summary>
    ''' A mail message to be sent. The only required properties are To, and Body.
    ''' </summary>
    Public Class SmtpMailMessage
        ''' <summary>
        ''' Address this email came from. Optional.
        ''' If not provided, an email address will be automatically generated based on the machine name. 
        ''' </summary>
        Public From As String
        ''' <summary>
        ''' Address(es) to send email to. Semicolon delimited. Required.
        ''' </summary>
        Public [To] As String
        ''' <summary>
        ''' Subject text for the email. Optional, but recommended.
        ''' </summary>
        Public Subject As String
        ''' <summary>
        ''' Plain text body. Required.
        ''' </summary>
        Public Body As String
        ''' <summary>
        ''' HTML text body. Optional.
        ''' </summary>
        Public BodyHTML As String
        ''' <summary>
        ''' Fully qualified path of the file you want to attach to the email. Optional.
        ''' </summary>
        Public AttachmentPath As String
        ''' <summary>
        ''' String you wish to attach to the email. Intended for large strings. Optional.
        ''' </summary>
        Public AttachmentText As String
        ''' <summary>
        ''' Name of the attachment as shown in the email. Optional.
        ''' </summary>
        Public AttachmentFilename As String
    End Class

    ''' <summary>
    ''' SMTP client used to submit SMTPMailMessage(s)
    ''' </summary>
    Public Class SmtpClient

        Private Const _BufferSize As Integer = 1024
        Private Const _ExpectedResponseTime As Integer = 10
        Private Const _MaxResponseTime As Integer = 750
        Private Const _MaxRetries As Integer = 5
        Private Const _ConfigKeyPrefix As String = "SimpleMail"
        Private Const _MaxLineWidth As Integer = 75

        Private _DefaultEncoding As Text.Encoding = Text.Encoding.UTF8
        Private _IsPlainTextOnly As Boolean = False
        Private _DefaultDomain As String = ConfigString("DefaultDomain", "gsk.com")
        Private _ServerName As String = ConfigString("Server", "smtphub.glaxo.com")
        Private _ServerPort As Integer = ConfigInteger("Port", 25)
        Private _UserName As String = ConfigString("AuthUser", "")
        Private _UserPassword As String = ConfigString("AuthPassword", "")
        Private _IsDebugMode As Boolean

        Private _Retries As Integer = 1
        Private _LastResponse As String

        Public Sub New()
            _IsDebugMode = Diagnostics.Debugger.IsAttached
        End Sub

        ''' <summary>
        ''' Authenticating username, if your mail server requires outgoing authentication.
        ''' Leave blank otherwise.
        ''' </summary>
        Public Property AuthUser() As String
            Get
                Return _UserName
            End Get
            Set(ByVal Value As String)
                _UserName = Value
            End Set
        End Property

        ''' <summary>
        ''' Authenticating password, if your mail server requires outgoing authentication.
        ''' Leave blank otherwise.
        ''' </summary>
        Public Property AuthPassword() As String
            Get
                Return _UserPassword
            End Get
            Set(ByVal Value As String)
                _UserPassword = Value
            End Set
        End Property

        ''' <summary>
        ''' TCP/IP port to use during SMTP communications.
        ''' </summary>
        ''' <remarks>
        ''' Defaults to 25, the standard SMTP port.
        ''' </remarks>
        Public Property Port() As Integer
            Get
                Return _ServerPort
            End Get
            Set(ByVal Value As Integer)
                _ServerPort = Value
            End Set
        End Property

        ''' <summary>
        ''' SMTP server name to connect to when sending mail.
        ''' </summary>
        Public Property Server() As String
            Get
                Return _ServerName
            End Get
            Set(ByVal Value As String)
                _ServerName = Value
            End Set
        End Property

        ''' <summary>
        ''' Default email domain, eg, 'mycompany.com'
        ''' </summary>
        Public Property DefaultDomain() As String
            Get
                Return _DefaultDomain
            End Get
            Set(ByVal Value As String)
                _DefaultDomain = Value
            End Set
        End Property

        ''' <summary>
        ''' Forces email to be as simple as possible, bypassing all MIME related headers
        ''' </summary>
        Public Property PlainTextOnly() As Boolean
            Get
                Return _IsPlainTextOnly
            End Get
            Set(ByVal Value As Boolean)
                _IsPlainTextOnly = Value
            End Set
        End Property

        ''' <summary>
        ''' Returns true if this class is running in a web context
        ''' </summary>
        Private Function IsWebHosted() As Boolean
            Return Not Web.HttpContext.Current Is Nothing
        End Function

        Private Function ConfigString(ByVal Key As String, Optional ByVal DefaultValue As String = Nothing) As String
            Return AppSettings.GetString(Key, _ConfigKeyPrefix, DefaultValue)
        End Function

        Private Function ConfigBoolean(ByVal Key As String, Optional ByVal DefaultValue As Boolean = Nothing) As Boolean
            Return AppSettings.GetBoolean(Key, _ConfigKeyPrefix, DefaultValue)
        End Function

        Private Function ConfigInteger(ByVal Key As String, Optional ByVal DefaultValue As Integer = Nothing) As Integer
            Return AppSettings.GetInteger(Key, _ConfigKeyPrefix, DefaultValue)
        End Function

        ''' <summary>
        ''' send data over the current network connection
        ''' </summary>
        Private Sub SendData(ByVal tcp As TcpClient, ByVal s As String)
            Dim ns As NetworkStream = tcp.GetStream()
            Dim b(s.Length) As Byte

            b = _DefaultEncoding.GetBytes(s)
            ns.Write(b, 0, b.Length)
        End Sub

        ''' <summary>
        ''' get data from the current network connection
        ''' </summary>
        Private Function GetData(ByVal tcp As TcpClient) As String
            Dim ns As Sockets.NetworkStream = tcp.GetStream()

            If ns.DataAvailable Then
                Dim b() As Byte = New Byte(_BufferSize) {}
                Dim i As Integer = ns.Read(b, 0, b.Length)
                Return _DefaultEncoding.GetString(b)
            Else
                Return ""
            End If
        End Function

        ''' <summary>
        ''' issue a required SMTP command
        ''' </summary>
        Private Sub Command(ByVal tcp As TcpClient, ByVal Command As String, _
            Optional ByVal ExpectedResponse As String = "250")

            If Not CommandInternal(tcp, Command, ExpectedResponse) Then
                tcp.Close()
                Throw New Exception("SMTP server at " & _ServerName.ToString & ":" & _ServerPort.ToString + _
                    " was provided command '" & Command & _
                    "', but did not return the expected response '" & ExpectedResponse & "':" _
                    + Environment.NewLine + _LastResponse)
            End If

        End Sub

        ''' <summary>
        ''' issue a SMTP command
        ''' </summary>
        Private Function CommandInternal(ByVal tcp As TcpClient, ByVal Command As String, _
            Optional ByVal ExpectedResponse As String = "250") As Boolean

            Dim intResponseTime As Integer

            '-- send the command over the socket with a trailing cr/lf
            If Command.Length > 0 Then
                SendData(tcp, Command & Environment.NewLine)
            End If

            '-- wait until we get a response, or time out
            _LastResponse = ""
            intResponseTime = 0
            Do While (_LastResponse = "") And (intResponseTime <= _MaxResponseTime)
                intResponseTime += _ExpectedResponseTime
                _LastResponse = GetData(tcp)
                Thread.CurrentThread.Sleep(_ExpectedResponseTime)
            Loop

            '-- this is helpful for debugging SMTP problems
            If _IsDebugMode Then
                Debug.WriteLine("SMTP >> " & Command & " (after " & intResponseTime.ToString & "ms)")
                Debug.WriteLine("SMTP << " & _LastResponse)
            End If

            '-- if we have a response, check the first 10 characters for the expected response code
            If _LastResponse = "" Then
                If _IsDebugMode Then
                    Debug.WriteLine("** EXPECTED RESPONSE " & ExpectedResponse & " NOT RETURNED **")
                End If
                Return False
            Else
                Return _LastResponse.StartsWith(ExpectedResponse)
            End If
        End Function

        ''' <summary>
        ''' send mail with integrated retry mechanism
        ''' </summary>
        Public Function SendMail(ByVal m As SmtpMailMessage) As Boolean
            Dim RetryInterval As Integer = 333
            Try
                SendMailInternal(m)
            Catch ex As Exception
                _Retries += 1
                If _IsDebugMode Then
                    Debug.WriteLine("--> SendMail Exception Caught")
                    Debug.WriteLine(ex.Message)
                End If
                If _Retries <= _MaxRetries Then
                    Thread.CurrentThread.Sleep(RetryInterval)
                    SendMail(m)
                Else
                    Throw
                End If
            End Try
            If _IsDebugMode Then
                Debug.WriteLine("sent after " & _Retries.ToString)
            End If
            _Retries = 1
            Return True
        End Function

        ''' <summary>
        ''' send an email via trivial SMTP implementation
        ''' </summary>
        Private Sub SendMailInternal(ByVal m As SmtpMailMessage)

            '-- resolve server text name to an IP address
            Dim iphost As IPHostEntry
            Try
                iphost = Dns.GetHostByName(_ServerName)
            Catch e As Exception
                Throw New Exception("Unable to resolve server name " & _ServerName, e)
            End Try

            '-- attempt to connect to the server by IP address and port number
            Dim tcp As New TcpClient
            Try
                tcp.Connect(iphost.AddressList(0), _ServerPort)
            Catch e As Exception
                Throw New Exception("Unable to connect to SMTP server at " & _ServerName.ToString & ":" & _ServerPort.ToString, e)
            End Try

            '-- make sure we get the SMTP welcome message
            Command(tcp, "", "220")
            Command(tcp, "HELO " & Environment.MachineName)

            '--
            '-- authenticate if we have username and password
            '-- http://www.ietf.org/rfc/rfc2554.txt
            '--
            If (_UserName & _UserPassword).Length > 0 Then
                Command(tcp, "auth login", "334 VXNlcm5hbWU6") 'VXNlcm5hbWU6=base64'Username:'
                Command(tcp, ToBase64(_UserName), "334 UGFzc3dvcmQ6") 'UGFzc3dvcmQ6=base64'Password:'
                Command(tcp, ToBase64(_UserPassword), "235")
            End If

            '-- if from address wasn't provided, synthesize one from the machine
            '-- this is useful for emails from unattended processes
            If m.From = "" Then
                If IsWebHosted() Then
                    m.From = Web.HttpContext.Current.Request.ServerVariables("server_name") & _
                        "@" & _DefaultDomain
                Else
                    m.From = AppDomain.CurrentDomain.FriendlyName.ToLower & "." & _
                        Environment.MachineName.ToLower & "@" & _DefaultDomain
                End If
            End If
            Command(tcp, "MAIL FROM: <" & m.From & ">")

            '-- send email to more than one recipient
            For Each s As String In GetDelimitedWordArray(m.To)
                Command(tcp, "RCPT TO: <" & s & ">")
            Next

            Command(tcp, "DATA", "354")

            Dim sb As New StringBuilder
            With sb
                '-- write common email headers
                .Append("To: " & m.To & Environment.NewLine)
                .Append("From: " & m.From & Environment.NewLine)
                .Append("Subject: " & m.Subject & Environment.NewLine)

                If _IsPlainTextOnly Then
                    '-- write plain text body
                    .Append(Environment.NewLine & m.Body & Environment.NewLine)
                Else
                    Dim strContentType As String
                    '-- typical case; mixed content will be displayed side-by-side
                    strContentType = "multipart/mixed"
                    '-- unusual case; text and HTML body are both included, let the reader determine which it can handle
                    If m.Body <> "" And m.BodyHTML <> "" Then
                        strContentType = "multipart/alternative"
                    End If

                    .Append("MIME-Version: 1.0" & Environment.NewLine)
                    .Append("Content-Type: " & strContentType & "; boundary=""NextMimePart""" & Environment.NewLine)
                    .Append("Content-Transfer-Encoding: 7bit" & Environment.NewLine)
                    .Append(Environment.NewLine)

                    ' -- default content (for non-MIME compliant email clients, should be extremely rare)
                    .Append("This message is in MIME format. Since your mail reader does not understand " & Environment.NewLine)
                    .Append("this format, some or all of this message may not be legible." & Environment.NewLine)
                    '-- handle text body (if any)
                    If m.Body <> "" Then
                        .Append(Environment.NewLine & "--NextMimePart" & Environment.NewLine)
                        .Append("Content-Type: text/plain;" & Environment.NewLine)
                        .Append(Environment.NewLine + m.Body + Environment.NewLine)
                    End If
                    ' -- handle HTML body (if any)
                    If m.BodyHTML <> "" Then
                        .Append(Environment.NewLine & "--NextMimePart" & Environment.NewLine)
                        .Append("Content-Type: text/html; charset=iso-8859-1" & Environment.NewLine)
                        .Append(Environment.NewLine + m.BodyHTML + Environment.NewLine)
                    End If
                    '-- handle attachment (if any)
                    If m.AttachmentPath <> "" Then
                        .Append(FileToMimeString(m.AttachmentPath, m.AttachmentFilename))
                    End If
                    If m.AttachmentText <> "" Then
                        .Append(ToMimeString(m.AttachmentText, m.AttachmentFilename))
                    End If
                End If
                '-- <crlf>.<crlf> marks end of message content
                .Append(Environment.NewLine & "." & Environment.NewLine)
            End With

            Command(tcp, sb.ToString)
            Command(tcp, "QUIT", "")
            tcp.Close()
        End Sub

        ''' <summary>
        ''' Given a list of flexibly delimited words, returns array of those words as string.
        ''' </summary>
        Public Function GetDelimitedWordArray(ByVal s As String) As String()
            Dim al As New ArrayList

            For Each m As Match In Regex.Matches(s, "[^;, ]+")
                al.Add(m.ToString)
            Next

            Return CType(al.ToArray(GetType(String)), String())
        End Function

        ''' <summary>
        ''' returns MIME header section string
        ''' </summary>
        Private Function MimeHeaderString(ByVal Filename As String) As String
            Dim sb As New StringBuilder
            If Filename Is Nothing Or Filename = "" Then Filename = "attachment.txt"
            With sb
                .Append(Environment.NewLine & "--NextMimePart" & Environment.NewLine)
                .Append("Content-Type: application/octet-stream; name=""" & Filename & """" & Environment.NewLine)
                .Append("Content-Transfer-Encoding: base64" & Environment.NewLine)
                .Append("Content-Disposition: attachment; filename=""" & Filename & """" & Environment.NewLine)
                .Append(Environment.NewLine)
            End With
            Return sb.ToString
        End Function

        ''' <summary>
        ''' turn string into a MIME attachment string
        ''' </summary>
        Private Function ToMimeString(ByVal s As String, Optional ByVal Filename As String = "Attachment.txt") As String
            Dim sb As New StringBuilder

            s = Convert.ToBase64String(_DefaultEncoding.GetBytes(s))

            sb.Append(MimeHeaderString(Filename))
            Dim c As Integer
            For i As Integer = 0 To s.Length - 1
                c += 1
                sb.Append(s.Substring(i, 1))
                If c = _MaxLineWidth - 1 Then
                    c = 0
                    sb.Append(Environment.NewLine)
                End If
            Next
            Return sb.ToString
        End Function

        ''' <summary>
        ''' turn a file into a MIME attachment string
        ''' </summary>
        Private Function FileToMimeString(ByVal Filepath As String, Optional ByVal FileName As String = "") As String

            Dim fs As FileStream
            Dim sb As New StringBuilder
            Dim Bytes(_MaxLineWidth) As Byte
            Dim BytesRead As Integer

            If FileName Is Nothing Or FileName = "" Then
                '-- get just the filename out of the path
                FileName = Path.GetFileName(Filepath)
            End If

            sb.Append(MimeHeaderString(FileName))

            fs = New FileStream(Filepath, FileMode.Open, FileAccess.Read)
            BytesRead = fs.Read(Bytes, 0, _MaxLineWidth)
            Do While BytesRead > 0
                sb.Append(Convert.ToBase64String(Bytes, 0, BytesRead))
                sb.Append(Environment.NewLine)
                BytesRead = fs.Read(Bytes, 0, _MaxLineWidth)
            Loop
            fs.Close()

            Return sb.ToString
        End Function

        ''' <summary>
        ''' Encodes a string as Base64
        ''' </summary>
        Private Function ToBase64(ByVal s As String) As String
            Return Convert.ToBase64String(_DefaultEncoding.GetBytes(s))
        End Function

    End Class

End Class