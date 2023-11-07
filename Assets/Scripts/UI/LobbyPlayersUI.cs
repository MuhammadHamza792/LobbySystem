using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace UI
{
    public class LobbyPlayersUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _playerCount;
        [SerializeField] private TMP_InputField _searchBar;
        [SerializeField] private LobbyPlayerData _lobbyPlayer;
        [SerializeField] private Transform _parent;

        private List<LobbyPlayerData> _players;

        public List<LobbyPlayerData> PlayerList => _players;
        
        private void Awake() => _players ??= new List<LobbyPlayerData>();

        private void OnEnable()
        {
            _searchBar.onValueChanged.AddListener(OnSearch);
            GameLobby.OnSyncLobby += RefreshLobby;
            GameLobby.OnSyncLobby += RefreshLobbyData;
        }

        private void OnDisable()
        {
            _searchBar.onValueChanged.RemoveListener(OnSearch);
            GameLobby.OnSyncLobby -= RefreshLobby;
            GameLobby.OnSyncLobby -= RefreshLobbyData;
        }

        private void OnSearch(string search)
        {
            if(_players == null) return;
            
            if (search.Length == 0)
            {
                foreach (var lobbyObject in _players)
                {
                    lobbyObject.gameObject.SetActive(true);
                }
            
                return;
            }
        
            var searchedLobbies = SearchManager.Instance.Search(_players, search);
            for (int i = 0; i < searchedLobbies.Count; i++)
            {
                for (int j = 0; j < _players.Count; j++)
                {
                    if (searchedLobbies[i].name == _players[j].name)
                        _players[j].gameObject.SetActive(true);
                    else
                        _players[j].gameObject.SetActive(false);
                }
            }
        }
        
        private void RefreshLobby(Lobby lobby)
        {
            if(_players.Count == lobby.Players.Count) return;
            
            ClearLobby();
            foreach (var player in lobby.Players)
            {
                PlayerJoined(player, lobby, player.Id == lobby.HostId);
            }
        }
        
        public void PlayerJoined(Player player, Lobby lobby, bool isHost)
        {
            var playerData = Instantiate(_lobbyPlayer, _parent);
            playerData.gameObject.name = player.Data["PlayerName"].Value;
            playerData.SetPlayerData(player, lobby, isHost);
            
            if(_players.Contains(playerData)) return;
            _players.Add(playerData);
            
            _playerCount.SetText($"{_players.Count}/{lobby.MaxPlayers}");
        }

        public void RefreshLobbyData(Lobby lobby)
        {
            for (var index = 0; index < lobby.Players.Count; index++)
            {
                var lobbyPlayer = lobby.Players[index];
                var player = _players[index];
                player.SetPlayerData(lobbyPlayer, lobby, lobbyPlayer.Id == lobby.HostId);
            }
        }

        /*public void RemovePlayer(string playerID)
        {
            Debug.Log($"Main Id: {playerID}");
            
            foreach (var playerData in _players)
            {
                Debug.Log(playerData.PlayerID);
            }
            
            var player = _players.FirstOrDefault(player => player.PlayerID == playerID);

            if (player == null) return;
            if (!_players.Contains(player)) return;
            _players.Remove(player);
            Destroy(player.gameObject);
        }*/

        private void ClearLobbyFromDelegate(Lobby lobby, GameLobby gameLobby) => ClearLobby();

        private void ClearLobby()
        {
            if (_players.Count <= 0) return;
            foreach (var player in _players.Where(player => player != null))
            {
                Destroy(player.gameObject);
            }
            _players.Clear();
        }
    }
}
