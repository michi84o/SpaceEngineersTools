﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpaceEngineersServerStopper
{
    internal class Program
    {
        static int MinutesToShutdown = 15;
        static string SecurityKey = "";

        static string ShutDownMessage = "Server shut down";

        static void Main(string[] args)
        {
            bool good = false;
            if (args.Length >= 2 && args.Length <= 3)
            {
                if (int.TryParse(args[1], out var m) && m > 0 && m <= 1440)
                {
                    MinutesToShutdown = m;
                }
                SecurityKey = args[0];

                if (args.Length == 3)
                {
                    ShutDownMessage = args[2];
                }

                good = true;
            }

            if (!good)
            {
                Console.WriteLine("Usage: SpaceEngineersServerStopper SecurityKey TimeInMinutes [shut down message override]");
                Console.WriteLine("\r\n  Example: SpaceEngineersServerStopper +tFFFFFFFFFFFFFFFFFFFF== 15");
                //EventLog.CreateEventSource("SpaceEngineers", "SpaceEngineers");
                //EventLog.Delete("SpaceEngineers");
                return;
            }
            ShutDownTask().GetAwaiter().GetResult();
            Environment.Exit(0);
        }

        static async Task ShutDownTask()
        {
            var client = new RemoteClientWrapper(8081, SecurityKey);

            for (int i = MinutesToShutdown; i > 1; --i)
            {
                await client.SendChat(ShutDownMessage + " in " + i + " minutes");
                await Task.Delay(60000);
            }
            await client.SendChat(ShutDownMessage + " in 1 minute");
            await Task.Delay(30000);
            await client.SendChat(ShutDownMessage + " in 30 seconds");
            await Task.Delay(10000);
            await client.SendChat(ShutDownMessage + " in 20 seconds");
            await Task.Delay(10000);
            await client.SendChat(ShutDownMessage + " in 10 seconds");
            await Task.Delay(10000);
            // Delay before this line or
            // server will not accept security token for some reason.
            await client.StopServer();
        }
    }
}
