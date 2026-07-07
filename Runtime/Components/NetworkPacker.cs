using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using System;

public class NetworkPacker : MonoBehaviour
{
    #region Events
    /// <summary>
    /// Fired whenever there is a payload sent from client or host
    /// </summary>
    /// <param name="payload">Data sent</param>
    public event Action<NetworkPack> OnPayloadReceived;
    #endregion

    #region Class
    public class NetworkPack {
        public ushort id;
        public NativeArray<byte> payload;
    }
    #endregion

    #region Fields
    // --- Singleton ---
    public static NetworkPacker networkPacker { get; private set; }

    NetworkManager networkManager;

    Dictionary<ushort, byte[]> PacketTypeSchemas = new();

    // Data Types
    public const byte BYTE = 0;
    public const byte SHORT = 1;
    public const byte USHORT = 2;
    public const byte INT = 3;
    public const byte UINT = 4;
    public const byte LONG = 5;
    public const byte ULONG = 6;
    public const byte FLOAT = 7;
    public const byte DOUBLE = 8;
    public const byte STRING = 9;
    public const byte PAYLOAD = 10;
    public const byte PAYLOAD8 = 11;
    public const byte PAYLOAD16 = 12;
    public const byte PAYLOAD32 = 13;
    #endregion

    #region Public Functions
    /// <summary>
    /// Registers a schema definition that can later be used to pack and unpack payloads.
    /// </summary>
    /// <param name="schemaId">
    /// Unique identifier of the schema.
    /// </param>
    /// <param name="schema">
    /// Structure definition describing the order and types of fields in the payload.
    /// </param>
    public void RegisterSchema(ushort schemaId, byte[] structure)
    {
        PacketTypeSchemas[schemaId] = structure;
    }

    /// <summary>
    /// Sends a payload to all connected clients using the specified schema.
    /// </summary>
    /// <param name="schemaId">
    /// Identifier of the schema used to interpret the payload.
    /// </param>
    /// <param name="payload">
    /// Serialized payload data matching the specified schema.
    /// </param>
    public void SendDataToAll(ushort schemaId, NativeArray<byte> payload)
    {
        DataStreamWriter writer = new DataStreamWriter(
            sizeof(ushort) + payload.Length,
            Allocator.Temp);
        writer.WriteUShort(schemaId);
        writer.WriteBytes(payload);
        networkManager.WriteDataToAllExceptSelf(writer.AsNativeArray().ToArray());
    }

    /// <summary>
    /// Sends a reliable "guaranteed" payload to all connected clients using the specified schema.
    /// </summary>
    /// <param name="schemaId">
    /// Identifier of the schema used to interpret the payload.
    /// </param>
    /// <param name="payload">
    /// Serialized payload data matching the specified schema.
    /// </param>
    public void SendReliableDataToAll(ushort schemaId, NativeArray<byte> payload)
    {
        DataStreamWriter writer = new DataStreamWriter(
            sizeof(ushort) + payload.Length,
            Allocator.Temp);
        writer.WriteUShort(schemaId);
        writer.WriteBytes(payload);
        networkManager.WriteReliableDataToAllExceptSelf(writer.AsNativeArray().ToArray());
    }

    /// <summary>
    /// Sends a payload to a specific connection using the specified schema.
    /// </summary>
    /// <param name="schemaId">
    /// Identifier of the schema used to interpret the payload.
    /// </param>
    /// <param name="payload">
    /// Serialized payload data matching the specified schema.
    /// </param>
    /// <param name="connectionId">
    /// Target connection identifier.
    /// </param>
    public void SendDataTo(ushort schemaId, NativeArray<byte> payload, int connectionId)
    {
        DataStreamWriter writer = new DataStreamWriter(
            sizeof(ushort) + payload.Length,
            Allocator.Temp);
        writer.WriteUShort(schemaId);
        writer.WriteBytes(payload);
        networkManager.WriteDataTo(connectionId, writer.AsNativeArray().ToArray());
    }

    /// <summary>
    /// Sends a reliable "guaranteed" payload to a specific connection using the specified schema.
    /// </summary>
    /// <param name="schemaId">
    /// Identifier of the schema used to interpret the payload.
    /// </param>
    /// <param name="payload">
    /// Serialized payload data matching the specified schema.
    /// </param>
    /// <param name="connectionId">
    /// Target connection identifier.
    /// </param>
    public void SendReliableDataTo(ushort schemaId, NativeArray<byte> payload, int connectionId)
    {
        DataStreamWriter writer = new DataStreamWriter(
            sizeof(ushort) + payload.Length,
            Allocator.Temp);
        writer.WriteUShort(schemaId);
        writer.WriteBytes(payload);
        networkManager.WriteReliableDataTo(connectionId, writer.AsNativeArray().ToArray());
    }

    /// <summary>
    /// Packs a payload into a network packet by prepending the schema identifier.
    /// </summary
    /// <param name="payload">
    /// Serialized payload data matching the specified schema.
    /// </param>
    /// <returns>
    /// A packed byte array containing the schema identifier followed by the payload.
    /// </returns>
    public NativeArray<byte> PackPayload(NativeArray<byte> payload)
    {
        var writer = new DataStreamWriter(
            sizeof(uint) + payload.Length,
            Allocator.Temp);

        if (payload.Length < 256)
        {
            writer.WriteByte(PAYLOAD8);
            writer.WriteByte((byte)payload.Length);
        }
        else if (payload.Length < 65536)
        {
            writer.WriteByte(PAYLOAD16);
            writer.WriteUShort((ushort)payload.Length);
        }
        else
        {
            writer.WriteByte(PAYLOAD32);
            writer.WriteUInt((uint)payload.Length);
        }

        writer.WriteBytes(payload);

        var result = new NativeArray<byte>(writer.Length, Allocator.Temp);
        result.CopyFrom(writer.AsNativeArray());

        return result;
    }
    // Create Payload Packer
    #endregion

    #region Listeners
    void Start()
    {
        networkPacker = this;
        networkManager = GetComponent<NetworkManager>();
        networkManager.OnBytesReceived += OnBytesReceived;
        RegisterSchema(1, new byte[] { PAYLOAD });
    }

    void OnBytesReceived(NativeArray<byte> data)
    {
        int loopCounter = 0;
        DataStreamReader reader = new DataStreamReader(data);
        while (reader.Length - reader.GetBytesRead() > 1)
        {            
            if (loopCounter >= 1000)
            {
                Debug.LogError("LOOP DETECTED IN NETWORK PACKER");
                break;
            }
            loopCounter++;

            DataStreamWriter writer = new DataStreamWriter(
                sizeof(ushort) + data.Length,
                Allocator.Temp);
            ushort id = reader.ReadUShort();
            if (PacketTypeSchemas.TryGetValue(id, out byte[] packetTypeSchema))
            {
                foreach (byte type in packetTypeSchema)
                {
                    switch (type)
                    {
                        case BYTE:
                            writer.WriteByte(reader.ReadByte());
                            break;
                        case SHORT:
                            writer.WriteShort(reader.ReadShort());
                            break;
                        case USHORT:
                            writer.WriteUShort(reader.ReadUShort());
                            break;
                        case INT:
                            writer.WriteInt(reader.ReadInt());
                            break;
                        case UINT:
                            writer.WriteUInt(reader.ReadUInt());
                            break;
                        case LONG:
                            writer.WriteLong(reader.ReadLong());
                            break;
                        case ULONG:
                            writer.WriteULong(reader.ReadULong());
                            break;
                        case FLOAT:
                            writer.WriteFloat(reader.ReadFloat());
                            break;
                        case DOUBLE:
                            writer.WriteDouble(reader.ReadDouble());
                            break;
                        case STRING:
                            StringPacker.TryReadData(reader, out var message);
                            byte[] packedMessage = StringPacker.PackAscii(message);
                            writer.WriteBytes(packedMessage);
                            break;
                        case PAYLOAD:
                            byte payloadType = reader.ReadByte();
                            switch (payloadType)
                            {
                                case PAYLOAD8:
                                    byte length8 = reader.ReadByte();
                                    for (byte i = 0; i < length8; i++)
                                    {
                                        writer.WriteByte(reader.ReadByte());
                                    }
                                    break;
                                case PAYLOAD16:
                                    ushort length16 = reader.ReadUShort();
                                    for (ushort i = 0; i < length16; i++)
                                    {
                                        writer.WriteByte(reader.ReadByte());
                                    }
                                    break;
                                case PAYLOAD32:
                                    uint length32 = reader.ReadUInt();
                                    for (uint i = 0; i < length32; i++)
                                    {
                                        writer.WriteByte(reader.ReadByte());
                                    }
                                    break;
                                default:
                                    break;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            OnPayloadReceived?.Invoke(new NetworkPack { id = id, payload = writer.AsNativeArray() });
        }
        //throw new System.NotImplementedException();
    }
    #endregion
}

// HOW TO WRITE DATA
// DataStreamWriter writer = new DataStreamWriter(128, Allocator.Temp);

// writer.WriteInt(id);
// writer.WriteByte(type);

// NativeArray<byte> payload = writer.AsNativeArray();