using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LobbyPackage.Scripts.UI.Notify
{
    public class LobbyNotificationHandler : Singleton<LobbyNotificationHandler>
    {
        [SerializeField] private List<LobbyNotification> _notifications;
        
        private Queue<NotificationData> _notifyQueue;
        private Queue<NotificationData> _bufferQueue;
        
        private NotificationData _currentData;
        private Coroutine _notificationsCo;

        private INotification _currentNotification;
           
        public  NotificationData CurrentData
        {
            private set
            {
                if(!CheckWhetherValueChanged(_currentData, value)) return;
                
                _currentData = value;

                _currentNotification =
                    _notifications.FirstOrDefault(notif  => notif.Notification.NotificationType == value.NotifyType)?.Notification;
                
                _currentNotification?.ShowNotification(value);
            }
            get => _currentData;
        }

        private bool CheckWhetherValueChanged(NotificationData currentData, NotificationData value)
        {
            if (currentData.Notifier == null) return true;
            
            return currentData.NotifyCallType != value.NotifyCallType ||
                   currentData.Notifier != value.Notifier ||
                   currentData.NotifyType != value.NotifyType;
        }

        private void OnEnable()
        {
            _notifyQueue ??= new Queue<NotificationData>();
            _bufferQueue ??= new Queue<NotificationData>();
            
            if(_notificationsCo != null) StopCoroutine(_notificationsCo);
            _notificationsCo = StartCoroutine(PlayNotifications());
        }

        private void Start()
        {
            foreach (var notification in _notifications)
            {
                notification.SetHandler(this);
            }
        }

        private void OnDisable()
        {
            if(_notificationsCo != null) StopCoroutine(_notificationsCo);
            if(_notifyQueue is {Count : > 0}) _notifyQueue.Clear();
            if(_bufferQueue is {Count : > 0}) _bufferQueue.Clear();
        }

        public void HandleNotificationPanel(NotificationData nData)
        {
            switch (nData.NotifyCallType)
            {
                case NotifyCallType.Open:
                    
                    if (_currentData is { Notifier: not null, NotifyCallType: NotifyCallType.Open })
                    {
                        _bufferQueue.Enqueue(nData);
                        return;
                    }

                    if (_notifyQueue.Count > 0)
                    {
                        if (_notifyQueue.Peek().NotifyCallType == NotifyCallType.Open)
                        {
                            _bufferQueue.Enqueue(nData);
                            return;
                        }
                        
                    }
                    
                    break;
                case NotifyCallType.Close:
                    
                    if (_currentData is { Notifier: not null, NotifyCallType: NotifyCallType.Open })
                    {
                        if (nData.Notifier == _currentData.Notifier)
                        {
                            EnqueueNotification(nData);
                            return;
                        }
                    }

                    if (_notifyQueue.Count > 0)
                    {
                        if (_notifyQueue.Peek().NotifyCallType == NotifyCallType.Open)
                        {
                            if (nData.Notifier == _currentData.Notifier)
                            {
                                EnqueueNotification(nData);
                                return;
                            }
                        }
                    }
                    
                    _bufferQueue.Enqueue(nData);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            CurrentData = nData;
        }

        IEnumerator PlayNotifications()
        {
            while (true)
            {
                if (CurrentData.Notifier == null) yield return null;
                
                switch (CurrentData.NotifyCallType)
                {
                    case NotifyCallType.Open:
                        yield return WaitForCloseCall();
                        yield return DequeueNotification();
                        break;
                    case NotifyCallType.Close:
                        yield return DequeueNotification();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                yield return null;
            }
        }

        private IEnumerator WaitForCloseCall()
        {
            if(_notifyQueue.Count == 0) yield break;
            while (_notifyQueue.Peek().Notifier != CurrentData.Notifier ||
                   _notifyQueue.Peek().NotifyCallType != NotifyCallType.Close)
            {
                yield return null;
            }
        }

        public void EnqueueNotification(NotificationData ndata) => _notifyQueue.Enqueue(ndata);

        public IEnumerator DequeueNotification()
        {
            if (_notifyQueue.Count > 0)
            {
                CurrentData = _notifyQueue.Dequeue();
                while (_currentNotification is { NotifyOnClose: false }) yield return null;
            }
            else if (_bufferQueue.Count > 0)
                CurrentData = _bufferQueue.Dequeue();
        }
    }

    public struct NotificationData
    {
        public NotificationType NotifyType;
        public INotifier Notifier;
        public string Text;
        public NotifyCallType NotifyCallType;
    }
    
    public enum NotifyCallType
    {
        Open,
        Close
    }

    public enum NotificationType
    {
        Error,
        RequiredField,
        Progress
    }
}