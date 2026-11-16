using Meta.XR.MRUtilityKit;
using UnityEngine;

// Проверочный скрипт для A-01: после загрузки сцены MRUK пишет в лог
// сколько комнат найдено и какие в них стены, пол и потолок.
// Вешается на тот же объект, где стоит компонент MRUK.
public class MrukCheck : MonoBehaviour
{
    private void Start()
    {
        if (MRUK.Instance == null)
        {
            Debug.LogError("[MRUK-CHECK] MRUK.Instance не найден. Проверь, что на сцене есть объект с компонентом MRUK.");
            return;
        }

        // Подписываемся: сработает, когда MRUK закончит загрузку комнат
        MRUK.Instance.SceneLoadedEvent.AddListener(OnSceneLoaded);
        Debug.Log("[MRUK-CHECK] Подписались на SceneLoadedEvent, ждём загрузку сцены...");
    }

    private void OnDestroy()
    {
        if (MRUK.Instance != null)
        {
            MRUK.Instance.SceneLoadedEvent.RemoveListener(OnSceneLoaded);
        }
    }

    private void OnSceneLoaded()
    {
        var rooms = MRUK.Instance.Rooms;
        Debug.Log("[MRUK-CHECK] MRUK: комнат " + rooms.Count);

        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            Debug.Log("[MRUK-CHECK] Комната " + (i + 1)
                + ": стен " + room.WallAnchors.Count
                + ", пол " + (room.FloorAnchor != null ? "есть" : "НЕТ")
                + ", потолок " + (room.CeilingAnchor != null ? "есть" : "НЕТ")
                + ", всего объектов " + room.Anchors.Count);

            for (int w = 0; w < room.WallAnchors.Count; w++)
            {
                var wall = room.WallAnchors[w];
                if (wall.PlaneRect.HasValue)
                {
                    Vector2 size = wall.PlaneRect.Value.size;
                    Debug.Log("[MRUK-CHECK]   Стена " + (w + 1)
                        + ": ширина " + size.x.ToString("F2") + " м, высота " + size.y.ToString("F2") + " м");
                }
            }
        }
    }
}