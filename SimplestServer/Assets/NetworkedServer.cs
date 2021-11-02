using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;
    List<int> m_ConnectionList = new List<int>();
    public Text m_ChatText = null;
    int m_ObserverID = 0;

    LinkedList<PlayerAccount> playerAccounts;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccounts = new LinkedList<PlayerAccount>();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                m_ConnectionList.Add(recConnectionID);

                if (m_ConnectionList.Count == 2)
                {
                    int Index = Random.Range(0, 2);

                    SendMessageToClient("Owner", m_ConnectionList[Index]);
                }
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);

                ProcessRecievedMsg(msg, recConnectionID);

                if (msg == "Observer")
                {
                    m_ObserverID = recConnectionID;
                }

                else
                {
                    for (int i = 0; i < m_ConnectionList.Count; ++i)
                    {
                        if (m_ConnectionList[i] == recConnectionID)
                            continue;

                        SendMessageToClient(msg, m_ConnectionList[i]);
                    }
                }
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                for (int i = 0; i < m_ConnectionList.Count; ++i)
                {
                    if(m_ConnectionList[i] == recConnectionID)
                    {
                        m_ConnectionList.RemoveAt(i);
                        break;
                    }
                }
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);

        if (error != 0)
            Debug.Log("DUDE, went wrong on send");
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        //Debug.Log("msg recieved = " + msg + ".  connection id = " + id);
        m_ChatText.text += id + " : " + msg + "\n";

        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if(signifier == ClientToServerSignifiers.CreateAccount)
        {
            Debug.Log("Create Account");

            string n = csv[1];
            string p = csv[2];
            bool nameIsInUse = false;

            foreach(PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                { 
                    nameIsInUse = true;
                    break;
                }
            }

            if(nameIsInUse)
            {
                SendMessageToClient(ServerToClientSignifiers.AccountCreationFailed + "", id);
            }
            else
            {
                PlayerAccount newPlayerAccount = new PlayerAccount(n, p);

                playerAccounts.AddLast(newPlayerAccount);
                SendMessageToClient(ServerToClientSignifiers.AccountCreationComplete + "", id);
            }
        }
        else if(signifier == ClientToServerSignifiers.Login)
        {
            Debug.Log("Login to Account");
        }
    }

    public class PlayerAccount
    {
        public string name, password;

        public PlayerAccount(string Name, string Password)
        {
            name = Name;
            password = password;
        }
    }

    static public class ClientToServerSignifiers
    {
        public const int CreateAccount = 1;
        public const int Login = 2;
    }

    static public class ServerToClientSignifiers
    {
        public const int LoginComplete = 1;
        public const int LoginFailed = 2;
        public const int AccountCreationComplete = 3;
        public const int AccountCreationFailed = 4;
    }

}
