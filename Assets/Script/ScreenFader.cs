using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] float duration = 0.6f;

    void Start() => StartCoroutine(Fade(1f, 0f));   // при старте проявляем из чёрного

    public IEnumerator FadeOut() => Fade(0f, 1f);

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }
}