using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;

public class MessageRouter
{
    #region Events
    /// <summary>
    /// Fired whenever there is a data sent from client or host
    /// </summary>
    /// <param name="data">Data sent</param>
    public event Action<NativeArray<byte>> OnBytesReceived;

    /// <summary>
    /// Fired when NetworkUpdate occurs
    /// </summary>
    public event Action OnNetworkUpdate;
    #endregion

    #region Fields
    // Setup
    NetworkDriverManager networkDriverManager;
    ConnectionManager connectionManager { get { return networkDriverManager.connectionManager; } }
    NetworkDriver driver { get { return networkDriverManager.driver; } }
    NetworkConnection connection { get { return networkDriverManager.connection; } }
    NetworkPipeline reliablePipeline { get { return networkDriverManager.reliablePipeline; } }

    // NetworkDriver Values
    Dictionary<int, NetworkConnection> connections { get { return networkDriverManager.connections; } }
    int connectionId { get { return networkDriverManager.connectionId; } }
    //int nextConnectionId { get { return networkDriverManager.nextConnectionId; } }
    //int defaultNextConnectionId { get { return networkDriverManager.defaultNextConnectionId; } }

    // Host
    //Dictionary<int, byte[]> bufferPayloads = new();
    Dictionary<int, Queue<byte[]>> payloads = new();
    //Dictionary<int, byte[]> reliableBufferPayloads = new();
    Dictionary<int, Queue<byte[]>> reliablePayloads = new();

    // Client
    Queue<PacketCodec.ClientPacket> clientPayloads = new();
    Queue<PacketCodec.ClientPacket> clientReliablePayloads = new();

    // Server State
    public bool IsHost { get { return networkDriverManager.IsHost; } }
    public bool IsClient { get { return networkDriverManager.IsClient; } }
    public bool IsStarted { get { return networkDriverManager.IsStarted; } }
    public int PlayerCount { get { return networkDriverManager.PlayerCount; } }

    // Network Update (DO SOMETHING)
    public bool enableNetworkUpdate = true;
    public float networkUpdatePerSecond = 20f;
    float networkDelta;
    #endregion

    #region Constructor
    public MessageRouter(NetworkDriverManager networkDriverManager)
    {
        this.networkDriverManager = networkDriverManager;
    }
    #endregion

    #region Helpers
    void HostHandlePacket(NativeArray<byte> payload)
    {
        var reader = new DataStreamReader(payload);
        PacketCodec.ClientPacket packet;
        // int loopCounter = 0;
        while (PacketCodec.ReadNext(ref reader, out packet))
        {
            if (packet.payload == null || packet.payload.Length == 0)
                continue;
            int loopCounter = 0;
            string message = "";
            DataStreamReader payloadReader = new DataStreamReader(new NativeArray<byte>(packet.payload, Allocator.Temp));
            while (payloadReader.Length - payloadReader.GetBytesRead() > 0)
            {
                if (loopCounter >= 1000)
                {
                    break;
                }
                loopCounter++;
                message += $"[{payloadReader.ReadByte()}]";
            }
            //print($"Packet connectionId: {packet.connectionId} type: {packet.type} message: {message}");
            switch (packet.type)
            {
                case 1:
                    AppendPayload(packet.connectionId, packet.payload);
                    break;

                case 2:
                    AppendAllPayloads(packet.payload);
                    break;

                case 3:
                    AppendAllPayloadsExcept(packet.connectionId, packet.payload);
                    break;
            }
        }
    }

    void HostHandleReliablePacket(NativeArray<byte> payload)
    {
        var reader = new DataStreamReader(payload);
        PacketCodec.ClientPacket packet;
        // int loopCounter = 0;
        while (PacketCodec.ReadNext(ref reader, out packet))
        {
            if (packet.payload == null || packet.payload.Length == 0)
                continue;
            int loopCounter = 0;
            string message = "";
            DataStreamReader payloadReader = new DataStreamReader(new NativeArray<byte>(packet.payload, Allocator.Temp));
            while (payloadReader.Length - payloadReader.GetBytesRead() > 0)
            {
                if (loopCounter >= 1000)
                {
                    break;
                }
                loopCounter++;
                message += $"[{payloadReader.ReadByte()}]";
            }
            //print($"Packet connectionId: {packet.connectionId} type: {packet.type} message: {message}");
            switch (packet.type)
            {
                case 1:
                    AppendReliablePayload(packet.connectionId, packet.payload);
                    break;

                case 2:
                    AppendAllReliablePayloads(packet.payload);
                    break;

                case 3:
                    AppendAllReliablePayloadsExcept(packet.connectionId, packet.payload);
                    break;
            }
        }
    }

    void AppendPayload(int connectionId, byte[] data)
    {
        if (connectionId == 1)
        {
            var native = new NativeArray<byte>(data, Allocator.Temp);
            OnBytesReceived?.Invoke(native);
            return;
        }

        if (data == null || data.Length == 0)
            return;

        if (!payloads.TryGetValue(connectionId, out var queue))
        {
            queue = new Queue<byte[]>();
            payloads[connectionId] = queue;
        }

        queue.Enqueue(data);
    }

    void AppendReliablePayload(int connectionId, byte[] data)
    {
        if (connectionId == 1)
        {
            var native = new NativeArray<byte>(data, Allocator.Temp);
            OnBytesReceived?.Invoke(native);
            return;
        }

        if (data == null || data.Length == 0)
            return;

        if (!reliablePayloads.TryGetValue(connectionId, out var queue))
        {
            queue = new Queue<byte[]>();
            reliablePayloads[connectionId] = queue;
        }

        queue.Enqueue(data);
    }

    void AppendAllPayloadsExcept(int connectionId, byte[] data)
    {
        foreach (var kvp in payloads)
        {
            if (kvp.Key == connectionId) continue;
            AppendPayload(kvp.Key, data);
        }
    }

    void AppendAllReliablePayloadsExcept(int connectionId, byte[] data)
    {
        foreach (var kvp in payloads)
        {
            if (kvp.Key == connectionId) continue;
            AppendReliablePayload(kvp.Key, data);
        }
    }

    void AppendAllPayloads(byte[] data)
    {
        foreach (var kvp in payloads)
        {
            AppendPayload(kvp.Key, data);
        }
    }
    void AppendAllReliablePayloads(byte[] data)
    {
        foreach (var kvp in payloads)
        {
            AppendReliablePayload(kvp.Key, data);
        }
    }

    void ClearPayload(int connectionId)
    {
        if (payloads.TryGetValue(connectionId, out var queue))
        {
            queue.Clear();
        }
    }

    bool AreAllPayloadsEmpty()
    {
        foreach (var queue in payloads.Values)
        {
            if (queue.Count > 0)
                return false;
        }

        return true;
    }

    bool AreAllReliablePayloadsEmpty()
    {
        foreach (var queue in reliablePayloads.Values)
        {
            if (queue.Count > 0)
                return false;
        }

        return true;
    }

    //void AppendBufferPayloads()
    //{
    //    foreach (var kvp in bufferPayloads)
    //    {
    //        int key = kvp.Key;
    //        byte[] buffer = kvp.Value;

    //        if (buffer == null || buffer.Length == 0)
    //            continue;

    //        // FIRST TIME: just move buffer into payload
    //        if (!payloads.TryGetValue(key, out var main) || main == null || main.Length == 0)
    //        {
    //            payloads[key] = buffer;
    //            continue;
    //        }

    //        // APPEND: merge arrays
    //        byte[] merged = new byte[main.Length + buffer.Length];

    //        Buffer.BlockCopy(main, 0, merged, 0, main.Length);
    //        Buffer.BlockCopy(buffer, 0, merged, main.Length, buffer.Length);

    //        payloads[key] = merged;
    //    }

    //    // CLEAR buffers (replace with empty arrays or null)
    //    var keys = new List<int>(bufferPayloads.Keys);
    //    foreach (var key in keys)
    //    {
    //        bufferPayloads[key] = Array.Empty<byte>();
    //    }

    //    // === RELIABLE ===

    //    foreach (var kvp in reliableBufferPayloads)
    //    {
    //        int key = kvp.Key;
    //        byte[] buffer = kvp.Value;

    //        if (buffer == null || buffer.Length == 0)
    //            continue;

    //        // FIRST TIME: just move buffer into payload
    //        if (!reliablePayloads.TryGetValue(key, out var main) || main == null || main.Length == 0)
    //        {
    //            reliablePayloads[key] = buffer;
    //            continue;
    //        }

    //        // APPEND: merge arrays
    //        byte[] merged = new byte[main.Length + buffer.Length];

    //        Buffer.BlockCopy(main, 0, merged, 0, main.Length);
    //        Buffer.BlockCopy(buffer, 0, merged, main.Length, buffer.Length);

    //        reliablePayloads[key] = merged;
    //    }

    //    // CLEAR buffers (replace with empty arrays or null)
    //    var reliableKeys = new List<int>(reliableBufferPayloads.Keys);
    //    foreach (var key in reliableKeys)
    //    {
    //        reliableBufferPayloads[key] = Array.Empty<byte>();
    //    }
    //}

    // --- SEND ---

    void HostSendPayloads()
    {
        if (!driver.IsCreated)
            return;

        if (!AreAllPayloadsEmpty())
        {
            // Snapshot keys only
            var keys = new List<int>(payloads.Keys);

            foreach (var key in keys)
            {
                if (!payloads.TryGetValue(key, out var queue) || queue.Count == 0)
                    continue;

                if (key == connectionId)
                {
                    var data = queue.Dequeue();
                    var native = new NativeArray<byte>(data, Allocator.Temp);
                    OnBytesReceived?.Invoke(native);
                    continue;
                }

                if (!connections.TryGetValue(key, out var targetConnection))
                    continue;

                PacketBuilder builder = new PacketBuilder(Allocator.Temp);
                while (queue.Count > 0)
                {
                    var data = queue.Dequeue();
                    builder.Add(data);
                }

                builder.Build();

                while (builder.TryGetNextPacket(out NativeArray<byte> packet))
                {
                    if (driver.BeginSend(targetConnection, out var writer) == 0)
                    {
                        writer.WriteByte(2);
                        writer.WriteBytes(packet);
                        driver.EndSend(writer);
                        packet.Dispose();
                    }
                }
                builder.Dispose();
            }
        }

        if (!AreAllReliablePayloadsEmpty())
        {
            // Snapshot keys only
            var keys = new List<int>(reliablePayloads.Keys);

            foreach (var key in keys)
            {
                if (!reliablePayloads.TryGetValue(key, out var queue) || queue.Count == 0)
                    continue;

                if (key == connectionId)
                {
                    var data = queue.Dequeue();
                    var native = new NativeArray<byte>(data, Allocator.Temp);
                    OnBytesReceived?.Invoke(native);
                    continue;
                }

                if (!connections.TryGetValue(key, out var targetConnection))
                    continue;

                PacketBuilder builder = new PacketBuilder(Allocator.Temp);
                while (queue.Count > 0)
                {
                    var data = queue.Dequeue();
                    builder.Add(data);
                }

                builder.Build();

                while (builder.TryGetNextPacket(out NativeArray<byte> packet))
                {
                    if (driver.BeginSend(targetConnection, out var writer) == 0)
                    {
                        writer.WriteByte(2);
                        writer.WriteBytes(packet);
                        driver.EndSend(writer);
                        packet.Dispose();
                    }
                }
                builder.Dispose();
            }
        }
    }

    void ClientSendPayload()
    {
        if (!driver.IsCreated)
            return;

        if (connection == default)
            return;

        if (clientPayloads.Count > 0)
        {
            PacketBuilder builder = new PacketBuilder(Allocator.Temp);
            while (clientPayloads.Count > 0)
            {
                var data = clientPayloads.Dequeue();
                builder.Add(data);
            }

            builder.Build();

            while (builder.TryGetNextPacket(out NativeArray<byte> packet))
            {
                if (driver.BeginSend(connection, out var writer) == 0)
                {
                    writer.WriteByte(2);
                    writer.WriteBytes(packet);
                    driver.EndSend(writer);
                }
                packet.Dispose();
            }
            builder.Dispose();
        }

        if (clientReliablePayloads.Count > 0)
        {
            PacketBuilder builder = new PacketBuilder(Allocator.Temp);
            while (clientReliablePayloads.Count > 0)
            {
                var data = clientReliablePayloads.Dequeue();
                builder.Add(data);
            }

            builder.Build();

            while (builder.TryGetNextPacket(out NativeArray<byte> packet))
            {
                if (driver.BeginSend(reliablePipeline, connection, out var writer) == 0)
                {
                    writer.WriteByte(3);
                    writer.WriteBytes(packet);
                    driver.EndSend(writer);
                }
                packet.Dispose();
            }
            builder.Dispose();
        }
    }

    void SyncPayloadsWithConnections()
    {
        if (!IsHost) return;

        // Remove payloads for disconnected clients
        List<int> toRemove = new();

        foreach (var kvp in payloads)
        {
            if (kvp.Key == 1) continue;
            if (!connections.ContainsKey(kvp.Key))
            {
                // handle join
                toRemove.Add(kvp.Key);
            }
        }

        foreach (int id in toRemove)
        {
            payloads.Remove(id);
        }

        // Ensure every connection has a payload buffer
        foreach (var connection in connections)
        {
            int id = connection.Key;

            if (!payloads.ContainsKey(id))
            {
                payloads[id] = new();
            }
        }

        if (!payloads.ContainsKey(1))
        {
            payloads[1] = new();
        }
    }
    #endregion

    #region Public Functions
    // --- Handle Receive

    public void HostHandleReceivedData(NativeArray<byte> data)
    {
        // Get Byte Intent
        // 1 = Set ConnectionId
        // 2 = OnByteReceived
        // 3 = OnByteReceived, Reliable
        byte intent = data[0];
        int payloadSize = data.Length - 1;
        NativeArray<byte> payload =
            new NativeArray<byte>(payloadSize, Allocator.Temp);
        NativeArray<byte>.Copy(
            data,
            1,
            payload,
            0,
            payloadSize
        );

        switch (intent)
        {
            case 1:
                DataStreamReader reader = new DataStreamReader(payload);
                reader.ReadInt();
                break;
            case 2:
                HostHandlePacket(payload);
                break;
            case 3:
                HostHandleReliablePacket(payload);
                break;
            default:
                break;
        }
    }

    public void ClientHandleReceivedData(NativeArray<byte> data)
    {
        // Get Byte Intent
        // 1 = Set ConnectionId
        // 2 = OnByteReceived
        byte intent = data[0];
        int payloadSize = data.Length - 1;
        NativeArray<byte> payload =
            new NativeArray<byte>(payloadSize, Allocator.Temp);
        NativeArray<byte>.Copy(
            data,
            1,
            payload,
            0,
            payloadSize
        );

        switch (intent)
        {
            case 1:
                DataStreamReader reader = new DataStreamReader(payload);
                connectionManager.SetConnectionId(reader.ReadInt());
                Debug.Log($"ConnectionId is now: {connectionId}");
                break;
            case 2:
                OnBytesReceived?.Invoke(payload);
                break;
            default:
                break;
        }

    }

    // --- Process ---

    /// <summary>
    /// Performs a single networking update cycle. This processes incoming packets, connection events, disconnection events, and outgoing messages. Use this manually if automatic updates are disabled.
    /// </summary>
    public void NetworkUpdate()
    {
        if (IsHost)
        {
            // 1. merge buffered input
            //AppendBufferPayloads();

            // 2. sync connection state BEFORE sending
            SyncPayloadsWithConnections();

            // 3. send
            HostSendPayloads();
        }

        if (IsClient)
        {
            //AppendBufferPackets();
            ClientSendPayload();
        }

        OnNetworkUpdate?.Invoke();
    }


    // --- Handle Send ---

    /// <summary>
    /// Queues a byte payload to the specified connection. Typically used by the host to send data to an individual client.
    /// </summary>
    /// <param name="connectionId">The target connection identifier that should receive the data. Typically assigned by the networking system when a client connects.</param>
    /// <param name="data">The raw payload to send. Can contain serialized game state, RPC data, chat messages, or any custom packet format.</param>
    public void WriteDataTo(int connectionId, byte[] data)
    {
        if (IsHost)
        {
            AppendPayload(connectionId, data);
        }
        if (IsClient)
        {
            PacketCodec.ClientPacket packet = new PacketCodec.ClientPacket
            { connectionId = connectionId, payload = data, type = 1 };
            clientPayloads.Enqueue(packet);
        }
    }

    /// <summary>
    /// Queues a "guaranteed" byte payload to the specified connection. Typically used by the host to send data to an individual client.
    /// </summary>
    /// <param name="connectionId">The target connection identifier that should receive the data. Typically assigned by the networking system when a client connects.</param>
    /// <param name="data">The raw payload to send. Can contain serialized game state, RPC data, chat messages, or any custom packet format.</param>
    public void WriteReliableDataTo(int connectionId, byte[] data)
    {
        if (IsHost)
        {
            AppendReliablePayload(connectionId, data);
        }
        if (IsClient)
        {
            PacketCodec.ClientPacket packet = new PacketCodec.ClientPacket
            { connectionId = connectionId, payload = data, type = 1 };
            clientReliablePayloads.Enqueue(packet);
        }
    }

    /// <summary>
    /// Sends a byte payload to every connected client except the local connection. Useful for relaying state updates received from one client to all other clients.
    /// </summary>
    /// <param name="data">The raw payload to broadcast to all connected remote clients except the local connection. Commonly used by a host to relay client messages to everyone else.</param>
    public void WriteDataToAllExceptSelf(byte[] data)
    {
        if (IsHost)
        {
            AppendAllPayloadsExcept(connectionId, data);
        }
        if (IsClient)
        {
            PacketCodec.ClientPacket packet = new PacketCodec.ClientPacket
            { connectionId = connectionId, payload = data, type = 3 };
            clientPayloads.Enqueue(packet);
        }
    }

    /// <summary>
    /// Sends a reliable byte payload to every connected client except the local connection. Useful for relaying "guaranteed" state updates received from one client to all other clients.
    /// </summary>
    /// <param name="data">The raw payload to broadcast to all connected remote clients except the local connection. Commonly used by a host to relay client messages to everyone else.</param>
    public void WriteReliableDataToAllExceptSelf(byte[] data)
    {
        if (IsHost)
        {
            AppendAllReliablePayloadsExcept(connectionId, data);
        }
        if (IsClient)
        {
            PacketCodec.ClientPacket packet = new PacketCodec.ClientPacket
            { connectionId = connectionId, payload = data, type = 3 };
            clientReliablePayloads.Enqueue(packet);
        }
    }
    #endregion
}
