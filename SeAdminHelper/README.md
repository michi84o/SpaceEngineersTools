# SeAdminHelper
Little helpers to run a Space Engineers dedicated server.

The vanilla dedicated server tool has no way to shedule restarts or to stop the server.

# SpaceEngineersServerStopper

This program 

You must start this program as administrator or launch AllowRemoteHttp.bat from the DedicatedServer64 folder and check the "Enable Remote API Port" option in the General Tab. Also write down the security key below the checkbox.

All options are set by using command line arguments. Usage:

SeAdminHelper.exe \[\-dir serverExeDir\] \[\-sk securitykey\] \[\-shutdown h:m \] \[\-restart h:m[\:ddddddd\] \] \[\-poweroff h:m[\:ddddddd\] \]

serverExeDir: 
Folder where 'SpaceEngineersDedicated.exe' is located. For example:
C:\Steam\steamapps\common\SpaceEngineersDedicatedServer\DedicatedServer64
-dir parameter is not required if you copy SeAdminHelper.exe into the same folder as 'SpaceEngineersDedicated.exe'.

securitykey: 
The security key. Copy it from the official dedicated server tool.
You can also find it in this file:
%appdata%\SpaceEngineersDedicated\SpaceEngineers\-Dedicated.cfg
Open the file and search for "RemoteSecurityKey".

shutdown/poweroff/restart:
Creates shutdown and restart points. You can define multiple points.
h is the hour and m the minute. Make sure the points are at least 5 minutes apart.
A shutdown point will end the program. You will have to start it manually again.
poweroff will end the program and SHUT DOWN THE COMPUTER\! If you don't have physical access to the computer you might be screwed.
Shutdown OS is useful to save energy during times when there is nobody online, like the early morning hours. You just need a way to restart the computer again. Some PCs have BIOS settings for automated start. You could also use Wake-On-LAN to restart it.

ddddddd:
Optional attachment to event points. Defines the days of the week where the event is valid. Replace d with 1 or 0. First digit is Monday, last is Sunday.
Example: Trigger event only during the week, not on the weekend
h:m:1111100

Full example:

This will run the server. It will restart at 6:00, 12:00 and 0:00. The 12:00 point will not be triggered on Saturday and Sunday.

SeAdminHelper.exe -dir "C:\Steam\steamapps\common\SpaceEngineersDedicatedServer\DedicatedServer64" \-sk XXXXXXX \-restart 6:00 \-restart 12:00:1111100 \-restart 0:00

![Screenshot of client](Screenshot/screenshot1.png)
