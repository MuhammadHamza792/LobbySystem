using System;
using TMPro;
using UI.Notify;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace UI.States
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
                
                _regions.onValueChanged.RemoveListener(RegionSelected);
            }
            #endregion
        
        private void Start()
        {
            SetRelayRegions();
            _startGame.onClick.AddListener(() =>
            {
                //GameRelay.Instance.StartGame(_region);
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
            NotificationHelper.SendNotification(NotificationType.Error, msg,
                this, NotifyCallType.Open);
        #endregion

        #region FindLobby
        private void FailedToFindLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Error, "Failed To Get Lobby." + "Either Lobby Is Destroyed Or" +
                                                                        " You Have Been Kicked",
                this, NotifyCallType.Open);
            _lobbyController.CheckAndChangeState("MainLobby");
        }
        #endregion
        
        private void SyncLobbyUI(Lobby lobby)
        {
            var currentPlayer = AuthenticationService.Instance.PlayerId;
            _regions.gameObject.SetActive(lobby.HostId == currentPlayer);
        }

        #region Host

        private void ChangingHost()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Changing Lobby Host",
                this, NotifyCallType.Open);
        }

        private void HostChanged()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Lobby Host Changed",
                this, NotifyCallType.Close);
        }

        private void FailedToChangeHost(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, msg,
                this, NotifyCallType.Close);
        }

        #endregion
        
        #region Destroy
        private void DestroyingLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Destroying Lobby",
                this, NotifyCallType.Open);
        }

        private void LobbyDestroyed()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Lobby Destroyed",
                this, NotifyCallType.Close);
            _lobbyController.CheckAndChangeState("MainLobby");
        }

        private void LobbyFailedToDestroy(string obj)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Lobby Failed To Destroy",
                this, NotifyCallType.Close);
        }
        #endregion
        
        #region Update
        
        private void UpdatingLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Updating Lobby",
                this, NotifyCallType.Open);
        }

        private void LobbyUpdated(Lobby lobby, GameLobby gameLobby)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Lobby Updated",
                this, NotifyCallType.Close);
        }
        
        private void LobbyFailedToUpdate(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Error, msg,
                this, NotifyCallType.Open);
        }
        
        #endregion
        
        #region KickFromLobby
        
        private void KickingFromLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Kicking From Lobby",
                this, NotifyCallType.Open);
        }
        
        private void KickedFromLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Error, "You are Kicked From Lobby",
                this, NotifyCallType.Open);
            _lobbyController.CheckAndChangeState("MainLobby");
        }
        
        private void PlayerKickedFromLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Kicked From Lobby",
                this, NotifyCallType.Close);
        }

        private void FailedToKickFromLobby(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Failed To Kick From Lobby",
                this, NotifyCallType.Close);
        }
        
        #endregion

        #region LeaveLobby

        private void LobbyLeft(Lobby lobby, GameLobby gameLobby)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Lobby Left",
                this, NotifyCallType.Close);
            _lobbyController.CheckAndChangeState("MainLobby");
        }

        private void LobbyFailedToLeave(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Failed To Leave Lobby",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, msg, this, NotifyCallType.Open);
        }

        private void LeavingLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Leaving Lobby",
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
            _lobbyCode.SetText($"{GameLobby.Instance.LobbyInstance.LobbyCode}");
            if (string.IsNullOrEmpty(lobbyData.LobbyName) || string.IsNullOrWhiteSpace(lobbyData.LobbyName))
            {
                _lobbyName.SetText($"{GameLobby.Instance.LobbyInstance.Name}");
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
