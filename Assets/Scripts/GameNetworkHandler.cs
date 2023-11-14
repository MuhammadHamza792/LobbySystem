using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HelperClasses;
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

    public static event Action OnStartingSession;
    public static event Action OnSessionStarted;
    public static event Action OnSessionFailedToStart;
    
    public static event Action OnJoiningSession;
    public static event Action OnSessionJoined;
    public static event Action OnSessionFailedToJoin;
    public static event Action OnLeavingSession;
    public static event Action OnSessionLeft;
    
    public bool SessionStarted { private set; get; }
    
    private bool _serverStarted;
    private bool _clientStarted;
    private bool _sessionLeft;
    
    private void OnEnable()
    {
        NetworkController.OnClientConnected += ClientConnected;
        NetworkController.OnClientConnected  += ClientStarted;
        NetworkManager.Singleton.OnServerStarted += ServerStarted;
        NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
        NetworkManager.Singleton.OnClientStopped += ClientStopped;
    }
    
    
    /// <summary>
    /// Checks If Host Has Crashed.
    /// </summary>
    private void Update()
    {
        if(GameLobby.Instance.IsLobbyHost()) return;
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
        NetworkController.OnClientConnected -= ClientStarted;
        if(NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnServerStarted -= ServerStarted;
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
    
    private void ClientStarted()
    {
        _isJoiningSession = false;
        SessionStarted = true;
        if(_startingClientTimeout == null) return;
        if(_startingClientTimeoutCo == null) return;
        Debug.Log("Client Timeout Coroutine Stopped");
        StopCoroutine(_startingClientTimeoutCo);
        _startingClientTimeout.Reset();
    }
    
    private void ServerStarted()
    {
        Debug.Log("Server Started");
        SessionStarted = true;
        _isSessionStarting = false;
        if(_startingHostTimeout == null) return;
        if(_startingHostTimeoutCo == null) return;
        Debug.Log("Host Timeout Coroutine Stopped");
        StopCoroutine(_startingHostTimeoutCo);
        _startingHostTimeout.Reset();
    }
    
    #region StartGame
    
    private TimeOut _startingHostTimeout;
    private Coroutine _startingHostTimeoutCo;
    private bool _isSessionStarting;
    
    public async void StartGame(string reg = null)
    {
        if(_isSessionStarting) return;
        _isSessionStarting = true;
        if(_startingHostTimeout is {IsTimerRunning: true}) return;

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

            _startingHostTimeout ??= new TimeOut(10);
            
            if (_loadSeparateGameScene)
            {
                _serverStarted = await Helper.LoadSceneAsync(() =>
                {
                    var serverHasStarted = false;
                    _startingHostTimeoutCo = StartCoroutine(_startingHostTimeout.StartTimer( () =>
                    {
                        OnSessionFailedToStart?.Invoke();
                        if(serverHasStarted)
                            NetworkManager.Singleton.Shutdown();
                    }));
                    serverHasStarted = NetworkManager.Singleton.StartHost();
                    return serverHasStarted;
                }, _sceneToLoad);
                
            }
            else
            {
                _startingHostTimeoutCo = StartCoroutine(_startingHostTimeout.StartTimer( () =>
                {
                    OnSessionFailedToStart?.Invoke();
                    if(_serverStarted)
                        NetworkManager.Singleton.Shutdown();
                }));
                _serverStarted = NetworkManager.Singleton.StartHost();
            }
            
            if (!_serverStarted)
            {
                OnGameFailedToStart?.Invoke("Failed To Start Game.");
                _isSessionStarting = false;
                SessionStarted = false;
                return;
            }
            
            //OnGameStarted?.Invoke();
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

    private TimeOut _startingClientTimeout;
    private Coroutine _startingClientTimeoutCo;
    private bool _isJoiningSession;
    
    public async void JoinGame()
    {
        if(_isJoiningSession) return;
        _isJoiningSession = true;
        if(_startingClientTimeout is { IsTimerRunning: true }) return;

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
            _startingClientTimeout ??= new TimeOut(10);
            _startingClientTimeoutCo = StartCoroutine(_startingClientTimeout.StartTimer( () =>
            {
                OnSessionFailedToJoin?.Invoke();
                if(_clientStarted)
                    NetworkManager.Singleton.Shutdown();
                GameLobby.Instance.LeaveLobby(() =>
                {
                    SessionStarted = false;
                    _isJoiningSession = false;
                });
            }));
            _clientStarted = NetworkManager.Singleton.StartClient();
        }
        catch (RelayServiceException e)
        {
            OnGameFailedToStart?.Invoke(e.Message);
            GameLobby.Instance.LeaveLobby(() =>
            {
                SessionStarted = false;
                _isJoiningSession = false;
            });
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
            LeaveLobbyAndSession();
        }
    }

    private void LeaveLobbyAndSession() => GameLobby.Instance.LeaveLobby(
        () =>
        {
            NetworkManager.Singleton.Shutdown();
        });

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
            StopGame(() =>
            {
                OnSessionLeft?.Invoke();
            });
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
            if (gameLobby.DestroyLobbyAfterSessionStarted ||
                !gameLobby.IsLobbyHost())
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
