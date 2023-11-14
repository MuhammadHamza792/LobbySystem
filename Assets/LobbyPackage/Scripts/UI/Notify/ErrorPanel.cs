using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyPackage.Scripts.UI.Notify
{
    public class ErrorPanel : LobbyNotification,INotification
    {
        [SerializeField] private Button _continue;
        [SerializeField] private TextMeshProUGUI _description;
        
        public bool NotifyOnClose { private set;  get; }

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
                    _description.SetText($"{nData.Text}");
                    gameObject.SetActive(true);
                    _continue.onClick.RemoveAllListeners();
                    _continue.onClick.AddListener(() =>
                    {
                        NotificationHelper.SendNotification(NotificationType.Error, nData.Text,
                            nData.Notifier, NotifyCallType.Close);
                    });
                    NotifyOnClose = false;
                    break;
                case NotifyCallType.Close:
                    gameObject.SetActive(false);
                    NotifyOnClose = true;
                    break;
            }
        }
    }
}