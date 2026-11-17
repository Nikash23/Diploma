// Превращает Project в JSON-текст и обратно. Только текст, без работы с файлами
// (запись на диск, атомарность и резервные копии - задача B-02).

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace XrInterior.ProjectData
{
    /// <summary>Файл проекта не удалось прочитать (повреждён или неизвестный формат).</summary>
    public class ProjectFormatException : Exception
    {
        public ProjectFormatException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>Файл создан более новой версией схемы. Исходный файл при этом не меняется.</summary>
    public sealed class UnsupportedProjectVersionException : ProjectFormatException
    {
        public int FileVersion { get; }
        public int SupportedVersion { get; }

        public UnsupportedProjectVersionException(int fileVersion, int supportedVersion)
            : base("Проект создан более новой версией приложения (версия схемы " + fileVersion +
                   ", поддерживается до " + supportedVersion + "). Файл не изменён.")
        {
            FileVersion = fileVersion;
            SupportedVersion = supportedVersion;
        }
    }

    public static class ProjectSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            // Имена в JSON: camelCase. Ключи словарей не меняем.
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false }
            },
            // Перечисления пишем словами: "notBuilt", а не числом.
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy()) },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat
        };

        public static string ToJson(Project project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            return JsonConvert.SerializeObject(project, Settings);
        }

        public static Project FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ProjectFormatException("Файл проекта пуст.");

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonReaderException ex)
            {
                throw new ProjectFormatException("Файл проекта повреждён: " + ex.Message, ex);
            }

            JToken versionToken = root["version"];
            if (versionToken == null || versionToken.Type != JTokenType.Integer)
                throw new ProjectFormatException("В файле проекта нет номера версии схемы.");

            int version = versionToken.Value<int>();
            if (version > Project.CurrentSchemaVersion)
                throw new UnsupportedProjectVersionException(version, Project.CurrentSchemaVersion);

            try
            {
                if (version == Project.CurrentSchemaVersion)
                {
                    Project p = JsonConvert.DeserializeObject<Project>(json, Settings);
                    if (p == null) throw new ProjectFormatException("Файл проекта пуст.");
                    return p;
                }

                // Файл старой версии: поднимаем на одну версию за шаг.
                while (version < Project.CurrentSchemaVersion)
                {
                    root = MigrateOneStep(root, version);
                    version++;
                    root["version"] = version;
                }
                return root.ToObject<Project>(JsonSerializer.Create(Settings));
            }
            catch (JsonException ex)
            {
                throw new ProjectFormatException("Не удалось прочитать проект: " + ex.Message, ex);
            }
        }

        /// <summary>Копия проекта через JSON (удобно в тестах и для вариантов).</summary>
        public static Project Clone(Project project)
        {
            return FromJson(ToJson(project));
        }

        /// <summary>
        /// Миграции. Когда появится схема версии 2, здесь будет:
        /// if (fromVersion == 1) { /* переименовать/добавить поля в root */ return root; }
        /// </summary>
        private static JObject MigrateOneStep(JObject root, int fromVersion)
        {
            throw new ProjectFormatException("Нет миграции с версии схемы " + fromVersion + ".");
        }
    }
}
