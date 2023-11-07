using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerData : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _playerName;
    [SerializeField] private Button _makePartyLeader;
    [SerializeField] private Button _kick;

    public string PlayerName { private set; get; }
    public string PlayerID { private set; get; }
        
    private Player _playerData;

    public void Start()
    {
        _kick.onClick.AddListener(() =>
        {
            GameLobby.Instance.KickAPlayer(_playerData);
        });
        
        _makePartyLeader.onClick.AddListener(() =>
        {
            GameLobby.Instance.ChangeHost(_playerData);
        });
    }
    
    public void SetPlayerData(Player playerData, Lobby lobby, bool isHost)
    {
        _playerData = playerData;
        PlayerID = playerData.Id;
        PlayerName = playerData.Data["PlayerName"].Value;
        _playerName.SetText($"{PlayerName}");
        var currentPlayer = AuthenticationService.Instance.PlayerId;
        _kick.gameObject.SetActive(!isHost && currentPlayer == lobby.HostId);
        _makePartyLeader.gameObject.SetActive(!isHost && currentPlayer == lobby.HostId);
    }
}
