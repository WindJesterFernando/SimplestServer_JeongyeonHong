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
    int[] MatchID = new int[2];
    int playerWaitingForMatchWithID = -1;

    int TicTacToeMatchCount = 0;
    int[] TicTacToeMatchID = new int[2];
    int[] TicTacToeCheck = new int[9];

    LinkedList<GameRoom> GameRooms;
    LinkedList<GameRoom> GameRoomsTicTacToe;

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
        GameRoomsTicTacToe = new LinkedList<GameRoom>();

        for(int i = 0;i < 9; ++i)
        {
            TicTacToeCheck[i] = 0;
        }
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

        if (signifier == ClientToServerSignifiers.CreateAccount)
        {
            Debug.Log("Create Account");

            string n = csv[1];
            string p = csv[2];
            bool nameIsInUse = false;

            foreach (PlayerAccount pa in playerAccounts)
            {
                if (pa.name == n)
                {
                    nameIsInUse = true;
                    break;
                }
            }

            if (nameIsInUse)
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
        else if (signifier == ClientToServerSignifiers.Login)
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

                        MatchID[MatchCount] = id;
                        ++MatchCount;

                        if (MatchCount == 2)
                        {
                            GameRoom gr = new GameRoom(MatchID[0], MatchID[1]);
                            GameRooms.AddLast(gr);

                            SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID2);
                            SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.playerID1);

                            MatchCount = 0;
                        }
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                        msgHasBeenSentToClient = true;
                    }
                }
            }

            if (!hasNameBeenFound)
            {
                if (!msgHasBeenSentToClient)
                {
                    SendMessageToClient(ServerToClientSignifiers.LoginFailed + "", id);
                }
            }
        }

        else if (signifier == ClientToServerSignifiers.ObserverLogin)
        {
            Debug.Log("We need to get this player into a waiting queue!");

            foreach (GameRoom gr in GameRooms)
            {
                if (gr.observer == -1)
                {
                    gr.observer = id;
                    SendMessageToClient(ServerToClientSignifiers.GameStart + "", gr.observer);
                    break;
                }
            }
        }

        else if (signifier == ClientToServerSignifiers.ChatMsg)
        {
            GameRoom gr = GetGameRoomWithClientID(id);
            if (gr != null)
            {
                if (gr.playerID1 == id)
                {
                    Debug.Log("ID 1");
                    SendMessageToClient(ServerToClientSignifiers.ChatMsg + ", " + csv[1], gr.playerID2);

                    if (gr.observer != -1)
                        SendMessageToClient(ServerToClientSignifiers.ChatMsg + ", " + csv[1], gr.observer);
                }
                else
                {
                    Debug.Log("ID 2");
                    SendMessageToClient(ServerToClientSignifiers.ChatMsg + ", " + csv[1], gr.playerID1);

                    if (gr.observer != -1)
                        SendMessageToClient(ServerToClientSignifiers.ChatMsg + ", " + csv[1], gr.observer);
                }
            }
        }

        else if (signifier == ClientToServerSignifiers.TicTacToeSomethingPlay)
        {
            GameRoom gr = GetGameRoomTicTacToeWithClientID(id);
            Debug.Log("TicPlay");
            if (gr != null)
            {
                int Number = int.Parse(csv[1]);

                if (gr.playerID1 == id)
                {
                    Debug.Log("ID 1");
                    TicTacToeCheck[Number] = 1;
                    SendMessageToClient(ServerToClientSignifiers.TicTacToePlay + ", " + Number.ToString(), gr.playerID2);

                    //if (gr.observer != -1)
                    //    SendMessageToClient(ServerToClientSignifiers.TicTacToePlay + ", " + Number.ToString(), gr.observer);
                }
                else
                {
                    Debug.Log("ID 2");
                    TicTacToeCheck[Number] = 2;
                    SendMessageToClient(ServerToClientSignifiers.TicTacToePlay + ", " + Number.ToString(), gr.playerID1);

                    //if (gr.observer != -1)
                    //    SendMessageToClient(ServerToClientSignifiers.TicTacToePlay + ", " + Number.ToString(), gr.observer);
                }

                //for (int i = 0; i < 3; ++i)
                //{
                //    int Player1XCount = 0;
                //    int Player2XCount = 0;

                //    int Player1YCount = 0;
                //    int Player2YCount = 0;
                //    for (int j = 0; j < 3; ++j)
                //    {
                //        if (TicTacToeCheck[i * 3 + j] == 1)
                //            ++Player1XCount;

                //        else if (TicTacToeCheck[i * 3 + j] == 2)
                //            ++Player2XCount;

                //        if (TicTacToeCheck[j * 3 + i] == 1)
                //            ++Player1YCount;

                //        else if (TicTacToeCheck[j * 3 + i] == 2)
                //            ++Player2YCount;
                //    }

                //    if (Player1XCount == 3)
                //    {
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID1);
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "1", gr.observer);
                //    }

                //    else if (Player1YCount == 3)
                //    {
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID1);
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "1", gr.observer);
                //    }

                //    else if (Player2XCount == 3)
                //    {
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID2);
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "2", gr.observer);
                //    }

                //    else if (Player2YCount == 3)
                //    {
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID2);
                //        SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "2", gr.observer);
                //    }
                //}

                //if (TicTacToeCheck[0] == 1 && TicTacToeCheck[4] == 1 && TicTacToeCheck[8] == 1)
                //{
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID1);
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "1", gr.observer);
                //}

                //else if (TicTacToeCheck[0] == 2 && TicTacToeCheck[4] == 2 && TicTacToeCheck[8] == 2)
                //{
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID1);
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "1", gr.observer);
                //}

                //else if (TicTacToeCheck[2] == 1 && TicTacToeCheck[4] == 1 && TicTacToeCheck[6] == 1)
                //{
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID2);
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "2", gr.observer);
                //}

                //else if (TicTacToeCheck[2] == 2 && TicTacToeCheck[4] == 2 && TicTacToeCheck[6] == 2)
                //{
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "", gr.playerID2);
                //    SendMessageToClient(ServerToClientSignifiers.TicTacToeWin + "2", gr.observer);
                //}
            }
        }

        else if (signifier == ClientToServerSignifiers.ChatBack)
        {
            GameRoom gr = GetGameRoomWithClientID(id);

            if (gr != null)
            {
                if (gr.playerID1 == id)
                    gr.playerID1 = 0;

                else
                    gr.playerID2 = 0;

                if (gr.playerID1 == 0 && gr.playerID2 == 0)
                    GameRooms.Remove(gr);
            }
        }

        else if (signifier == ClientToServerSignifiers.TicTacToeIn)
        {
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
                        SendMessageToClient(ServerToClientSignifiers.TicTacToeLoginComplete + "", id);
                        msgHasBeenSentToClient = true;

                        Debug.Log("Login Complete");

                        TicTacToeMatchID[TicTacToeMatchCount] = id;
                        ++TicTacToeMatchCount;

                        if (TicTacToeMatchCount == 2)
                        {
                            //GameRoom gr = GetGameRoomTicTacToeWithClientID(id);

                            //if (gr != null)
                            //{
                            //    gr.playerID1 = TicTacToeMatchID[0];
                            //    gr.playerID2 = TicTacToeMatchID[1];
                            //}

                            //else
                            //{
                            //    gr = new GameRoom(TicTacToeMatchID[0], TicTacToeMatchID[1]);
                            //    GameRoomsTicTacToe.AddLast(gr);
                            //}

                            GameRoom gr = new GameRoom(TicTacToeMatchID[0], TicTacToeMatchID[1]);
                            GameRoomsTicTacToe.AddLast(gr);

                            SendMessageToClient(ServerToClientSignifiers.TicTacToeGameStart + "", gr.playerID2);
                            SendMessageToClient(ServerToClientSignifiers.TicTacToeOwner + "", gr.playerID1);

                            TicTacToeMatchCount = 0;
                            break;
                        }
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.TicTacToeLoginFailed + "", id);
                        msgHasBeenSentToClient = true;
                    }
                }
            }

            if (!hasNameBeenFound)
            {
                if (!msgHasBeenSentToClient)
                {
                    SendMessageToClient(ServerToClientSignifiers.TicTacToeLoginFailed + "", id);
                }
            }
        }

        else if (signifier == ClientToServerSignifiers.TicTacToeOut)
        {
            GameRoom gr = GetGameRoomTicTacToeWithClientID(id);

            if (gr != null)
            {
                if (gr.playerID1 == id)
                    gr.playerID1 = -1;

                else if (gr.observer == id)
                    gr.observer = -1;

                else
                    gr.playerID2 = -1;

                if (gr.playerID1 == -1 && gr.playerID2 == -1 && gr.observer == -1)
                    GameRoomsTicTacToe.Remove(gr);
            }
        }

        else if (signifier == ClientToServerSignifiers.TicTacToeObserverIn)
        {
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
                        GameRoom gr = GetGameRoomTicTacToeWithClientID(id);

                        if (gr != null)
                        {
                            gr.observer = id;
                        }

                        else
                        {
                            gr = new GameRoom();
                            GameRoomsTicTacToe.AddLast(gr);
                            gr.observer = id;
                        }

                        SendMessageToClient(ServerToClientSignifiers.TicTacToeLoginComplete + "", id);
                        msgHasBeenSentToClient = true;
                    }
                    else
                    {
                        SendMessageToClient(ServerToClientSignifiers.TicTacToeLoginFailed + "", id);
                        msgHasBeenSentToClient = true;
                    }
                }
            }

            if (!hasNameBeenFound)
            {
                if (!msgHasBeenSentToClient)
                {
                    SendMessageToClient(ServerToClientSignifiers.TicTacToeLoginFailed + "", id);
                }
            }
        }

        else if (signifier == ClientToServerSignifiers.TicTacToeObserverOut)
        {
            GameRoom gr = GetGameRoomTicTacToeWithClientID(id);

            if (gr != null)
            {
                gr.observer = -1;
            }
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
            if (gr.playerID1 == id || gr.playerID2 == id || gr.observer == id)
                return gr;
        }
        return null;
    }

    private GameRoom GetGameRoomTicTacToeWithClientID(int id)
    {
        foreach (GameRoom gr in GameRoomsTicTacToe)
        {
            if (gr.playerID1 == id || gr.playerID2 == id || gr.observer == id)
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
    public int  playerID1 = -1, playerID2 = -1, observer = -1;

    public GameRoom()
    {
    }
    public GameRoom(int PlayerID1, int PlayerID2)
    {
        playerID1 = PlayerID1;
        playerID2 = PlayerID2;
    }


    public void SetObserver(int ob)
    {
        observer = ob;
    }

}

static public class ClientToServerSignifiers
{
    public const int CreateAccount = 1;
    public const int Login = 2;
    public const int ChatMsg = 3;
    public const int TicTacToeSomethingPlay = 4;
    public const int ObserverLogin = 5;
    public const int ChatBack = 6;
    public const int TicTacToeIn = 7;
    public const int TicTacToeOut = 8;
    public const int TicTacToeObserverIn = 9;
    public const int TicTacToeObserverOut = 10;
}

static public class ServerToClientSignifiers
{
    public const int LoginComplete = 1;
    public const int LoginFailed = 2;
    public const int AccountCreationComplete = 3;
    public const int AccountCreationFailed = 4;
    public const int GameStart = 5;
    public const int ChatMsg = 6;
    public const int TicTacToePlay = 7;
    public const int TicTacToeGameStart = 8;
    public const int TicTacToeOwner = 9;
    public const int TicTacToeWin = 10;
    public const int TicTacToeLoginComplete = 11;
    public const int TicTacToeLoginFailed = 12;
}
