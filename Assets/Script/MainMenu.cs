using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Панели")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject objectsPanel;
    [SerializeField] GameObject settingsPanel;

    [Header("Затемнение")]
    [SerializeField] ScreenFader fader;

    void Start() => ShowMain();

    // ---- переключение панелей ----
    public void ShowMain() => Show(mainPanel);
    public void ShowObjects() => Show(objectsPanel);
    public void ShowSettings() => Show(settingsPanel);

    void Show(GameObject target)
    {
        mainPanel.SetActive(mainPanel == target);
        objectsPanel.SetActive(objectsPanel == target);
        settingsPanel.SetActive(settingsPanel == target);
    }

    // ---- загрузка сцены ----
    public void LoadScene(string sceneName) => StartCoroutine(LoadRoutine(sceneName));

    IEnumerator LoadRoutine(string sceneName)
    {
        if (fader != null)
            yield return fader.FadeOut();                  // затемняем, если оно настроено

        var op = SceneManager.LoadSceneAsync(sceneName);   // грузим в фоне
        while (!op.isDone) yield return null;
    }

    // ---- выход ----
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}