using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.RemoteClient.Core;

namespace SpaceEngineersServerStopper
{
    class RemoteClientWrapper
    {
        int _port;
        string _securityKey;
        public RemoteClientWrapper(int port, string securityKey)
        {
            _port = port;
            _securityKey = securityKey;
        }

        async Task<RemoteClient> RemoteConnect()
        {
            RemoteClient client = new RemoteClient();
            Uri uri = new Uri(string.Format("{0}:{1}", "http://localhost", _port));
            var tcs = new TaskCompletionSource<bool>();
            EventHandler ConnectHandler = (s, a) => tcs.TrySetResult(true);
            EventHandler FailHandler = (s, a) => tcs.TrySetResult(false);
            client.Connected += ConnectHandler;
            client.ConnectFailed += FailHandler;
            client.Connect(uri, _securityKey);
            var connectResult = await tcs.Task;
            client.Connected -= ConnectHandler;
            client.ConnectFailed -= FailHandler;
            if (!connectResult)
            {
                Console.WriteLine("Server remote connect failed!");
                return null;
            }
            return client;
        }

        public async Task<bool> SendChat(string text)
        {
            var client = await RemoteConnect();
            if (client == null) return false;
            var tcs = new TaskCompletionSource<bool>();
            Action<IRestResponse> chatResponseAction = (response) => tcs.TrySetResult(response.IsSuccessful);
            client.SendChatMessageAsync(text, chatResponseAction);
            var result = await tcs.Task;
            return result;
        }

        public async Task<bool> StopServer()
        {
            var client = await RemoteConnect();
            if (client == null) return false;
            var tcs = new TaskCompletionSource<bool>();
            Action<IRestResponse> stopResponse = (response) => tcs.TrySetResult(response.IsSuccessful);
            client.StopServer(stopResponse);
            var result = await tcs.Task;
            return result;
        }

        //void SysLog(string message)
        //{
        //    if (EventLog.SourceExists("SpaceEngineers"))
        //    {
        //        EventLog.WriteEntry("SpaceEngineers", message);
        //    }
        //    else
        //    {
        //        EventLog.CreateEventSource("SpaceEngineers", "SpaceEngineers");
        //    }
        //}
    }
}
