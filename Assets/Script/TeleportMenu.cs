using System;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI-меню телепортации для XR. Для каждой точки создаётся кнопка.
/// По нажатию XR Origin переносится в выбранное место.
/// </summary>
public class TeleportMenu : MonoBehaviour
{
    [Serializable]
    public class TeleportPoint
    {
        public string title = "Точка";
        public Transform destination;
    }

    [Header("XR")]
    [SerializeField] private XROrigin xrOrigin;

    [Header("UI")]
    [SerializeField] private Button buttonPrefab;      // префаб кнопки с TMP_Text внутри
    [SerializeField] private RectTransform container;  // родитель с Vertical Layout Group
    [SerializeField] private GameObject menuRoot;      // окно меню (для скрытия после телепорта)
    [SerializeField] private bool closeMenuAfterTeleport = false;

    [Header("Точки телепортации")]
    [SerializeField] private List<TeleportPoint> points = new List<TeleportPoint>();

    private void Start()
    {
        if (xrOrigin == null)
            xrOrigin = FindAnyObjectByType<XROrigin>();

        foreach (var p in points)
        {
            var point = p; // копия для замыкания
            var btn = Instantiate(buttonPrefab, container);
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = point.title;
            btn.onClick.AddListener(() => TeleportTo(point));
        }
    }

    public void TeleportTo(TeleportPoint point)
    {
        if (point == null || point.destination == null || xrOrigin == null) return;

        // Поворачиваем игрока по направлению точки (только по Y)
        Vector3 forward = Vector3.ProjectOnPlane(point.destination.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude > 0.001f)
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, forward);

        // Ставим камеру (голову игрока) точно в точку назначения
        xrOrigin.MoveCameraToWorldLocation(point.destination.position);

        if (closeMenuAfterTeleport && menuRoot != null)
            menuRoot.SetActive(false);
    }
}
