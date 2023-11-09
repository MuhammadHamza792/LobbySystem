using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public static class Helper
{
    public static async Task<bool> LoadSceneAsync(Func<bool> onComplete = null, string sceneToLoad = null)
    {
        var task = SceneManager.LoadSceneAsync(sceneToLoad);
        while (!task.isDone)
        {
            await Task.Yield();
        }
        var onTaskCompleted = onComplete?.Invoke();
        return onTaskCompleted != null && onTaskCompleted.Value;
    }
    
    public static async Task<bool> LoadAdditiveSceneAsync(Func<bool> onComplete = null, string sceneToLoad = null)
    {
        var task = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
        while (!task.isDone)
        {
            await Task.Yield();
        }
        var onTaskCompleted = onComplete?.Invoke();
        return onTaskCompleted != null && onTaskCompleted.Value;
    }
    
    public static async Task<bool> UnLoadSceneAsync(Func<bool> onComplete = null, string sceneToLoad = null)
    {
        var task = SceneManager.UnloadSceneAsync(sceneToLoad);
        while (!task.isDone)
        {
            await Task.Yield();
        }
        var onTaskCompleted = onComplete?.Invoke();
        return onTaskCompleted != null && onTaskCompleted.Value;
    }
}
