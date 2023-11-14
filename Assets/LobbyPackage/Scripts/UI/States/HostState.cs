using LobbyPackage.Scripts.UI.Notify;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyPackage.Scripts.UI.States
{
    public class HostState : MonoBehaviour, IPanelState, INotifier
    {
        [Header("UI References")] 
        [SerializeField] private TextMeshProUGUI _lobbyName;

        [Header("UI Togglers")] 
        [SerializeField] private Toggle _publicLobby; 
        [SerializeField] private Toggle _privateLobby; 
        [SerializeField] private Toggle _protectedLobby; 
        [SerializeField] private Toggle _customConnections;
        [SerializeField] private Toggle _keepLobbyAlive;
        [SerializeField] private Toggle _destroyLobbyWithHost;

        [Header("UI InputFields")] 
        [SerializeField] private TMP_InputField _lobbyNameField;
        [SerializeField] private TMP_InputField _lobbyPassField;
        [SerializeField] private TMP_InputField _lobbyMaxPlayersField;
        [SerializeField] private TMP_InputField _lobbyMaxConnectionsNameField;

        [Header("UI Buttons")] 
        [SerializeField] private Button _createLobby;
        [SerializeField] private Button _backBtn;
        
        private LobbyController _lobbyController;
        private bool _shouldResetData = true;
        
        #region Event/Delegates
            private void OnEnable()
            {
                ResetState();
                
                GameLobby.OnCreatingLobby += CreatingLobby;
                GameLobby.OnLobbyCreated += LobbyCreated;
                GameLobby.OnLobbyFailedToCreate += LobbyFailedToCreate;
                
                _publicLobby.onValueChanged.AddListener(OnPublicToggle);
                _privateLobby.onValueChanged.AddListener(OnPrivateToggle);
                _protectedLobby.onValueChanged.AddListener(OnProtectedToggle);
                _customConnections.onValueChanged.AddListener(OnCustomConnectionToggle);
                _keepLobbyAlive.onValueChanged.AddListener(OnLobbyAliveToggle);
            }
            
            private void OnDisable()
            {
                GameLobby.OnCreatingLobby -= CreatingLobby;
                GameLobby.OnLobbyCreated -= LobbyCreated;
                GameLobby.OnLobbyFailedToCreate -= LobbyFailedToCreate;
                
                _publicLobby.onValueChanged.RemoveListener(OnPublicToggle);
                _privateLobby.onValueChanged.RemoveListener(OnPrivateToggle);
                _protectedLobby.onValueChanged.RemoveListener(OnProtectedToggle);
                _customConnections.onValueChanged.RemoveListener(OnCustomConnectionToggle);
                _keepLobbyAlive.onValueChanged.RemoveListener(OnLobbyAliveToggle);
            }
            
            #endregion
            
        private void ResetState()
        {
            if (!_shouldResetData) return;
            
            _lobbyMaxPlayersField.text = "2";
            _lobbyMaxConnectionsNameField.text = "2";
            
            _protectedLobby.isOn = false;
            _lobbyPassField.gameObject.SetActive(_protectedLobby.isOn);
            _keepLobbyAlive.isOn = false;
            _customConnections.isOn = false;
            _lobbyMaxConnectionsNameField.gameObject.SetActive(_customConnections.isOn);
            _customConnections.gameObject.SetActive(_keepLobbyAlive.isOn);
            _destroyLobbyWithHost.isOn = true;
            _publicLobby.isOn = true;
        }

        private void OnPublicToggle(bool active) => 
            _privateLobby.isOn = !active;

        private void OnPrivateToggle(bool active) =>
            _publicLobby.isOn = !active;

        private void OnProtectedToggle(bool active) => 
            _lobbyPassField.gameObject.SetActive(active);

        private void OnCustomConnectionToggle(bool active) => 
            _lobbyMaxConnectionsNameField.gameObject.SetActive(active);

        private void OnLobbyAliveToggle(bool active)
        {
            _customConnections.isOn = false;
            _customConnections.gameObject.SetActive(active);
        }
        
        private void CreatingLobby() =>
            NotificationHelper.SendNotification(NotificationType.Progress, "Creating Lobby",
                this, NotifyCallType.Open);

        private void LobbyCreated(Lobby arg1, GameLobby arg2)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Lobby Created",
                this, NotifyCallType.Close);
            _lobbyController.CheckAndChangeState("PlayerPanel");
        }
        
        private void LobbyFailedToCreate(string context)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Failed To Create Lobby",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, context,
                this, NotifyCallType.Open);
        }
        
        public void HandleState(LobbyController lobbyController)
        {
            ResetState();
            
            _lobbyController = lobbyController;
            
            _createLobby.onClick.RemoveAllListeners();
            _createLobby.onClick.AddListener(() =>
            {
                var maxLobbyPlayers = int.Parse(_lobbyMaxPlayersField.text) <= 0
                    ? 1
                    : int.Parse(_lobbyMaxPlayersField.text);

                if (int.Parse(_lobbyMaxConnectionsNameField.text) > 100)
                {
                    NotificationHelper.SendNotification(NotificationType.Error, "Number of custom Connections can't exceed 100.",
                        this, NotifyCallType.Open);
                    return;
                }
                
                lobbyController.LobbyData = new LobbyData
                {
                    LobbyName = _lobbyNameField.text,
                    IsPublicLobby = _publicLobby.isOn,
                    HasPassword = _protectedLobby.isOn,
                    Password = _lobbyPassField.text,
                    MaxLobbyPlayers = maxLobbyPlayers,
                    CustomConnections = _customConnections.isOn,
                    KeepLobbyAlive = _keepLobbyAlive.isOn,
                    CustomMaxConnections = int.Parse(_lobbyMaxConnectionsNameField.text) <= 0 ? maxLobbyPlayers : int.Parse(_lobbyMaxConnectionsNameField.text),
                    DestroyLobbyWithHost = _destroyLobbyWithHost.isOn
                };

                _shouldResetData = true;
                GameLobby.Instance.CreateLobby(lobbyController.LobbyData, true);
            });
            
            _backBtn.onClick.RemoveAllListeners();
            _backBtn.onClick.AddListener(() =>
            {
                _shouldResetData = false;
                lobbyController.CheckAndChangeState("MainLobby");
            });
            
            _lobbyName.SetText("Lobby");
        }

        public bool PrerequisiteCheck()
        {
            return true;
        }

        public void ResetState(LobbyController lobbyController)
        {
            if(!_shouldResetData) return;
            
            _lobbyNameField.text = "";
            _lobbyPassField.text = "";
            _lobbyMaxPlayersField.text = "0";
            _lobbyMaxConnectionsNameField.text = "0";

            _publicLobby.isOn = true;
            _protectedLobby.isOn = false;
            _keepLobbyAlive.isOn = false;
            _destroyLobbyWithHost.isOn = true;
        }
        
        public void Notify(string notifyData)
        {
            
        }
    }
}