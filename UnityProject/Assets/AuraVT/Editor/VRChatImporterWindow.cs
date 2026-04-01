// AuraVT — VRChatImporterWindow
// Unity EditorWindow. Provides a clean UI for importing VRChat avatars.
// Open via: AuraVT menu → Import VRChat Avatar
//
// Features:
//   - Drag-and-drop .unitypackage onto the window
//   - Progress bar during import
//   - Results summary (files extracted, VRC components stripped, materials remapped)
//   - Ping / select imported prefab in Project window
//   - One-click "Load in AuraVT" to add the avatar to the scene

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public class VRChatImporterWindow : EditorWindow
{
    // ── State ─────────────────────────────────────────────────────────────────
    private string  _packagePath   = "";
    private bool    _isImporting   = false;
    private float   _progress      = 0f;
    private string  _progressLabel = "";
    private VRChatImporter.ImportResult _lastResult;
    private bool    _hasResult     = false;
    private Vector2 _scroll;

    // ── Styles ────────────────────────────────────────────────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _boxStyle;
    private bool     _stylesInit;

    // ── Menu item ─────────────────────────────────────────────────────────────
    [MenuItem("AuraVT/Import VRChat Avatar")]
    public static void Open()
    {
        var win = GetWindow<VRChatImporterWindow>("VRChat Importer");
        win.minSize = new Vector2(420, 500);
        win.Show();
    }

    // ── GUI ───────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        InitStyles();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawHeader();
        EditorGUILayout.Space(8);
        DrawPackageSelector();
        EditorGUILayout.Space(8);

        if (_isImporting)
            DrawProgress();
        else
            DrawImportButton();

        if (_hasResult)
        {
            EditorGUILayout.Space(12);
            DrawResults();
        }

        DrawDropArea();
        EditorGUILayout.EndScrollView();
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(_boxStyle);
        GUILayout.Label("AuraVT — VRChat Avatar Importer", _headerStyle);
        EditorGUILayout.LabelField(
            "Import .unitypackage VRChat avatars without the VRChat SDK.\n" +
            "Poiyomi, lilToon, and VRC components are automatically handled.",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawPackageSelector()
    {
        EditorGUILayout.LabelField("Avatar Package (.unitypackage)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _packagePath = EditorGUILayout.TextField(_packagePath);
        if (GUILayout.Button("Browse…", GUILayout.Width(70)))
        {
            string picked = EditorUtility.OpenFilePanel(
                "Select VRChat Avatar Package",
                "",
                "unitypackage");
            if (!string.IsNullOrEmpty(picked))
                _packagePath = picked;
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_packagePath) && !File.Exists(_packagePath))
            EditorGUILayout.HelpBox("File not found.", MessageType.Error);
    }

    private void DrawImportButton()
    {
        bool canImport = !string.IsNullOrEmpty(_packagePath) && File.Exists(_packagePath);

        GUI.enabled = canImport;
        if (GUILayout.Button("▶  Import Avatar", GUILayout.Height(36)))
            StartImport();
        GUI.enabled = true;

        if (!canImport)
            EditorGUILayout.HelpBox("Select a .unitypackage file above to begin.", MessageType.Info);
    }

    private void DrawProgress()
    {
        EditorGUILayout.HelpBox(_progressLabel, MessageType.None);
        var rect = GUILayoutUtility.GetRect(18, 20, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(rect, _progress, _progressLabel);
    }

    private void DrawResults()
    {
        EditorGUILayout.LabelField("Import Results", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(_boxStyle);

        if (_lastResult.Success)
        {
            EditorGUILayout.LabelField($"✅ Files extracted:          {_lastResult.FilesExtracted}");
            EditorGUILayout.LabelField($"🗑  VRC components removed:  {_lastResult.VRCComponentsStripped}");
            EditorGUILayout.LabelField($"🎨 Materials remapped:       {_lastResult.MaterialsRemapped}");
            EditorGUILayout.LabelField($"📦 Prefabs found:            {_lastResult.PrefabPaths?.Length ?? 0}");
            EditorGUILayout.LabelField($"📐 FBX files found:          {_lastResult.FBXPaths?.Length ?? 0}");
            EditorGUILayout.LabelField($"📁 Staging folder:           {_lastResult.StagingDir}");

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping Prefab in Project"))
                VRChatImporter.PingFirstPrefab(_lastResult);

            if (GUILayout.Button("Add to Scene"))
                AddAvatarToScene();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox($"❌ Import failed:\n{_lastResult.Error}", MessageType.Error);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawDropArea()
    {
        // Handle drag-and-drop from OS file manager onto the window
        var dropRect = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "— or drag a .unitypackage here —", EditorStyles.centeredGreyMiniLabel);

        var e = Event.current;
        if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) &&
             dropRect.Contains(e.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var path in DragAndDrop.paths)
                {
                    if (path.EndsWith(".unitypackage"))
                    {
                        _packagePath = path;
                        Repaint();
                        break;
                    }
                }
            }
            e.Use();
        }
    }

    // ── Import logic ──────────────────────────────────────────────────────────

    private void StartImport()
    {
        _isImporting = true;
        _hasResult   = false;
        _progress    = 0f;
        Repaint();

        try
        {
            _lastResult = VRChatImporter.Import(_packagePath, (p, msg) =>
            {
                _progress      = p;
                _progressLabel = msg;
                Repaint();
            });
            _hasResult = true;
        }
        finally
        {
            _isImporting = false;
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    private void AddAvatarToScene()
    {
        string prefabPath = (_lastResult.PrefabPaths != null && _lastResult.PrefabPaths.Length > 0)
            ? _lastResult.PrefabPaths[0]
            : (_lastResult.FBXPaths != null && _lastResult.FBXPaths.Length > 0
                ? _lastResult.FBXPaths[0] : null);

        if (prefabPath == null)
        {
            EditorUtility.DisplayDialog("AuraVT", "No prefab or FBX found to add to scene.", "OK");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance != null)
        {
            instance.transform.position = Vector3.zero;
            Selection.activeGameObject  = instance;
            SceneView.FrameLastActiveSceneView();
            Debug.Log($"[AuraVT] Added to scene: {instance.name}");
        }
    }

    // ── Styles ────────────────────────────────────────────────────────────────

    private void InitStyles()
    {
        if (_stylesInit) return;
        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleCenter,
        };
        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 8, 8),
        };
        _stylesInit = true;
    }
}
#endif
