// See https://aka.ms/new-console-template for more information

using System.Collections.Specialized;
using System.Net;

namespace LocalRadioServer
{
    public static class Program
    {
        public static AutoResetEvent Signal = new(false);

        private static HttpListener server;
        private static readonly string url = "http://127.0.0.1:8888/";

        private static Dictionary<string, HttpListenerContext> clients = new();

        private static int bytesToRead;

        public static void Main(string[] args)
        {
            Task.Run(MusicThread.SliceMusicSegment);
            
            server = new HttpListener();
            server.Prefixes.Add(url);
            server.Start();
            
            Console.WriteLine($"Listening on {url}");
            
            Task task = HandleRequests();
            task.GetAwaiter().GetResult();
            
            server.Close();
        }

        private static void HandleRequestsAsync(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            NameValueCollection headers = request.Headers;

            foreach (var header in headers)
            {
                if ("icy-metadata" == header.ToString()?.ToLower())
                {
                    response.AddHeader("icy-metaint", "8192");
                }
            }

            while (true)
            {
                Signal.WaitOne();
                
                byte[] data = new byte[8 * 1024 + 1];
                Buffer.BlockCopy(MusicThread.segments, 0, data, 0, data.Length - 1);
                data[8 * 1024] = 0;

                try
                {
                    if (response.OutputStream.CanWrite)
                    {
                        response.OutputStream.WriteAsync(data, 0, data.Length);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return;
                }
            }
        }

        public static async Task HandleRequests()
        {
            while (true)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} waiting new requests...");
                HttpListenerContext context = await server.GetContextAsync();

                Console.WriteLine($"clients: {clients.Count}");
                if (clients.ContainsKey(context.Request.UserHostName))
                {
                    foreach (var client in clients)
                    {
                        if (client.Key == context.Request.UserHostName)
                        {
                            client.Value.Response.Close();
                            clients.Remove(context.Request.UserHostName);
                        }
                    }
                }
                
                clients.Add(context.Request.UserHostName, context);

                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} handling new request...");
                Task.Run(() => HandleRequestsAsync(context));
            }
        }
    }
}