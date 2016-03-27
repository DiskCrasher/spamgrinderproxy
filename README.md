<html>

<p align="center"><strong><font size="6">SPAMGrinderProxy<br>

				</font></strong><em>"Written by a spam-hater for spam-haters!"</em></p>

		
<p align="left">Updated 03/24/2016</p>

		
<p align="left"><a href="#whatisit">What is it?</a>
			<br>

			<a href="#wherecanigetit">Where can I get it?</a>
			<br>

			<a href="#howdoesitwork">How does it work?</a>
			<br>

			<a href="#howdoiuseit">How do I use it?</a>
			<br>

			<a href="#blacklist">How do the SPAMGrinderProxy blacklist and whitelist files 
				work?</a>
			<br>

			<a href="#howdoiuninstallit">How do I uninstall it?</a>
			<br>

			<a href="#whatelsedoineedtoknow">What else do I need to know?</a>
			<br>

			<a href="#additionalcomments">Additional comments.</a>
			<br>

			<a href="#knownissuesbugs">Known issues/bugs.</a>
			<br>

			<a href="#whocanicontact">Who can I contact if I have a problem?</a>
			<br>

		</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><font style="font-size: 15pt;" size="4"><a name="whatisit">What 
								is it?</a></font></em></font></font></p>

		
<p align="left">SPAMGrinderProxy is a <a href="http://www.gnu.org/philosophy/free-sw.html">
				free</a> e-mail&nbsp;SPAM
			filtering program licensed under <a href="http://www.gnu.org/copyleft/gpl.html">GNU 
				General Public License</a>&nbsp;and written in Visual Basic.Net. It can 
			operate in both POP3 and SMTP modes. In POP3 mode, it runs on the client 
			(Windows NT/2K/XP&nbsp;platforms) and communicates with POP3 mail 
			servers.&nbsp; In SMTP mode, it runs on the mail server and scans all incoming 
			mail. Once installed and configured, it will run in the background as a Windows 
			service, awaiting connection from an e-mail client or SMTP 
			server.&nbsp;&nbsp;The e-mail client (e.g. Outlook Express) needs to be 
			configured to connect to the server "localhost" which will redirect it to this 
			program running on the local machine.&nbsp; SPAMGrinderProxy&nbsp;will then 
			analyze each message and try to determine if it's spam, adding 
			"*****SPAM*****"&nbsp;to the subject of suspected messages.&nbsp; E-mail 
			clients can then filter based on this text.</p>

		
<p align="left">Interaction with <a href="http://www.spamassassin.org/index.html">SpamAssassin</a>
			is also possible (and enabled by default in the <strong>config.xml</strong> 
file discussed below.)</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><font style="font-size: 15pt;" size="4"><a name="wherecanigetit">Where 
								can I get it?</a></font></em></font></font></p>

		
<p style="margin-top: 0.17in;">The program, including source files, is available 
<a href="https://sourceforge.net/projects/spamgrinderprox/">here</a>.</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><font style="font-size: 15pt;" size="4"><a name="howdoesitwork">How 
								does it work?</a></font></em></font></font></p>

		
<p align="left">In addition to supporting user-configured blacklists and 
			whitelists, SPAMGrinder can check e-mails against 
<a href="http://www.dnsbl.info/">DNSBL</a> lists.&nbsp; It also makes some other checks, like looking for a 
			valid return e-mail address format, including checking the existence of the 
			sender's domain.
		</p>

		
<p align="left">If you intend to use SpamAssassin with SPAMGrinderProxy, please 
			note that it can also perform most of these (and other) checks, so there is no 
			need to do them twice!&nbsp; SPAMGrinderProxy's internal spam filters are 
			currently&nbsp;simplistic compared to SpamAssassin's and you probably wouldn't 
			want to use both at the same time.&nbsp; This can be controlled by a setting in 
			SPAMGrinderProxy's <strong>config.xml</strong> file.&nbsp; Consult the 
			SpamAssassin documentation for more information on configuring SpamAssassin 
			options.
		</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><a name="howdoiuseit">How 
							do I use it?</a></em></font></font></p>

		
<p align="left">Download, unzip,&nbsp;and run the <strong>setup.exe</strong> file 
			to install it on your computer.&nbsp; You will need the 
				Microsoft .Net Framework 1.0&nbsp;installed on your machine since the 
			program was written in Visual Basic.Net 2002 (later versions of the .Net Framework should also 
			work if you're not planning on modifying or compiling the source code.)&nbsp;</p>

		
<p align="left">The setup program copies the file <strong>spamgrinderproxy.exe </strong>
			to "<font color="#000000">C:\Program Files\Shooting Star\SPAMGrinderProxySetup</font>" 
			(unless you specified a different location.)&nbsp; You now need to open a 
			command prompt, go to this folder,&nbsp;and typ<font color="#000000">e "installutil 
				spamgrinderproxy.exe".&nbsp; </font>The program "installutil" is part of 
			the .Net Framework and is&nbsp;typically located at 
			"C:\WINDOWS\Microsoft.NET\Framework\v1.0.3705" (or "v1.1.4322" if using 
			v1.1).&nbsp; It installs SPAMGrinderProxy as a Windows service on your 
			machine.&nbsp; It may be easiest to add this path&nbsp;to your computer's PATH 
			statement first and&nbsp;for future installs, but this is not required.</p>

		
<p align="left">Before starting the service, note that there is a <strong>config.xml</strong>
			file located at "C:\Program Files\Shooting Star\SPAMGrinderProxy" which 
			contains several configuration settings with comments.&nbsp; Take a look at 
			this file and edit any settings you want to change.&nbsp; The &lt;DNSBL&gt; 
			section lists all the DNSBL servers that SPAMGrinderProxy will check to help 
			try and determine if an e-mail is spam.&nbsp; You can add or remove servers if 
			you wish.&nbsp; Note that SpamAssassin uses its own config file.</p>

		
<p align="left">If you're using SpamAssassin, pay special attention the <strong>local.cf</strong>
			file stored under "C:\Perl\share\spamassassin".&nbsp; This file controls all 
			the SpamAssassin settings and is documented on the SpamAssassin 
			website.&nbsp;&nbsp;The following lines should be in this file:</p>

		
<p align="left">use_razor2 0&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; # This 
			feature does not work on Windows so disable it<br>

			use_dcc 0&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; # 
			Same as above<br>

			use_pyzor 0&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; # Same as above</p>

		
<p align="left">For POP3, one last change needs to be made, this time to your 
			e-mail client.&nbsp; The incoming POP3 server has to be set to "localhost" 
			(your computer) which SPAMGrinderProxy monitors.&nbsp; You must also add the 
			name of your outgoing SMTP server to the <b>config.xml</b> file in the 
			&lt;POPSMTPServer&gt; node.</p>

		
<p align="left">Now you are ready to start the&nbsp;service.&nbsp; At the command 
			prompt, type "net start spamgrinderproxy" or launch Computer Management and 
			find SPAMGrinderProxy listed under Services.&nbsp; You may want to set the 
			Startup Type to Automatic so that it runs each time your computer starts.</p>

		
<p align="left">Launch your e-mail client and verify that you can receive 
			e-mail.&nbsp; Note that it will take a little longer to retrieve your e-mail 
			since it's now being filtered for spam.</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><a name="blacklist">How 
							do the SPAMGrinderProxy blacklist and whitelist files work?</a></em></font></font></p>

		
<p align="left">Two text files are used to store blacklist and whitelist addresses: <strong>
				blacklist.txt</strong> and <strong>whitelist.txt</strong>.&nbsp; You can 
			add complete e-mail addresses to these files (user@domain.com) or specify a 
			wildcard character to block or add an entire domain (*routerfive.com).&nbsp; 
			For example, if you're getting a lot of spam from e-mail addresses at 
			routerfive.com (spam@routerfive.com), the previous example will cause 
			SPAMGrinderProxy to mark them all as spam.&nbsp; These files should be saved in 
			the same folder as <strong>config.xml</strong>.&nbsp; Once again, if you're 
			using SpamAssassin, it uses and saves its own white and blacklist files.</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><a name="howdoiuninstallit">How 
							do I uninstall it?</a></em></font></font></p>

		
<p align="left"><em><span style="font-style: normal;">There are two
steps. The first is to remove the service itself. You can do this by
going to the SPAMGrinderProxySetup folder and typing &ldquo;installutil
/u spamgrinderproxy.exe&rdquo;. After that, you can go into the
Control Panel's Add/Remove Programs icon and remove
SPAMGrinderProxySetup. </span></em>
		</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><a name="whatelsedoineedtoknow">What 
							else do I need to know?</a></em></font></font></p>

		
<p align="left">There currently isn't a lot of error checking in the code, so if 
			something doesn't work as expected (like your ISP's POP3 server isn't online) 
			the program may crash.&nbsp;&nbsp;If this happens, you should be able to look 
			in Window's Event Log for information. Note that in most cases the risk of 
			losing e-mails due to a crash is minimal (never happend to me while writing and 
			testing the program, since DELE is the last command sent to the ISP and a crash 
			would most likely happen before this.)</p>

		
<p align="left">If running in SMTP mode and the program crashes, your mail server 
			will stop receiving e-mails. This is obviously not a good thing, but most SMTP 
			servers will attempt to deliver mail again to a mail server that is not 
			responding. So if you can restart the service (or automatically detect when it 
			crashes and restart it) in a reasonable amount of time, you shouldn't lose any 
			messages. Worst case scenario is that the sender gets a undeliverable response 
			to their e-mail and will have to try again.</p>

		
<p align="left">SPAMGrinderProxy has been tested and appears to work fine with 
			ActivePerl 5.6.1 and SpamAssassin v2.6x.&nbsp; E-mail client testing was done 
			using Outlook Express. SMTP server testing was done using <a href="http://www.pmail.com/index.htm">
				Mercury Mail</a>.</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><a name="additionalcomments">Additional 
							comments:</a></em></font></font></p>

		
<p align="left">The program uses the environment variable "ProgramFiles" to locate 
			the <strong>config.xml</strong> file.&nbsp; This environment variable should 
			already exist in Windows 2000 and XP, but it does <u>not</u> exist in Windows 
			NT.&nbsp; You can verify this by going to the Command Prompt and typing 
			"SET".&nbsp; For Windows NT users, please manually add this environment 
			variable to your computer.&nbsp;&nbsp;For example: "SET ProgramFiles= 
			C:\Program Files".&nbsp; Note that this should be added as a system environment 
			variable.</p>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><i><a name="knownissuesbugs">Known 
							issues/bugs:</a></i></font></font></p>

		
<ol>

			<li>
				
    <p align="left">When using SpamAssassin, the program writes e-mail messages to a 
					text file on the local system so SA can do its scan. If the e-mail contains a 
					virus and you have anti-virus software running on the machine, it may prevent 
					this file from being saved which will cause an error in the program. The 
					original e-mail should still be received, but no scanning will have been done.</p>

			</li>
  <li>
				
    <p align="left">When using the internal filters and a message is identified as spam 
					but contains no subject, the *****SPAM***** tag will not be created in the 
					subject line since it does not exist in the original message.</p>

			</li>

		
</ol>

		
<p style="margin-top: 0.17in;"><font face="Albany, sans-serif"><font size="4"><em><a name="whocanicontact">Who 
							can I contact if I have a problem?</a></em></font></font></p>

		
<p align="left">The author can be reached at SourceForge.net.&nbsp;The program, source,&nbsp;discussions, and other related 
			information is available at
<a href="https://sourceforge.net/projects/spamgrinderprox/">https://sourceforge.net/projects/spamgrinderprox</a>.</p>
<p align="left">Also visit <a href="http://shootingstarbbs.us">Shooting Star BBS</a>.
		</p>

		
		
</body>
</html>
