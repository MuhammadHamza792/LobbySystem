using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public static class Helper
{
    public static async Task<bool> LoadSceneAsync(Func<bool> onComplete, string sceneToLoad)
    {
        var task = SceneManager.LoadSceneAsync(sceneToLoad);
        while (!task.isDone)
        {
            await Task.Yield();
        }
        var onTaskCompleted = onComplete?.Invoke();
        return onTaskCompleted != null && onTaskCompleted.Value;
    }
}
