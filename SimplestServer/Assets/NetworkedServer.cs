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

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);
        
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
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        //Debug.Log("msg recieved = " + msg + ".  connection id = " + id);
        m_ChatText.text += id + " : " + msg + "\n";
    }

}
