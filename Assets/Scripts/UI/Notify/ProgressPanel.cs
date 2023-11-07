using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UI.Notify
{
    public class ProgressPanel : LobbyNotification ,INotification
    {
        [SerializeField] private TextMeshProUGUI _progress;
        public bool NotifyOnClose { private set; get; }
        public NotificationType NotificationType => _type;
        private void Awake()
        {
            Notification = this;
            gameObject.SetActive(false);
        }

        public void ShowNotification(NotificationData nData)
        {
            switch (nData.NotifyCallType)
            {
                case NotifyCallType.Open:
                    _progress.SetText($"{nData.Text}");
                    gameObject.SetActive(true);
                    NotifyOnClose = false;
                    break;
                case NotifyCallType.Close:
                    _progress.SetText($"{nData.Text}");
                    Invoke(nameof(DisableObject), .5f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(nData.NotifyCallType), nData.NotifyCallType, null);
            }
        }

        private void DisableObject()
        {
            gameObject.SetActive(false);
            NotifyOnClose = true;
        }
    }
}