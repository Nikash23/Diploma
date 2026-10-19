using UnityEngine;

public class MinimapFloors : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] Transform head;                    // Main Camera из XR Origin
    [SerializeField] Camera minimapCamera;              // MinimapCamera
    [SerializeField] MinimapRenderOnce minimapRender;   // скрипт из шага 6

    [Header("Этажи")]
    [SerializeField] float[] floorHeights = { 0f };     // Y пола каждого этажа
    [SerializeField] float cutHeight = 4f;              // сколько метров над полом показывать (чуть выше стен)

    [Header("Камера")]
    [SerializeField] float cameraHeight = 20f;          // камера всегда на этой высоте

    int currentFloor = -1;

    void Update()
    {
        int floor = GetFloor(head.position.y);
        if (floor != currentFloor)
        {
            currentFloor = floor;
            SetFloor(floor);
        }
    }

    int GetFloor(float headY)
    {
        int result = 0;
        for (int i = 0; i < floorHeights.Length; i++)
            if (headY >= floorHeights[i] + 1.0f) result = i;
        return result;
    }

    public void SetFloor(int floor)
    {
        // камера всегда на одной высоте над зданием
        Vector3 p = minimapCamera.transform.position;
        p.y = cameraHeight;
        minimapCamera.transform.position = p;

        // полоса высоты, которую показываем
        float bottom = floorHeights[floor] - 0.2f;
        float top = floorHeights[floor] + cutHeight;

        // не залезать на следующий этаж
        if (floor + 1 < floorHeights.Length)
            top = Mathf.Min(top, floorHeights[floor + 1] - 0.05f);

        minimapCamera.nearClipPlane = Mathf.Max(0.01f, cameraHeight - top);
        minimapCamera.farClipPlane = cameraHeight - bottom;

        minimapRender.Refresh();
    }
}