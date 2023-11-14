using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace LobbyPackage.Scripts
{
    public class GameLobby : Singleton<GameLobby>
    {
        [SerializeField] private bool _destroyLobbyWithHost;
        [SerializeField] private float _lobbyRefreshTime = 15f;
        [SerializeField] private bool _destroyLobbyAfterSessionStarted;

        public bool HasCustomConnection { private set; get; }
        public int NumbersOfCustomConnections { private set; get; }
    
        #region Events

        public static event Action OnCreatingLobby;
        public static event Action<Unity.Services.Lobbies.Models.Lobby, GameLobby> OnLobbyCreated; 
        public static event Action<string> OnLobbyFailedToCreate;
    
        public static event Action OnJoiningLobby;
        public static event Action<Unity.Services.Lobbies.Models.Lobby, GameLobby> OnLobbyJoined; 
        public static event Action<string> OnLobbyFailedToJoin; 
    
        public static event Action OnUpdatingLobby;
        public static event Action<Unity.Services.Lobbies.Models.Lobby, GameLobby> OnLobbyUpdated; 
        public static event Action<string> OnLobbyFailedToUpdate;
    
        public static event Action OnLeavingLobby;
        public static event Action<Unity.Services.Lobbies.Models.Lobby, GameLobby> OnLobbyLeft; 
        public static event Action<string> OnLobbyFailedToLeave;
        public static event Action OnLobbyFailedToFind;
        public static event Action<Unity.Services.Lobbies.Models.Lobby> OnSyncLobby;

        public static event Action OnDestroyingLobby;
        public static event Action OnLobbyDestroyed; 
        public static event Action<string> OnLobbyFailedToDestroy;

        public static event Action OnKickingPlayer;
        public static event Action PlayerKicked;
        public static event Action<string> OnPlayerFailedToKicked;
        public static event Action OnPlayerKicked;
    
        public static event Action OnChangingHost;
        public static event Action OnHostChanged;
        public static event Action<string> OnFailedToChangeHost;
    
        public static event Action<string> OnHeartBeatFailedToSend;

        #endregion
    
        public Unity.Services.Lobbies.Models.Lobby LobbyInstance { private set; get; }
        public bool DestroyLobbyAfterSessionStarted => _destroyLobbyAfterSessionStarted;
    
        private float _heartBeatTimer;
        private float _lobbyPollTimer;
        private bool _sessionStarted;
        private bool _getLobby;
        private int _maxTries = 3;
        private int _tries;
        private bool _canInteractWithLobby;
        private bool _destroyingLobbyAtEnd;

        #region UnityFuctions
    
        private void Update()
        {
            HandleLobbyHeartBeat();
            HandleLobbyState();
        }
    
        #endregion
    
        #region LobbyFunctions

        /// <summary>
        /// Sending Heartbeat to keep lobby alive.
        /// </summary>
        private async void HandleLobbyHeartBeat()
        {
            if(LobbyInstance == null) return;
            if(!IsLobbyHost()) return;
        
            _heartBeatTimer -= Time.deltaTime;
            if (!(_heartBeatTimer < 0)) return;
            _heartBeatTimer = _lobbyRefreshTime;
            try
            {
                //Debug.Log(LobbyInstance.Id);
                await LobbyService.Instance.SendHeartbeatPingAsync(LobbyInstance.Id);
            }
            catch (LobbyServiceException e)
            {
                OnHeartBeatFailedToSend?.Invoke(e.Message);
                Debug.Log(e);
                throw;
            }
        
        }

        /// <summary>
        /// Handling Lobby states at different intervals and on different clients.
        /// Checking whether player is kicked or not, host has started session etc.
        /// </summary>
        private async void HandleLobbyState()
        {
            if(LobbyInstance == null) return;
            if(GameNetworkHandler.Instance.SessionStarted) return;
        
            _lobbyPollTimer -= Time.deltaTime;
            if (!(_lobbyPollTimer < 0f)) return;
            var lobbyPollTimerMax = 1.25f;
            _lobbyPollTimer = lobbyPollTimerMax;
        
            try
            {
                //Debug.Log(LobbyInstance.Id);
                if (!_getLobby)
                {
                    _getLobby = true;
                    _canInteractWithLobby = false;
                    var lobby = await LobbyService.Instance.GetLobbyAsync(LobbyInstance.Id);
                    _tries = 0;
                    _canInteractWithLobby = true;
                    LobbyInstance = lobby;
                    _getLobby = false;
                }
            
            }
            catch (LobbyServiceException e)
            {
                _tries++;
                _getLobby = false;
                _canInteractWithLobby = false;
                if (_tries >= _maxTries)
                {
                    LobbyInstance = null;
                    OnLobbyFailedToFind?.Invoke();
                }
                Debug.Log(e);
                return;
            }
        
            if(LobbyInstance == null) return;
        
            if (!CheckPlayerIsInLobby())
            {
                OnPlayerKicked?.Invoke();
                LobbyInstance = null;
                return;
            }
        
            OnSyncLobby?.Invoke(LobbyInstance);
        
            if(!CheckWhetherGameHasStarted()) return;
        
            if (!IsLobbyHost())
            {
                GameNetworkHandler.Instance.JoinGame();
            }
        
            if (LobbyInstance.Data["DestroyLobbyAfterSession"].Value == "true")
            {
                LobbyInstance = null;
            }
        
        }
    
        private bool CheckWhetherGameHasStarted() => LobbyInstance.Data["START_GAME"].Value != "0";

        private bool CheckPlayerIsInLobby()
        {
            var currentPlayer = AuthenticationService.Instance.PlayerId;
            var playerInLobby = LobbyInstance.Players.FirstOrDefault(player => player.Id == currentPlayer);
            return playerInLobby != null;
        }

    
        #region CreatingLobby

        private bool _lobbyIsBeingCreated;
    
        /// <summary>
        /// Creating a lobby and handling all the cases if whether lobby was created successfully or not.
        /// Sending callbacks to update UI.
        /// </summary>
        /// <param name="lobbyData">Data required to create a lobby i.e public/private, is lobby protected etc.</param>
        /// <param name="shouldUpdateUI">Should update UI or not.</param>
        /// <returns>Returns a task so it can wait until it finishes</returns>
        public async void CreateLobby(LobbyData lobbyData, bool shouldUpdateUI) => await CreateLobbyAsync(lobbyData, shouldUpdateUI);
    
        private async Task CreateLobbyAsync(LobbyData lobbyData, bool shouldUpdateUI)
        {
            if (_lobbyIsBeingCreated)
            {
                return;
            }

            _lobbyIsBeingCreated = true;
        
            try
            {
                OnCreatingLobby?.Invoke();
            
                var player = GetPlayer();
            
                var shouldUpdateLobbyName = string.IsNullOrWhiteSpace(lobbyData.LobbyName);
                var lobbyName = shouldUpdateLobbyName ? $"{player.Data["PlayerName"].Value}'s Lobby" : lobbyData.LobbyName;
                _destroyLobbyAfterSessionStarted = !lobbyData.KeepLobbyAlive;
                HasCustomConnection = lobbyData.CustomConnections;
                NumbersOfCustomConnections = lobbyData.CustomMaxConnections;
                _destroyLobbyWithHost = lobbyData.DestroyLobbyWithHost;
                var destroyLobbyAfterSession = _destroyLobbyAfterSessionStarted ? "true" : "false"; 
                var options = new CreateLobbyOptions
                {
                    Player = player,
                    IsPrivate = !lobbyData.IsPublicLobby,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "LOBBY_NAME", new DataObject(DataObject.VisibilityOptions.Member, lobbyName)},
                        {"DestroyLobbyAfterSession",new DataObject(DataObject.VisibilityOptions.Public, destroyLobbyAfterSession)},
                        { "START_GAME", new DataObject(DataObject.VisibilityOptions.Public, "0") },
                        { "PLAYER_COUNT", new DataObject(DataObject.VisibilityOptions.Member, "0") },
                    }
                };

                if (lobbyData.HasPassword)
                    options.Password = lobbyData.Password;
            
                var lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, lobbyData.MaxLobbyPlayers,options);

                _lobbyIsBeingCreated = false;
            
                Debug.Log($"Created Lobby {lobby.Name} with Code {lobby.LobbyCode}");
            
                LobbyInstance = lobby;
            
            
                if (shouldUpdateUI)
                {
                    OnLobbyCreated?.Invoke(lobby, this);
                }
            }
            catch (LobbyServiceException e)
            {
                _lobbyIsBeingCreated = false;
                OnLobbyFailedToCreate?.Invoke(e.Message);
                Debug.Log(e);
                throw;
            }
        }
    
        #endregion

        #region LobbyRequirements

        private Player GetPlayer() => new() {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, $"{Initialization.Instance.PlayerName}") }
            }
        };
    
        public bool IsLobbyHost() => 
            LobbyInstance != null && LobbyInstance.HostId == AuthenticationService.Instance.PlayerId;

        #endregion
    
        #region JoiningLobby

        private bool _isJoiningLobby;
    
        /// <summary>
        /// Joining a lobby and handling all the cases if whether lobby was joined successfully or not.
        /// Sending callbacks to update UI.
        /// Different handling for public and private lobby.
        /// If it's public lobby you have to join through Lobby Id.
        /// If it's private you have to join through lobby code.
        /// </summary>
        /// <param name="lobbyInfo">Info to join a lobby i.e lobby id or lobby code.</param>
        /// <param name="publicLobby">Whether it's a public or private lobby.</param>
        /// <param name="password">If Lobby is protected it requires password.</param>
        /// <param name="shouldUpdateUI">Should update UI or not.</param>
        public async void JoinLobby(string lobbyInfo, bool publicLobby,string password = null,bool shouldUpdateUI = true)
        {
            if (!publicLobby)
            {
                await JoinPrivateLobbyByCode(lobbyInfo ,password ,shouldUpdateUI);
            }
            else
            {
                await JoinLobbyById(lobbyInfo ,password ,shouldUpdateUI);
            }
        }
    
        private async Task JoinPrivateLobbyByCode(string lobbyCode ,string pass, bool shouldUpdateUI) 
        {
            if (_isJoiningLobby)
            {
                return;
            }

            _isJoiningLobby = true;
        
            OnJoiningLobby?.Invoke();
        
            var player = GetPlayer();

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                _isJoiningLobby = false;
                OnLobbyFailedToJoin?.Invoke("Invalid Code Please Try Again!");
                return;
            }
        
            try
            {
                var options = new JoinLobbyByCodeOptions()
                {
                    Player = player,
                };

                if (pass is { Length: >= 8 }) options.Password = pass;
            
                var lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            
                _isJoiningLobby = false;
                LobbyInstance = lobby;
            
                if (shouldUpdateUI)
                {
                    OnLobbyJoined?.Invoke(lobby, this);
                }
            }
        
            catch (LobbyServiceException e)
            {
                _isJoiningLobby = false;
                OnLobbyFailedToJoin?.Invoke(e.Message);
                Debug.Log(e);
                throw;
            }
        }
    
        private async Task JoinLobbyById(string lobbyID, string pass,bool shouldUpdateUI) 
        {
            if (_isJoiningLobby)
            {
                return;
            }

            _isJoiningLobby = true;
        
            OnJoiningLobby?.Invoke();
        
            var player = GetPlayer();

            try
            {
                var options = new JoinLobbyByIdOptions()
                {
                    Player = player,
                };

                if (pass is { Length: >= 8 }) options.Password = pass;

                var lobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyID, options);

                if (shouldUpdateUI)
                {
                    OnLobbyJoined?.Invoke(lobby, this);
                }
            
                LobbyInstance = lobby;
                _isJoiningLobby = false;
            }
        
            catch (LobbyServiceException e)
            {
                _isJoiningLobby = false;
                OnLobbyFailedToJoin?.Invoke(e.Message);
                Debug.Log(e);
            }
        }
    
        #endregion
    
        #region HostMigration

        private bool _isChangingHost;
    
        /// <summary>
        /// Updating lobby with a new Host.
        /// Dealing with the cases whether it was successful or not.
        /// </summary>
        /// <param name="player">Player to make the new host.</param>
        public void ChangeHost(Player player)
        {
            if(!_canInteractWithLobby) return;
        
            if(_isChangingHost) return;
            _isChangingHost = true;

            OnChangingHost?.Invoke();
        
            try
            {
                UpdateLobby(LobbyInstance.Id, new UpdateLobbyOptions
                {
                    HostId = player.Id
                }, () =>
                {
                    _isChangingHost = false;
                    OnHostChanged?.Invoke();
                });
            }
            catch (LobbyServiceException e)
            {
                OnFailedToChangeHost?.Invoke(e.Message);
                _isChangingHost = false;
                Debug.Log(e);
                throw;
            }
        }

        #endregion
    
        #region UpdateLobby

        private bool _isUpdatingLobby;
    
        /// <summary>
        /// Sending an update call to modify current lobby.
        /// Dealing with cases whether it was successful or not
        /// </summary>
        /// <param name="lobbyId">Id of the lobby to update.</param>
        /// <param name="lobbyOptions">Updated options i.e Lobby options we've set in lobby creation.</param>
        /// <param name="onComplete">Callback when updating is completed.</param>
        public async void UpdateLobby(string lobbyId, UpdateLobbyOptions lobbyOptions, Action onComplete = null) =>
            await UpdateLobbyAsync(lobbyId, lobbyOptions, onComplete);

        private async Task UpdateLobbyAsync(string lobbyId, UpdateLobbyOptions lobbyOptions, Action onComplete = null)
        {
            if(_isUpdatingLobby) return;

            _isUpdatingLobby = true;
        
            OnUpdatingLobby?.Invoke();
        
            try
            {
                Debug.Log("Updating Lobby");
                var lobby = await Lobbies.Instance.UpdateLobbyAsync(lobbyId, lobbyOptions);
                LobbyInstance = lobby;
                _isUpdatingLobby = false;
                OnLobbyUpdated?.Invoke(lobby, this);
                onComplete?.Invoke();
            }
            catch (Exception e)
            {
                OnLobbyFailedToUpdate?.Invoke(e.Message);
                _isUpdatingLobby = false;
                Console.WriteLine(e);
                throw;
            }
        }
    
        #endregion
    
        #region LeavingLobby

        private bool _isLeavingLobby;
        private bool _isKickingPlayer;
        private bool _isRemovingPlayer;
        private bool _hostHasLeftLobby;
    
        /// <summary>
        /// Leaving a lobby.
        /// Differs for host and client.
        /// For host if the lobby is supposed to be destroyed with host, the host destroys lobby when leaves.
        /// Else it just removes itself from the lobby, for client handling as well.
        /// Dealing with cases whether it was successful or not.
        /// </summary>
        /// <param name="onComplete">Callback when Leaving is completed.</param>
        public void LeaveLobby(Action onComplete = null)
        {
            if(!_canInteractWithLobby) return;
        
            if (_isLeavingLobby) { return; }

            _isLeavingLobby = true;
        
            OnLeavingLobby?.Invoke();

            if (LobbyInstance == null)
            {
                _isLeavingLobby = false;
                OnLobbyLeft?.Invoke(null, this);
                onComplete?.Invoke();
                return;
            }
        
            try
            {
                _hostHasLeftLobby = IsLobbyHost();
                var playerId = AuthenticationService.Instance.PlayerId;
                if (_hostHasLeftLobby && _destroyLobbyWithHost)
                {
                    OnLobbyLeft?.Invoke(LobbyInstance, this);
                    DestroyLobby(() =>
                    {
                        _isLeavingLobby = false;
                        onComplete?.Invoke();
                    });
                    return;
                }
            
                RemoveAPlayer(LobbyInstance.Id, playerId, () =>
                {
                    _isLeavingLobby = false;
                    LobbyInstance = null;
                    OnLobbyLeft?.Invoke(LobbyInstance, this);
                    onComplete?.Invoke();
                } );
            }
            catch (LobbyServiceException e)
            {
                _isLeavingLobby = false;
                if (IsLobbyHost()) _hostHasLeftLobby = false;
                OnLobbyFailedToLeave?.Invoke(e.Message);
                Debug.Log(e);
                throw;
            }
        }
    
        /// <summary>
        /// Kicking a player from lobby.
        /// Only host can Kick other players
        /// </summary>
        /// <param name="player">Player to kick.</param>
        public void KickAPlayer(Player player)
        {
            if(!_canInteractWithLobby) return;
        
            if(_isKickingPlayer) return;
            _isKickingPlayer = true;
        
            OnKickingPlayer?.Invoke();
        
            try
            {
                RemoveAPlayer(LobbyInstance.Id, player.Id, () =>
                {
                    _isKickingPlayer = false;
                });
            
                PlayerKicked?.Invoke();
            }
            catch (LobbyServiceException e)
            {
                _isKickingPlayer = false;
                OnPlayerFailedToKicked?.Invoke(e.Message);
                Debug.Log(e);
                throw;
            }
        }
    
        /// <summary>
        /// General function to remove a player from lobby.
        /// </summary>
        /// <param name="lobbyId">From the lobby.</param>
        /// <param name="playerId">Player to remove.</param>
        /// <param name="onComplete">On player removed callback.</param>
        public async void RemoveAPlayer(string lobbyId, string playerId, Action onComplete = null)
        {
            await RemoveAPlayerAsync(lobbyId, playerId, onComplete);
        }
        private async Task RemoveAPlayerAsync(string lobbyId, string playerId, Action onComplete = null)
        {
            if(_isRemovingPlayer) return;
            _isRemovingPlayer = true;
        
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                onComplete?.Invoke();
                _isRemovingPlayer = false;
            }
            catch (LobbyServiceException e)
            {
                _isRemovingPlayer = false;
                Debug.Log(e);
                throw;
            }
        }
        #endregion
    
        #region DestroyingLobby

        private bool _isDestroyingLobby;
    
        /// <summary>
        /// Send Call to Destroy Lobby.
        /// Only Host can Destroy Lobby.
        /// </summary>
        /// <param name="onComplete">On Lobby Destroyed callback.</param>
        public async void DestroyLobby(Action onComplete = null) => await AsyncDestroyLobby(onComplete);
        private async Task AsyncDestroyLobby(Action onComplete)
        {
            if(_isDestroyingLobby) return;
            _isDestroyingLobby = true;
        
            OnDestroyingLobby?.Invoke();
        
            try
            {
                if (!IsLobbyHost())
                {
                    LobbyInstance = null;
                    _isDestroyingLobby = false;
                    OnLobbyDestroyed?.Invoke();
                    onComplete?.Invoke();
                    return;
                }
                await LobbyService.Instance.DeleteLobbyAsync(LobbyInstance.Id);
                
                LobbyInstance = null;
                OnLobbyDestroyed?.Invoke();
                onComplete?.Invoke();
                _isDestroyingLobby = false;
            }
            catch (LobbyServiceException e)
            {
                OnLobbyFailedToDestroy?.Invoke(e.Message);
                _isDestroyingLobby = false;
                Debug.Log(e);
                throw;
            }
        }

        #endregion

        #endregion

        private async void OnApplicationQuit()
        {
            if(Initialization.Instance == null) { return;}
        
            if (!Initialization.Instance.IsInitialized) return;
        
            await LeaveLobbyIfExits();
        }

        /// <summary>
        /// On Leaving the game if the player is in lobby.
        /// If its a host destroy Lobby.
        /// Else if its a client remove him from the lobby.
        /// </summary>
        public async Task LeaveLobbyIfExits()
        {
            if (LobbyInstance == null) return;

            if (IsLobbyHost())
            {
                try
                {
                    if(_destroyingLobbyAtEnd) return;
                    _destroyingLobbyAtEnd = true;
                    await LobbyService.Instance.DeleteLobbyAsync(LobbyInstance.Id);
                    Debug.Log("Lobby Destroyed");
                    LobbyInstance = null;
                    _destroyingLobbyAtEnd = false;
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    throw;
                }
           
            }
            else
            {
                try
                {
                    await LobbyService.Instance.RemovePlayerAsync(LobbyInstance.Id,
                        AuthenticationService.Instance.PlayerId);
                    Debug.Log("Left Lobby");
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    throw;
                }
            
            }
        }
    }

    public struct LobbyData
    {
        public string LobbyName;
        public int MaxLobbyPlayers;
        public bool IsPublicLobby;
        public bool CustomConnections;
        public bool HasPassword;
        public bool KeepLobbyAlive;
        public bool DestroyLobbyWithHost;
        public string Password;
        public int CustomMaxConnections;
    }
}