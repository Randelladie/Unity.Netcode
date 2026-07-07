using UnityEngine;
using System;
using Unity.Collections;

public class NetworkChat : MonoBehaviour
{
    #region Events
    /// <summary>
    /// Fired whenever message was received
    /// </summary>
    /// <param name="data">Message Received</param>
    public event Action<string> OnMessageReceived;
    #endregion

    #region Fields
    public NetworkManager networkManager;
    public NetworkPacker networkPacker;

    public ushort Handshake = 255;

    int count = 0;
    #endregion

    #region Public Functions
    public void SendMessageData(string message)
    {
        byte[] packedMessage = StringPacker.PackAscii(message);
        networkPacker.SendReliableDataToAll(Handshake, new NativeArray<byte>(packedMessage, Allocator.Temp));
        //DataStreamWriter writer = new();
        //writer.WriteBytes(Handshake);
        //writer.WriteBytes(packedMessage);
        //networkManager.WriteDataToAllExceptSelf(writer.AsNativeArray().ToArray());
        //networkPacker.SendDataToAll()
    }

    public void SendMessageDataAsPlayer(string playerName, string message)
    {
        byte[] packedMessage = StringPacker.PackAscii($"{playerName}: {message}");
        networkPacker.SendReliableDataToAll(Handshake, new NativeArray<byte>(packedMessage, Allocator.Temp));

        //DataStreamWriter writer = new();
        //writer.WriteBytes(Handshake);
        //writer.WriteBytes(packedMessage);
        //networkManager.WriteDataToAllExceptSelf(writer.AsNativeArray().ToArray());
    }
    #endregion

    #region Listeners
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        networkManager = GetComponent<NetworkManager>();
        networkPacker = GetComponent<NetworkPacker>();
        networkPacker.RegisterSchema(Handshake, new byte[] { NetworkPacker.STRING });
        networkPacker.OnPayloadReceived += OnDataReceived;
    }

    void OnDataReceived(NetworkPacker.NetworkPack pack)
    {
        if (pack.id == Handshake)
        {
            DataStreamReader reader = new DataStreamReader(pack.payload);
            StringPacker.TryReadData(reader, out var message);
            OnMessageReceived?.Invoke(message);
        }
    }
    #endregion
}
