using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

public class Client : MonoBehaviour
{
    #region Variables
    
    public string clientName;
    public bool isHost = false;
    
    private bool _isSocketReady;
    private TcpClient _socket;
    private NetworkStream _stream;
    private StreamWriter _writer;
    private StreamReader _reader;

    public List<GameClient> players = new List<GameClient>();

    #endregion

    #region Functions

    public bool ConnectToServer(string host, int port)
    {
        if (_isSocketReady)
            return false;

        try
        {
            _socket = new TcpClient(host, port);
            _stream = _socket.GetStream();
            _writer = new StreamWriter(_stream);
            _reader = new StreamReader(_stream);

            _isSocketReady = true;
        }
        catch (Exception e)
        {
            Debug.Log("Socket error : " + e.Message);
        }

        return _isSocketReady;
    }
    
    // Read messages from server
    private void OnIncomingData(string data)
    {
        Debug.Log("Client:" + data);
        string[] stringData = data.Split('|');

        switch (stringData[0])
        {
            case "ServerWho":
                for (int i = 1; i < stringData.Length - 1; i++)
                {
                    UserConnected(stringData[i]);
                }
                Send("ClientWho|" + clientName + '|' + ((isHost) ? 1 : 0).ToString());
                break;
            
            case "ServerConnection":
                UserConnected(stringData[1]);
                break;
            
            case "ServerMove":
                CheckersBoard.Instance.TryMove(int.Parse(stringData[1]), int.Parse(stringData[2]), int.Parse(stringData[3]), int.Parse(stringData[4]));
                break;
            
            case "ServerMsg":
                CheckersBoard.Instance.ChatMessage(stringData[1]);
                break;
        }
    }

    private void UserConnected(string name)
    {
        GameClient c = new GameClient();
        c.name = name;
        
        players.Add(c);
        
        if (players.Count == 2)
            GameManager.Instance.StartGame();
    }
    
    // Send messages to server
    public void Send(string data)
    {
        if (!_isSocketReady)
            return;
        
        _writer.WriteLine(data);
        _writer.Flush();
    }

    private void CloseSocket()
    {
        if (!_isSocketReady)
            return;
        
        _writer.Close();
        _reader.Close();
        _socket.Close();
        _isSocketReady = false;
    }

    #endregion

    #region UnityFunctions

    private void OnApplicationQuit()
    {
        CloseSocket();
    }

    private void OnDisable()
    {
        CloseSocket();
    }
    
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (_isSocketReady)
        {
            if (_stream.DataAvailable)
            {
                string data = _reader.ReadLine();
                if (data != null)
                    OnIncomingData(data);
            }
        }
    }

    #endregion
    
}

public class GameClient
{
    public string name;
    public bool isHost;
}