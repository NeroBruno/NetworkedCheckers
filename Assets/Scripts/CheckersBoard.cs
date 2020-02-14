using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CheckersBoard : MonoBehaviour
{
    #region Variables

    public static CheckersBoard Instance { get; set; }

    public GameObject highlightContainer;
    public Piece[,] pieces = new Piece[8, 8];
    public GameObject whitePiecePrefab;
    public GameObject blackPiecePrefab;
    public CanvasGroup alertCanvas;
    public Transform chatMessageContainer;
    public GameObject messagePrefab;
        
    private float _lastAlert;
    private bool _isAlertActive;
    private bool _gameIsOver;
    private float _winTime;
    
    private readonly Vector3 _boardOffset = new Vector3(-4f, 0, -4f);
    private readonly Vector3 _pieceOffset = new Vector3(0.5f, 0.10f, 0.5f);

    public bool isWhite;
    private bool _isWhiteTurn;
    private bool _hasKilled;
    
    private Piece _selectedPiece;
    private List<Piece> _forcedPieces;
    
    private Vector2 _startDragPosition;
    private Vector2 _endDragPosition;
    
    private Vector2 _mousePosition;

    private Client _client;

    #endregion

    #region Functions

    private void UpdateMousePosition()
    {
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 20.0f,
            LayerMask.GetMask("Board")))
        {
            _mousePosition.x = (int) (hit.point.x - _boardOffset.x);
            _mousePosition.y = (int) (hit.point.z - _boardOffset.z);
        }
        else
        {
            _mousePosition.x = -1;
            _mousePosition.y = -1;
        }
    }

    private void GenerateBoard()
    {
        //Generate White Pieces
        for (int y = 0; y < 3; y++)
        {
            bool isOddRow = (y % 2 == 0);

            for (int x = 0; x < 8; x += 2)
            {
                GeneratePiece(((isOddRow) ? x : x + 1), y);
            }
        }

        //Generate Black Pieces
        for (int y = 7; y > 4; y--)
        {
            bool isOddRow = (y % 2 == 0);

            for (int x = 0; x < 8; x += 2)
            {
                GeneratePiece(((isOddRow) ? x : x + 1), y);
            }
        }
    }

    private void GeneratePiece(int x, int y)
    {
        bool isPieceWhite = (y > 3) ? false : true;

        GameObject boardPiece = Instantiate((isPieceWhite) ? whitePiecePrefab : blackPiecePrefab) as GameObject;
        boardPiece.transform.SetParent(transform);
        Piece p = boardPiece.GetComponent<Piece>();
        pieces[x, y] = p;
        PlacePiece(p, x, y);
    }

    private void PlacePiece(Piece p, int x, int y)
    {
        p.transform.position = (Vector3.right * x) + (Vector3.forward * y) + _boardOffset + _pieceOffset;
    }

    private void SelectPiece(int x, int y)
    {
        // If out of bound
        if (x < 0 || x >= 8 || y < 0 || y >= 8)
            return;

        Piece p = pieces[x, y];

        if (p != null && p.isWhite == isWhite)
        {
            if (_forcedPieces.Count == 0)
            {
                _selectedPiece = p;
                _startDragPosition = _mousePosition;
            }
            else
            {
                // Look for pieces on the forced to move list
                if (_forcedPieces.Find(fp => fp == p) == null)
                    return;

                _selectedPiece = p;
                _startDragPosition = _mousePosition;
            }
        }
    }

    private void UpdatePieceDragPosition(Piece p)
    {
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 20f,
            LayerMask.GetMask("Board")))
        {
            p.transform.position = hit.point + Vector3.up;
        }
    }

    public void TryMove(int x1, int y1, int x2, int y2)
    {
        _forcedPieces = ScanForPossibleMove();
        
        // Multiplayer support
        _startDragPosition = new Vector2(x1, y1);
        _endDragPosition = new Vector2(x2, y2);
        _selectedPiece = pieces[x1, y1];
        
        //Out of bounds
        if (x2 < 0 || x2 >= 8 || y2 < 0 || y2 >= 8)
        {
            if (_selectedPiece != null)
                PlacePiece(_selectedPiece, x1, y1);
            
            _startDragPosition = Vector2.zero;
            _selectedPiece = null;
            Highlight();
            return;
        }

        // If there is a selected piece
        if (_selectedPiece != null)
        {
            // If it was not moved
            if (_endDragPosition == _startDragPosition)
            {
                PlacePiece(_selectedPiece, x1, y1);
                _startDragPosition = Vector2.zero;
                _selectedPiece = null;
                Highlight();
                return;
            }
            
            // Check if valid move
            if (_selectedPiece.ValidMove(pieces, x1, y1, x2, y2))
            {
                //Did we kill??
                //Is this a jump
                if (Mathf.Abs(x2 - x1) == 2)
                {
                    Piece p = pieces[(x1 + x2) / 2, (y1 + y2) / 2];
                    if (p != null)
                    {
                        pieces[(x1 + x2) / 2, (y1 + y2) / 2] = null;
                        DestroyImmediate(p.gameObject);
                        _hasKilled = true;
                    }
                }
                
                // Was I supposed to murder the shit out of anything??
                if (_forcedPieces.Count != 0 && !_hasKilled)
                {
                    PlacePiece(_selectedPiece, x1, y1);
                    _startDragPosition = Vector2.zero;
                    _selectedPiece = null;
                    Highlight();
                    return;
                }

                pieces[x2, y2] = _selectedPiece;
                pieces[x1, y1] = null;
                PlacePiece(_selectedPiece, x2, y2);

                EndTurn();
            }
            else
            {
                PlacePiece(_selectedPiece, x1, y1);
                _startDragPosition = Vector2.zero;
                _selectedPiece = null;
                Highlight();
                return;
            }
        }
    }

    private void EndTurn()
    {
        int x = (int)_endDragPosition.x;
        int y = (int)_endDragPosition.y;

        // King Upgrade
        if (_selectedPiece != null)
        {
            if (_selectedPiece.isWhite && !_selectedPiece.isKing && y == 7)
            {
                _selectedPiece.isKing = true;
                _selectedPiece.transform.Rotate(Vector3.right * 180);
            }
            else if (!_selectedPiece.isWhite && !_selectedPiece.isKing && y == 0)
            {
                _selectedPiece.isKing = true;
                _selectedPiece.transform.Rotate(Vector3.right * 180);
            }
        }

        string msg = "ClientMove|";
        msg += _startDragPosition.x.ToString() + "|";
        msg += _startDragPosition.y.ToString() + "|";
        msg += _endDragPosition.x.ToString() + "|";
        msg += _endDragPosition.y.ToString();
        
        _client.Send(msg);
        
        _selectedPiece = null;
        _startDragPosition = Vector2.zero;
        
        if (ScanForPossibleMove(_selectedPiece, x, y).Count != 0 && _hasKilled)
        {
            return;
        }

        _hasKilled = false;
        _isWhiteTurn = !_isWhiteTurn;
//        isWhite = !isWhite;
        CheckVictory();

        if (!_gameIsOver)
        {
            if (_isWhiteTurn)
                Alert(_client.players[0].name + "'s Turn");
            else
                Alert(_client.players[1].name + "'s Turn");
        }
        
        ScanForPossibleMove();
    }

    private void CheckVictory()
    {
        var ps = FindObjectsOfType<Piece>();
        bool hasWhite = false, hasBlack = false;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].isWhite)
                hasWhite = true;
            else
                hasBlack = true;
        }

        if (!hasWhite)
            Victory(false);
        if (!hasBlack)
            Victory(true);
    }

    private void Victory(bool isWhitePiece)
    {
        _winTime = Time.time;
        
        Alert(isWhitePiece ? "White player has won!" : "Black player has won!");

        _gameIsOver = true;
    }

    private List<Piece> ScanForPossibleMove(Piece p, int x, int y)
    {
        _forcedPieces = new List<Piece>();

        if (pieces[x, y].IsForcedToMove(pieces, x, y))
            _forcedPieces.Add(pieces[x, y]);
        
        Highlight();
        return _forcedPieces;
    }
    
    private List<Piece> ScanForPossibleMove()
    {
        _forcedPieces = new List<Piece>();
        
        //Check all the pieces
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (pieces[i, j] != null && pieces[i, j].isWhite == _isWhiteTurn)
                {
                    if (pieces[i,j].IsForcedToMove(pieces, i, j))
                        _forcedPieces.Add(pieces[i,j]);
                }
            }
        }

        Highlight();
        return _forcedPieces;
    }

    public void Alert(string msg)
    {
        alertCanvas.GetComponentInChildren<Text>().text = msg;
        alertCanvas.alpha = 1;
        _lastAlert = Time.time;
        _isAlertActive = true;
    }
    
    public void UpdateAlert()
    {
        if (_isAlertActive)
        {
            if (Time.time - _lastAlert > 1.5f)
            {
                alertCanvas.alpha = 1 - ((Time.time - _lastAlert) - 1.5f);
                
                if (Time.time - _lastAlert > 2.5f)
                {
                    _isAlertActive = false;
                }
            }
        }
    }

    private void Highlight()
    {
        foreach (Transform t in highlightContainer.transform)
        {
            t.position = Vector3.down * 5;
        }
        
        if (_forcedPieces.Count > 0)
            highlightContainer.transform.GetChild(0).transform.position = _forcedPieces[0].transform.position;
        if (_forcedPieces.Count > 1)
            highlightContainer.transform.GetChild(1).transform.position = _forcedPieces[1].transform.position;
        if (_forcedPieces.Count > 2)
            highlightContainer.transform.GetChild(2).transform.position = _forcedPieces[2].transform.position;
    }

    public void ChatMessage(string msg)
    {
        GameObject go = Instantiate(messagePrefab) as GameObject;
        go.transform.SetParent(chatMessageContainer);

        go.GetComponentInChildren<Text>().text = msg;
    }

    public void SendChatMessage()
    {
        InputField input = GameObject.Find("MessageInput").GetComponent<InputField>();
        
        if (input.text == "")
            return;
        
        _client.Send("ClientMsg|" + input.text);

        input.text = "";
    }
    
    #endregion

    #region UnityFunctions

    private void Start()
    {
        Instance = this;
        _gameIsOver = false;
        _client = FindObjectOfType<Client>();

        foreach (Transform t in highlightContainer.transform)
        {
            t.position = Vector3.down * 5;
        }
        
        Alert(_client.players[0].name + " VS " + _client.players[1].name);
        
        isWhite = _client.isHost;
        
        _isWhiteTurn = true;
        _forcedPieces = new List<Piece>();
        GenerateBoard();
    }

    private void Update()
    {
        if (_gameIsOver)
        {
            if (Time.time - _winTime > 2.0f)
            {
                Server server = FindObjectOfType<Server>();
                Client client = FindObjectOfType<Client>();
                
                if (server)
                    Destroy(server.gameObject);
                if (client)
                    Destroy(client.gameObject);

                SceneManager.LoadScene("Menu");
            }
            return;
        }
        
        foreach (Transform t in highlightContainer.transform)
        {
            t.Rotate(Vector3.up * 90 * Time.deltaTime);
        }
        
        UpdateAlert();
        UpdateMousePosition();

        if ((isWhite) ? _isWhiteTurn : !_isWhiteTurn)
        {
            int x = (int) _mousePosition.x;
            int y = (int) _mousePosition.y;

            if (_selectedPiece != null)
            {
                UpdatePieceDragPosition(_selectedPiece);
            }
            
            if (Input.GetMouseButtonDown(0))
            {
                SelectPiece(x, y);
            }

            if (Input.GetMouseButtonUp(0))
            {
                TryMove((int)_startDragPosition.x, (int)_startDragPosition.y, x, y);
            }
        }
    }

    #endregion
}