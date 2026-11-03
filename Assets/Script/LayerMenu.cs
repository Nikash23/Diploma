using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class LayerMenu : MonoBehaviour
{
    public BuildingLayerManager manager;
    public GameObject menuRoot;      // LayerMenuCanvas
    public Transform container;      // Container внутри Canvas
    public Toggle togglePrefab;      // префаб строки
    public Transform head;           // Main Camera внутри XR Origin
    public float distance = 1.2f;

    [Header("Вызов меню")]
    public Key toggleKey = Key.M;                 // для теста с клавиатуры
    public InputActionReference toggleAction;     // потом: кнопка контроллера

    void Start()
    {
        foreach (var g in manager.groups)
        {
            var id = g.id;
            var t = Instantiate(togglePrefab, container);

            // Подпись: работает и с TextMeshPro, и с обычным Text
            var tmp = t.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = id; // потом заменим на локализацию
            }
            else
            {
                var legacy = t.GetComponentInChildren<Text>();
                if (legacy != null) legacy.text = id;
                else Debug.LogWarning("В Toggle не найден компонент текста", t);
            }

            t.SetIsOnWithoutNotify(g.visible);
            t.onValueChanged.AddListener(v =>
            {
                Debug.Log($"Клик: {id} = {v}");
                manager.SetVisible(id, v);
            });
        }

        menuRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (toggleAction != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnAction;
        }
    }

    void OnDisable()
    {
        if (toggleAction != null)
            toggleAction.action.performed -= OnAction;
    }

    void OnAction(InputAction.CallbackContext _) => ToggleMenu();

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            ToggleMenu();
    }

    public void ToggleMenu()
    {
        bool open = !menuRoot.activeSelf;
        menuRoot.SetActive(open);
        if (!open) return;

        // Поставить перед лицом и развернуть к игроку
        Vector3 fwd = head.forward; fwd.y = 0; fwd.Normalize();
        Vector3 pos = head.position + fwd * distance;
        menuRoot.transform.position = new Vector3(pos.x, head.position.y - 0.1f, pos.z);
        menuRoot.transform.rotation = Quaternion.LookRotation(menuRoot.transform.position - head.position);
    }
}