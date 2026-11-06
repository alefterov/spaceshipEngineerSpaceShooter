using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if REVIEW_SDK && UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Review;
#endif

public static class GameRating
{
    private const float SecondsAfterLoading = 1.5f;

    private static bool running;

    public static void OpenStorePage()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Application.OpenURL("market://details?id=" + Application.identifier);
#else
        Application.OpenURL("https://play.google.com/store/apps/details?id=" + Application.identifier);
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        running = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var data = GameDataManager.Instance;
        if (mode != LoadSceneMode.Single || data == null) return;

        if (scene.name == PendingBattle.BattleSceneName)
        {
            data.ClearRatingMoment();
            return;
        }

        if (!running && data.ShouldRequestRating()) data.StartCoroutine(RequestRoutine(data));
    }

    private static IEnumerator RequestRoutine(GameDataManager data)
    {
        running = true;
        data.ClearRatingMoment();

        while (SceneLoader.IsLoading) yield return null;
        yield return new WaitForSecondsRealtime(SecondsAfterLoading);

        if (SomethingOnScreen())
        {
            running = false;
            yield break;
        }

#if REVIEW_SDK && UNITY_ANDROID && !UNITY_EDITOR
        var manager = new ReviewManager();

        var request = manager.RequestReviewFlow();
        yield return request;
        if (request.Error != ReviewErrorCode.NoError)
        {
            Debug.LogWarning($"GameRating: the review could not be prepared: {request.Error}");
            running = false;
            yield break;
        }

        var launch = manager.LaunchReviewFlow(request.GetResult());
        yield return launch;
        if (launch.Error != ReviewErrorCode.NoError)
        {
            Debug.LogWarning($"GameRating: the review could not be launched: {launch.Error}");
            running = false;
            yield break;
        }

        data.MarkRatingRequested();
#endif

        running = false;
    }

    private static bool SomethingOnScreen()
    {
        if (AdsManager.Instance != null && AdsManager.Instance.IsFullScreenAdBusy) return true;

        var tutorial = Object.FindAnyObjectByType<TutorialController>();
        return tutorial != null && tutorial.IsShowing;
    }
}
