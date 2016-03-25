'SPAMGrinderProxy v2.0
'Written by Michael Lowery (spamgrinder@shootingstarbbs.2y.net)
'Copyright (C) 2003-2004

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
'This program is free software; you can redistribute it and/or
'modify it under the terms of the GNU General Public License as published
'by the Free Software Foundation; either version 2 of the License, or any
'later version.

'This program is distributed in the hope that it will be useful,
'but WITHOUT ANY WARRANTY; without even the implied warranty of
'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
'Public License for more details.

'You should have received a copy of the GNU General Public
'License along with this program; if not, write to the Free Software
'Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
'Version history:

'2.1    Added multi-threading to proxy.
'       Used temporary file names for log/temp files (prevents file locking.)
'       Added connection timeout setting to config.xml.

'2.0    Added SMTP server filtering.
'       Added several settings to config.xml for SMTP.
'       Completely rewrote TcpClient interface and added dead socket detection.
'       Fixed several minor bugs.
'       Added ProxyServer setting to config.xml file for POP.

'1.1    Added debug/log file option in config.xml file.
'       Added configurable port settings to config.xml file.
'       Added more robust code for reading config.xml file; added defaults
'           for missing nodes.
'       Fixed bug in DNSRBL lookup code under IsSpam() sub.

'1.0    Initial release.
'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
'Steps to build and install project:
'Create and build SPAMGrinderProxy project as usual
'Create and build SPAMGrinderProxySetup project (installer)
'Install app from project's Release or Debug folder
'Go to app install folder and type "installutil spamgrinderproxy.exe"
'Start the service: "net start spamgrinderproxy"

'Steps to uninstall:
'Stop the service: "net stop spamgrinderproxy"
'Type "installutil /u spamgrinderproxy.exe"
'Uninstall SPAMGrinderProxySetup from Add/Remove Programs

'For debugging:
'Build in Debug mode by following instructions above
'While service is running, open the project in Visual Studio
'Select Tools, Debug Processes, and Attach to SPAMGrinderProxy
'You are now in full debug mode
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

Imports System.ServiceProcess
Imports System.Net.Sockets
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml
Imports System.Net.DnsPermissionAttribute
Imports System.Security.Permissions
Imports System.IO
Imports System.Timers
Imports System.Threading

<System.Net.DnsPermissionAttribute(System.Security.Permissions.SecurityAction.Demand, Unrestricted:=True)> Public Class SPAMGrinderProxy
    Inherits System.ServiceProcess.ServiceBase
    Protected evtLog As New System.Diagnostics.EventLog("Application", ".", "SPAMGrinderProxy")  'For writing errors/etc to the system's event log
    Protected myTimer As System.Timers.Timer 'Initialized in Sub New()
    Protected POPListener, SMTPListener As TcpListener 'TCP server class
    Protected POPincomingPort, POPoutgoingPort, SMTPincomingPort, SMTPoutgoingPort, connectionTimeout As Integer 'TCP ports to listen and send on, set in config.xml
    Protected dataPath As String = Environ("ProgramFiles") & "\Shooting Star\SPAMGrinderProxy" 'Path to data files
    'Protected tempPath As String = Environ("TEMP") 'Temporary files
    Protected tempPath As String = Path.GetTempPath
    Protected useSpamAssassin, useSPAMGrinderFilters, enableLogging, usePOP, useSMTP As Boolean 'Set by config.xml setting
    Protected DNSBL(), SASwitches, ProxyServer, SMTPServer, POPSMTPServer As String

#Region " Component Designer generated code "

    Public Sub New()
        MyBase.New()

        myTimer = New System.Timers.Timer(1000)
        AddHandler myTimer.Elapsed, AddressOf OnTimer

        ' This call is required by the Component Designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call

    End Sub

    'UserService overrides dispose to clean up the component list.
    Protected Overloads Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            If Not (components Is Nothing) Then
                components.Dispose()
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    ' The main entry point for the process
    <MTAThread()> _
    Shared Sub Main()
        Dim ServicesToRun() As System.ServiceProcess.ServiceBase

        ' More than one NT Service may run within the same process. To add
        ' another service to this process, change the following line to
        ' create a second service object. For example,
        '
        '   ServicesToRun = New System.ServiceProcess.ServiceBase () {New Service1, New MySecondUserService}
        '
        ServicesToRun = New System.ServiceProcess.ServiceBase() {New SPAMGrinderProxy()}

        System.ServiceProcess.ServiceBase.Run(ServicesToRun)
    End Sub

    'Required by the Component Designer
    Private components As System.ComponentModel.IContainer

    ' NOTE: The following procedure is required by the Component Designer
    ' It can be modified using the Component Designer.  
    ' Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> Private Sub InitializeComponent()
        '
        'SPAMGrinderProxy
        '
        Me.ServiceName = "SPAMGrinderProxy"

    End Sub

#End Region

    Protected Overrides Sub OnStart(ByVal args() As String)
        ' Add code here to start your service. This method should set things
        ' in motion so your service can do its work.
        ReadConfig()
        StartListener() 'Start listening for connections on TCP port

        'Display application version in log file
        'Dim sMajor As String = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly.Location).FileMajorPart
        'Dim sMinor As String = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly.Location).FileMinorPart
        'evtLog.WriteEntry("SPAMGrinderProxy v" & sMajor & "." & sMinor & " service started", EventLogEntryType.Information)

        myTimer.Enabled = True 'Begin monitoring incoming connections on TCP ports (110/POP3, 25/SMTP)
    End Sub

    Protected Overrides Sub OnStop()
        ' Add code here to perform any tear-down necessary to stop your service.

        myTimer.Enabled = False 'Stop monitoring incoming connections
        If usePOP Then POPListener.Stop() 'Kill the TCP listener
        If useSMTP Then SMTPListener.Stop()

    End Sub

    Protected Sub StartListener()
        'Start the listener servers

        '"Localhost" string is used when the client and the listener are on the same computer.
        'If the listener is listening at a computer that is different from the client, then
        'provide the host name of the computer where the listener is listening.

        'This can present a problem when a tcpClient is on a different computer than
        'the tcpServer client.  Both have to refer to the same IP address, so "localhost"
        'will not work since that's 127.0.0.1 (the local machine.)  In these cases, it's
        'necessary to look up the real IP address and use that, on both ends.

        'For POP, most users will run the server and client on the same box, so "localhost"
        'is fine.  This may not be true for other users, so may have to provide a method
        'to change this to something else later via config.xml.

        If usePOP Then
            POPListener = New TcpListener(CType(Dns.Resolve(ProxyServer).AddressList(0), IPAddress), POPincomingPort)
            POPListener.Start()
        End If

        If useSMTP Then
            'We need to get this computer's real IP address,
            'not just "localhost" (127.0.0.1) since this listener will be accepting
            'connections from the outside Internet under typical configurations.

            'Get this computer's Host Name
            Dim strMachineName As String = Dns.GetHostName()
            'Get the Host by Name
            Dim ipHost As IPHostEntry = Dns.GetHostByName(strMachineName)
            'Get the list of addresses associated with the host in an array
            Dim ipAddr() As IPAddress = ipHost.AddressList
            Dim count As Byte

            'Enumerate the IP Addresses
            'For count = 0 To ipAddr.GetUpperBound(0)
            'Console.Write("IP Addresses {0}: {1} ", count, ipAddr(count).ToString)
            'Next

            SMTPListener = New TcpListener(ipAddr(0), SMTPincomingPort)
            'SMTPListener = New TcpListener(CType(Dns.Resolve("Localhost").AddressList(0), IPAddress), SMTPincomingPort)
            SMTPListener.Start()
        End If

    End Sub

    Protected Sub OnTimer(ByVal source As Object, ByVal e As ElapsedEventArgs)
        'Check to see if any connection requests have been made

        myTimer.Enabled = False 'Disable check for new connection requests
        If usePOP AndAlso POPListener.Pending Then
            Dim POPThread As New Thread(AddressOf ProcessPOP)
            POPThread.IsBackground = True
            POPThread.Start()
            'ProcessPOP() 'Initiate POP connection
        End If
        If useSMTP AndAlso SMTPListener.Pending Then
            Dim SMTPThread As New Thread(AddressOf ProcessSMTP)
            SMTPThread.IsBackground = True
            SMTPThread.Start()
            'ProcessSMTP() 'Initiate SMTP connection
        End If
        myTimer.Enabled = True  'Enable new connection request checking

    End Sub

    Protected Sub ReadConfig()
        'Read configuration information from config.xml, use defaults for missing entries

        If Not File.Exists(dataPath & "\config.xml") Then
            evtLog.WriteEntry("No config.xml file found! Looking in:" & vbCrLf & dataPath, EventLogEntryType.Error)
            End
        End If

        Dim i As Byte
        Dim serverNodes As XmlNodeList
        Dim serverNode As XmlElement
        Dim strLog As String
        Dim doc As XmlDocument = New XmlDocument()

        doc.Load(dataPath & "\config.xml")
        ''Dim serverNodes As XmlNodeList = doc.GetElementsByTagName("config/POP/server")
        'serverNodes = doc.SelectNodes("config/POP/server")
        'ReDim POPServers(serverNodes.Count, 2)
        'For i = 0 To (serverNodes.Count - 1)
        'Dim serverNode As XmlElement = serverNodes.Item(i)
        'POPServers(i, 0) = serverNode.GetAttribute("Name").ToString
        'POPServers(i, 1) = serverNode.SelectSingleNode("username").InnerText
        'POPServers(i, 2) = serverNode.SelectSingleNode("password").InnerText
        'Next

        'Make sure config.xml has correct information
        'If POPServers(0, 0) = "popservername" Then
        'i = MsgBox("You must edit the config.xml file first!" & vbCrLf & "Edit now?", MsgBoxStyle.YesNo)
        'If i = vbYes Then
        'Dim procID As Integer = Shell("notepad.exe " & Chr(34) & Application.StartupPath & "\config.xml" & Chr(34), AppWinStyle.NormalFocus, False)
        'End If
        '#If Not Debug Then
        '   Me.Close()
        '   End
        '#End If
        'End If

        'Read SpamAssassin setting
        serverNode = doc.SelectSingleNode("config/SpamAssassin/UseSpamAssassin")
        If Not IsNothing(serverNode) Then
            useSpamAssassin = (serverNode.InnerText = "1")
        Else
            strLog &= "Missing config/SpamAssassin/UseSpamAssassin node (default=1)" & vbCrLf
            useSpamAssassin = True
        End If

        'Read SPAMGrinderProxy filter setting
        serverNode = doc.SelectSingleNode("config/SPAMGrinderProxyFilters/UseSPAMGrinderFilters")
        If Not IsNothing(serverNode) Then
            useSPAMGrinderFilters = (serverNode.InnerText = "1")
        Else
            strLog &= "Missing config/SPAMGrinderProxyFilters/UseSPAMGrinderFilters node (default=1)" & vbCrLf
            useSPAMGrinderFilters = False
        End If

        If useSPAMGrinderFilters Then 'SPAMGrinderProxy filter settings
            'Read DNSBL server information from config.xml
            'serverNodes = doc.GetElementsByTagName("config/DNSBL/server")
            serverNodes = doc.SelectNodes("config/SPAMGrinderProxyFilters/DNSBL/server")
            If Not IsNothing(serverNodes) Then
                ReDim DNSBL(serverNodes.Count)
                For i = 0 To (serverNodes.Count - 1)
                    serverNode = serverNodes.Item(i)
                    DNSBL(i) = serverNode.InnerText
                Next
            Else
                strLog &= "Missing config/SPAMGrinderProxyFilters/DNSBL/server node" & vbCrLf
            End If
        End If

        If useSpamAssassin Then 'SpamAssassin settings
            'Read SpamAssassin command-line switches
            serverNode = doc.SelectSingleNode("config/SpamAssassin/SASwitches")
            If Not IsNothing(serverNode) Then
                SASwitches = serverNode.InnerText
            Else
                strLog &= "Missing config/SpamAssassin/SASwitches node (default=-a)" & vbCrLf
                SASwitches = "-a"
            End If
        End If

        'Read log file setting
        serverNode = doc.SelectSingleNode("config/EnableLogging")
        If Not IsNothing(serverNode) Then
            enableLogging = (serverNode.InnerText = "1")
        Else
            strLog &= "Missing config/EnableLogging node (default=1)" & vbCrLf
            enableLogging = True
        End If

        'Read timeout value
        serverNode = doc.SelectSingleNode("config/ConnectionTimeout")
        If Not IsNothing(serverNode) Then
            connectionTimeout = CInt(serverNode.InnerText) * 1000
        Else
            strLog &= "Missing config/ConnectionTimeout node (default=60)" & vbCrLf
            connectionTimeout = 60000
        End If

        'Read log file path; tempPath is currently set to Path.GetTempPath
        If enableLogging Then
            serverNode = doc.SelectSingleNode("config/LogFilePath")
            If Not IsNothing(serverNode) Then
                If serverNode.InnerText <> "" Then tempPath = serverNode.InnerText
            Else
                strLog &= "Missing config/LogFilePath node (default=" & tempPath & ")" & vbCrLf
                enableLogging = True
            End If
        End If

        'Read POP settings
        serverNode = doc.SelectSingleNode("config/POP/UsePOP")
        If Not IsNothing(serverNode) Then
            usePOP = (serverNode.InnerText = "1")
        Else
            strLog &= "Missing config/POP/UsePOP node (default=1)" & vbCrLf
            usePOP = True
        End If

        'Read SMTP settings
        serverNode = doc.SelectSingleNode("config/SMTP/UseSMTP")
        If Not IsNothing(serverNode) Then
            useSMTP = (serverNode.InnerText = "1")
        Else
            strLog &= "Missing config/SMTP/UseSMTP node (default=0)" & vbCrLf
            useSMTP = False
        End If

        If usePOP Then
            'Read ProxyServer setting
            serverNode = doc.SelectSingleNode("config/POP/ProxyServer")
            If Not IsNothing(serverNode) Then
                ProxyServer = serverNode.InnerText
            Else
                strLog &= "Missing config/POP/ProxyServer node (default=localhost)" & vbCrLf
                ProxyServer = "localhost"
            End If
            'Read ISP SMTP server
            serverNode = doc.SelectSingleNode("config/POP/POPSMTPServer")
            If Not IsNothing(serverNode) Then
                POPSMTPServer = serverNode.InnerText
            Else
                strLog &= "Missing config/POP/POPSMTPServer node (default=localhost)" & vbCrLf
                POPSMTPServer = "localhost"
            End If
            'Read POP port settings
            serverNode = doc.SelectSingleNode("config/POP/IncomingPort")
            If Not IsNothing(serverNode) Then
                POPincomingPort = CInt(serverNode.InnerText)
            Else
                strLog &= "Missing config/POP/IncomingPort node (default 110)" & vbCrLf
                POPincomingPort = 110
            End If
            serverNode = doc.SelectSingleNode("config/POP/OutgoingPort")
            If Not IsNothing(serverNode) Then
                POPoutgoingPort = CInt(serverNode.InnerText)
            Else
                strLog &= "Missing config/POP/OutgoingPort node (default 110)" & vbCrLf
                POPoutgoingPort = 110
            End If
        End If

        If useSMTP Then
            'Read SMTP server and port settings
            serverNode = doc.SelectSingleNode("config/SMTP/SMTPServer")
            If Not IsNothing(serverNode) Then
                SMTPServer = serverNode.InnerText
            Else
                strLog &= "Missing config/SMTP/SMTPServer node (default=localhost)"
                SMTPServer = "localhost"
            End If
            serverNode = doc.SelectSingleNode("config/SMTP/SMTPincomingPort")
            If Not IsNothing(serverNode) Then
                SMTPincomingPort = CInt(serverNode.InnerText)
            Else
                strLog &= "Missing config/SMTP/SMTPincomingPort node (default=25)" & vbCrLf
                SMTPincomingPort = 25
            End If
            serverNode = doc.SelectSingleNode("config/SMTP/SMTPoutgoingPort")
            If Not IsNothing(serverNode) Then
                SMTPoutgoingPort = CInt(serverNode.InnerText)
            Else
                strLog &= "Missing config/SMTP/SMTPoutgoingPort node (default=25)" & vbCrLf
                SMTPoutgoingPort = 25
            End If
        End If

        'Write any errors to event log
        If strLog <> Nothing Then
            evtLog.WriteEntry("The following entries were missing from the config.xml file; defaults will be used:" & vbCrLf & vbCrLf & strLog, EventLogEntryType.Warning)
        End If

    End Sub

    Protected Function IsSpam(ByRef msg As String, ByRef swFile As StreamWriter)
        'Check to see if message contains SPAM.

        'Run SpamAssassin check if set in config.xml file
        If useSpamAssassin Then
            RunSpamAssassin(msg)
            'msg now contains SA info and headers
            If msg.IndexOf("X-Spam-Flag: YES") <> -1 Then
                Return True
            Else
                Return False
            End If
        End If

        'Run internal filters if set in config.xml file
        'ADD X-Spam-Flag: YES HEADER TO E-MAIL MESSAGE!
        If useSPAMGrinderFilters Then
            Dim strFrom As String = MsgFrom(msg)
            Dim strSubject As String = MsgSubject(msg)
            Dim strDate As String = MsgDate(msg)

            'Perform obvious spam checks
            If strFrom = String.Empty Then Return True 'No return address
            If strDate = String.Empty Then Return True 'Null for date/time
            If strSubject.StartsWith("ADV:") Then Return True 'ADV: at start is spam

            Dim strIPAddress, strFile, strTemp As String

            'Check address against whitelist and return "OK" if match
            strFile = dataPath & "\whitelist.txt"
            If File.Exists(strFile) Then
                Dim objStreamReader As StreamReader
                Dim strLine As String
                objStreamReader = New StreamReader(strFile)
                strLine = objStreamReader.ReadLine
                Do While Not strLine Is Nothing
                    If strLine = strFrom Then
                        objStreamReader.Close()
                        Return False
                    ElseIf strline.IndexOf("*") = 0 Then 'Wildcard character
                        strline = strline.Substring(1)
                        If strFrom.LastIndexOf(strline) <> -1 Then 'Match found
                            objstreamreader.Close()
                            Return False 'In whitelist, not SPAM
                        End If
                    End If
                    strLine = objStreamReader.ReadLine
                Loop
                objStreamReader.Close()
            End If

            'Check address against blacklist and return "SPAM" if match
            strFile = dataPath & "\blacklist.txt"
            If File.Exists(strFile) Then
                Dim objStreamReader As StreamReader
                Dim strLine As String
                objStreamReader = New StreamReader(strFile)
                strLine = objStreamReader.ReadLine
                Do While Not strLine Is Nothing
                    If strLine = strFrom Then
                        objStreamReader.Close()
                        Return True
                    ElseIf strline.IndexOf("*") = 0 Then 'Wildcard character
                        strline = strline.Substring(1)
                        If strFrom.LastIndexOf(strline) <> -1 Then 'Match found
                            objstreamreader.Close()
                            Return True 'In blacklist, SPAM
                        End If
                    End If
                    strLine = objStreamReader.ReadLine
                Loop
                objStreamReader.Close()
            End If

            'Do a DNS lookup on sender's domain
            Dim strDomain As String = strFrom.Split("@")(1)
            Dim hostinfo() As IPAddress
            Try
                hostinfo = Dns.Resolve(strDomain).AddressList
            Catch e As Exception
                swFile.WriteLine("[DNS lookup failed on " & strDomain & "]")
                'evtLog.WriteEntry("ERROR: " & e.ToString, EventLogEntryType.Warning)
            End Try
            If IsNothing(hostinfo) Then Return True 'Invalid domain
            'For i = 0 To hostinfo.Length - 1
            'Console.WriteLine("Host is " & hostinfo(i).ToString)
            'Next

            'Check IP address against DNSBL spam database
            Dim i As Short
            strIPAddress = hostinfo(0).ToString
            Dim tmpArray As String() = strIPAddress.Split(".")
            'Reverse IP address
            strIPAddress = Nothing
            For i = 3 To 0 Step -1
                strIPAddress &= tmpArray(i) & "."
            Next
            'Check against DNSBL servers
            For i = 0 To UBound(DNSBL) - 1
                'strIPAddress &= "bl.spamcop.net"
                'strIPAddress = "2.0.0.127.bl.spamcop.net"
                strTemp = strIPAddress & DNSBL(i)
                hostinfo = Nothing
                Try
                    hostinfo = Dns.Resolve(strTemp).AddressList
                    swFile.WriteLine("[" & strTemp & " exists in DNSBL server]")
                    'evtLog.WriteEntry(hostinfo(0).ToString)
                Catch e As Exception
                    'If not in database, we get an error which means not spam
                End Try
                If Not IsNothing(hostinfo) AndAlso hostinfo.Length > 0 Then Return True 'An IP indicates a spam match
            Next i
        End If

        Return False

    End Function

    Protected Function MsgFrom(ByRef msg As String) As String
        'Return sender of message

        Const strMatchEmail As String = "[\w-_\.]+\@([\w]+\.)+\w+"
        'Const strMatchEmail As String = "[\w-]+@([\w-]+\.)+[\w-]+"
        Const strMatchReturnPath As String = "Return-path:\s<" & strMatchEmail & ">"

        'Check against regular expression pattern
        If Regex.IsMatch(msg, strMatchReturnPath, RegexOptions.IgnoreCase) Then
            'Line found, extract e-mail address
            MsgFrom = Regex.Match(msg, strMatchReturnPath, RegexOptions.IgnoreCase).ToString
            MsgFrom = Regex.Match(MsgFrom, strMatchEmail, RegexOptions.IgnoreCase).ToString
        End If

    End Function

    Protected Function MsgSubject(ByRef msg As String) As String
        'Return subject of e-mail

        Const strMatchSubjectLine As String = "\nSubject:\s.+\n"

        'Check against regular expression pattern
        If Regex.IsMatch(msg, strMatchSubjectLine, RegexOptions.IgnoreCase) Then
            'Line found, extract subject
            MsgSubject = Regex.Match(msg, strMatchSubjectLine, RegexOptions.IgnoreCase).ToString
            MsgSubject = MsgSubject.Substring(10, MsgSubject.Length - 12)
        End If

    End Function

    Protected Function MsgDate(ByRef msg As String)
        'Return sent date of e-mail
        Dim returnDate, returnTime As String
        Dim strArray As String()
        Dim a As Byte

        Const strMatchDateLine As String = "\nDate:\s.+\n"

        'Check against regular expression pattern
        If Regex.IsMatch(msg, strMatchDateLine, RegexOptions.IgnoreCase) Then
            'Line found, extract e-mail address
            returnDate = Regex.Match(msg, strMatchDateLine, RegexOptions.IgnoreCase).ToString
            returnDate = returnDate.Substring(7)
            strArray = returnDate.Split(" ") 'Chop it up using the spaces
            returnDate = Nothing
            For a = 0 To 3 'Build the date string
                returnDate &= strArray(a) & " "
            Next
            Return returnDate & " " & strArray(4) 'Date and time
        End If

    End Function

    Protected Function RunSpamAssassin(ByRef msg As String)
        'Returns modified msg after running SpamAssassin
        Dim ProcID As Integer
        Dim fs As FileStream, swFile As StreamWriter, srFile As StreamReader
        Dim filTemp1, filTemp2 As String

        'Generate temp file name
        Dim fso As New Scripting.FileSystemObject()
        filTemp2 = fso.GetTempName
        filTemp1 = tempPath & "MSG" & filTemp2
        filTemp2 = tempPath & "SA" & filTemp2

        Try
            'Write the e-mail to a text file for SpamAssassin
            fs = New FileStream(filTemp1, FileMode.Create)
            swFile = New StreamWriter(fs)
            swFile.AutoFlush = True
            swFile.Write(msg)
            swFile.Close()
            fs.Close()

            ' Run SpamAssassin
            ProcID = Shell("spamassassin.bat " & SASwitches & " < " & filTemp1 & " > " & filTemp2, AppWinStyle.Hide, True)

            'Read modified message
            fs = New FileStream(filTemp2, FileMode.Open)
            srFile = New StreamReader(fs, System.Text.Encoding.ASCII)
            srFile.BaseStream.Seek(0, SeekOrigin.Begin)
            msg = srFile.ReadToEnd
            srFile.Close()
            fs.Close()

            If enableLogging = False Then
                File.Delete(filTemp1)
                File.Delete(filTemp2)
            End If

        Catch err As Exception
            'Anti-virus software can cause this to fail by locking msg.txt file.
            'In most cases, this should fail gracefully and leave original message intact.
            evtLog.WriteEntry("SPAMGrinderProxy error:" & vbCrLf & err.Message & vbCrLf & err.StackTrace, EventLogEntryType.Warning)
        End Try

    End Function

    Protected Sub ModifySubject(ByRef msg As String)
        'Add *****SPAM***** to subject
        Dim regMatch As Match
        Dim i As Integer

        Const strMatchSubjectLine As String = "\nSubject:\s.+\n" '\n is chr$(10) linefeed

        'Check against regular expression pattern
        If Regex.IsMatch(msg, strMatchSubjectLine, RegexOptions.IgnoreCase) Then
            'Line found, find location
            regMatch = Regex.Match(msg, strMatchSubjectLine, RegexOptions.IgnoreCase)
            i = regMatch.Index 'Location of subject line in msg
            msg = msg.Insert(i + 10, "*****SPAM***** ") 'Add to subject
        Else
            'No subject line found, create one

        End If

    End Sub

    Protected Sub ProcessPOP()
        'Process POP messages
        'Dim ServerSender As New TcpClient()
        'Dim ClientSender As TcpClient
        Dim ServerSender As New MyTcpClientDerivedClass()
        Dim ClientSender As New MyTcpClientDerivedClass()
        Dim fs As FileStream
        Dim nsServer, nsClient As NetworkStream
        Dim srServer, srClient As StreamReader
        Dim swServer, swClient, swFile As StreamWriter
        Dim msgBuffer As New StringBuilder()
        Dim clientData, split(), ServerData, strFirstLine, filTemp As String

        Try
            'Get temporary file name
            Dim fso As New Scripting.FileSystemObject()
            filTemp = tempPath & "POP" & fso.GetTempName
            'Open up a log file
            'fs = New FileStream(tempPath & "\SPAMGrinderProxyPOP.log", FileMode.Create)
            fs = New FileStream(filTemp, FileMode.Create)
            swFile = New StreamWriter(fs)
            swFile.AutoFlush = True
            swFile.WriteLine("Logging started at " & Now & vbCrLf)

            'Accept the pending client connection and return a TcpClient for communication
            'ClientSender = POPListener.AcceptTcpClient()
            ClientSender.MyClient = POPListener.AcceptSocket
            swFile.WriteLine("[Connection accepted]")
            ' Get the data stream
            nsClient = ClientSender.GetStream()
            srClient = New StreamReader(nsClient)
            swClient = New StreamWriter(nsClient, swClient.Encoding.ASCII)
            swClient.AutoFlush = True

            'Make connection to POP SMTP server and start the proxy
            Try
                ServerSender.Connect(POPSMTPServer, POPoutgoingPort)
            Catch err As Exception
                'Add error handler here
                evtLog.WriteEntry("ServerSender.Connect failed!" & vbCrLf & err.Message & vbCrLf & err.StackTrace, EventLogEntryType.Warning)
                swFile.WriteLine("[ServerSender.Connect failed!" & vbCrLf & err.Message & "]")
                swFile.Close()
                Exit Sub
            End Try
            nsServer = ServerSender.GetStream
            srServer = New StreamReader(nsServer)
            swServer = New StreamWriter(nsServer)
            swServer.AutoFlush = True

            'Set timeouts on TcpListeners
            If connectionTimeout > 0 Then
                ServerSender.SendTimeout = connectionTimeout
                ServerSender.ReceiveTimeout = connectionTimeout
                ClientSender.SendTimeout = connectionTimeout
                ClientSender.ReceiveTimeout = connectionTimeout
            End If

            Do While ServerSender.SocketActive AndAlso ClientSender.SocketActive
                'StreamReader.Peek doesn't work with pending data in a NetworkStream.
                'However, once the data buffer is sent from the NetworkStream into the
                'StreamReader, Peek does work.  So we must check both.
                If nsServer.DataAvailable Or srServer.Peek <> -1 Then
                    'Two-way connection established, start monitoring for RETR command
                    ServerData = srServer.ReadLine 'Should be +OK from POP connection
                    swClient.WriteLine(ServerData) 'Send to requesting client
                    swFile.WriteLine(ServerData)
                    If Not IsNothing(clientData) AndAlso clientData.ToLower = "quit" Then Exit Do
                End If
                If nsClient.DataAvailable Or srClient.Peek <> -1 Then
                    clientData = srClient.ReadLine 'Read remote computer (client) input (incoming SMTP)
                    swServer.WriteLine(clientData) 'Send client data to SMTP server
                    swFile.WriteLine(clientData)   'Write to log file
                    If clientData.ToLower.StartsWith("retr ") Then 'Client is retrieving an e-mail
                        strFirstLine = srServer.ReadLine '+OK not part of e-mail message
                        Do
                            ServerData = srServer.ReadLine        'Read each line of e-mail message
                            If ServerData = "." Then Exit Do 'Message terminator
                            msgBuffer.Append(ServerData & vbCrLf) 'Append to StringBuilder
                        Loop
                        ServerData = msgBuffer.ToString
                        msgBuffer.Remove(0, msgBuffer.Length) 'Clear StringBuilder
                        If IsSpam(ServerData, swFile) Then
                            ModifySubject(ServerData) 'Add SPAM to subject line of e-mail
                        End If
                        'Add first and last lines back to message for client
                        ServerData = strFirstLine & vbCrLf & ServerData & "." & vbCrLf
                        swClient.Write(ServerData) 'Write modified message to client
                        swFile.Write(ServerData)   'Write to log file
                    End If
                End If
            Loop

            'Close all ISP connections
            swServer.Close()
            srServer.Close()
            nsServer.Close()
            ServerSender.Close()

            'Close all Client connections
            swClient.Close()
            srClient.Close()
            nsClient.Close()
            ClientSender.Close()
            swFile.Close()
            fs.Close()

            If enableLogging = False Then
                'Kill(tempPath & "\SPAMGrinderProxyPOP.log")
                File.Delete(filTemp)
            End If

        Catch err As Exception
            evtLog.WriteEntry("SPAMGrinderProxy error:" & vbCrLf & err.Message & vbCrLf & err.StackTrace, EventLogEntryType.Error)
        End Try

    End Sub

    Sub ProcessSMTP()
        'Process SMTP messages
        'Dim ServerSender As New TcpClient() 'The receiving SMTP server
        'Dim ClientSender As TcpClient       'The sending SMTP server (client)
        Dim ServerSender As New MyTcpClientDerivedClass()
        Dim ClientSender As New MyTcpClientDerivedClass()
        Dim fs As FileStream
        Dim nsServer, nsClient As NetworkStream
        Dim srServer, srClient As StreamReader
        Dim swServer, swClient, swFile As StreamWriter
        Dim msgBuffer As New StringBuilder()
        Dim clientData, split(), ServerData, strFirstLine, filTemp As String

        'Get temporary file name
        Dim fso As New Scripting.FileSystemObject()
        filTemp = tempPath & "SMTP" & fso.GetTempName

        Try
            'Open up a log file
            'fs = New FileStream(tempPath & "\SPAMGrinderProxySMTP.log", FileMode.Create)
            fs = New FileStream(filTemp, FileMode.Create)
            swFile = New StreamWriter(fs)
            swFile.AutoFlush = True
            swFile.WriteLine("Logging started at " & Now & vbCrLf)

            'Accept the pending client connection and return a TcpClient for communication
            'ClientSender = SMTPListener.AcceptTcpClient() 'This is the remote requesting computer
            ClientSender.MyClient = SMTPListener.AcceptSocket
            swFile.WriteLine("[Connection accepted]")
            ' Get the data stream
            nsClient = ClientSender.GetStream()
            srClient = New StreamReader(nsClient)
            swClient = New StreamWriter(nsClient, swClient.Encoding.ASCII)
            swClient.AutoFlush = True

            'Make connection to SMTP server and start the proxy
            Try
                ServerSender.Connect(SMTPServer, SMTPoutgoingPort)
            Catch err As Exception
                'Add error handler here
                evtLog.WriteEntry("ServerSender.Connect failed!" & vbCrLf & err.Message & vbCrLf & err.StackTrace, EventLogEntryType.Warning)
                swFile.WriteLine("[ServerSender.Connect failed!" & vbCrLf & err.Message & "]")
                swFile.Close()
                Exit Sub
            End Try
            nsServer = ServerSender.GetStream
            srServer = New StreamReader(nsServer)
            swServer = New StreamWriter(nsServer)
            swServer.AutoFlush = True

            'Set timeouts on TcpListeners
            If connectionTimeout > 0 Then
                ServerSender.SendTimeout = connectionTimeout
                ServerSender.ReceiveTimeout = connectionTimeout
                ClientSender.SendTimeout = connectionTimeout
                ClientSender.ReceiveTimeout = connectionTimeout
            End If

            Try 'Catch stream I/O errors
                Do While ServerSender.SocketActive AndAlso ClientSender.SocketActive
                    'StreamReader.Peek doesn't work with pending data in a NetworkStream.
                    'However, once the data buffer is sent from the NetworkStream into the
                    'StreamReader, Peek does work.  So we must check both.
                    If nsServer.DataAvailable Or srServer.Peek <> -1 Then
                        'Two-way connection established, start monitoring for DATA command
                        ServerData = srServer.ReadLine 'Should be 220 from SMTP connection
                        swClient.WriteLine(ServerData) 'Send to requesting client
                        swFile.WriteLine(ServerData)
                        'Intercept e-mail message
                        'First get entire message, determine if SPAM, then send result to SMTP server
                        If ServerData.StartsWith("354 ") Then 'Server telling client to send e-mail message
                            Do
                                clientData = srClient.ReadLine        'Read each line of e-mail message
                                swFile.Write(clientData)   'Write to log file
                                If clientData = "." Then Exit Do 'Message terminator
                                msgBuffer.Append(clientData & vbCrLf) 'Append to StringBuilder
                            Loop
                            clientData = msgBuffer.ToString
                            msgBuffer.Remove(0, msgBuffer.Length) 'Clear StringBuilder
                            If IsSpam(clientData, swFile) Then
                                ModifySubject(clientData) 'Add SPAM to subject line of e-mail
                            End If
                            clientData &= "." & vbCrLf 'Re-attach message terminator
                            swServer.Write(clientData) 'Write modified message to server
                            'ServerData = srServer.ReadLine 'Get server response (should be 250 OK)
                            'swClient.WriteLine(ServerData) 'Send it to client
                            'swFile.WriteLine(ServerData)   'Write to log file
                        ElseIf ServerData.StartsWith("221 ") Then
                            Exit Do 'Server close session code
                        End If
                    End If
                    If nsClient.DataAvailable Or srClient.Peek <> -1 Then
                        clientData = srClient.ReadLine 'Read remote computer (client) input (incoming SMTP)
                        swServer.WriteLine(clientData) 'Send client data to SMTP server
                        swFile.WriteLine(clientData)   'Write to log file
                    End If
                Loop
            Catch err As Exception
                evtLog.WriteEntry("SPAMGrinderProxy error:" & vbCrLf & err.Message & vbCrLf & err.StackTrace, EventLogEntryType.Warning)
            Finally
                'Close all ISP connections
                swServer.Close()
                srServer.Close()
                nsServer.Close()
                ServerSender.Close()

                'Close all Client connections
                swClient.Close()
                srClient.Close()
                nsClient.Close()
                ClientSender.Close()
                swFile.Close()
                fs.Close()

                If enableLogging = False And err.Number = 0 Then
                    'Kill(tempPath & "\SPAMGrinderProxySMTP.log")
                    File.Delete(filTemp)
                End If
            End Try

        Catch err As Exception
            evtLog.WriteEntry("SPAMGrinderProxy error:" & vbCrLf & err.Message & vbCrLf & err.StackTrace, EventLogEntryType.Warning)
        End Try

    End Sub
    ''''''''''''''''''''''''''''''''''''''''''''''

    Public Class MyTcpClientDerivedClass
        Inherits TcpClient
        'The TcpClient class does not expose the .Client property which we need in order
        'to test whether the socket is still open.  So we must inherit from the TcpClient
        'class and expose this property to our program.

        Public Sub New()
            MyBase.New()
        End Sub 'New

        Public ReadOnly Property SocketActive() As Boolean
            Get
                'The .Poll method returns True if data available or if connection has
                'been lost.  So if True and no data is available, connection is dead.
                If Me.Client.Poll(1000, SelectMode.SelectRead) = True And Me.Client.Available = 0 Then
                    Return False 'Connection lost
                Else
                    Return True  'Connection live
                End If
                'Return Me.Client.Connected 'This does not detect lost connections!
            End Get
        End Property

        Public WriteOnly Property MyClient() As Socket
            'Sets the socket for this derived TcpClient class.
            Set(ByVal s As Socket)
                Me.Client = s
            End Set
        End Property

    End Class 'MyTcpClientDerivedClass

End Class
