# SpaceEngineersServerStopper

This program allows you to shut down a server using a count down that warns the players via chat message.

You must start this program as administrator or launch AllowRemoteHttp.bat from the DedicatedServer64 folder and check the "Enable Remote API Port" option in the General Tab. Also write down the security key below the checkbox.

All options are set by using command line arguments. Usage:

SpaceEngineersServerStopper.exe SecurityKey TimeInMinutes ShutDownMessageOverride

SecurityKey: 
The security key. Copy it from the official dedicated server tool.
You can also find it in this file:
%appdata%\SpaceEngineersDedicated\SpaceEngineers\-Dedicated.cfg
Open the file and search for "RemoteSecurityKey".

TimeInMinutes:
Time to shut down in minutes. Chat messages will be sent once per minute.

ShutDownMessageOverride:
The default chat message is "Server shut down". You can change this text with this optional parameter.


Full example:
SpaceEngineersServerStopper.exe "XYZABC1234==" 15
SpaceEngineersServerStopper.exe XYZABC1234==" 10 "Server restart"

# Service Watchdog

This program checks every 30 seconds if a Windows service is running. If not it will be started.
Together with the server stopper this can be used to shedule server restarts.

Usage:
ServiceWatchdog.exe ServiceName EndTime

ServiceName:
Name of the Windows Service

EndTime:
Time at which the watchdog should exit. If you use the server stopper set the time before the server gets stopped so that the watchdog won't restart the server.

Full Example:
ServiceWatchdog.exe "Engineers in Space" 3:30