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

    const int PlayerAccountNameAndPassword = 1;

    string playerAccountsFilePath;

    int MatchCount = 0;
    int ObserverIndex = -1;
    int[] MatchID = new int[3];
    int playerWaitingForMatchWithID = -1;

    LinkedList<GameRoom> GameRooms;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccountsFilePath = Application.dataPath + Path.DirectorySeparatorChar + "PlayerAccounts.txt";
        playerAccounts = new LinkedList<PlayerAccount>();

        MatchCount = 0;


        LoadPlayerAccounts();

        //foreach (PlayerAccount pa in playerAccounts)
        //    Debug.Log(pa.name + " " + pa.password);

        GameRooms = new LinkedList<GameRoom>();
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
                //m_ConnectionList.Add(recConnectionID);

                //if (m_ConnectionList.Count == 2)
                //{
                //    int Index = Random.Range(0, 2);

                //    SendMessageToClient("Owner", m_ConnectionList[Index]);
                //}
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);

                ProcessRecievedMsg(msg, recConnectionID);

                //if (msg == "Observer")
                //{
                //    m_ObserverID = recConnectionID;
                //}

                //else
                //{
                //    for (int i = 0; i < m_ConnectionList.Count; ++i)
                //    {
                //        if (m_ConnectionList[i] == recConnectionID)
                //            continue;

                //        SendMessageToClient(msg, m_ConnectionList[i]);
                //    }
                //}
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                //for (int i = 0; i < m_ConnectionList.Count; ++i)
                //{
                //    if(m_ConnectionList[i] == recConnectionID)
                //    {
                //        m_ConnectionList.RemoveAt(i);
                //        break;
                //    }
                //}
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
        //m_ChatText.text += id + " : " + msg + "\n";

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

                SavePlayerAccounts();
            }
        }
        else if(signifier == ClientToServerSignifiers.Login)
        {
            Debug.Log("Login to Account");

            string n = csv[1];
            string p = csv[2];
            bool hasNameBeenFound = false;
            bool msgHasBeenSentToClient = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    hasNameBeenFound = true;
                    

                    if (pa.password == p)
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginComplete + "", id);
                        msgHasBeenSentToClient = true;
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                        msgHasBeenSentToClient = true;
                    }
                }
            }

            if(!hasNameBeenFound)
            {
                if(!msgHasBeenSentToClient)
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                }
            }
        }

        else if (signifier == ClientToServerSignifiers.ObserverJoin)
        {
            Debug.Log("We need to get this player into a waiting queue!");

            MatchID[MatchCount] = id;
            ObserverIndex = MatchCount;
            ++MatchCount;

            if (MatchCount == 3)
            {
                int[] WaitID = new int[2];
                int WaitIndex = 0;

                for (int i = 0; i < 3; ++i)
                {
                    if (i != ObserverIndex)
                    {
                        WaitID[WaitIndex] = MatchID[i];
                        ++WaitIndex;
                    }
                }

                GameRoom gr = new GameRoom(WaitID[0], WaitID[1], MatchID[ObserverIndex]);
                GameRooms.AddLast(gr);

                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID2);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID1);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.observer);

                ObserverIndex = -1;
                MatchCount = 0;
            }
        }

        else if (signifier == ClientToServerSignifiers.JoinQueueForGameRoom)
        {
            Debug.Log("We need to get this player into a waiting queue!");

            MatchID[MatchCount] = id;
            ++MatchCount;

            if (MatchCount == 3)
            {
                int[] WaitID = new int[2];
                int WaitIndex = 0;

                for(int i = 0; i < 3; ++i)
                {
                    if (i != ObserverIndex)
                    {
                        WaitID[WaitIndex] = MatchID[i];
                        ++WaitIndex;
                    }
                }

                GameRoom gr = new GameRoom(WaitID[0], WaitID[1], MatchID[ObserverIndex]);
                GameRooms.AddLast(gr);

                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID2);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID1);
                SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.observer);

                ObserverIndex = -1;
                MatchCount = 0;
            }
        }
        else if (signifier == ClientToServerSignifiers.TicTacToeSomethingPlay)
        {
            GameRoom gr = GetGameRoomWithClientID(id);
            if (gr != null)
            {
                if (gr.playerID1 == id)
                {
                    SendMessageToClient(ServerToClientSignifiers.Msg + ", " + csv[1], gr.playerID2);
                }
                else
                {
                    SendMessageToClient(ServerToClientSignifiers.Msg + ", " + csv[1], gr.playerID1);
                }
            }
            // we need to get the game room that the client ID is in
        }
    }

    private void SavePlayerAccounts()
    {
        StreamWriter sw = new StreamWriter(playerAccountsFilePath);

        foreach(PlayerAccount pa in playerAccounts)
        {
            sw.WriteLine(PlayerAccountNameAndPassword + ", " + pa.name + ", " + pa.password);
        }

        sw.Close();
    }

    private void LoadPlayerAccounts()
    {
        if (File.Exists(playerAccountsFilePath))
        {

            StreamReader sr = new StreamReader(playerAccountsFilePath);

            string line;

            while (true)
            {
                line = sr.ReadLine();

                if (line == null)
                    break;

                string[] csv = line.Split(',');

                int signifier = int.Parse(csv[0]);

                if (signifier == PlayerAccountNameAndPassword)
                {
                    PlayerAccount pa = new PlayerAccount(csv[1], csv[2]);
                    playerAccounts.AddLast(pa);
                }
            }

            sr.Close();
        }
    }

    private GameRoom GetGameRoomWithClientID(int id)
    {
        foreach (GameRoom gr in GameRooms)
        {
            if (gr.playerID1 == id || gr.playerID2 == id)
                return gr;
        }
        return null;
    }

}

public class PlayerAccount
{
    public string name, password;

    public PlayerAccount(string Name, string Password)
    {
        name = Name;
        password = Password;
    }
}

public class GameRoom
{
    public int  playerID1, playerID2, observer;
    public GameRoom(int PlayerID1, int PlayerID2, int Observer)
    {
        playerID1 = PlayerID1;
        playerID2 = PlayerID2;
        observer = Observer;
    }


}

static public class ClientToServerSignifiers
{
    public const int CreateAccount = 1;
    public const int Login = 2;
    public const int JoinQueueForGameRoom = 3;
    public const int TicTacToeSomethingPlay = 4;
    public const int ObserverJoin = 5;
}

static public class ServerToClientSignifiers
{
    public const int LoginComplete = 1;
    public const int LoginFailed = 2;
    public const int AccountCreationComplete = 3;
    public const int AccountCreationFailed = 4;
    public const int OpponentPlay = 5;
    public const int GameStart = 6;
    public const int Msg = 7;
    public const int Owner = 8;
}
