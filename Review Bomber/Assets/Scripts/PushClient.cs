using UnityEngine;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class PushClient : MonoBehaviour
{
    [Header("Server Settings")]
    public int port = 8001;

    [Header("HTML File To Serve")]
    public TextAsset htmlFile;

    private TcpListener listener;
    private Thread serverThread;
    private string tempFilePath;
    private bool running;

    void Start()
    {
        if (htmlFile == null)
        {
            Debug.LogError("No HTML file assigned.");
            return;
        }

        // Write HTML to a temp file (network thread can't read Unity asset memory safely)
        tempFilePath = Path.Combine(Application.persistentDataPath, "served_page.html");
        File.WriteAllText(tempFilePath, htmlFile.text);

        StartServer();
    }

    void StartServer()
    {
        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            running = true;

            serverThread = new Thread(ServerLoop);
            serverThread.IsBackground = true;
            serverThread.Start();

            Debug.Log($"Server started on http://localhost:{port}");
            Debug.Log($"Serving file: {tempFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError("Server failed to start: " + e.Message);
        }
    }

    void ServerLoop()
    {
        while (running)
        {
            try
            {
                if (!listener.Pending())
                {
                    Thread.Sleep(10);
                    continue;
                }

                TcpClient client = listener.AcceptTcpClient();
                HandleClient(client);
            }
            catch { }
        }
    }

    void HandleClient(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            // Read request (we ignore contents for simplicity)
            byte[] buffer = new byte[1024];
            stream.Read(buffer, 0, buffer.Length);

            string html = File.ReadAllText(tempFilePath);
            string response =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: text/html\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(html)}\r\n" +
                "Connection: close\r\n\r\n" +
                html;

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        listener?.Stop();
        serverThread?.Abort();
    }
}
