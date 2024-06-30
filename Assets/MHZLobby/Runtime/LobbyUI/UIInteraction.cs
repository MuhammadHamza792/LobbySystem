using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace MHZ.LobbyUI
{
    public class UIInteraction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IUIEventSubscriber
    {
        [SerializeField] private bool _interactOnFingerUp;

        [SerializeField] private UnityEvent _onInteract;

        public event Action OnInteract; 
        
        public void OnPointerDown(PointerEventData eventData)
        {
            if(_interactOnFingerUp) return;
            _onInteract.Invoke();
            OnInteract?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_interactOnFingerUp) return;
            _onInteract.Invoke();
            OnInteract?.Invoke();
        }

        public void SubscribeToEvent(Action action) => OnInteract += action;

        public void UnSubscribeToEvent(Action action) => OnInteract -= action;
    }

    public interface IUIEventSubscriber
    {
        public void SubscribeToEvent(Action action);
        public void UnSubscribeToEvent(Action action);
    }
}
