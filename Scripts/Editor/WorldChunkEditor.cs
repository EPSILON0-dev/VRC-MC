#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UdonSharp;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;

[CustomEditor(typeof(WorldChunk), true)]
public class WorldChunkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // TODO
        // Default for now
        UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false);
        DrawDefaultInspector();
    }
}
#endif