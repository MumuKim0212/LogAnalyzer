using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LogAnalyzerAgent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int port = 5000;
            if (args.Length > 0 && int.TryParse(args[0], out int p)) port = p;

            Console.WriteLine($"Starting LogAnalyzer TCP Agent on Port {port}...");
            var listener = new TcpListener(IPAddress.Any, port);
            
            try 
            {
                listener.Start();
                Console.WriteLine("Agent is efficiently running. Waiting for connections from WPF Client...");
                
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    // Process each WPF client connection in a separate lightweight Task
                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical Error: {ex.Message}");
            }
        }

        static async Task HandleClientAsync(TcpClient client)
        {
            EndPoint? endPoint = null;
            try
            {
                endPoint = client.Client.RemoteEndPoint;
                Console.WriteLine($"[+] Client connected: {endPoint}");

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                // AutoFlush is essential so lines are sent instantly without delay
                using var networkWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // 1. Receive the absolute file path requested by the client
                var filePath = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(filePath)) return;
                filePath = filePath.Trim();

                Console.WriteLine($"[>] Client requested file: {filePath}");

                if (!File.Exists(filePath))
                {
                    await networkWriter.WriteLineAsync($"[TCP_AGENT_ERROR] File not found: {filePath}");
                    return;
                }

                // 2. Open the file perfectly safely allowing write/delete sharing
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var fileReader = new StreamReader(fs, Encoding.UTF8);

                // 3. Ultra lightweight Tail -f loop
                while (client.Connected)
                {
                    string? line = await fileReader.ReadLineAsync();
                    
                    if (line != null)
                    {
                        try 
                        {
                            await networkWriter.WriteLineAsync(line);
                        }
                        catch (IOException) 
                        {
                            break; // Network dropped
                        }
                    }
                    else
                    {
                        // End Of File. 
                        // Handle Log Rotation or Cleaning
                        if (fs.Length < fs.Position)
                        {
                            fs.Seek(0, SeekOrigin.Begin);
                            fileReader.DiscardBufferedData();
                            Console.WriteLine($"[*] File {filePath} was Truncated/Rotated. Resetting tail offset.");
                        }
                        
                        // Sleep a fraction of a second to spare CPU before querying EOF again
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error with client {endPoint}: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine($"[-] Client disconnected: {endPoint}");
            }
        }
    }
}
