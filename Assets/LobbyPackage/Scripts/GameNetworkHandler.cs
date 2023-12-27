using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LobbyPackage.Scripts.HelperClasses;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LobbyPackage.Scripts
{
    public class GameNetworkHandler : Singleton<GameNetworkHandler>
    {
        [SerializeField] private bool _loadSeparateGameScene;
        [SerializeField] private string _sceneToLoad;
        [SerializeField] private string _baseSceneToReturn;
    
        public static event Action OnStartingGame;
        public static Action OnGameStarted;
        public static event Action<string> OnGameFailedToStart;
        public static event Action OnSessionFailedToStart;
        
        public static event Action OnSessionFailedToJoin;
        public static event Action OnLeavingSession;
        public static event Action<bool, string> OnSessionLeft;
    
        public bool SessionStarted { private set; get; }
        public bool InSession { set; get; }
        
        public string BaseSceneToReturn => _baseSceneToReturn;
    
        private bool _setCustomRelaySize;
        private int _relaySize;
        private bool _serverStarted;
        private bool _clientStarted;
        private bool _sessionLeft;
    
        private void OnEnable()
        {
            NetworkController.OnClientConnected  += ClientStarted;
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
            if (_isClosingNetwork) return;
            CloseNetwork(true, _baseSceneToReturn);
        }

        private void OnDisable()
        {
            NetworkController.OnClientConnected -= ClientStarted;
            if(NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientDisconnectCallback -= ClientDisconnected;
            NetworkManager.Singleton.OnClientStopped -= ClientStopped;
        }
        
        private void ClientDisconnected(ulong client)
        {
            if (NetworkManager.Singleton.LocalClientId != client) return;
            CloseNetwork(true, _baseSceneToReturn);
        }
    
        private void ClientStarted(bool isHost)
        {
            if (isHost)
            {
                GameLobby.Instance.UpdateLobby(GameLobby.Instance.LobbyInstance.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "SESSION_STARTED", new DataObject(DataObject.VisibilityOptions.Public, "1") },
                    }
                });
                ResetHostSide();
                ServerTimedOut = false;
            }
            else
            {
                ResetClient();
                ClientTimedOut = false;
            }
            
            SessionStarted = true;
        }

        private void ResetHostSide()
        {
            _isSessionStarting = false;
            if (_startingHostTimeout == null) return;
            if (_startingHostTimeoutCo == null) return;
            StopCoroutine(_startingHostTimeoutCo);
            _startingHostTimeout.Reset();
        }

        private void ResetClient()
        {
            IsJoiningSession = false;
            if (_startingClientTimeout == null) return;
            if (_startingClientTimeoutCo == null) return;
            StopCoroutine(_startingClientTimeoutCo);
            _startingClientTimeout.Reset();
        }


        #region StartGame
    
        private TimeOut _startingHostTimeout;
        private Coroutine _startingHostTimeoutCo;
        public bool ServerTimedOut { get; private set; }
        private bool _isSessionStarting;
    
        public async void StartGame(string reg = null)
        {
            if(_isSessionStarting) return;
            _isSessionStarting = true;
            if(_startingHostTimeout is {IsTimerRunning: true}) return;

            ServerTimedOut = false;
            
            var gameLobby = GameLobby.Instance;
        
            if(!gameLobby.IsLobbyHost()) return;
        
            string relayCode;
            bool serverStarted;

            _setCustomRelaySize = gameLobby.HasCustomConnection;
            _relaySize = gameLobby.NumbersOfCustomConnections;
        
            var gameRelay = GameRelay.Instance;
        
            try
            {
                relayCode = await gameRelay.CreateRelay(_setCustomRelaySize ? _relaySize :
                    gameLobby.LobbyInstance.MaxPlayers, reg);

                OnStartingGame?.Invoke();

                _startingHostTimeout ??= new TimeOut(35);
            
                if (_loadSeparateGameScene)
                {
                    _serverStarted = await Helper.LoadSceneAsync(() =>
                    {
                        var serverHasStarted = false;
                        _startingHostTimeoutCo = StartCoroutine(_startingHostTimeout.StartTimer( () =>
                        {
                            OnSessionFailedToStart?.Invoke();
                            ServerTimedOut = true;
                            SessionStarted = false;
                            _isSessionStarting = false;
                            if(serverHasStarted)
                                CloseNetwork(false);
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
                            CloseNetwork(false);
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
                CloseNetwork(false);
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
        public bool ClientTimedOut { private set; get; }
        public bool IsJoiningSession { private set; get; }
    
        public async void JoinGame(string relayCode, string plotId = null)
        {
            if(IsJoiningSession) return;
            IsJoiningSession = true;
            if(_startingClientTimeout is { IsTimerRunning: true }) return;

            ClientTimedOut = false;
            
            var gameRelay = GameRelay.Instance;
        
            try
            {
                var relayToJoin = await gameRelay.JoinRelay(relayCode);
            
                OnStartingGame?.Invoke();
            
                //await Helper.LoadAdditiveSceneAsync(null, "MultiplayerDependencies");
            
                var serverData = new RelayServerData(relayToJoin, "dtls");
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(serverData);
                _startingClientTimeout ??= new TimeOut(35);
                _startingClientTimeoutCo = StartCoroutine(_startingClientTimeout.StartTimer( () =>
                {
                    OnSessionFailedToJoin?.Invoke();
                    if(_clientStarted)
                        CloseNetwork(true, _baseSceneToReturn);
                    GameLobby.Instance.LeaveLobby(() =>
                    {
                        SessionStarted = false;
                        ClientTimedOut = true;
                        IsJoiningSession = false;
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
                    IsJoiningSession = false;
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
                OnLeavingSession?.Invoke();
                StopGame(() =>
                {
                    CloseNetwork(false);
                });
            }
            else
            {
                LeaveLobbyAndSession();
            }
        }

        private void LeaveLobbyAndSession() => GameLobby.Instance.LeaveLobby(
            () =>
            {
                CloseNetwork(true, _baseSceneToReturn);
            });

        private void ClientStopped(bool hostStopped)
        {
            OnClientStopped(_shouldChangeScene, _sceneName);
            _isClosingNetwork = false;
        }

        private bool _shouldChangeScene;
        private string _sceneName;

        private bool _isClosingNetwork;
        public void CloseNetwork(bool shouldChangeScene, string sceneName = null)
        {
            _isClosingNetwork = true;
            _shouldChangeScene = shouldChangeScene;
            _sceneName = sceneName;
            NetworkManager.Singleton.Shutdown();
        }
        
    
        private void OnClientStopped(bool shouldChangeScene, string sceneName = null)
        {
            if (!SessionStarted)
            {
                SessionLeft(shouldChangeScene, sceneName);
                return;
            }
            
            OnLeavingSession?.Invoke();
            StopGame(() => { SessionLeft(shouldChangeScene, sceneName); });
        }

        private void SessionLeft(bool shouldChangeScene, string sceneName)
        {
            OnSessionLeft?.Invoke(shouldChangeScene, sceneName);
            InSession = false;
        }

        #endregion
    
        #region StopGame
        private bool _isSessionStoping;
    
        public void StopGame(Action onComplete = null)
        {
            if(_isSessionStoping) return;
            _isSessionStoping = true;
            
            if(!SessionStarted) return;
        
            try
            {
                var gameLobby = GameLobby.Instance;
                if (!gameLobby.IsLobbyHost())
                {
                    SessionStarted = false;
                    _isSessionStoping = false;
                    ResetHostSide();
                    ResetClient();
                    onComplete?.Invoke();
                    return;
                }

                if (gameLobby.DestroyLobbyAfterSessionStarted)
                {
                    GameLobby.Instance.DestroyLobby(() =>
                    {
                        SessionStarted = false;
                        _isSessionStoping = false;
                        ResetHostSide();
                        ResetClient();
                        onComplete?.Invoke();    
                    });    
                    
                    return;
                }
                
                
                gameLobby.UpdateLobby(gameLobby.LobbyInstance.Id, new UpdateLobbyOptions 
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {"START_GAME", new DataObject(DataObject.VisibilityOptions.Public, "0")},
                        { "SESSION_STARTED", new DataObject(DataObject.VisibilityOptions.Public, "0") }, 
                    }
                }, () =>
                {
                    Debug.Log($"Session Stopped");
                    SessionStarted = false;
                    _isSessionStoping = false;
                    ResetHostSide();
                    ResetClient();
                    onComplete?.Invoke();
                });
            }
            catch (LobbyServiceException e)
            {
                CloseNetwork(true, _baseSceneToReturn);
                OnGameFailedToStart?.Invoke(e.Message);
                _isSessionStoping = false;
                SessionStarted = false;
                Debug.Log(e);
                throw;
            }
        }
        #endregion
    }
}
