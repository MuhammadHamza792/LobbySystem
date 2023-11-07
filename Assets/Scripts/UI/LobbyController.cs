using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace UI
{
    public class LobbyController : Singleton<LobbyController>
    {
        [SerializeField] private GameObject _lobbyPanel;
        [SerializeField] private List<PanelData> _panelData;

        private PanelContext _panelContext;

        private IPanelState[] _panelStates;

        private PanelData _currentPanelData;
        private PanelData _previousPanelData;

        public PanelData CurrentPanelData => _currentPanelData;
        public PanelData PreviousPanelData => _previousPanelData;

        public LobbyData LobbyData { set; get; }

        public override void Awake()
        {
            base.Awake();
            _panelContext = new PanelContext(this);
        }

        private void OnEnable() => _lobbyPanel.SetActive(false);

        //private void Start() => ChangeState("MainLobby");

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
    }
}
