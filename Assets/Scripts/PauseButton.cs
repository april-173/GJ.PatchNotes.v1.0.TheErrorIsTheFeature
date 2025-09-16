using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseButton : MonoBehaviour
{
    public PlayerController playerController;
    public PlayerCombat playerCombat;
    [Space]
    public GameObject PausePanel;

    public void PashGame()
    {
        playerController.ClearKeyStack();

        playerController.enabled = false;
        playerCombat.enabled = false;
        PausePanel.SetActive(true);
    }

    public void ContinueGame()
    {
        playerController.enabled = true;
        playerCombat.enabled = true;
        PausePanel.SetActive(false);
    }

    public void MainMenu()
    {
        SceneManager.LoadScene(0);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        // �����Unity�༭�������У���رձ༭������
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
        // ���ڶ���ƽ̨����Windows, Mac, Linux�����˳�Ӧ�ó���
        Application.Quit();
#elif UNITY_WEBGL
        // ����WebGLƽ̨��ͨ��������ֱ�ӹر���������ڣ��������ض�����һ��ҳ�������ʾһ����ʾ��Ϣ
        Application.OpenURL("about:blank");
#elif UNITY_IOS || UNITY_ANDROID || UNITY_WP_8_1 // ��Щƽ̨������ֱ���˳�Ӧ��
        // ���Գ��Իص����˵�����ʾһ���˳�ȷ�ϵ�UI
        Application.Quit(); // ��ͨ����Ҫ�û��������������簴���ؼ����
#endif
    }

}
