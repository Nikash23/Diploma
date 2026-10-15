#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Положите этот файл в папку Assets/Editor.
// Меню: Furniture → Generate Icons — рисует иконки из префабов и записывает их в FurnitureItem.icon
public static class FurnitureIconGenerator
{
    const string OutDir = "Assets/Prefabs/Catalog/Icons";
    const int Size = 256;

    [MenuItem("Furniture/Generate Icons")]
    static void GenerateAll()
    {
        Directory.CreateDirectory(OutDir);
        AssetDatabase.Refresh();

        int done = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:FurnitureItem"))
        {
            var item = AssetDatabase.LoadAssetAtPath<FurnitureItem>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || item.prefab == null) continue;

            string png = $"{OutDir}/{item.name}.png";
            RenderIcon(item.prefab, png);

            AssetDatabase.ImportAsset(png);
            var ti = (TextureImporter)AssetImporter.GetAtPath(png);
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = false;
            ti.SaveAndReimport();

            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            EditorUtility.SetDirty(item);
            done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Иконок создано: {done}");
    }

    static void RenderIcon(GameObject prefab, string path)
    {
        Vector3 origin = new Vector3(5000, 5000, 5000);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.SetPositionAndRotation(origin, Quaternion.identity);

        var rends = inst.GetComponentsInChildren<Renderer>();
        var b = new Bounds(rends[0].bounds.center, Vector3.zero);
        foreach (var r in rends) b.Encapsulate(r.bounds);

        var lightGO = new GameObject("IconLight");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        var camGO = new GameObject("IconCam");
        var cam = camGO.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.82f, 0.84f, 0.88f, 1f);
        cam.fieldOfView = 25f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 200f;

        float dist = b.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
        Vector3 dir = new Vector3(1f, 0.6f, -1f).normalized;
        camGO.transform.position = b.center + dir * dist;
        camGO.transform.LookAt(b.center);

        var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());

        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGO);
        Object.DestroyImmediate(lightGO);
        Object.DestroyImmediate(inst);
    }
}
#endif