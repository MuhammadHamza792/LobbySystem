using UnityEngine;

namespace MHZ.LobbyUI
{
    public class PanelData : MonoBehaviour
    {
        [SerializeField] private string _panelName;
        [SerializeField] private GameObject _panel;

        public string PanelName => _panelName;
        public GameObject Panel => _panel;
        public IPanelState PanelState { private set; get; }

        private void Awake()
        {
            PanelState = GetComponent<IPanelState>();
            gameObject.SetActive(false);
        }
    }
}