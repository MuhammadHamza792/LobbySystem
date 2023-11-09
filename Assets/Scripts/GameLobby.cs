using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UI.Notify;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class GameLobby : Singleton<GameLobby>
{
    [SerializeField] private bool _destroyLobbyWithHost;
    [SerializeField] private float _lobbyRefreshTime = 15f;
    [SerializeField] private bool _destroyLobbyAfterSessionStarted;

    public bool HasCustomConnection { private set; get; }
    public int NumbersOfCustomConnections { private set; get; }
    
    #region Events

    public static event Action OnCreatingLobby;
    public static event Action<Lobby, GameLobby> OnLobbyCreated; 
    public static event Action<string> OnLobbyFailedToCreate;
    
    public static event Action OnJoiningLobby;
    public static event Action<Lobby, GameLobby> OnLobbyJoined; 
    public static event Action<string> OnLobbyFailedToJoin; 
    
    public static event Action OnUpdatingLobby;
    public static event Action<Lobby, GameLobby> OnLobbyUpdated; 
    public static event Action<string> OnLobbyFailedToUpdate;
    
    public static event Action OnLeavingLobby;
    public static event Action<Lobby, GameLobby> OnLobbyLeft; 
    public static event Action<string> OnLobbyFailedToLeave;
    public static event Action OnLobbyFailedToFind;
    public static event Action<Lobby> OnSyncLobby;

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
    
    public Lobby LobbyInstance { private set; get; }
    public bool DestroyLobbyAfterSessionStarted => _destroyLobbyAfterSessionStarted;
    
    private float _heartBeatTimer;
    private float _lobbyPollTimer;
    private bool _sessionStarted;

    #region UnityFuctions
    
    private void Update()
    {
        HandleLobbyHeartBeat();
        HandleLobbyState();
    }
    
    #endregion
    
    #region LobbyFunctions

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

    private async void HandleLobbyState()
    {
        if(LobbyInstance == null) return;
        
        _lobbyPollTimer -= Time.deltaTime;
        if (!(_lobbyPollTimer < 0f)) return;
        var lobbyPollTimerMax = 1.25f;
        _lobbyPollTimer = lobbyPollTimerMax;
        
        try
        {
            //Debug.Log(LobbyInstance.Id);
            var lobby = await LobbyService.Instance.GetLobbyAsync(LobbyInstance.Id);
            LobbyInstance = lobby;
        }
        catch (LobbyServiceException e)
        {
            LobbyInstance = null;
            OnLobbyFailedToFind?.Invoke();
            Debug.Log(e);
            return;
        }
        
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
            GameRelay.Instance.JoinGame();
        }
        
        if (_destroyLobbyAfterSessionStarted)
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
                    /*{
                        "HOST_PLOT_DATA",
                        new DataObject(DataObject.VisibilityOptions.Member, PlotDataManager.data.id)
                    },*/
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
                //OnLobbyCreated?.Invoke(this, lobby);
                //OnLobbyChanged?.Invoke(this, lobby);
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

            LobbyInstance = lobby;
            _isJoiningLobby = false;
            
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
        }
    }
    
    #endregion
    
    #region HostMigration

    private bool _isChangingHost;
    
    public void ChangeHost(Player player)
    {
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
    
    public async void UpdateLobby(string lobbyId, UpdateLobbyOptions lobbyOptions, Action onComplete = null) =>
        await UpdateLobbyAsync(lobbyId, lobbyOptions, onComplete);

    private async Task UpdateLobbyAsync(string lobbyId, UpdateLobbyOptions lobbyOptions, Action onComplete = null)
    {
        if(_isUpdatingLobby) return;

        _isUpdatingLobby = true;
        
        OnUpdatingLobby?.Invoke();
        
        try
        {
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
    
    [Button]
    public void LeaveLobby()
    {
        if (_isLeavingLobby) { return; }

        _isLeavingLobby = true;
        
        OnLeavingLobby?.Invoke();
        
        try
        {
            _hostHasLeftLobby = IsLobbyHost();
            var playerId = AuthenticationService.Instance.PlayerId;
            if (_hostHasLeftLobby && _destroyLobbyWithHost)
            {
                DestroyLobby();
                _isLeavingLobby = false;
                return;
            }
            
            RemoveAPlayer(LobbyInstance.Id, playerId, () =>
            {
                _isLeavingLobby = false;
                LobbyInstance = null;
                OnLobbyLeft?.Invoke(LobbyInstance, this);
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
    public void KickAPlayer(Player player)
    {
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
            _isDestroyingLobby = false;
            OnLobbyDestroyed?.Invoke();
            onComplete?.Invoke();
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
