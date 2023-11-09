using UnityEngine;
using Task = System.Threading.Tasks.Task;

public class InitialScene : MonoBehaviour
{
    async void Start()
    {
        await Task.Delay(1500);
        await Helper.LoadSceneAsync(null, "Lobby");
    }
}
