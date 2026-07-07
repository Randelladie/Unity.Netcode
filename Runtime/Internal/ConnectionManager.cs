using System.Collections.Generic;
using Unity.Networking.Transport;

public class ConnectionManager
{
    #region Fields

    // General
    public NetworkDriverManager networkDriverManager;
    public NetworkConnection connection;
    public int connectionId { get; private set; } = 1;

    // Host
    public Dictionary<int, NetworkConnection> connections = new();
    int nextConnectionId = 2;
    int defaultNextConnectionId = 2;
    #endregion


    #region Constructor
    public ConnectionManager(NetworkDriverManager networkDriverManager)
    {
        this.networkDriverManager = networkDriverManager;
    }
    #endregion

    #region Public Functions
    public int AddConnection(NetworkConnection newConnection)
    {
        int newConnectionId = nextConnectionId;
        nextConnectionId++;
        connections.Add(newConnectionId, newConnection);
        return newConnectionId;
    }

    public void RemoveConnectionById(int id)
    {
        connections.Remove(id);
    }

    public void RemoveConnectionSelf()
    {
        connection = default;
    }

    public void ResetConnectionId() 
    {
        connectionId = 1;
        nextConnectionId = defaultNextConnectionId;
    }

    public void SetConnectionId(int connectionId)
    {
        this.connectionId = connectionId;
    }

    public void RemoveNonConnections()
    {
        List<int> disconnectedKeys = new List<int>();
        foreach (var kvp in connections)
        {
            int clientId = kvp.Key;
            NetworkConnection targetconnection = kvp.Value;
            if (!targetconnection.IsCreated)
            {
                // Mark disconnected clients for removal
                disconnectedKeys.Add(clientId);
            }
        }
        // Remove disconnected clients
        foreach (var key in disconnectedKeys)
        {
            connections.Remove(key);
        }
    }
    #endregion
}
