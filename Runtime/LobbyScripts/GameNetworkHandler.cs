using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MHZ.HelperClasses;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

namespace MHZ.LobbyScripts
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
        public static event Action OnSessionFailedToLeave;

        public bool SessionStarted { private set; get; }
        public bool InSession { set; get; }

        public string BaseSceneToReturn => _baseSceneToReturn;
        public bool LoadSeparateGameScene => _loadSeparateGameScene;

        private bool _setCustomRelaySize;
        private int _relaySize;
        private bool _serverStarted;
        private bool _clientStarted;
        private bool _sessionLeft;

        private void OnEnable()
        {
            NetworkController.OnClientConnected += ClientStarted;
            NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;
            NetworkManager.Singleton.OnClientStopped += ClientStopped;
        }


        /// <summary>
        /// Checks If Host Has Crashed.
        /// </summary>
        private void Update()
        {
            if (GameLobby.Instance.IsLobbyHost()) return;
            if (!NetworkManager.Singleton.ShutdownInProgress)
            {
                _sessionLeft = false;
                return;
            }

            if (_sessionLeft) return;
            _sessionLeft = true;
            if (_isClosingNetwork) return;
            CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
        }

        private void OnDisable()
        {
            NetworkController.OnClientConnected -= ClientStarted;
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientDisconnectCallback -= ClientDisconnected;
            NetworkManager.Singleton.OnClientStopped -= ClientStopped;
        }

        private void ClientDisconnected(ulong client)
        {
            if (NetworkManager.Singleton.LocalClientId != client) return;
            CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
        }

        private void ClientStarted(bool isHost)
        {
            if (isHost)
            {
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
            if (_isSessionStarting) return;
            _isSessionStarting = true;
            if (_startingHostTimeout is { IsTimerRunning: true }) return;

            ServerTimedOut = false;

            var gameLobby = GameLobby.Instance;

            if (!gameLobby.IsLobbyHost()) return;

            var relayCode = string.Empty;
            bool serverStarted;

            _setCustomRelaySize = gameLobby.HasCustomConnection;
            _relaySize = gameLobby.NumbersOfCustomConnections;

            var gameRelay = GameRelay.Instance;

            try
            {
                OnStartingGame?.Invoke();

                _startingHostTimeout ??= new TimeOut(35);

                if (_loadSeparateGameScene)
                {
                    _serverStarted = await Helper.LoadSceneAsync(() =>
                    {
                        var serverHasStarted = false;
                        relayCode = CreateRelayAllocation(
                            _setCustomRelaySize ? _relaySize : gameLobby.LobbyInstance.MaxPlayers, reg, () =>
                            {
                                _startingHostTimeoutCo = StartCoroutine(_startingHostTimeout.StartTimer(() =>
                                {
                                    OnSessionFailedToStart?.Invoke();
                                    ServerTimedOut = true;
                                    SessionStarted = false;
                                    _isSessionStarting = false;
                                    if (serverHasStarted)
                                        CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
                                }));
                                serverHasStarted = NetworkManager.Singleton.StartHost();
                            } ).Result;
                        return serverHasStarted;
                    }, _sceneToLoad);

                }
                else
                {
                    relayCode = CreateRelayAllocation(
                        _setCustomRelaySize ? _relaySize : gameLobby.LobbyInstance.MaxPlayers, reg, () =>
                        { 
                            var serverHasStarted = false;
                            _startingHostTimeoutCo = StartCoroutine(_startingHostTimeout.StartTimer(() =>
                            {
                                OnSessionFailedToStart?.Invoke();
                                ServerTimedOut = true;
                                SessionStarted = false;
                                _isSessionStarting = false;
                                if (serverHasStarted)
                                    CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
                            }));
                            serverHasStarted = NetworkManager.Singleton.StartHost();
                        } ).Result;
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
            
            gameLobby.UpdateLobby(gameLobby.LobbyInstance.Id, new UpdateLobbyOptions 
            {
                Data = new Dictionary<string, DataObject>
                {
                    {"START_GAME", new DataObject(DataObject.VisibilityOptions.Member, relayCode)},
                    {"PLAYER_COUNT", new DataObject(DataObject.VisibilityOptions.Member, gameLobby.LobbyInstance.Players.Count.ToString())},
                    {"SESSION_STARTED", new DataObject(DataObject.VisibilityOptions.Public, "1") }
                }
            }, () =>
            {
                Debug.Log("Relay Code Updated");
                _isSessionStarting = false;
            }, () =>
            {
                CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
                OnGameFailedToStart?.Invoke("Failed to Start Game.");
                _isSessionStarting = false;
                SessionStarted = false;
            });
        }

        private async Task<string> CreateRelayAllocation(int maxPlayers, string reg, Action onRelayAllocated)
        {
            try
            {
                var relayCode = await GameRelay.Instance.CreateRelay(maxPlayers, reg);
                onRelayAllocated?.Invoke();
                return relayCode;
            }
            catch
            {
                await Helper.LoadSceneAsync(() =>
                {
                    OnGameFailedToStart?.Invoke("Failed To Start Game.");
                    _isSessionStarting = false;
                    SessionStarted = false;
                    return false;
                }, _baseSceneToReturn);
                throw;
            }
        }

        #endregion

        #region JoinGame

        private TimeOut _startingClientTimeout;
        private Coroutine _startingClientTimeoutCo;
        public bool ClientTimedOut { private set; get; }
        public bool IsJoiningSession { private set; get; }
    
        public async void JoinGame(string relayCode)
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
                        CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
                    GameLobby.Instance.LeaveLobby(() =>
                    {
                        SessionStarted = false;
                        ClientTimedOut = true;
                        IsJoiningSession = false;
                    }, () =>
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
                }, () =>
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
                    CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
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
                CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
            }, () =>
            {
                CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
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
                ResetState();
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
            
            var gameLobby = GameLobby.Instance;
            if (!gameLobby.IsLobbyHost())
            {
                ResetState();
                onComplete?.Invoke();
                return;
            }
            
            if (gameLobby.DestroyLobbyAfterSessionStarted)
            {
                GameLobby.Instance.DestroyLobby(() =>
                {
                    ResetState();
                    onComplete?.Invoke();    
                }, () =>
                {
                    ResetState();
                    GameLobby.Instance.AbandonLobby();
                });    
                
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
                ResetState();
                onComplete?.Invoke();
            }, () =>
            {
                ResetState();
                GameFailedToStop();
            });
            
        }

        private void GameFailedToStop()
        {
            SessionStarted = false;
            OnSessionFailedToLeave?.Invoke();
            CloseNetwork(_loadSeparateGameScene, _baseSceneToReturn);
            _isSessionStoping = false;
        }

        private void ResetState()
        {
            SessionStarted = false;
            _isSessionStoping = false;
            ResetHostSide();
            ResetClient();
        }

        #endregion
    }
}
