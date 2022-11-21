using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using UnityEngine;
using LiteNetLib;
using QuickCodeIteration.Scripts.Runtime;

public class CompiledDllReceiver : MonoBehaviour, INetEventListener
{
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

    [ContextMenu(nameof(LoadDll))] //TODO: move away to separate class
    private void LoadDll(byte[] dllBytes)
    {
        var loadedAssembly = Assembly.Load(dllBytes);
        // var t = asm.GetType("TestDynamicCompileChangeScale");
        //     
        // var instance = Activator.CreateInstance(t);
        // t.GetMethod("ChangeScale", BindingFlags.Instance | BindingFlags.Public)
        //     .Invoke(instance, null);
        AssemblyChangesLoader.DynamicallyUpdateMethodsForCreatedAssembly(loadedAssembly);
    }

    void Update()
    {
        _netClient.PollEvents();

        var peer = _netClient.FirstPeer;
        // if (peer != null && peer.ConnectionState == ConnectionState.Connected)
        // {
        //     //Fixed delta set to 0.05
        //     var pos = _clientBallInterpolated.transform.position;
        //     pos.x = Mathf.Lerp(_oldBallPosX, _newBallPosX, _lerpTime);
        //     _clientBallInterpolated.transform.position = pos;
        //
        //     //Basic lerp
        //     _lerpTime += Time.deltaTime / Time.fixedDeltaTime;
        // }
        // else
        // {
        if (peer == null)
        {
            _netClient.SendBroadcast(new byte[] {1}, 5000);
        }

        // }
    }

    void OnDestroy()
    {
        if (_netClient != null)
            _netClient.Stop();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.Log("[CLIENT] We connected to " + peer.EndPoint);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
    {
        Debug.Log("[CLIENT] We received error " + socketErrorCode);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        var dll = reader.Get<DllData>();
        LoadDll(dll.RawData);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        if (messageType == UnconnectedMessageType.BasicMessage && _netClient.ConnectedPeersCount == 0 && reader.GetInt() == 1)
        {
            Debug.Log("[CLIENT] Received discovery response. Connecting to: " + remoteEndPoint);
            _netClient.Connect(remoteEndPoint, "sample_app");
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
        Debug.Log("[CLIENT] We disconnected because " + disconnectInfo.Reason);
    }
}