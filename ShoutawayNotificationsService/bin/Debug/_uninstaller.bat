@ECHO OFF
echo Uninstalling Windows service...
echo ---------------------------------------------------
C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\InstallUtil /LogFile= /u ShoutawayNotificationsService.exe
echo ---------------------------------------------------
echo Done.
pause