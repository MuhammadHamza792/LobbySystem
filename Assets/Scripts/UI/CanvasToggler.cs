using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI
{
    public class CanvasToggler : MonoBehaviour
    {
        [SerializeField] private bool _hasCanvasGroup;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private GraphicRaycaster _graphicRaycaster;
        [SerializeField] private CanvasGroup _canvasGroup;

        public UnityEvent OnCanvasOpened;
        public UnityEvent OnCanvasClosed;

        public void ToggleCanvas(bool active)
        {
            _canvas.enabled = active;
            _graphicRaycaster.enabled = active;
            if (_hasCanvasGroup)
            {
                _canvasGroup.interactable = active;
                _canvasGroup.blocksRaycasts = active;
            }
            if (active)
                OnCanvasOpened?.Invoke();
            else
                OnCanvasClosed?.Invoke();
        }
    }
}
