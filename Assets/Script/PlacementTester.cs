using UnityEngine;
using UnityEngine.InputSystem;

// Временный скрипт для теста без UI: клавиши 1, 2, 3 запускают размещение предмета.
public class PlacementTester : MonoBehaviour
{
    [SerializeField] PlacementController controller;
    [SerializeField] FurnitureItem[] items;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame && items.Length > 0) controller.BeginPlacement(items[0]);
        if (kb.digit2Key.wasPressedThisFrame && items.Length > 1) controller.BeginPlacement(items[1]);
        if (kb.digit3Key.wasPressedThisFrame && items.Length > 2) controller.BeginPlacement(items[2]);
    }
}