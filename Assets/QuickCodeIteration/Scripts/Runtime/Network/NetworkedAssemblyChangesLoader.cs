using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using UnityEngine;
using LiteNetLib;
using QuickCodeIteration.Scripts.Runtime;
using Debug = UnityEngine.Debug;

[PreventHotReload]
public class NetworkedAssemblyChangesLoader : MonoBehaviour, INetEventListener
{
    private static readonly string LOG_PREFIX = $"{nameof(NetworkedAssemblyChangesLoader)}: ";

    private NetManager _netClient;

    [SerializeField] private bool _runInEditor;

    void Start()
    {

#if UNITY_EDITOR
        if(!_runInEditor) {
            Debug.Log("Receiver running in Editor - disabling");
            enabled = false;
            return;
        }
#endif
        
        _netClient = new NetManager(this);
        _netClient.UnconnectedMessagesEnabled = true;
        _netClient.UpdateTime = 15;
        _netClient.Start();
    }
    
    void Update()
    {
        _netClient.PollEvents();

        var peer = _netClient.FirstPeer;
        if (peer == null)
        {
            _netClient.SendBroadcast(new byte[] {1}, 5000);
        }
    }

    void OnDestroy()
    {
        if (_netClient != null)
            _netClient.Stop();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.Log($"{LOG_PREFIX}[CLIENT] connected to " + peer.EndPoint);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
    {
        Debug.Log($"{LOG_PREFIX}[CLIENT] received error " + socketErrorCode);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        //TODO: check how big assembly can be sent?
        var dllData = reader.Get<DllData>();
        if (dllData.RawData.Length > 0) //TODO: handle different data types?
        {
            var loadedAssembly = Assembly.Load(dllData.RawData);
            AssemblyChangesLoader.Instance.DynamicallyUpdateMethodsForCreatedAssembly(loadedAssembly);
        }
        else
        {
            Debug.LogWarning($"{LOG_PREFIX}Received data is not of {nameof(DllData)} type");
        }
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        if (messageType == UnconnectedMessageType.BasicMessage && _netClient.ConnectedPeersCount == 0 && reader.GetInt() == 1)
        {
            Debug.Log($"{LOG_PREFIX}[CLIENT] Received discovery response. Connecting to: " + remoteEndPoint);
            _netClient.Connect(remoteEndPoint, nameof(NetworkedAssemblyChangesLoader));
        }
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {

    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Debug.Log($"{LOG_PREFIX}[CLIENT] We disconnected because " + disconnectInfo.Reason);
    }
}