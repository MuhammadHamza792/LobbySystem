using System;
using TMPro;
using UI.Notify;
using UnityEngine;
using UnityEngine.UI;

public class LobbyObject : MonoBehaviour , INotifier
{
    [SerializeField] private TextMeshProUGUI _playersCount;
    [SerializeField] private TextMeshProUGUI _lobbyName;
    [SerializeField] private Button _joinLobby;
    [SerializeField] private GameObject _lockedImage;

    private LobbyObjectData _lobbyObjectData;
    
    private void Start()
    {
        _joinLobby.onClick.AddListener(() =>
        {
            if (_lobbyObjectData.HasPassword)
            {
                NotificationHelper.SendNotification(NotificationType.RequiredField, "Please Enter Lobby's Password.",
                    this, NotifyCallType.Open);
                return;
            }

            JoinLobbyWithoutPass();
        });
    }
    
    public void Notify(string notifyData) => JoinLobbyWithPass(notifyData);

    private void JoinLobbyWithPass(string pass) => GameLobby.Instance.JoinLobby(_lobbyObjectData.LobbyID,
        true, pass);
    
    private void JoinLobbyWithoutPass() => GameLobby.Instance.JoinLobby(_lobbyObjectData.LobbyID, true);

    public void SetObjectsData(LobbyObjectData data)
    {
        _lobbyObjectData = data;
        _lockedImage.SetActive(data.HasPassword);
        _lobbyName.SetText($"{data.LobbyName}");
        _playersCount.SetText($"{data.PlayersCount}/{data.TotalPlayers}");
    }
}

public struct LobbyObjectData
{
    public string LobbyName;
    public string LobbyID;
    public bool HasPassword;
    public int PlayersCount;
    public int TotalPlayers;
}
