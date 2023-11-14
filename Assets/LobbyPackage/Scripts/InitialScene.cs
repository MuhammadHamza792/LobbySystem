using UnityEngine;
using Task = System.Threading.Tasks.Task;

namespace LobbyPackage.Scripts
{
    public class InitialScene : MonoBehaviour
    {
        async void Start()
        {
            await Task.Delay(1500);
            await Helper.LoadSceneAsync(null, "Lobby");
        }
    }
}
