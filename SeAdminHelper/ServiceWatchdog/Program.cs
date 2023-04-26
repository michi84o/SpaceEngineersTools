using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace ServiceWatchdog
{
    internal class Program
    {
        static DateTime ShutDownTime;

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ServiceWatchdog.exe \"ServiceName\" hour:minute");
                Console.WriteLine("Watchdog will exit at the specified time");
                return;
            }

            try
            {
                var timeSpl = args[1].Split(':');
                if (timeSpl.Length != 2)
                {
                    Console.WriteLine("Invalid time");
                    return;
                }
                int shutDownHour = int.Parse(timeSpl[0]);
                int shutDownMinute = int.Parse(timeSpl[1]);

                var now = DateTime.Now;
                now = now.AddMilliseconds(-now.Millisecond);
                now = now.AddSeconds(-now.Second);
                var shutDownTime = new DateTime(now.Year, now.Month, now.Day, shutDownHour, shutDownMinute, 0);

                if (shutDownTime < now)
                    shutDownTime = shutDownTime.AddHours(24);

                ShutDownTime = shutDownTime;

                WatchdogTask(args[0]).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static async Task WatchdogTask(string serviceName)
        {
            DateTime now = DateTime.Now;
            while (now < ShutDownTime)
            {
                try
                {
                    using (ServiceController sc = new ServiceController(serviceName))
                    {
                        if (sc.Status == ServiceControllerStatus.Stopped)
                        {
                            sc.Start();
                        }
                    }
                }
                catch { }
                await Task.Delay(30000);
                now = DateTime.Now;
            }
        }
    }
}
