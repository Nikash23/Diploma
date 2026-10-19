using UnityEngine;

public class MinimapRenderOnce : MonoBehaviour
{
    [SerializeField] Camera minimapCamera;

    void Start()
    {
        minimapCamera.enabled = false;   // не рисовать каждый кадр
        minimapCamera.Render();          // один снимок при запуске
    }

    // Вызывай это, когда меняется этаж
    public void Refresh()
    {
        minimapCamera.Render();
    }
}