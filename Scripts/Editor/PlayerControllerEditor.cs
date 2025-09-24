#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharp;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;

[CustomEditor(typeof(PlayerController), true)]
public class PlayerControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false);

        DrawDefaultInspector();

        if (GUILayout.Button("Clear Colliders"))
        {
            ClearColliders();
        }

        if (GUILayout.Button("Setup Colliders"))
        {
            SetupColliders();
        }
    }

    private void ClearColliders()
    {
        PlayerController controller = (PlayerController)target;
        for (int i = controller.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(controller.transform.GetChild(i).gameObject);
    }

    private void SetupColliders()
    {
        ClearColliders();

        PlayerController controller = (PlayerController)target;

        controller.PlayerColliders = new GameObject[controller.ColliderOffsets.Length];
        for (int i = 0; i < controller.ColliderOffsets.Length; i++)
        {
            GameObject colliderObject = new GameObject($"Collider {i}");
            colliderObject.transform.parent = controller.transform;
            colliderObject.transform.localPosition = controller.ColliderOffsets[i];
            var collider = colliderObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0.5f, -0.5f, 0.5f);
            controller.PlayerColliders[i] = colliderObject;
        }
    }
}
#endif