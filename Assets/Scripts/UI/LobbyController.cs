using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UI.Notify;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI
{
    public class LobbyController : Singleton<LobbyController>, INotifier
    {
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private Button _lobbyButton;
        [SerializeField] private Button _leaveButton;
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
            NetworkController.OnSessionLeft += EnableLobbyButton;
            NetworkController.OnLeavingSession += LeavingSession;
            NetworkController.OnSessionLeft += SessionLeft;
            GameRelay.OnGameStarted += EnableLeaveButton;
        }

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
                CheckAndChangeState(GameLobby.Instance.LobbyInstance != null ? "PlayerPanel" : "LoginPanel");
                _lobbyPanel.SetActive(true);
                return true;
            }, "Lobby");
            _lobbyButton.gameObject.SetActive(true);
            _leaveButton.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            NetworkController.OnSessionLeft -= EnableLobbyButton;
            NetworkController.OnLeavingSession -= EnableLobbyButton;
            NetworkController.OnSessionLeft -= EnableLobbyButton;
            GameRelay.OnGameStarted -= EnableLeaveButton;
        }

        private void Start()
        {
            _leaveButton.onClick.AddListener(LeaveSession);
            _leaveButton.gameObject.SetActive(false);
        }

        [Button()]
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
