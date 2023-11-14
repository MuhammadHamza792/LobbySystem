using System;
using System.Collections.Generic;
using System.Linq;
using UI.Notify;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyController : Singleton<LobbyController>, INotifier
    {
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private Button _lobbyButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private List<PanelData> _panelData;

        private PanelContext _panelContext;

        private IPanelState[] _panelStates;

        private PanelData _currentPanelData;
        private PanelData _previousPanelData;

        public PanelData CurrentPanelData => _currentPanelData;
        public PanelData PreviousPanelData => _previousPanelData;

        public LobbyData LobbyData { set; get; }

        public static event Action DoLeaveSession;

        public override void Awake()
        {
            base.Awake();
            _panelContext = new PanelContext(this);
        }

        private void OnEnable()
        {
            _lobbyPanel.SetActive(false);
            GameNetworkHandler.OnSessionFailedToJoin += SessionFailedToJoin;
            GameNetworkHandler.OnSessionFailedToStart += SessionFailedToStart;
            GameNetworkHandler.OnSessionLeft += EnableLobbyButton;
            GameNetworkHandler.OnLeavingSession += LeavingSession;
            GameNetworkHandler.OnSessionLeft += SessionLeft;
            GameNetworkHandler.OnGameStarted += EnableLeaveButton;
        }

        private void SessionFailedToStart() =>
            NotificationHelper.SendNotification(NotificationType.Error, "Failed To Start Server : Timed Out", this, NotifyCallType.Open);

        private void SessionFailedToJoin() => 
            NotificationHelper.SendNotification(NotificationType.Error, "Failed To Join Server : Timed Out", this, NotifyCallType.Open);

        private void EnableLeaveButton()
        {
            _lobbyPanel.SetActive(false);
            _lobbyButton.gameObject.SetActive(false);
            _leaveButton.gameObject.SetActive(true);
        }
        
        #region LeaveSession

        private void LeavingSession()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Leaving Session", this, NotifyCallType.Open);
        }

        private void SessionLeft()
        {
            NotificationHelper.SendNotification(NotificationType.Progress, "Session Left", this, NotifyCallType.Close);
        }
        
        #endregion

        private async void EnableLobbyButton()
        {
            await Helper.LoadSceneAsync(() =>
            {
                var shouldOpenPlayerPanel = false;
                if (GameLobby.Instance.LobbyInstance != null)
                {
                    shouldOpenPlayerPanel = GameLobby.Instance.LobbyInstance.Data["DestroyLobbyAfterSession"].Value != "true";
                }
                
                CheckAndChangeState(shouldOpenPlayerPanel ? "PlayerPanel" : "LoginPanel");
                _lobbyPanel.SetActive(true);
                return true;
            }, "Lobby");
            _lobbyButton.gameObject.SetActive(true);
            _leaveButton.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            GameNetworkHandler.OnSessionLeft -= EnableLobbyButton;
            GameNetworkHandler.OnLeavingSession -= LeavingSession;
            GameNetworkHandler.OnSessionLeft -= SessionLeft;
            GameNetworkHandler.OnGameStarted -= EnableLeaveButton;
        }

        private void Start()
        {
            _leaveButton.onClick.AddListener(LeaveSession);
            _leaveButton.gameObject.SetActive(false);
            _exitButton.onClick.AddListener(Application.Quit);
        }
        
        public static void LeaveSession() => DoLeaveSession?.Invoke();

        public void CheckAndChangeState(string canvasName)
        {
            if(!_currentPanelData.PanelState.PrerequisiteCheck()) return;
            ChangeState(canvasName);
        }

        public void ChangeState(string panelName)
        {
            _currentPanelData = _panelData.First(canvas => canvas.PanelName == panelName);
            TransitionToState(_currentPanelData.PanelState);
            _previousPanelData = _currentPanelData;
        }

        private void TransitionToState(IPanelState panelState)
        {
            TogglePanels();
            _panelContext.PreviousState?.ResetState(this);
            _panelContext.ChangeState(panelState);
        }

        private void TogglePanels()
        {
            if(_previousPanelData != null)
                _previousPanelData.Panel.SetActive(false);
            
            if(_currentPanelData != null)
                _currentPanelData.Panel.SetActive(true);
        }

        public void Notify(string notifyData)
        {
            
        }
    }
}
