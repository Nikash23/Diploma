using UnityEngine;

public class MinimapMarker : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] Transform head;               // Main Camera внутри XR Origin
    [SerializeField] Camera minimapCamera;         // камера карты
    [SerializeField] RectTransform mapImage;       // RawImage с картой
    [SerializeField] RectTransform marker;         // маркер игрока

    void Start()
    {
        // Принудительно ставим якоря и pivot маркера в центр,
        // чтобы не зависеть от того, как они выставлены руками
        marker.anchorMin = new Vector2(0.5f, 0.5f);
        marker.anchorMax = new Vector2(0.5f, 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
    }

    void LateUpdate()
    {
        if (head == null || minimapCamera == null) return;

        // Размер области, которую видит камера, в метрах
        float worldHeight = minimapCamera.orthographicSize * 2f;
        float worldWidth = worldHeight * minimapCamera.aspect;

        // Позиция игрока относительно центра камеры карты
        Vector3 offset = head.position - minimapCamera.transform.position;

        // Доля от центра карты: от -0,5 до +0,5
        float u = offset.x / worldWidth;
        float v = offset.z / worldHeight;

        // Переводим в пиксели от центра картинки
        marker.anchoredPosition = new Vector2(
            u * mapImage.rect.width,
            v * mapImage.rect.height
        );

        // Поворот стрелки (стрелка на картинке должна смотреть вверх)
        marker.localRotation = Quaternion.Euler(0f, 0f, -head.eulerAngles.y);
    }
}