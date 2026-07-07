using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public sealed class PacketBuilder : IDisposable
{
    public const int MaxPacketSize = 1100;

    private readonly Allocator allocator;

    private NativeList<byte> currentPacket;
    private readonly Queue<NativeArray<byte>> completedPackets = new();

    public void Add(byte[] packedPayload)
    {
        if (packedPayload == null)
            throw new ArgumentNullException(nameof(packedPayload));

        if (packedPayload.Length > MaxPacketSize)
            throw new ArgumentException(
                $"Packed payload ({packedPayload.Length} bytes) exceeds the maximum packet size ({MaxPacketSize} bytes).");

        // Doesn't fit? Finish the current packet first.
        if (currentPacket.Length + packedPayload.Length > MaxPacketSize)
        {
            FinalizeCurrentPacket();
        }

        currentPacket.AddRange(new NativeArray<byte>(packedPayload, Allocator.Temp));
    }

    public PacketBuilder(Allocator allocator = Allocator.Temp)
    {
        this.allocator = allocator;
        currentPacket = new NativeList<byte>(MaxPacketSize, allocator);
    }

    /// <summary>
    /// Adds a packed payload ([ushort size][payload]) to the builder.
    /// If it doesn't fit in the current packet, a new packet is started.
    /// </summary>
    public void Add(NativeArray<byte> packedPayload)
    {
        if (packedPayload.Length > MaxPacketSize)
            throw new ArgumentException(
                $"Packed payload ({packedPayload.Length} bytes) exceeds the maximum packet size ({MaxPacketSize} bytes).");

        // Doesn't fit? Finish the current packet first.
        if (currentPacket.Length + packedPayload.Length > MaxPacketSize)
        {
            FinalizeCurrentPacket();
        }

        currentPacket.AddRange(packedPayload);
    }

    public void Add(PacketCodec.ClientPacket clientPacket)
    {
        var payload = clientPacket.payload;
        var connectionId = clientPacket.connectionId;
        if (payload.Length > MaxPacketSize)
            throw new ArgumentException(
                $"Packed payload ({payload.Length} bytes) exceeds the maximum packet size ({MaxPacketSize} bytes).");

        // Doesn't fit? Finish the current packet first.
        if (currentPacket.Length + payload.Length > MaxPacketSize)
        {
            FinalizeCurrentPacket();
        }

        DataStreamWriter writer = new DataStreamWriter(1100, Allocator.Temp);

        if (clientPacket.type == 1) {
            writer.WriteByte(clientPacket.type);
            writer.WriteInt(connectionId);

            writer.WriteInt(payload.Length);
            writer.WriteBytes(payload);
        }
        if (clientPacket.type == 2)
        {
            writer.WriteByte(clientPacket.type);

            writer.WriteInt(payload.Length);
            writer.WriteBytes(payload); ;
        }
        if (clientPacket.type == 3)
        {
            writer.WriteByte(clientPacket.type);
            writer.WriteInt(connectionId);

            writer.WriteInt(payload.Length);
            writer.WriteBytes(payload);
        }
        currentPacket.AddRange(writer.AsNativeArray());
    }

    //public static void WriteSendToAll(ref DataStreamWriter writer, byte[] payload)
    //{
    //    writer.WriteByte(2);

    //    writer.WriteInt(payload.Length);
    //    writer.WriteBytes(payload);
    //}

    //public static void WriteSendToAllExcept(ref DataStreamWriter writer, int excludedConnectionId, byte[] payload)
    //{
    //    writer.WriteByte(3);
    //    writer.WriteInt(excludedConnectionId);

    //    writer.WriteInt(payload.Length);
    //    writer.WriteBytes(payload);

    //    NativeArray<byte> nativePayload = writer.AsNativeArray();
    //    //currentPacket.AddRange(nativePayload);
    //}

    /// <summary>
    /// Finalizes any remaining data into a packet.
    /// Call once after all Add() calls.
    /// </summary>
    public void Build()
    {
        FinalizeCurrentPacket();
    }

    /// <summary>
    /// Gets the next completed packet.
    /// Dispose the returned NativeArray after sending.
    /// </summary>
    public bool TryGetNextPacket(out NativeArray<byte> packet)
    {
        if (completedPackets.Count == 0)
        {
            packet = default;
            return false;
        }

        packet = completedPackets.Dequeue();
        return true;
    }

    private void FinalizeCurrentPacket()
    {
        if (currentPacket.Length == 0)
            return;

        NativeArray<byte> packet = new NativeArray<byte>(
            currentPacket.Length,
            allocator);

        NativeArray<byte>.Copy(
            currentPacket.AsArray(),
            packet,
            currentPacket.Length);

        completedPackets.Enqueue(packet);

        currentPacket.Dispose();
        currentPacket = new NativeList<byte>(MaxPacketSize, allocator);
    }

    public void Dispose()
    {
        if (currentPacket.IsCreated)
            currentPacket.Dispose();

        while (completedPackets.Count > 0)
        {
            NativeArray<byte> packet = completedPackets.Dequeue();

            if (packet.IsCreated)
                packet.Dispose();
        }
    }
}

// EXAMPLE

//PacketBuilder builder = new PacketBuilder(Allocator.Temp);

//// Already packed as [ushort][payload]
//builder.Add(payload1);
//builder.Add(payload2);
//builder.Add(payload3);
//builder.Add(payload4);

//builder.Build();

//while (builder.TryGetNextPacket(out NativeArray<byte> packet))
//{
//    Send(packet);

//    packet.Dispose();
//}

//builder.Dispose();