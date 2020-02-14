using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class Server : MonoBehaviour
{
    #region Variables

    public int port = 56789;

    private List<ServerClient> _clients;
    private List<ServerClient> _disconnectList;

    private TcpListener _server;
    private bool _isServerStarted;

    #endregion

    #region Functions
    public void Init()
    {
        DontDestroyOnLoad(gameObject);
        _clients = new List<ServerClient>();
        _disconnectList = new List<ServerClient>();

        try
        {
            _server = new TcpListener(IPAddress.Any, port);
            _server.Start();
            
            StartListening();
            _isServerStarted = true;
        }
        catch (Exception e)
        {
            Debug.Log("Socket error: " + e.Message);
        }
    }
    
    private void StartListening()
    {
        _server.BeginAcceptTcpClient(AcceptTcpClient, _server);
    }

    private void AcceptTcpClient(IAsyncResult ar)
    {
        TcpListener listener = (TcpListener)ar.AsyncState;
        
        string allUsers = "";
        foreach (ServerClient c in _clients)
        {
            allUsers += c.clientName + '|';
        }

        
        ServerClient sc = new ServerClient(listener.EndAcceptTcpClient(ar));
        _clients.Add(sc);
        
        StartListening();

        
        Broadcast("ServerWho|" + allUsers, _clients[_clients.Count - 1]);
    }

    private bool IsConnected(TcpClient client)
    {
        try
        {
            if (client != null && client.Client != null && client.Client.Connected)
            {
                if (client.Client.Poll(0, SelectMode.SelectRead))
                    return client.Client.Receive(new byte[1], SocketFlags.Peek) != 0;

                return true;
            }
            else
                return false;
        }
        catch
        {
            return false;
        }
    }

    // Server Send function
    private void Broadcast(string data, List<ServerClient> clientList)
    {
        foreach (ServerClient client in clientList)
        {
            try
            {
                StreamWriter writer = new StreamWriter(client.tcpClient.GetStream());
                writer.WriteLine(data);
                writer.Flush();
            }
            catch (Exception e)
            {
                Debug.Log("Error : " + e.Message);
            }
        }
    }
    
    private void Broadcast(string data, ServerClient client)
    {
        List<ServerClient> sc = new List<ServerClient> { client };
        Broadcast(data, sc);
    }
    
    // Server Read function
    private void OnIncomingData(ServerClient client, string data)
    {
        Debug.Log("Server:" + data);
        string[] stringData = data.Split('|');

        switch (stringData[0])
        {
            case "ClientWho":
                client.clientName = stringData[1];
                client.isHost = (stringData[2] == "0") ? false : true;
                Broadcast("ServerConnection|" + client.clientName, _clients);
                break;
            
            case "ClientMove":
                Broadcast("ServerMove|" + stringData[1] + "|" + stringData[2] + "|" + stringData[3] + "|" + stringData[4], _clients);
                break;
            
            case "ClientMsg":
                Broadcast("ServerMsg|" + client.clientName + " : " + stringData[1], _clients);
                break;
        }
    }
    
    #endregion

    #region UnityFunctions
    private void Update()
    {
        if (!_isServerStarted)
            return;

        foreach (ServerClient client in _clients)
        {
            // Is the client still connected?
            if (!IsConnected(client.tcpClient))
            {
                client.tcpClient.Close();
                _disconnectList.Add(client);
                continue;
            }
            else
            {
                NetworkStream stream = client.tcpClient.GetStream();
                if (stream.DataAvailable)
                {
                    StreamReader reader = new StreamReader(stream, true);
                    string data = reader.ReadLine();

                    if (data != null)
                        OnIncomingData(client, data);
                }
            }
        }

        for (int i = 0; i < _disconnectList.Count - 1; i++)
        {
            _clients.Remove(_disconnectList[i]);
            _disconnectList.RemoveAt(i);
        }
    }

    #endregion
}

public class ServerClient
{
    public string clientName;
    public TcpClient tcpClient;
    public bool isHost;

    public ServerClient(TcpClient tcp)
    {
        this.tcpClient = tcp;
    }
}
