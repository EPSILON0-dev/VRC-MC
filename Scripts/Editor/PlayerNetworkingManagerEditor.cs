#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerNetworkingManager))]
public class PlayerNetworkingManagerEditor : Editor
{
    private int _maxPlayers = 8;
    private WorldManager _worldManager;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        _maxPlayers = EditorGUILayout.IntField("Max Players", _maxPlayers);
        _worldManager = (WorldManager)EditorGUILayout.ObjectField("World Manager", _worldManager, typeof(WorldManager), true);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate"))
        {
            ClearObjectsList();
            GenerateObjectsList();
        }
        if (GUILayout.Button("Clear"))
        {
            ClearObjectsList();
        }
        GUILayout.EndHorizontal();
    }

    private void ClearObjectsList()
    {
        PlayerNetworkingManager manager = (PlayerNetworkingManager)target;

        if (manager.objects != null)
        {
            for (int i = manager.objects.Length - 1; i >= 0; i--)
            {
                DestroyImmediate(manager.objects[i]);
            }
        }

        manager.objects = null;
        EditorUtility.SetDirty(manager);
    }

    private void GenerateObjectsList()
    {
        PlayerNetworkingManager manager = (PlayerNetworkingManager)target;

        manager.objects = new GameObject[_maxPlayers];
        for (int i = 0; i < _maxPlayers; i++)
        {
            GameObject obj = new GameObject($"requester_{i}");
            var requester = obj.AddComponent<BlockPlaceRequester>();
            obj.transform.parent = manager.transform;
            requester.WorldManager = _worldManager;
            manager.objects[i] = obj;
        }

        EditorUtility.SetDirty(manager);
    }
}

#endif