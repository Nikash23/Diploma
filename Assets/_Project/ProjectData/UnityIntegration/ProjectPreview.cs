// Наглядный предпросмотр проекта в Unity (нажать Play). Рисует комнату, стены, проёмы, объекты и светильники
// простыми цветными блоками и даёт кнопки, чтобы испытать команды, undo/redo и сохранение.
// Это инструмент проверки, не часть приложения. Работает в отдельной сцене, не трогая сцены команды.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XrInterior.ProjectData.UnityIntegration
{
    public class ProjectPreview : MonoBehaviour
    {
        public enum PreviewSource { NewTemplateRoom, ControlProject }

        [Header("Что показать")]
        public PreviewSource source = PreviewSource.NewTemplateRoom;

        [Header("Шаблон помещения (для NewTemplateRoom), размеры в метрах")]
        public RoomTemplate template = RoomTemplate.Rectangular;
        public float width = 4f;
        public float depth = 3f;
        public float length = 8f;
        public float cutWidth = 2.5f;
        public float cutDepth = 2f;

        private string _storageDir;
        private ProjectStorage _storage;
        private ProjectService _projects;
        private CommandService _commands;
        private GameObject _viewRoot;
        private bool _framed;
        private string _status = "";
        private GUIStyle _label;
        private GUIStyle _button;
        private Vector2 _scroll;

        private void Start()
        {
            try { BuildProject(); }
            catch (Exception ex)
            {
                _status = "Ошибка: " + ex.Message;
                Debug.LogError(ex);
            }
        }

        private void OnDestroy()
        {
            try { if (!string.IsNullOrEmpty(_storageDir) && Directory.Exists(_storageDir)) Directory.Delete(_storageDir, true); }
            catch (IOException) { }
        }

        // ---------- проект ----------

        private void BuildProject()
        {
            _storageDir = Path.Combine(Path.GetTempPath(), "xri_preview_" + Guid.NewGuid().ToString("N"));
            _storage = new ProjectStorage(_storageDir);
            _projects = new ProjectService(_storage);

            if (source == PreviewSource.ControlProject)
            {
                string path = Path.Combine(Application.dataPath, "_Project/ProjectData/Samples/control_project.json");
                if (!File.Exists(path)) { _status = "Не найден файл: " + path; return; }
                _projects.Import(path);
                _status = "Открыт контрольный проект из файла.";
            }
            else
            {
                var spec = new RoomSpec { Name = "Комната", Template = template, Params = ParamsFor(template) };
                try
                {
                    TemplateProjectBuilder.CreateFromTemplate(_projects, "Предпросмотр", spec);
                    _status = "Создан новый проект из шаблона " + template + ".";
                }
                catch (ArgumentException ex)
                {
                    _status = ex.Message;
                    return;
                }
            }

            AttachCommands();
            Rebuild();
        }

        private void AttachCommands()
        {
            _commands = new CommandService(_projects);
            _projects.ProjectChanged += Rebuild;
        }

        private Dictionary<string, float> ParamsFor(RoomTemplate t)
        {
            var p = new Dictionary<string, float>();
            switch (t)
            {
                case RoomTemplate.Corridor:
                    p[TemplateParam.Length] = length;
                    p[TemplateParam.Width] = width;
                    break;
                case RoomTemplate.LShaped:
                    p[TemplateParam.Width] = width;
                    p[TemplateParam.Depth] = depth;
                    p[TemplateParam.CutWidth] = cutWidth;
                    p[TemplateParam.CutDepth] = cutDepth;
                    break;
                default:
                    p[TemplateParam.Width] = width;
                    p[TemplateParam.Depth] = depth;
                    break;
            }
            return p;
        }

        // ---------- рисование ----------

        private void Rebuild()
        {
            if (_viewRoot != null) Destroy(_viewRoot);
            _viewRoot = new GameObject("Предпросмотр");
            _viewRoot.transform.SetParent(transform, false);

            Project p = _projects.Current;
            if (p == null) return;

            var rooms = new Dictionary<string, Room>();
            foreach (Room r in p.Rooms) rooms[r.Id] = r;
            var walls = new Dictionary<string, Wall>();
            foreach (Wall w in p.Walls) walls[w.Id] = w;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (Room r in p.Rooms)
            {
                DrawFloor(r);
                foreach (PlanPoint pt in r.Boundary)
                {
                    minX = Mathf.Min(minX, pt.X); maxX = Mathf.Max(maxX, pt.X);
                    minZ = Mathf.Min(minZ, pt.Z); maxZ = Mathf.Max(maxZ, pt.Z);
                }
            }
            foreach (Wall w in p.Walls) DrawWall(w, BaseY(w, rooms));
            foreach (Opening o in p.Openings) DrawOpening(o, walls, rooms);
            foreach (PlacedObject o in p.Objects) DrawObject(o);
            foreach (ProjectLight l in p.Lights) DrawLight(l);

            if (!_framed && p.Rooms.Count > 0) FrameCamera(minX, maxX, minZ, maxZ);
        }

        private static float BaseY(Wall w, Dictionary<string, Room> rooms)
        {
            Room room;
            if (w.RoomId != null && rooms.TryGetValue(w.RoomId, out room)) return room.Elevation;
            return 0f;
        }

        private void DrawWall(Wall w, float baseY)
        {
            Vector3 a = new Vector3(w.A.X, 0f, w.A.Z);
            Vector3 b = new Vector3(w.B.X, 0f, w.B.Z);
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) return;

            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            Vector3 center = (a + b) / 2f + Vector3.up * (baseY + w.Height / 2f);
            Color color = w.State == ElementState.Existing ? new Color(0.85f, 0.80f, 0.70f) : new Color(0.55f, 0.85f, 0.55f);
            Box("Стена " + w.Id, center, new Vector3(len + w.Thickness, w.Height, w.Thickness), Quaternion.Euler(0f, yaw, 0f), color);
        }

        private void DrawOpening(Opening o, Dictionary<string, Wall> walls, Dictionary<string, Room> rooms)
        {
            Wall w;
            if (o.WallId == null || !walls.TryGetValue(o.WallId, out w)) return;

            Vector3 a = new Vector3(w.A.X, 0f, w.A.Z);
            Vector3 b = new Vector3(w.B.X, 0f, w.B.Z);
            Vector3 d = b - a;
            if (d.magnitude < 0.001f) return;
            Vector3 dir = d.normalized;

            float yaw = Mathf.Atan2(-d.z, d.x) * Mathf.Rad2Deg;
            Vector3 pos = a + dir * (o.Offset + o.Width / 2f);
            pos.y = BaseY(w, rooms) + o.SillHeight + o.Height / 2f;
            Color color = o.Type == OpeningType.Door ? new Color(0.45f, 0.30f, 0.15f) : new Color(0.5f, 0.8f, 1f);
            Box((o.Type == OpeningType.Door ? "Дверь " : "Окно ") + o.Id, pos,
                new Vector3(o.Width, o.Height, w.Thickness + 0.04f), Quaternion.Euler(0f, yaw, 0f), color);
        }

        private void DrawObject(PlacedObject o)
        {
            Quaternion rot = Quaternion.Euler(o.Rotation.X, o.Rotation.Y, o.Rotation.Z);
            Vector3 pos = new Vector3(o.Position.X, o.Position.Y, o.Position.Z) + rot * (Vector3.up * 0.4f);
            float hue = Mathf.Abs((o.CatalogId ?? "").GetHashCode() % 100) / 100f;
            Box("Объект " + o.CatalogId + " (" + o.Id + ")", pos, new Vector3(0.8f, 0.8f, 0.8f), rot,
                Color.HSVToRGB(hue, 0.55f, 0.9f));
        }

        private void DrawLight(ProjectLight l)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Светильник " + l.Id;
            go.transform.SetParent(_viewRoot.transform, false);
            go.transform.position = new Vector3(l.Position.X, l.Position.Y, l.Position.Z);
            go.transform.localScale = Vector3.one * 0.25f;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = l.Enabled ? new Color(1f, 0.9f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
        }

        private void DrawFloor(Room r)
        {
            var pts = new List<Vector2>();
            foreach (PlanPoint pt in r.Boundary) pts.Add(new Vector2(pt.X, pt.Z));
            int[] tris = Triangulate(pts);
            if (tris.Length == 0) return;

            int n = pts.Count;
            var verts = new Vector3[n * 2];
            var normals = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i] = new Vector3(pts[i].x, r.Elevation, pts[i].y);
                verts[i + n] = verts[i];
                normals[i] = Vector3.up;
                normals[i + n] = Vector3.down;
            }

            // Пол рисуем с обеих сторон, чтобы он был виден при любом направлении взгляда.
            var indices = new int[tris.Length * 2];
            for (int t = 0; t < tris.Length; t += 3)
            {
                indices[t] = tris[t];
                indices[t + 1] = tris[t + 2];
                indices[t + 2] = tris[t + 1];
                indices[tris.Length + t] = tris[t] + n;
                indices[tris.Length + t + 1] = tris[t + 1] + n;
                indices[tris.Length + t + 2] = tris[t + 2] + n;
            }

            var mesh = new Mesh { name = "Пол" };
            mesh.vertices = verts;
            mesh.triangles = indices;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Пол " + r.Name;
            go.transform.SetParent(_viewRoot.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<Renderer>().material.color = new Color(0.40f, 0.40f, 0.46f);
        }

        private GameObject Box(string objName, Vector3 center, Vector3 size, Quaternion rot, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objName;
            go.transform.SetParent(_viewRoot.transform, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.transform.localScale = size;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        private void FrameCamera(float minX, float maxX, float minZ, float maxZ)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 center = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);
            float size = Mathf.Max(maxX - minX, maxZ - minZ, 2f);
            cam.transform.position = center + new Vector3(0f, size * 1.1f, -size * 0.9f);
            cam.transform.LookAt(center);
            _framed = true;
        }

        // Разбиение многоугольника на треугольники (метод "отсечения ушей").
        private static int[] Triangulate(List<Vector2> pts)
        {
            var result = new List<int>();
            int n = pts.Count;
            if (n < 3) return result.ToArray();

            var idx = new List<int>();
            for (int i = 0; i < n; i++) idx.Add(i);

            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % n];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f) idx.Reverse();

            int guard = 0;
            while (idx.Count > 3 && guard++ < 1000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[i0], b = pts[i1], c = pts[i2];
                    if (Cross(b - a, c - b) <= 0f) continue;

                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (PointInTriangle(pts[j], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;

                    result.Add(i0); result.Add(i1); result.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result.ToArray();
        }

        private static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        // ---------- кнопки ----------

        private void OnGUI()
        {
            EnsureStyles();
            GUILayout.BeginArea(new Rect(12f, 12f, 480f, Screen.height - 24f), GUI.skin.box);

            if (_projects == null || _projects.Current == null)
            {
                GUILayout.Label(_status, _label);
                GUILayout.EndArea();
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            Project p = _projects.Current;
            GUILayout.Label("Проект: " + p.Name + " (источник: " + p.Source + ")", _label);
            GUILayout.Label("Помещений: " + p.Rooms.Count + "   Стен: " + p.Walls.Count + "   Объектов: " + p.Objects.Count, _label);
            GUILayout.Label("Команд в истории: " + _commands.UndoCount, _label);
            GUILayout.Space(8f);

            if (GUILayout.Button("Добавить стул", _button)) Run(AddChair);

            GUI.enabled = p.Objects.Count > 0;
            if (GUILayout.Button("Сдвинуть последний объект", _button)) Run(MoveLast);
            if (GUILayout.Button("Удалить последний объект", _button)) Run(RemoveLast);
            GUI.enabled = true;

            GUILayout.Space(6f);
            if (GUILayout.Button("Добавить перегородку", _button)) Run(AddPartition);
            GUI.enabled = p.Walls.Count > 0;
            if (GUILayout.Button("Добавить окно в последнюю стену", _button)) Run(AddWindowToLastWall);
            if (GUILayout.Button("Добавить дверь в последнюю стену", _button)) Run(AddDoorToLastWall);
            if (GUILayout.Button("Сдвинуть последнюю стену", _button)) Run(MoveLastWall);
            if (GUILayout.Button("Удалить последнюю стену", _button)) Run(RemoveLastWall);
            GUI.enabled = true;

            GUILayout.Space(8f);

            GUI.enabled = _commands.CanUndo;
            if (GUILayout.Button("Отменить: " + _commands.NextUndoName, _button)) Run(_commands.Undo);
            GUI.enabled = _commands.CanRedo;
            if (GUILayout.Button("Повторить: " + _commands.NextRedoName, _button)) Run(_commands.Redo);
            GUI.enabled = true;

            GUILayout.Space(8f);
            if (GUILayout.Button("Сохранить и открыть заново", _button)) Run(ReopenFromDisk);

            GUILayout.Space(8f);
            GUILayout.Label(_status, _label);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _label.normal.textColor = Color.white;
            _button = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 32f };
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                _status = "Ошибка: " + ex.Message;
                Debug.LogError(ex);
            }
        }

        private void AddChair()
        {
            Project p = _projects.Current;
            float minX = 0f, maxX = 4f, minZ = 0f, maxZ = 3f;
            if (p.Rooms.Count > 0)
            {
                minX = float.MaxValue; maxX = float.MinValue; minZ = float.MaxValue; maxZ = float.MinValue;
                foreach (PlanPoint pt in p.Rooms[0].Boundary)
                {
                    minX = Mathf.Min(minX, pt.X); maxX = Mathf.Max(maxX, pt.X);
                    minZ = Mathf.Min(minZ, pt.Z); maxZ = Mathf.Max(maxZ, pt.Z);
                }
            }

            var chair = new PlacedObject
            {
                Id = IdGenerator.New("obj"),
                CatalogId = "chair_demo",
                Position = new Vec3(UnityEngine.Random.Range(minX + 0.5f, maxX - 0.5f), 0f,
                                    UnityEngine.Random.Range(minZ + 0.5f, maxZ - 0.5f)),
                Rotation = new Vec3(0f, UnityEngine.Random.Range(0, 24) * 15f, 0f)
            };
            _commands.Execute(new AddObjectCommand(chair, "Добавить стул"));
            _status = "Добавлен стул.";
        }

        private void MoveLast()
        {
            Project p = _projects.Current;
            PlacedObject o = p.Objects[p.Objects.Count - 1];
            _commands.Execute(new SetObjectTransformCommand(o.Id,
                new Vec3(o.Position.X + 0.5f, o.Position.Y, o.Position.Z), o.Rotation, o.Scale, "Сдвинуть объект"));
            _status = "Объект сдвинут на 0,5 м.";
        }

        private void RemoveLast()
        {
            Project p = _projects.Current;
            _commands.Execute(new RemoveObjectCommand(p.Objects[p.Objects.Count - 1].Id, "Удалить объект"));
            _status = "Объект удалён.";
        }

        private void GetBounds(Project p, out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = 0f; maxX = 4f; minZ = 0f; maxZ = 3f;
            if (p.Rooms.Count == 0) return;
            minX = float.MaxValue; maxX = float.MinValue; minZ = float.MaxValue; maxZ = float.MinValue;
            foreach (PlanPoint pt in p.Rooms[0].Boundary)
            {
                minX = Mathf.Min(minX, pt.X); maxX = Mathf.Max(maxX, pt.X);
                minZ = Mathf.Min(minZ, pt.Z); maxZ = Mathf.Max(maxZ, pt.Z);
            }
        }

        private void AddPartition()
        {
            Project p = _projects.Current;
            float minX, maxX, minZ, maxZ;
            GetBounds(p, out minX, out maxX, out minZ, out maxZ);

            float x = UnityEngine.Random.Range(minX + 0.5f, maxX - 0.5f);
            string roomId = p.Rooms.Count > 0 ? p.Rooms[0].Id : null;
            float height = p.Rooms.Count > 0 ? p.Rooms[0].Height : 2.7f;

            _commands.Execute(new AddWallCommand(new PlanPoint(x, minZ), new PlanPoint(x, maxZ),
                height, 0.1f, roomId, ElementState.Proposed, "Добавить перегородку"));
            _status = "Добавлена перегородка (зелёная).";
        }

        private void AddWindowToLastWall()
        {
            Project p = _projects.Current;
            Wall w = p.Walls[p.Walls.Count - 1];
            OpeningSize size = OpeningPresets.Window;
            _commands.Execute(new AddOpeningCommand(w.Id, OpeningType.Window, 0.05f,
                size.Width, size.Height, size.SillHeight, "mat_glass_01"));
            _status = "Добавлено окно в начале последней стены.";
        }

        private void AddDoorToLastWall()
        {
            Project p = _projects.Current;
            Wall w = p.Walls[p.Walls.Count - 1];
            OpeningSize size = OpeningPresets.Door;
            float offset = Mathf.Max(0f, WallRules.Length(w) - size.Width - 0.05f);
            _commands.Execute(new AddOpeningCommand(w.Id, OpeningType.Door, offset,
                size.Width, size.Height, size.SillHeight, "mat_door_01"));
            _status = "Добавлена дверь в конце последней стены.";
        }

        private void MoveLastWall()
        {
            Project p = _projects.Current;
            Wall w = p.Walls[p.Walls.Count - 1];
            _commands.Execute(new MoveWallCommand(w.Id,
                new PlanPoint(w.A.X + 0.5f, w.A.Z), new PlanPoint(w.B.X + 0.5f, w.B.Z), "Сдвинуть стену"));
            _status = "Стена сдвинута на 0,5 м.";
        }

        private void RemoveLastWall()
        {
            Project p = _projects.Current;
            _commands.Execute(new RemoveWallCommand(p.Walls[p.Walls.Count - 1].Id, "Удалить стену"));
            _status = "Стена удалена вместе с её окнами и дверями.";
        }

        private void ReopenFromDisk()
        {
            _projects.Save();
            string id = _projects.Current.Id;

            _projects = new ProjectService(new ProjectStorage(_storageDir));
            _projects.Load(id);
            AttachCommands();
            Rebuild();

            _status = "Проект записан на диск и открыт заново. Объектов: " + _projects.Current.Objects.Count + ".";
        }
    }
}
