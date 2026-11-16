\## Версии и окружение



| Компонент | Версия |

| --- | --- |

| Unity | 6000.6.3f1 |

| Meta XR All-in-One SDK | 207.0.1 |

| XR Interaction Toolkit (XRI) | 3.6.1 |

| Input System | New (включён) |

| Render Pipeline | URP |

| Целевое устройство | Meta Quest 3 (также quest3s в манифесте) |



\## Настройки сборки (Quest)



\- Платформа: Android, Build Profile «Meta Quest»

\- Scripting Backend: IL2CPP, архитектура ARM64

\- Graphics API: Vulkan, Color Space: Linear

\- Min API Level: 32

\- Texture Compression: ASTC

\- XR Plug-in Management (Android): OpenXR + Meta XR feature group, Oculus Touch Controller Profile



\## Как открыть проект



1\. Установить Unity Hub и Unity \*\*6000.6.3f1\*\* с модулями Android Build Support (включая OpenJDK и Android SDK \& NDK Tools).

2\. Клонировать репозиторий, открыть папку проекта через Unity Hub.

3\. Дождаться импорта пакетов (версии зафиксированы в Packages/manifest.json и packages-lock.json).

4\. Основная сцена: `MR\_Test\_XRI` (XR Origin + XRI).



\## Как собрать APK



1\. File → Build Profiles → выбрать профиль Meta Quest.

2\. Убедиться, что в Scene List включена только `MR\_Test\_XRI`.

3\. Подключить шлем по USB (Developer Mode включён), нажать Build And Run.



\## Известные ограничения



\- Папки Library, Temp, Logs, obj, Builds и файлы \*.apk в репозиторий не входят.

\- Passthrough и MRUK проверяются только на реальном шлеме.

