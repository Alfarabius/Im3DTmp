using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GameSessionController
{
    public static void CompleteGame()
    {
        GameApplication.Instance.CompleteGame();
#if !UNITY_STANDALONE_WIN || UNITY_EDITOR
        // В игровых тестах остаёмся в Editor/браузере и начинаем новую попытку.
        RestartGame();
#endif
    }

    public static void LeaveGame()
    {
        GameApplication.Instance.LeaveGame();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#elif !UNITY_STANDALONE_WIN
        RestartGame();
#endif
    }

    public static void RestartGame()
    {
        if (PauseManager.IsPaused && PauseManager.Instance != null) PauseManager.Instance.ResumeGame();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
