using System;
using LiteNetLib;
using LiteNetLib.Utils;
using Silk.NET.Maths;

namespace ERus.Engine.Network.Core;

public class NetworkPacketDispatcher
{
    private readonly NetPacketProcessor _packetProcessor = new NetPacketProcessor();
    private readonly NetworkTransport _transport;

    public NetworkPacketDispatcher(NetworkTransport transport)
    {
        _transport = transport;
        _transport.OnNetworkReceiveEvent += OnNetworkReceive;

        // Registrar tipos base para que o LiteNetLib consiga serializá-los
        _packetProcessor.RegisterNestedType<Vector3D<float>>(
            (writer, vector) => { writer.Put(vector.X); writer.Put(vector.Y); writer.Put(vector.Z); },
            reader => new Vector3D<float>(reader.GetFloat(), reader.GetFloat(), reader.GetFloat())
        );

        _packetProcessor.RegisterNestedType<ERus.Engine.Network.Packets.Events.ScriptPacketData>(
            (writer, data) => {
                writer.Put(data.ScriptTypeName);
                writer.Put(data.FieldValues.Count);
                foreach (var kvp in data.FieldValues)
                {
                    writer.Put(kvp.Key);
                    writer.Put(kvp.Value);
                }
            },
            reader => {
                var data = new ERus.Engine.Network.Packets.Events.ScriptPacketData();
                data.ScriptTypeName = reader.GetString();
                int fieldCount = reader.GetInt();
                data.FieldValues = new System.Collections.Generic.Dictionary<string, string>(fieldCount);
                for (int f = 0; f < fieldCount; f++)
                {
                    string key = reader.GetString();
                    string val = reader.GetString();
                    data.FieldValues[key] = val;
                }
                return data;
            }
        );
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod method)
    {
        _packetProcessor.ReadAllPackets(reader, peer);
    }

    public void SubscribeReusable<T>(Action<T, NetPeer> onReceive) where T : class, new()
    {
        _packetProcessor.SubscribeReusable<T, NetPeer>(onReceive);
    }

    public void SendToPeer<T>(NetPeer peer, T packet, DeliveryMethod method, byte channelNumber = 0) where T : class, new()
    {
        var writer = new NetDataWriter();
        _packetProcessor.Write(writer, packet);
        peer.Send(writer, channelNumber, method);
    }

    public void SendToServer<T>(T packet, DeliveryMethod method, byte channelNumber = 0) where T : class, new()
    {
        if (_transport.NetManager?.FirstPeer != null)
        {
            var writer = new NetDataWriter();
            _packetProcessor.Write(writer, packet);
            _transport.NetManager.FirstPeer.Send(writer, channelNumber, method);
        }
    }

    public void SendToAllExcept<T>(T packet, NetPeer? excludedPeer, DeliveryMethod method, byte channelNumber = 0) where T : class, new()
    {
        if (_transport.NetManager == null) return;
        
        var writer = new NetDataWriter();
        _packetProcessor.Write(writer, packet);

        foreach (var peer in _transport.NetManager)
        {
            if (peer != excludedPeer)
                peer.Send(writer, channelNumber, method);
        }
    }
}
