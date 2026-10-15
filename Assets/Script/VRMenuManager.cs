using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Единое VR-меню (World Space Canvas).
/// - Открывается/закрывается кнопкой контроллера.
/// - Плавно следует за пользователем (не жёстко прикреплено к голове — меньше укачивает).
/// - Можно "закрепить" в пространстве кнопкой в меню (TogglePin).
/// Повесьте на корневой объект Canvas (там же должен быть CanvasGroup).
/// </summary>
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
public class VRMenuManager : MonoBehaviour
{
    [Header("Ввод")]
    [Tooltip("Кнопка открытия/закрытия (например, Menu или Y на левом контроллере)")]
    public InputActionProperty toggleAction;

    [Header("Ссылки")]
    [Tooltip("Камера игрока (Main Camera внутри XR Origin)")]
    public Transform head;

    [Header("Положение")]
    public float distance = 1.3f;        // расстояние от глаз, м
    public float heightOffset = -0.1f;   // смещение по высоте относительно глаз
    public float menuScale = 0.0012f;    // масштаб World Space Canvas

    [Header("Следование")]
    public float followSpeed = 4f;       // скорость догоняния
    public float maxAngle = 35f;         // на сколько градусов можно отвернуться, прежде чем меню поедет
    public float maxDrift = 0.5f;        // на сколько метров можно отойти
    public float stopAngle = 3f;         // когда меню "догнало" — остановиться

    [Header("Поведение")]
    public bool startOpen = false;
    public float fadeSpeed = 8f;

    public bool IsOpen { get; private set; }
    public bool IsPinned { get; private set; }

    Canvas _canvas;
    CanvasGroup _group;
    bool _following;
    float _targetAlpha;

    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _group = GetComponent<CanvasGroup>();
        _canvas.renderMode = RenderMode.WorldSpace;
        transform.localScale = Vector3.one * menuScale;

        if (head == null && Camera.main != null)
            head = Camera.main.transform;
    }

    void OnEnable()
    {
        if (toggleAction.action != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnTogglePressed;
        }
    }

    void OnDisable()
    {
        if (toggleAction.action != null)
            toggleAction.action.performed -= OnTogglePressed;
    }

    void Start()
    {
        if (startOpen) Open(); else SetVisibleImmediate(false);
    }

    void OnTogglePressed(InputAction.CallbackContext _) => Toggle();

    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public void Open()
    {
        if (head == null) return;
        IsOpen = true;
        _canvas.enabled = true;
        _group.interactable = true;
        _group.blocksRaycasts = true;
        _targetAlpha = 1f;
        SnapInFront();           // появляется прямо перед глазами
    }

    public void Close()
    {
        IsOpen = false;
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _targetAlpha = 0f;
    }

    /// <summary>Вызывать с кнопки "Закрепить" в меню.</summary>
    public void TogglePin()
    {
        IsPinned = !IsPinned;
        _following = false;
    }

    void SetVisibleImmediate(bool visible)
    {
        IsOpen = visible;
        _group.alpha = visible ? 1f : 0f;
        _targetAlpha = _group.alpha;
        _group.interactable = visible;
        _group.blocksRaycasts = visible;
        _canvas.enabled = visible;
    }

    void LateUpdate()
    {
        // плавное появление/исчезновение
        _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, fadeSpeed * Time.deltaTime);
        if (!IsOpen && _group.alpha <= 0.001f)
        {
            _canvas.enabled = false;
            return;
        }

        if (!IsOpen || IsPinned || head == null) return;

        Vector3 targetPos = GetTargetPosition();
        Vector3 toMenu = transform.position - head.position;
        toMenu.y = 0f;
        Vector3 headFwd = head.forward;
        headFwd.y = 0f;

        float angle = (toMenu.sqrMagnitude < 0.0001f || headFwd.sqrMagnitude < 0.0001f)
            ? 0f : Vector3.Angle(headFwd, toMenu);
        float drift = Vector3.Distance(transform.position, targetPos);

        // начинаем догонять, если пользователь отвернулся или ушёл
        if (angle > maxAngle || drift > maxDrift) _following = true;

        if (_following)
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, t);
            FaceHead();

            // догнали — останавливаемся (меню снова "стоит" на месте)
            if (angle < stopAngle && drift < 0.05f) _following = false;
        }
        else
        {
            FaceHead();
        }
    }

    Vector3 GetTargetPosition()
    {
        Vector3 fwd = head.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = head.up; // если смотрит строго вверх/вниз
        fwd.Normalize();
        return head.position + fwd * distance + Vector3.up * heightOffset;
    }

    void SnapInFront()
    {
        transform.position = GetTargetPosition();
        FaceHead();
        _following = false;
    }

    void FaceHead()
    {
        Vector3 dir = transform.position - head.position;
        dir.y = 0f; // меню остаётся вертикальным
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
