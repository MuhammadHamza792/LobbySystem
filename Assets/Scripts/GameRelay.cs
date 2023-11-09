using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameRelay : Singleton<GameRelay>
{
    [SerializeField] private bool _setCustomRelaySize;
    [SerializeField] private int _relaySize;
    [SerializeField] private bool _loadSeparateGameScene;
    [SerializeField] private string _sceneToLoad;
    
    [SerializeField] private List<string> _regions;

    public ReadOnlyCollection<string> Regions => new (_regions);
    
    public string RelayCode { private set; get; }
    
    public bool SessionStarted { private set; get; }
    
    private bool _serverStarted;
    
    #region Events

    public static event Action OnCreatingRelay; 
    public static event Action OnRelayCreated; 
    public static event Action<string> OnRelayFailedToCreate; 
    
    public static event Action OnJoiningRelay; 
    public static event Action OnRelayJoined; 
    public static event Action<string> OnRelayFailedToJoined;
    
    public static event Action OnStartingGame;
    public static  Action OnGameStarted;
    public static event Action<string> OnGameFailedToStart;
    
    #endregion

    private void OnEnable() => NetworkController.OnClientConnected += ClientConnected;

    private void OnDisable() => NetworkController.OnClientConnected -= ClientConnected;

    public async Task<string> CreateRelay(int maxPlayer, string region = null)
    {
        try
        {
            OnCreatingRelay?.Invoke();
            
            var relay = await Relay.Instance.CreateAllocationAsync(maxPlayer, region);

            RelayCode = await Relay.Instance.GetJoinCodeAsync(relay.AllocationId);
            
            var serverData = new RelayServerData(relay, "dtls");
            
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(serverData);
            
            OnRelayCreated?.Invoke();
            
            return RelayCode;
        }
        catch (RelayServiceException e)
        {
            OnRelayFailedToCreate?.Invoke(e.Message);
            Debug.Log(e);
            throw;
        }
    }
    
    public async Task<JoinAllocation> JoinRelay(string relayCode)
    {
        try
        {
            OnJoiningRelay?.Invoke();
            
            var relayToJoin = await Relay.Instance.JoinAllocationAsync(relayCode);
            
            OnRelayJoined?.Invoke();
            
            return relayToJoin;
        }
        catch (RelayServiceException e)
        {
            OnRelayFailedToJoined?.Invoke(e.Message);
            Debug.Log(e);
            throw;
        }
    }
    
    /*private async Task<bool> LoadSceneAsync(Func<bool> onComplete)
    {
        var task = SceneManager.LoadSceneAsync(_sceneToLoad);
        while (!task.isDone)
        {
            await Task.Yield();
        }
        var onTaskCompleted = onComplete?.Invoke();
        return onTaskCompleted != null && onTaskCompleted.Value;
    }*/
    
    #region StartSession

    private bool _isSessionStarting;
    private bool _isSessionStoping;
    
    public async void StartGame(string reg = null)
    {
        if(_isSessionStarting) return;
        _isSessionStarting = true;

        var gameLobby = GameLobby.Instance;
        
        if(!gameLobby.IsLobbyHost()) return;
        
        string relayCode;
        bool serverStarted;

        _setCustomRelaySize = gameLobby.HasCustomConnection;
        _relaySize = gameLobby.NumbersOfCustomConnections;
        
        try
        {
            var selectedRegion = _regions.FirstOrDefault(region => region == reg);
            relayCode = await CreateRelay(_setCustomRelaySize ? _relaySize :
                gameLobby.LobbyInstance.MaxPlayers, selectedRegion);

            OnStartingGame?.Invoke();
            
            if (_loadSeparateGameScene)
            {
                _serverStarted = await Helper.LoadSceneAsync(() =>
                {
                    var serverHasStarted = NetworkManager.Singleton.StartHost();
                    return serverHasStarted;
                }, _sceneToLoad);
                
            }
            else
            {
                _serverStarted = NetworkManager.Singleton.StartHost();
            }
            
            if (!_serverStarted)
            {
                OnGameFailedToStart?.Invoke("Failed To Start Server.");
                _isSessionStarting = false;
                SessionStarted = false;
                return;
            }
            
            //OnGameStarted?.Invoke();
            SessionStarted = true;
        }
    
        catch (RelayServiceException e)
        {
            OnGameFailedToStart?.Invoke(e.Message);
            _isSessionStarting = false;
            SessionStarted = false;
            Debug.Log(e);
            return;
        }
        
        try
        {
            gameLobby.UpdateLobby(gameLobby.LobbyInstance.Id, new UpdateLobbyOptions 
            {
                Data = new Dictionary<string, DataObject>
                {
                    {"START_GAME", new DataObject(DataObject.VisibilityOptions.Public, relayCode)},
                    {"PLAYER_COUNT", new DataObject(DataObject.VisibilityOptions.Member, gameLobby.LobbyInstance.Players.Count.ToString())}
                }
            }, () => _isSessionStarting = false);
        }
        catch (LobbyServiceException e)
        {
            NetworkManager.Singleton.Shutdown();
            OnGameFailedToStart?.Invoke(e.Message);
            _isSessionStarting = false;
            SessionStarted = false;
            Debug.Log(e);
            throw;
        }
        
    }
    private bool _isJoiningSession;
    
    public async void JoinGame()
    {
        if(_isJoiningSession) return;
        _isJoiningSession = true;

        var gameLobby = GameLobby.Instance;
        
        var relayCode = gameLobby.LobbyInstance.Data["START_GAME"].Value;

        try
        {
            var relayToJoin = await JoinRelay(relayCode);
            
            OnStartingGame?.Invoke();
            
            //await Helper.LoadAdditiveSceneAsync(null, "MultiplayerDependencies");
            
            var serverData = new RelayServerData(relayToJoin, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(serverData);
            NetworkManager.Singleton.StartClient();
            
        }
        catch (RelayServiceException e)
        {
            OnGameFailedToStart?.Invoke(e.Message);
            SessionStarted = false;
            _isJoiningSession = false;
            Console.WriteLine(e);
            throw;
        }
        
    }

    private async void ClientConnected()
    {
        SessionStarted = true;
        _isJoiningSession = false;
    }

    public void StopGame(Action onComplete = null)
    {
        if(_isSessionStoping) return;
        _isSessionStoping = true;
        
        try
        {
            var gameLobby = GameLobby.Instance;
            if (gameLobby.DestroyLobbyAfterSessionStarted || !gameLobby.IsLobbyHost())
            {
                SessionStarted = false;
                _isSessionStoping = false;
                onComplete?.Invoke();
                return;
            }
            gameLobby.UpdateLobby(gameLobby.LobbyInstance.Id, new UpdateLobbyOptions 
            {
                Data = new Dictionary<string, DataObject>
                {
                    {"START_GAME", new DataObject(DataObject.VisibilityOptions.Public, "0")},
                }
            }, () =>
            {
                Debug.Log($"Session Stopped");
                SessionStarted = false;
                _isSessionStoping = false;
                onComplete?.Invoke();
            });
        }
        catch (LobbyServiceException e)
        {
            NetworkManager.Singleton.Shutdown();
            OnGameFailedToStart?.Invoke(e.Message);
            _isSessionStoping = false;
            SessionStarted = false;
            Debug.Log(e);
            throw;
        }
    }
    
    #endregion
}
