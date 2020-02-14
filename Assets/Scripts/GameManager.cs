using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    #region Variables

    public static GameManager Instance { get; set; }

    public GameObject mainMenu;
    public GameObject connectMenu;
    public GameObject hostMenu;

    public GameObject serverPrefab;
    public GameObject clientPrefab;

    public InputField nameInput;

    #endregion

    #region Functions

    public void Connect()
    {
        mainMenu.SetActive(false);
        connectMenu.SetActive(true);
    }

    public void Host()
    {
        try
        {
            Server s = Instantiate(serverPrefab).GetComponent<Server>();
            s.Init();
            
            Client c = Instantiate(clientPrefab).GetComponent<Client>();
            c.clientName = nameInput.text;
            c.isHost = true;
            if (c.clientName == "")
                c.clientName = "Host";
            c.ConnectToServer("localhost", 56789);
        }
        catch (Exception e)
        {
            Debug.Log("Error : " + e.Message);
        }
        
        mainMenu.SetActive(false);
        hostMenu.SetActive(true);
    }

    public void ConnectToServerButton()
    {
        string ipAdress = GameObject.Find("IPInput").GetComponent<InputField>().text;
        if (ipAdress == "")
            ipAdress = "localhost";

        try
        {
            Client c = Instantiate(clientPrefab).GetComponent<Client>();
            c.clientName = nameInput.text;
            if (c.clientName == "")
                c.clientName = "Client";
            c.ConnectToServer(ipAdress, 56789);
            connectMenu.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.Log("Error : " + e.Message);
        }
    }

    public void BackButton()
    {
        connectMenu.SetActive(false);
        hostMenu.SetActive(false);
        mainMenu.SetActive(true);

        Server s = FindObjectOfType<Server>();
        if (s != null)
            Destroy(s.gameObject);

        Client c = FindObjectOfType<Client>();
        if (c != null)
            Destroy(c.gameObject);
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Game");
    }

    #endregion

    #region UnityFunctions

    private void Start()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #endregion
    
}
