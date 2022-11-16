using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

public class CompiledDllSender : MonoBehaviour
#if UNITY_EDITOR
    , INetEventListener, INetLogger
#endif
{
#if UNITY_EDITOR
    private NetManager _netServer;
    private NetPeer _ourPeer;
    private NetDataWriter _dataWriter;
    
    public static CompiledDllSender Instance { get; private set; } 

    void Start()
    {
        Instance = this; //TODO: make sure just one
        
        NetDebug.Logger = this;
        _dataWriter = new NetDataWriter();
        _netServer = new NetManager(this);
        _netServer.Start(5000);
        _netServer.BroadcastReceiveEnabled = true;
        _netServer.UpdateTime = 15;
    }
    
    void Update()
    {
        _netServer.PollEvents();
    }

    [ContextMenu(nameof(SendDllTest))]
    void SendDllTest()
    {
        if (_ourPeer != null)
        {
            _dataWriter.Reset();
            _dataWriter.Put(new DllData(File.ReadAllBytes(@"F:\_src\!Temp\TestDynamicCompile.dll")));
            _ourPeer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
        }
    }

    public void SendDll(byte[] assemblyBytes)
    {
        if (_ourPeer != null)
        {
            _dataWriter.Reset();
            _dataWriter.Put(new DllData(assemblyBytes));
            _ourPeer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
        }
    }

    // void FixedUpdate()
    // {
    //     if (_ourPeer != null)
    //     {
    //         _dataWriter.Reset();
    //         _dataWriter.Put(new DllData(File.ReadAllBytes(@"F:\_src\!Temp\TestDynamicCompile.dll"));
    //         _ourPeer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
    //     }
    // }

    void OnDestroy()
    {
        NetDebug.Logger = null;
        if (_netServer != null)
            _netServer.Stop();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.Log("[SERVER] We have new peer " + peer.EndPoint);
        _ourPeer = peer;
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
    {
        Debug.Log("[SERVER] error " + socketErrorCode);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
        if (messageType == UnconnectedMessageType.Broadcast)
        {
            Debug.Log("[SERVER] Received discovery request. Send discovery response");
            NetDataWriter resp = new NetDataWriter();
            resp.Put(1);
            _netServer.SendUnconnectedMessage(resp, remoteEndPoint);
        }
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("sample_app");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Debug.Log("[SERVER] peer disconnected " + peer.EndPoint + ", info: " + disconnectInfo.Reason);
        if (peer == _ourPeer)
            _ourPeer = null;
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
    }

    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        Debug.LogFormat(str, args);
    }
#endif
}

[Serializable]
public struct DllData: INetSerializable
{
    public byte[] RawData;

    public DllData(byte[] rawData)
    {
        RawData = rawData;
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.PutBytesWithLength(RawData);
    }

    public void Deserialize(NetDataReader reader)
    {
        RawData = reader.GetBytesWithLength();
    }
}