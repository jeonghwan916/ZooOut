using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScreen : MonoBehaviour
{
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _exitButton;
    
    [SerializeField] private CanvasGroup _loadingPanelGroup;
    [SerializeField] private Slider _progressBar;

    private void Awake()
    {
        if (_playButton != null)
        { 
            _playButton.interactable = true;
        }

        if (_exitButton != null)
        {
            _exitButton.interactable = true;
        }

        if (_loadingPanelGroup != null)
        {
            _loadingPanelGroup.alpha = 0;
            _loadingPanelGroup.blocksRaycasts = false;
            _loadingPanelGroup.interactable = false;
        }
    }

    public void OnStartButtonClick()
    {
        WebGLFullscreenUtility.RequestFullscreen();

        _playButton.interactable = false;
        _exitButton.interactable = false;
        
        _loadingPanelGroup.alpha = 1.0f;
        _loadingPanelGroup.blocksRaycasts = true;
        
        StartCoroutine(LoadSceneProcess("Main"));
    }

    public void QuitButtonClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadSceneProcess(string sceneName)
    {
        // 비동기 씬 로드 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

        // 씬 로드가 완료될 때까지 바로 전환되지 않게 설정 (부드러운 연출용)
        op.allowSceneActivation = false;

        float timer = 0f;
        while (!op.isDone)
        {
            yield return null;
            
            timer += Time.unscaledDeltaTime;
            
            // op.progress 0에서 0.9까지만 올라감 (0.9에서 로딩 완료)
            if (op.progress < 0.9f)
            {
                if (_progressBar != null)
                {
                    _progressBar.value = Mathf.Lerp(_progressBar.value, op.progress, timer);
                }
            }
            else
            {
                // 로딩이 90% 이상 되었을 때 나머지를 부드럽게 채움
                if (_progressBar != null)
                {
                    _progressBar.value = Mathf.Lerp(_progressBar.value, 1.0f, timer);
                }

                if (_progressBar.value >= 0.99f)
                {
                    // 로딩 완료 후 씬 전환
                    op.allowSceneActivation = true;
                    yield break;
                }
            }
        }
    }
}
