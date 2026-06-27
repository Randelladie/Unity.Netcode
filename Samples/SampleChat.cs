using UnityEngine;
using UnityEngine.UI;
using System;

public class SampleChat : MonoBehaviour
{
    public InputField chatInput;
    public Button chatSend;
    public NetworkChat networkChat;
    public NetworkManager networkManager;

    public Action<string> OnChat;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        chatSend.onClick.AddListener(OnChatSend);
        networkChat.OnMessageReceived += OnMessageReceived;
    }

    void OnChatSend()
    {
        string val = chatInput.text;
        chatInput.text = "";
        networkChat.SendMessageDataAsPlayer($"Player{networkManager.connectionId}", val);
        print($"Player{networkManager.connectionId}: {val}");
        OnChat?.Invoke(val);
    }

    void OnMessageReceived(string val)
    {
        print(val);
        OnChat?.Invoke(val);
    }
}
