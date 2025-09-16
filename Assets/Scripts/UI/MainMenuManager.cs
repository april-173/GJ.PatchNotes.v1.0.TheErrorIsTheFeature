using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    public void StartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(1);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        // 如果在Unity编辑器中运行，则关闭编辑器窗口
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
        // 对于独立平台（如Windows, Mac, Linux），退出应用程序
        Application.Quit();
#elif UNITY_WEBGL
        // 对于WebGL平台，通常不允许直接关闭浏览器窗口，但可以重定向到另一个页面或者显示一个提示信息
        Application.OpenURL("about:blank");
#elif UNITY_IOS || UNITY_ANDROID || UNITY_WP_8_1 // 这些平台不允许直接退出应用
        // 可以尝试回到主菜单或显示一个退出确认的UI
        Application.Quit(); // 但通常需要用户主动操作，比如按返回键多次
#endif
    }
}
