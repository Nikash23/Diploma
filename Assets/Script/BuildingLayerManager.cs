using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class BuildingLayerManager : MonoBehaviour
{
    [Serializable]
    public class LayerGroup
    {
        public string id;                    // "Walls", "Doors"...
        public GameObject root;              // родитель группы
        public bool disableColliders = true; // убирать ли коллизию при скрытии
        [HideInInspector] public bool visible = true;
    }

    public List<LayerGroup> groups = new();
    public UnityEvent<string, bool> onLayerChanged;

    [Header("Тест в редакторе: клавиши 1-5")]
    public bool debugHotkeys = true;

    readonly Dictionary<string, LayerGroup> map = new();
    readonly Dictionary<string, Renderer[]> renderers = new();
    readonly Dictionary<string, Collider[]> colliders = new();

    void Awake()
    {
        foreach (var g in groups)
        {
            map[g.id] = g;
            renderers[g.id] = g.root.GetComponentsInChildren<Renderer>(true);
            colliders[g.id] = g.root.GetComponentsInChildren<Collider>(true);
        }
    }

    void Update()
    {
        if (!debugHotkeys || Keyboard.current == null) return;
        for (int i = 0; i < groups.Count && i < 9; i++)
        {
            var key = Keyboard.current[(Key)((int)Key.Digit1 + i)];
            if (key.wasPressedThisFrame) Toggle(groups[i].id);
        }
    }

    public bool IsVisible(string id) => map[id].visible;

    public void SetVisible(string id, bool visible)
    {
        if (!map.TryGetValue(id, out var g)) return;
        g.visible = visible;

        foreach (var r in renderers[id]) r.enabled = visible;
        if (g.disableColliders)
            foreach (var c in colliders[id]) c.enabled = visible;

        onLayerChanged?.Invoke(id, visible);
    }

    public void Toggle(string id) => SetVisible(id, !map[id].visible);

    // Для кнопок UI
    public void ToggleWalls() => Toggle("Walls");
    public void ToggleInteriorWalls() => Toggle("InteriorWalls");
    public void ToggleFloors() => Toggle("Floors");
    public void ToggleDoors() => Toggle("Doors");
    public void ToggleWindows() => Toggle("Windows");

    // Кнопка «Все стены»
    public void ToggleAllWalls()
    {
        bool show = !(map["Walls"].visible || map["InteriorWalls"].visible);
        SetVisible("Walls", show);
        SetVisible("InteriorWalls", show);
    }
}