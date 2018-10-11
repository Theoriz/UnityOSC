using System;
using System.Net;
using UnityEngine;
using System.Collections.Generic;
using UnityOSC;


public class OSCMaster : MonoBehaviour
{
    private static OSCMaster _instance;
    public static OSCMaster Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameObject("OSCMaster").AddComponent<OSCMaster>();
            }

            return _instance;
        }
        set
        {
            _instance = value;
        }
    }

    public static Dictionary<string, OSCReceiver> Receivers;
    public static Dictionary<string, OSCClient> Clients;

    public bool ShowDebug;

    public bool LogIncoming;
    public bool LogOutgoing;

    void Awake()
    {
        Instance = this;
        Receivers = new Dictionary<string, OSCReceiver>();
        Clients = new Dictionary<string, OSCClient>();
    }

    //public void Connect()
    //{
    //    try
    //    {
    //        if(server != null)
    //            server.Close();

    //        server = new OSCReceiver();
    //        server.Open(localPort);

    //        isConnected = true;
    //    }
    //    catch (Exception e)
    //    {
    //        Debug.LogError("Error with port " + localPort);
    //        Debug.LogWarning(e.StackTrace);
    //        isConnected = false;
    //        server = null;
    //    }
    //}

    //void packetReceived(OSCServer server, OSCPacket p)
    //{
    //    if (logIncoming)
    //        Debug.Log("Received : " + p.Address + p.Data + " from : " + server.LocalPort);

    //    if (p.IsBundle())
    //    {
    //        foreach (OSCMessage m in p.Data)
    //        {
    //            processMessage(m);
    //        }
    //    }else processMessage((OSCMessage)p);
    //   // Debug.Log("Packet processed");
    //}

    // void processMessage(OSCMessage m)
    // {   
    //     if(logIncoming)
    //            Debug.Log("Received : " + m.Address + " " + m.Data);

    //    string[] addressSplit = m.Address.Split(new char[] { '/' });

    //         Debug.Log(addressSplit.Length);

    //     if (addressSplit.Length == 1 || addressSplit[1] != "OCF") //If length == 1 then it's not an OSC address, don't process it but propagate anyway
    //     {
    //if (messageAvailable != null)
    //             messageAvailable(m); //propagate the message
    //     }
    //     else //Starts with /OCF/ so it's control
    //     {
    //string target = "";
    //string property = "";
    //try {
    //	target = addressSplit[2];
    //	property = addressSplit[3];
    //}
    //catch(Exception e) {
    //	Debug.LogWarning("Error parsing OCF command ! ");
    //}

    //if (logIncoming) Debug.Log("Message received for Target : " + target + ", property = " + property);

    //         ControllableMaster.UpdateValue(target, property, m.Data);
    //     }
    // }

    private void Update()
    {
        foreach(var receiver in Receivers)
        {
            while (receiver.Value.HasWaitingMessage()) //Allow to switch from receiver/server thread to main thread
                receiver.Value.PropagateEvent();
        }
    }

    public static void CreateClient(string clientId, IPAddress destination, int port)
    {
        var client = new OSCClient(destination, port)
        {
            Name = clientId
        };

        Clients.Add(clientId, client);

        if (Instance.ShowDebug)
            Debug.Log("Client " + clientId + " on " + destination + ":" + port + "created.");
    }

    public static void RemoveClient(string clientId)
    {
        Clients[clientId].Close();
        Clients.Remove(clientId);

        if (Instance.ShowDebug)
            Debug.Log("Client " + clientId + " removed.");
    }

    public static OSCReceiver CreateReceiver(string receiverId, int port)
    {
        var receiver = new OSCReceiver
        {
            Name = receiverId
        };

        if(!receiver.Open(port))
        {
            return null;
        }

        Receivers.Add(receiverId, receiver);

        if (Instance.ShowDebug)
            Debug.Log("Receiver " + receiverId + " on " + port + " created.");

        return receiver;
    }

    public static void RemoveReceiver(string receiverId)
    {
        Receivers[receiverId].Close();
        Receivers.Remove(receiverId);

        if (Instance.ShowDebug)
            Debug.Log("Receiver " + receiverId + " removed.");
    }

    public static void SendMessageUsingClient(string clientId, OSCMessage msg)
    {
        Clients[clientId].Send(msg);

        if (Instance.LogOutgoing)
        {
            Debug.Log("[" + clientId + "|" + DateTime.Now.ToLocalTime() + "] " + msg.Address);
            foreach (var data in msg.Data)
                Debug.Log(data);
        }
    }

    public static void SendMessage(OSCMessage m, string host, int port)
    {
        if (Instance.LogOutgoing)
        {
            string args = "";
            for (int i = 0; i < m.Data.Count; i++)
                args += (i > 0 ? ", " : "") + m.Data[i].ToString();

            Debug.Log("[OSCMaster | " + DateTime.Now.ToLocalTime() + "] " + m.Address + " : " + args);
        }

        var tempClient = new OSCClient(System.Net.IPAddress.Loopback, port);
        tempClient.SendTo(m, host, port);
        tempClient.Close();
    }


    void OnApplicationQuit()
    {
        foreach (var pair in Clients)
        {
            pair.Value.Close();
        }

        foreach (var pair in Receivers)
        {
            pair.Value.Close();
        }

        _instance = null;
    }
}
