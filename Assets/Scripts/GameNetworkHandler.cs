using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

public class GameNetworkHandler : Singleton<GameNetworkHandler>
{
    [SerializeField] private bool _setCustomRelaySize;
    [SerializeField] private int _relaySize;
    [SerializeField] private bool _loadSeparateGameScene;
    [SerializeField] private string _sceneToLoad;
    
    public static event Action OnStartingGame;
    public static  Action OnGameStarted;
    public static event Action<string> OnGameFailedToStart;
    
    public static event Action OnLeavingSession;
    public static event Action OnSessionLeft;
    
    public bool SessionStarted { private set; get; }
    
    private bool _serverStarted;
    private bool _sessionLeft;
    
    private void OnEnable()
    {
        NetworkController.OnClientConnected += ClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
        NetworkManager.Singleton.OnClientStopped += ClientStopped;
    }

    private void Update()
    {
        if (!NetworkManager.Singleton.ShutdownInProgress)
        {
            _sessionLeft = false;
            return;
        }
        if(_sessionLeft) return;
        _sessionLeft = true;
        NetworkManager.Singleton.Shutdown();
    }

    private void OnDisable()
    {
        NetworkController.OnClientConnected -= ClientConnected;
        if(NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientDisconnectCallback -= ClientDisconnected;
        NetworkManager.Singleton.OnClientStopped -= ClientStopped;
    }
    
    private void ClientConnected()
    {
        SessionStarted = true;
        _isJoiningSession = false;
    }
    
    private void ClientDisconnected(ulong client)
    {
        if (NetworkManager.Singleton.LocalClientId != client) return;
        NetworkManager.Singleton.Shutdown();
    }
    
    
    #region StartGame
    
    private bool _isSessionStarting;
    
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
        
        var gameRelay = GameRelay.Instance;
        
        try
        {
            var selectedRegion = gameRelay.Regions.FirstOrDefault(region => region == reg);
            relayCode = await gameRelay.CreateRelay(_setCustomRelaySize ? _relaySize :
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
    #endregion

    #region JoinGame

    private bool _isJoiningSession;
    
    public async void JoinGame()
    {
        if(_isJoiningSession) return;
        _isJoiningSession = true;

        var gameLobby = GameLobby.Instance;
        
        var relayCode = gameLobby.LobbyInstance.Data["START_GAME"].Value;

        var gameRelay = GameRelay.Instance;
        
        try
        {
            var relayToJoin = await gameRelay.JoinRelay(relayCode);
            
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

    #endregion

    #region LeaveGame

    public void LeaveGame(bool isHost)
    {
        if (isHost)
        {
            OnHostStopped();
        }
        else
        {
            GameLobby.Instance.LeaveLobby(() =>
            {
                NetworkManager.Singleton.Shutdown();
            });  
        }
    }
    
    private async void ClientStopped(bool hostStopped)
    {
        if (hostStopped) return;
        await OnClientStopped();
    }
    
    private void OnHostStopped()
    {
        OnLeavingSession?.Invoke();
        StopGame(() =>
        {
            NetworkManager.Singleton.Shutdown();
            OnSessionLeft?.Invoke();
        });
    }
    
    private async Task OnClientStopped()
    {
        OnLeavingSession?.Invoke();
        await Helper.LoadSceneAsync(() =>
        {
            GameNetworkHandler.Instance.StopGame(() => { OnSessionLeft?.Invoke(); });
            return true;
        }, "Lobby");
    }

    #endregion
    
    #region StopGame
    private bool _isSessionStoping;
    
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
