using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class Bootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        if (FindFirstObjectByType<Bootstrap>() != null) return;
        var go = new GameObject("[Bootstrap]");
        go.AddComponent<Bootstrap>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (FindObjectsByType<Bootstrap>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        if (GetComponent<GameFlowService>() == null)
            gameObject.AddComponent<GameFlowService>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameFlowService.RestoreGlobalRuntimeState();
        if (scene.name == GameFlowService.MainMenuSceneName)
        {
            if (CardManager.Instance != null) CardManager.Instance.Reset();
            if (CombatManager.Instance != null) CombatManager.Instance.Reset();
            if (RunManager.Instance != null) RunManager.Instance.Reset();
        }
    }
}
