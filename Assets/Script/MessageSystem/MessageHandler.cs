using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MessageHandler : MonoBehaviour
{
    public static MessageHandler instance;

    [SerializeField] Transform messageContent;
    [SerializeField] MessageList messageListPrefab;

    private void Awake()
    {
        instance = this;
    }

    //Spawn Message List Item and initialize message
    public void ShowMessage(string message)
    {
        MessageList spawnedMessage = Instantiate(messageListPrefab, messageContent);
        spawnedMessage.Initialize(message);
    }
}
