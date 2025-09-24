#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using System.Security.Policy;

[CustomEditor(typeof(BlockRegistryObject))]
public class BlockRegistryObjectEditor : Editor
{
    private ReorderableList BlockList;
    private int TextureSize = 32;

    private void OnEnable()
    {
        // Get the serialized property for the array
        SerializedProperty blocksProp = serializedObject.FindProperty("Blocks");

        BlockList = new ReorderableList(serializedObject, blocksProp, true, true, true, true);

        // Header
        BlockList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Block Registry");
        };

        // Element
        BlockList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            SerializedProperty element = BlockList.serializedProperty.GetArrayElementAtIndex(index);
            SerializedProperty nameProp = element.FindPropertyRelative("BlockName");
            SerializedProperty idProp = element.FindPropertyRelative("ID");
            SerializedProperty textureProp = element.FindPropertyRelative("Textures");

            string label = string.IsNullOrEmpty(nameProp.stringValue) ?
                $"Block {idProp.intValue}" : nameProp.stringValue + $" ({idProp.intValue})";
            GUIContent content = new GUIContent(label);

            if (textureProp != null && textureProp.arraySize > 0)
            {
                SerializedProperty textureProp0 = textureProp.GetArrayElementAtIndex(0);
                content = new GUIContent(" " + label, AssetPreview.GetAssetPreview(textureProp0.objectReferenceValue as Texture2D));
            }

            EditorGUI.indentLevel++;
            EditorGUI.PropertyField(rect, element, content, true); // true = draw children
            EditorGUI.indentLevel--;
        };

        // Adjust height
        BlockList.elementHeightCallback = (int index) =>
        {
            var element = BlockList.serializedProperty.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        BlockList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
        GUILayout.Label("Note: If multiple textures are used, they are ordered:", EditorStyles.miniLabel);
        GUILayout.Label("Front, Back, Left, Right, Top, Bottom", EditorStyles.miniLabel);

        TextureSize = EditorGUILayout.IntField("Expected Texture Size", TextureSize);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate IDs"))
        {
            // Go through all blocks and assign IDs
            BlockRegistryObject registry = (BlockRegistryObject)target;
            for (int i = 0; i < registry.Blocks.Length; i++)
            {
                registry.Blocks[i].ID = i + 1; // Start from 1
            }
            EditorUtility.SetDirty(registry);
        }

        if (GUILayout.Button("Generate Atlases"))
        {
            GenerateAtlases();
        }
    }

    private void GenerateAtlases()
    {
        if (!ValidateBlockIDs())
        {
            Debug.LogError("Block ID validation failed. Try regenerating IDs. Aborting...");
            return;
        }
        Debug.Log("Block ID validation passed.");

        if (!ValidateTextures())
        {
            Debug.LogError("Texture validation failed. Aborting...");
            return;
        }
        Debug.Log("Texture validation passed.");

        var uniqueTextures = GetUniqueTextures();
        var uniqueTexturesCount = uniqueTextures.Count;
        Debug.Log($"Found {uniqueTextures.Count} unique textures.");

        // Create the atlas texture
        Texture2DArray atlasTextures = new Texture2DArray(TextureSize, TextureSize, uniqueTexturesCount, TextureFormat.RGBA32, false);
        atlasTextures.filterMode = FilterMode.Point;
        atlasTextures.wrapMode = TextureWrapMode.Repeat;
        int index = 0;
        foreach (var tex in uniqueTextures)
        {
            // Convert texture to RGBA32 if necessary
            if (tex.format != TextureFormat.RGBA32)
            {
                Texture2D convertedTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                if (!Graphics.ConvertTexture(tex, convertedTex))
                {
                    Debug.LogWarning($"Failed to convert texture '{tex.name}' to RGBA32 format. Skipping...");
                    continue;
                }
                Graphics.CopyTexture(convertedTex, 0, 0, atlasTextures, index, 0);
            }
            else
            {
                Graphics.CopyTexture(tex, 0, 0, atlasTextures, index, 0);
            }
            index++;
        }

        // Save the atlas texture as an asset
        AssetDatabase.CreateAsset(atlasTextures, "Assets/Scene/Textures/BlockAtlasTextures.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Generated texture atlas at Assets/Scene/Textures/BlockAtlasTextures.asset");
    }

    private bool ValidateBlockIDs()
    {
        BlockRegistryObject registry = (BlockRegistryObject)target;
        HashSet<int> usedIDs = new HashSet<int>();
        foreach (var block in registry.Blocks)
        {
            if (block.ID == 0)
            {
                Debug.LogWarning($"Block '{block.BlockName}' has ID 0, which is invalid.");
                return false;
            }
            else if (usedIDs.Contains(block.ID))
            {
                Debug.LogWarning($"Duplicate ID {block.ID} found in block '{block.BlockName}'.");
                return false;
            }
            else
            {
                usedIDs.Add(block.ID);
            }
        }

        return true;
    }

    private bool ValidateTextures()
    {
        BlockRegistryObject registry = (BlockRegistryObject)target;
        foreach (var block in registry.Blocks)
        {
            if (block.Textures == null || block.Textures.Length == 0)
            {
                Debug.LogWarning($"Block '{block.BlockName}' has no textures assigned.");
                return false;
            }
            else if (block.HasMultipleTextures && block.Textures.Length < 6)
            {
                Debug.LogWarning($"Block '{block.BlockName}' is marked as having multiple textures but has less than 6 textures assigned.");
                return false;
            }
            else if (!block.HasMultipleTextures && block.Textures.Length > 1)
            {
                Debug.LogWarning($"Block '{block.BlockName}' is marked as not having multiple textures but has more than 1 texture assigned.");
                return false;
            }

            foreach (var tex in block.Textures)
            {
                if (tex == null)
                {
                    Debug.LogWarning($"Block '{block.BlockName}' has a null texture assigned.");
                    return false;
                }

                if (tex.width != TextureSize || tex.height != TextureSize)
                {
                    Debug.LogWarning($"Texture '{tex.name}' in block '{block.BlockName}' does not match expected size of {TextureSize}x{TextureSize}.");
                    return false;
                }
            }
        }

        return true;
    }

    private HashSet<Texture2D> GetUniqueTextures()
    {
        BlockRegistryObject registry = (BlockRegistryObject)target;
        HashSet<Texture2D> uniqueTextures = new HashSet<Texture2D>();
        foreach (var block in registry.Blocks)
        {
            if (block.Textures != null)
            {
                foreach (var tex in block.Textures)
                {
                    if (tex != null)
                    {
                        uniqueTextures.Add(tex);
                    }
                }
            }
        }
        return uniqueTextures;
    }
}
#endif