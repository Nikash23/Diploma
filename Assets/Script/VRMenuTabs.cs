using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Левая колонка вкладок + правая область с панелями.
/// Нажатие на кнопку слева показывает нужную панель и скрывает остальные.
/// </summary>
public class VRMenuTabs : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public string name;           // для удобства в инспекторе: Главная, Настройки, Телепорт, Слои
        public Button button;         // кнопка в левой колонке
        public GameObject panel;      // содержимое справа
    }

    public Tab[] tabs;
    public int startIndex = 0;

    [Header("Цвета кнопок")]
    public Color normalColor = new Color(1f, 1f, 1f, 0.06f);
    public Color activeColor = new Color(0.2f, 0.9f, 1f, 0.28f);

    int _current = -1;

    void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i; // копия для замыкания
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => Show(index));
        }
    }

    void Start() => Show(startIndex);

    public void Show(int index)
    {
        if (index < 0 || index >= tabs.Length || index == _current) return;
        _current = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == index;

            if (tabs[i].panel != null)
                tabs[i].panel.SetActive(active);

            if (tabs[i].button != null && tabs[i].button.targetGraphic != null)
                tabs[i].button.targetGraphic.color = active ? activeColor : normalColor;
        }
    }

    /// <summary>Можно вызывать из других скриптов, например открыть сразу "Телепорт".</summary>
    public void ShowByName(string tabName)
    {
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i].name == tabName) { Show(i); return; }
    }
}