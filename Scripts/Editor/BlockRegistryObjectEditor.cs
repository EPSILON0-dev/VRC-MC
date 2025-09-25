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
                $"Block {idProp.intValue}" : nameProp.stringValue +
                ((idProp.intValue >> 8 != 0) ? $" ({idProp.intValue & 0xff} | 0x{idProp.intValue >> 8:x2})" :
                $" ({idProp.intValue})");
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
                int id = i + 1; // Start from 1
                id |= (registry.Blocks[i].IsGlass ? 1 : 0) << 14;
                id |= (registry.Blocks[i].IsNotFull ? 1 : 0) << 13;
                registry.Blocks[i].ID = id;
            }
            EditorUtility.SetDirty(registry);
        }

        if (GUILayout.Button("Generate Atlas"))
        {
            GenerateAtlas();
        }

        if (GUILayout.Button("Generate Side Mapping"))
        {
            GenerateSideMapping();
        }
    }

    private void GenerateAtlas()
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

        GenerateAtlasInternal();
    }

    private void GenerateSideMapping()
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

        GenerateSideMappingInternal();
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

    private void GenerateAtlasInternal()
    {
        var uniqueTextures = GetUniqueTextures();
        var uniqueTexturesCount = uniqueTextures.Count;
        Debug.Log($"Found {uniqueTextures.Count} unique textures.");

        // Create the atlas texture
        Texture2DArray atlasTextures = new Texture2DArray(TextureSize, TextureSize, uniqueTexturesCount + 1, TextureFormat.RGBA32, false);
        atlasTextures.filterMode = FilterMode.Point;
        atlasTextures.wrapMode = TextureWrapMode.Repeat;

        // First texture is a dummy texture (black)
        Texture2D blackTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0, 0, 0, 0);
        blackTexture.SetPixels(pixels);
        blackTexture.Apply();
        Graphics.CopyTexture(blackTexture, 0, 0, atlasTextures, 0, 0);

        // Fill the rest of the textures
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
                Graphics.CopyTexture(convertedTex, 0, 0, atlasTextures, index + 1, 0);
            }
            else
            {
                Graphics.CopyTexture(tex, 0, 0, atlasTextures, index + 1, 0);
            }
            index++;
        }

        // Save the atlas texture as an asset
        AssetDatabase.CreateAsset(atlasTextures, "Assets/Scene/Textures/BlockAtlasTextures.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Generated texture atlas at Assets/Scene/Textures/BlockAtlasTextures.asset");
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

    private void GenerateSideMappingInternal()
    {
        BlockRegistryObject registry = (BlockRegistryObject)target;

        var uniqueTextures = GetUniqueTextures();

        // Texture has 128 slots for 128 blocks (may be increased later)
        Texture2D sideMapping = new Texture2D(16, 16, TextureFormat.RGBA32, false, true);
        sideMapping.filterMode = FilterMode.Point;
        sideMapping.wrapMode = TextureWrapMode.Repeat;

        // Fill the texture with the side mapping data
        // Skip the air block (ID 0)
        for (int i = 1; i < 128; i++)
        {
            int[] sides = new int[6] { 0, 0, 0, 0, 0, 0 };

            if (i < registry.Blocks.Length)
            {
                var block = registry.Blocks[i - 1];
                if (block.HasMultipleTextures && block.Textures.Length >= 6)
                {
                    for (int s = 0; s < 6; s++)
                    {
                        sides[s] = System.Array.IndexOf(new List<Texture2D>(uniqueTextures).ToArray(), block.Textures[s]) + 1;
                    }
                }
                else if (block.Textures.Length >= 1)
                {
                    int texIndex = System.Array.IndexOf(new List<Texture2D>(uniqueTextures).ToArray(), block.Textures[0]) + 1;
                    for (int s = 0; s < 6; s++)
                    {
                        sides[s] = texIndex;
                    }
                }
                Debug.Log($"Block ID {i + 1}: Sides = [{sides[0]}, {sides[1]}, {sides[2]}, {sides[3]}, {sides[4]}, {sides[5]}]");
            }

            Color32 sideDataLower = new Color32((byte)sides[0], (byte)sides[1], (byte)sides[2], 255);
            Color32 sideDataUpper = new Color32((byte)sides[3], (byte)sides[4], (byte)sides[5], 255);

            sideMapping.SetPixel(i % 8 * 2, i / 8, sideDataLower);
            sideMapping.SetPixel(i % 8 * 2 + 1, i / 8, sideDataUpper);
        }

        sideMapping.Apply();

        // Save the side mapping texture as an asset
        AssetDatabase.CreateAsset(sideMapping, "Assets/Scene/Textures/BlockSideMapping.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("Generated side mapping texture at Assets/Scene/Textures/BlockSideMapping.asset");
    }
}
#endif