// Мост между сервисом проектов и Unity: создаёт ProjectService, хранит файлы проектов
// в папке приложения и сохраняет проект при паузе и выходе (FR-003).
// Добавляется на пустой объект сцены. Сейчас в общую сцену НЕ добавляем - подключим на интеграции.

using System.IO;
using UnityEngine;

namespace XrInterior.ProjectData.UnityIntegration
{
    public class ProjectServiceHost : MonoBehaviour
    {
        public static ProjectServiceHost Instance { get; private set; }

        public ProjectService Service { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            string root = Path.Combine(Application.persistentDataPath, "Projects");
            Service = new ProjectService(new ProjectStorage(root));
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Service != null) Service.SaveIfDirty();
        }

        private void OnApplicationQuit()
        {
            if (Service != null) Service.SaveIfDirty();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
