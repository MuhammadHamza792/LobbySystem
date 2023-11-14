using LobbyPackage.Scripts.UI.Notify;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyPackage.Scripts.UI.States
{
    public class LobbyState : MonoBehaviour, IPanelState, INotifier
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _lobbyTitle;
        [SerializeField] private TMP_Dropdown _dropdown;
        [SerializeField] private Toggle _joinToggle;
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private GameObject _loginPanel;
        
        [Header("UI InputFields")]
        [SerializeField] private TMP_InputField _searchField;
        [SerializeField] private TMP_InputField _codeField;
        [SerializeField] private TMP_InputField _passField;
        
        [Header("UI Buttons")]
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _copy;
        [SerializeField] private Button _close;

        [SerializeField] private LobbyServers _lobbyServers;

        private string _lobbyId;
        private LobbyController _lobbyController;
        private bool _shouldResetData = true;

        #region Event/Delegates
        
            private void OnEnable()
            {
                GameLobby.OnJoiningLobby += JoiningLobby;
                GameLobby.OnLobbyJoined += LobbyJoined;
                GameLobby.OnLobbyFailedToJoin += LobbyFailedToJoin;
                _joinToggle.onValueChanged.AddListener(JoinToggle);
            }
            
            private void OnDisable()
            {
                GameLobby.OnJoiningLobby -= JoiningLobby;
                GameLobby.OnLobbyJoined -= LobbyJoined;
                GameLobby.OnLobbyFailedToJoin -= LobbyFailedToJoin;
                _joinToggle.onValueChanged.RemoveListener(JoinToggle);
            }

        
        #endregion

        private void Start()
        {
            _copy.onClick.AddListener(() =>
            {
                CopyToClipBoard(_codeField.text);
            });
            
            _close.onClick.AddListener(() =>
            {
                _lobbyPanel.SetActive(false);
            });
        }
        
        private void ResetState()
        {
            if (!_shouldResetData) return;
            
            _joinToggle.isOn = false;
            _joinButton.gameObject.SetActive(_joinToggle.isOn);
            _codeField.gameObject.SetActive(_joinToggle.isOn);
            _copy.gameObject.SetActive(_joinToggle.isOn);
            _passField.gameObject.SetActive(_joinToggle.isOn);
            _dropdown.value = 2;
            _searchField.text = null;
            _passField.text = null;
        }

        
        private void LobbyJoined(Lobby lobby, GameLobby gameLobby)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Lobby Joined",
                this, NotifyCallType.Close);
            _lobbyController.CheckAndChangeState("PlayerPanel");
        }

        private void JoiningLobby()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Joining Lobby", 
                this, NotifyCallType.Open);
        }
        
        private void JoinToggle(bool isOn)
        {
            _joinButton.gameObject.SetActive(isOn);
            _codeField.gameObject.SetActive(isOn);
            _copy.gameObject.SetActive(isOn);
            _passField.gameObject.SetActive(isOn);
        }

        private void LobbyFailedToJoin(string msg)
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Failed To Join Lobby",
                this, NotifyCallType.Close);
            NotificationHelper.SendNotification(NotificationType.Error, msg, this, NotifyCallType.Open);
        }

        private void CopyToClipBoard(string str) => GUIUtility.systemCopyBuffer = str;
        
        public void HandleState(LobbyController lobbyController)
        {
            ResetState();
            
            _loginPanel.SetActive(false);
            
            _lobbyController = lobbyController;
            _lobbyServers.RefreshLobbies();
            
            var lobbyData = lobbyController.LobbyData;
            
            _hostButton.onClick.RemoveAllListeners();
            _hostButton.onClick.AddListener(() =>
            {
                _shouldResetData = false;
                lobbyController.CheckAndChangeState("HostPanel");
            });
            
            _joinButton.onClick.RemoveAllListeners();
            _joinButton.onClick.AddListener(() =>
            {
                _shouldResetData = true;
                GameLobby.Instance.JoinLobby(_codeField.text,
                    false, _passField.text);
            });
            
            _lobbyTitle.SetText("Lobby");
        }

        public bool PrerequisiteCheck()
        {
            return true;
        }

        public void ResetState(LobbyController lobbyController)
        {
            if (!_shouldResetData) return;

            _joinToggle.isOn = false;
            _dropdown.value = 2;
            _searchField.text = null;
            _passField.text = null;
        }
        
        public void Notify(string notifyData)
        {
            
        }
    }
}
