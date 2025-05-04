using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [SerializeField]
    private float _sceneFadeDuration;

    private SceneFade _sceneFade;

    private void Awake()
    {
        _sceneFade = GetComponentInChildren<SceneFade>();
    }

    private IEnumerator Start()
    {
        yield return _sceneFade.FadeInCoroutine(_sceneFadeDuration);
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        if (_sceneFade == null)
        {
            Debug.LogError("SceneFade referansı LoadSceneCoroutine içinde null! GetComponentInChildren<SceneFade>() kontrol edin.");
            yield break;
        }
        yield return _sceneFade.FadeOutCoroutine(_sceneFadeDuration);
        yield return SceneManager.LoadSceneAsync(sceneName);
    }
}