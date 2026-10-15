using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Строит World Space меню с иконками мебели и ставит его перед игроком.
// Клавиша M (симулятор) возвращает меню перед игроком.
public class CatalogMenu : MonoBehaviour
{
    [SerializeField] PlacementController placement;
    [SerializeField] FurnitureItem[] items;

    [Header("Вид меню")]
    [SerializeField] int columns = 3;
    [SerializeField] float iconSize = 160f;   // 160 px = 16 см (масштаб канваса 0.001)
    [SerializeField] float spacing = 20f;
    [SerializeField] float distance = 0.9f;   // расстояние от головы, м
    [SerializeField] float heightOffset = -0.1f;
    [SerializeField] Color panelColor = new Color(0.1f, 0.12f, 0.16f, 0.85f);
    [SerializeField] Color slotColor = new Color(0.82f, 0.84f, 0.88f, 1f);

    void Start()
    {
        Build();
        Summon();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) Summon();
    }

    public void Summon()
    {
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType< Camera>();
        if (cam == null) return;

        Vector3 fwd = cam.transform.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();

        transform.position = cam.transform.position + fwd * distance + Vector3.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(fwd); // лицевая сторона канваса к игроку
    }

    void Build()
    {
        int count = items.Length;
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        int cols = Mathf.Min(columns, Mathf.Max(1, count));
        float w = cols * iconSize + (cols + 1) * spacing;
        float h = rows * iconSize + (rows + 1) * spacing;

        var canvasGO = new GameObject("CatalogCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        var rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one * 0.001f;

        // фон панели
        var bg = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGO.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = panelColor;

        for (int i = 0; i < count; i++)
        {
            int r = i / columns, c = i % columns;
            float x = spacing + c * (iconSize + spacing) + iconSize * 0.5f - w * 0.5f;
            float y = h * 0.5f - (spacing + r * (iconSize + spacing) + iconSize * 0.5f);
            CreateSlot(canvasGO.transform, items[i], new Vector2(x, y));
        }
    }

    void CreateSlot(Transform parent, FurnitureItem item, Vector2 pos)
    {
        var slot = new GameObject("Slot_" + item.name, typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(parent, false);
        var rt = slot.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = pos;
        slot.GetComponent<Image>().color = slotColor;

        if (item.icon != null)
        {
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(slot.transform, false);
            var irt = icon.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(8, 8); irt.offsetMax = new Vector2(-8, -8);
            var img = icon.GetComponent<Image>();
            img.sprite = item.icon;
            img.preserveAspect = true;
            img.raycastTarget = false; // клики принимает сам слот
        }
        else
        {
            // запасной вариант, если иконки ещё нет: короткая надпись
            var tgo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(slot.transform, false);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.black;
            t.fontSize = 28;
            t.raycastTarget = false;
        }

        var drag = slot.AddComponent<CatalogDragItem>();
        drag.item = item;
        drag.placement = placement;
    }
}