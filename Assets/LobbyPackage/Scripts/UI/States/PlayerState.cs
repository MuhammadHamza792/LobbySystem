using LobbyPackage.Scripts.UI.Notify;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyPackage.Scripts.UI.States
{
    public class PlayerState : MonoBehaviour, IPanelState , INotifier
    {
        [Header("UI References")] 
        [SerializeField] private TextMeshProUGUI _lobbyName;
        [SerializeField] private TextMeshProUGUI _lobbyCode;
        [SerializeField] private TMP_InputField _searchField;
        [SerializeField] private TMP_Dropdown _regions;
        [SerializeField] private Button _leave;
        [SerializeField] private Button _startGame;
        [SerializeField] private Button _copy;
        
        private LobbyController _lobbyController;
        private string _region;
        
        #region Event/Delegates
        
            private void OnEnable()
            {
                GameLobby.OnLeavingLobby += LeavingLobby;
                GameLobby.OnLobbyLeft += LobbyLeft;
                GameLobby.OnLobbyFailedToLeave += LobbyFailedToLeave;

                GameLobby.OnKickingPlayer += KickingFromLobby;
                GameLobby.PlayerKicked += PlayerKickedFromLobby;
                GameLobby.OnPlayerKicked += KickedFromLobby;
                GameLobby.OnPlayerFailedToKicked += FailedToKickFromLobby;

                GameLobby.OnDestroyingLobby += DestroyingLobby;
                GameLobby.OnLobbyDestroyed += LobbyDestroyed;
                GameLobby.OnLobbyFailedToDestroy += LobbyFailedToDestroy;

                GameLobby.OnUpdatingLobby += UpdatingLobby;
                GameLobby.OnLobbyUpdated += LobbyUpdated;
                GameLobby.OnLobbyFailedToUpdate += LobbyFailedToUpdate;

                GameLobby.OnChangingHost += ChangingHost;
                GameLobby.OnHostChanged += HostChanged;
                GameLobby.OnFailedToChangeHost += FailedToChangeHost;

                GameLobby.OnSyncLobby += SyncLobbyUI;
                GameLobby.OnLobbyFailedToFind += FailedToFindLobby;

                GameLobby.OnHeartBeatFailedToSend += FailedToSendHeartBeat;

                GameRelay.OnCreatingRelay += CreatingRelay;
                GameRelay.OnRelayCreated += RelayCreated;
                GameRelay.OnRelayFailedToCreate += RelayFailedToCreate;
                
                GameRelay.OnJoiningRelay += JoiningRelay;
                GameRelay.OnRelayJoined += RelayJoined;
                GameRelay.OnRelayFailedToJoined += RelayFailedToJoin;

                GameNetworkHandler.OnStartingGame += StartingGame;
                GameNetworkHandler.OnGameStarted += GameStarted;
                GameNetworkHandler.OnGameFailedToStart += GameFailedToStart;
                    
                _regions.onValueChanged.AddListener(RegionSelected);
            }
            
            private void OnDisable()
            {
                GameLobby.OnLeavingLobby -= LeavingLobby;
                GameLobby.OnLobbyLeft -= LobbyLeft;
                GameLobby.OnLobbyFailedToLeave -= LobbyFailedToLeave;
                
                GameLobby.OnKickingPlayer -= KickingFromLobby;
                GameLobby.PlayerKicked -= PlayerKickedFromLobby;
                GameLobby.OnPlayerKicked -= KickedFromLobby;
                GameLobby.OnPlayerFailedToKicked -= FailedToKickFromLobby;
                
                GameLobby.OnDestroyingLobby -= DestroyingLobby;
                GameLobby.OnLobbyDestroyed -= LobbyDestroyed;
                GameLobby.OnLobbyFailedToDestroy -= LobbyFailedToDestroy;
                
                GameLobby.OnUpdatingLobby -= UpdatingLobby;
                GameLobby.OnLobbyUpdated -= LobbyUpdated;
                GameLobby.OnLobbyFailedToUpdate -= LobbyFailedToUpdate;
                
                GameLobby.OnChangingHost -= ChangingHost;
                GameLobby.OnHostChanged -= HostChanged;
                GameLobby.OnFailedToChangeHost -= FailedToChangeHost;
                
                GameLobby.OnSyncLobby -= SyncLobbyUI;
                GameLobby.OnLobbyFailedToFind -= FailedToFindLobby;
                
                GameLobby.OnHeartBeatFailedToSend -= FailedToSendHeartBeat;
                
                GameRelay.OnCreatingRelay -= CreatingRelay;
                GameRelay.OnRelayCreated -= RelayCreated;
                GameRelay.OnRelayFailedToCreate -= RelayFailedToCreate;
                
                GameRelay.OnJoiningRelay -= JoiningRelay;
                GameRelay.OnRelayJoined -= RelayJoined;
                GameRelay.OnRelayFailedToJoined -= RelayFailedToJoin;

                GameNetworkHandler.OnStartingGame -= StartingGame;
                GameNetworkHandler.OnGameStarted -= GameStarted;
                GameNetworkHandler.OnGameFailedToStart -= GameFailedToStart;
                
                _regions.onValueChanged.RemoveListener(RegionSelected);
            }
            
        #endregion
        
        private void Start()
        {
            SetRelayRegions();
            _startGame.onClick.AddListener(() =>
            {
                GameNetworkHandler.Instance.StartGame(_region);
            });
            
            _leave.onClick.AddListener(() =>
            {
                GameLobby.Instance.LeaveLobby();
            });
            
            _copy.onClick.AddListener(() =>
            {
                CopyToClipBoard(_lobbyCode.text);
            });
        }
        
        private void CopyToClipBoard(string str) => GUIUtility.systemCopyBuffer = str;
        
        private void ResetState()
        {
            _searchField.text = null;
            _regions.value = 0;
        }
        
        #region HearBeat
        private void FailedToSendHeartBeat(string msg) =>
            NotificationHelper.SendNotification(NotificationType.Error, "Heartbeat",msg,
                this, NotifyCallType.Open);
        #endregion

        #region FindLobby
        private void FailedToFindLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Error, "Find Lobby","Failed To Get Lobby." + "Either Lobby Is Destroyed Or" +
                                                                        " You Have Been Kicked",
                this, NotifyCallType.Open);
            _lobbyController.CheckAndChangeState("MainLobby");
        }
        #endregion
        
        private void SyncLobbyUI(Lobby lobby)
        {
            var currentPlayer = AuthenticationService.Instance.PlayerId;
            var isHost = lobby.HostId == currentPlayer;
            _regions.gameObject.SetActive(isHost);
            _startGame.gameObject.SetActive(isHost);
            _lobbyCode.gameObject.SetActive(isHost);
        }

        #region Host

        private void ChangingHost()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Host Change","Changing Lobby Host",
                this, NotifyCallType.Open);
        }

        private void HostChanged()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Host Change","Lobby Host Changed",
                this, NotifyCallType.Close);
        }

        private void FailedToChangeHost(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Host Change",msg,
                this, NotifyCallType.Close);
        }

        #endregion
        
        #region Destroy
        private void DestroyingLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Destroy Lobby","Destroying Lobby",
                this, NotifyCallType.Open);
        }

        private void LobbyDestroyed()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Destroy Lobby","Lobby Destroyed",
                this, NotifyCallType.Close);
            _lobbyController.CheckAndChangeState("MainLobby");
        }

        private void LobbyFailedToDestroy(string obj)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Destroy Lobby","Lobby Failed To Destroy",
                this, NotifyCallType.Close);
        }
        #endregion
        
        #region Update
        
        private void UpdatingLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Update Lobby","Updating Lobby",
                this, NotifyCallType.Open);
        }

        private void LobbyUpdated(Lobby lobby, GameLobby gameLobby)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Update Lobby","Lobby Updated",
                this, NotifyCallType.Close);
        }
        
        private void LobbyFailedToUpdate(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Error, "Update Lobby",msg,
                this, NotifyCallType.Open);
        }
        
        #endregion
        
        #region KickFromLobby
        
        private void KickingFromLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Kick Player","Kicking From Lobby",
                this, NotifyCallType.Open);
        }
        
        private void KickedFromLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Error, "Kick Player","You are Kicked From Lobby",
                this, NotifyCallType.Open);
            _lobbyController.CheckAndChangeState("MainLobby");
        }
        
        private void PlayerKickedFromLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Kick Player","Kicked From Lobby",
                this, NotifyCallType.Close);
        }

        private void FailedToKickFromLobby(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Kick Player","Failed To Kick From Lobby",
                this, NotifyCallType.Close);
        }
        
        #endregion

        #region LeaveLobby

        private void LobbyLeft(Lobby lobby, GameLobby gameLobby)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Leave Lobby","Lobby Left",
                this, NotifyCallType.Close);
            _lobbyController.CheckAndChangeState("MainLobby");
        }

        private void LobbyFailedToLeave(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Leave Lobby","Failed To Leave Lobby",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, "Leave Lobby",msg, this, NotifyCallType.Open);
        }

        private void LeavingLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Leave Lobby","Leaving Lobby",
                this, NotifyCallType.Open);
        }

        #endregion
        
        #region CreateRelay
        
        private void CreatingRelay()
        {
            _leave.interactable = false;
            NotificationHelper.SendNotification(NotificationType.Progress, "Relay Creation","Creating Relay",
                this, NotifyCallType.Open);
        }
        private void RelayCreated()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Relay Creation","Relay Created",
                this, NotifyCallType.Close);
        }
        private void RelayFailedToCreate(string msg)
        {
            _leave.interactable = true;
            NotificationHelper.SendNotification(NotificationType.Progress, "Relay Creation","Relay Failed To Create",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, "Relay Creation",msg,
                this, NotifyCallType.Open);
        }
        
        #endregion
        
        #region Join Relay
        
        private void JoiningRelay()
        {
            _leave.interactable = false;
            NotificationHelper.SendNotification(NotificationType.Progress, "Relay Joining","Joining Relay",
                this, NotifyCallType.Open);
        }
        private void RelayJoined()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Relay Joining","Relay Joined",
                this, NotifyCallType.Close);
        }
        private void RelayFailedToJoin(string msg)
        {
            _leave.interactable = true;
            NotificationHelper.SendNotification(NotificationType.Progress, "Relay Joining","Relay Failed To Join",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, "Relay Joining",msg,
                this, NotifyCallType.Open);
        }
        
        #endregion

        #region SessionStarted

        private void StartingGame()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Start Game","Starting Game",
                this, NotifyCallType.Open);
        }
        
        private void GameStarted()
        {
            _leave.interactable = true;
            NotificationHelper.SendNotification(NotificationType.Progress, "Start Game","Game Started",
                this, NotifyCallType.Close);
        }

        private void GameFailedToStart(string msg)
        {
            _leave.interactable = true;
            NotificationHelper.SendNotification(NotificationType.Progress, "Start Game","Game Failed To Start",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, "Start Game",msg,
                this, NotifyCallType.Open);
        }
        
        #endregion
        
        private void RegionSelected(int region) => 
            _region = _regions.options[region].text == "None" ? null : _regions.options[region].text;

        private void SetRelayRegions()
        {
            var regions = GameRelay.Instance.Regions;

            foreach (var region in regions)
            {
                var option = new TMP_Dropdown.OptionData
                {
                    text = region
                };
                _regions.options.Add(option);
            }
        }

        public void HandleState(LobbyController lobbyController)
        {
            ResetState();
            
            _lobbyController = lobbyController;
            var lobbyData = lobbyController.LobbyData;
            if(GameLobby.Instance == null) return;
            var gameLobby = GameLobby.Instance;
            if(gameLobby.LobbyInstance == null) return;
            _lobbyCode.SetText($"{gameLobby.LobbyInstance.LobbyCode}");
            if (string.IsNullOrEmpty(lobbyData.LobbyName) || string.IsNullOrWhiteSpace(lobbyData.LobbyName))
            {
                _lobbyName.SetText($"{gameLobby.LobbyInstance.Name}");
                return;
            }
            _lobbyName.SetText($"{lobbyData.LobbyName}");
        }

        public bool PrerequisiteCheck() => true;

        public void ResetState(LobbyController lobbyController)
        {
            _searchField.text = null;
            _regions.value = 0;
        }
        
        public void Notify(string notifyData)
        {
            
        }
    }
}
