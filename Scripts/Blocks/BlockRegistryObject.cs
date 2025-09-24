using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BlockRegistry", menuName = "ScriptableObjects/BlockRegistry", order = 1)]
public class BlockRegistryObject : ScriptableObject
{
    [System.Serializable]
    public class BlockEntry
    {
        // Block name -- display name
        public string BlockName;
        // Block ID -- internal ID, should be unique and non-zero
        public int ID;
        // Block textures -- If there are multiple textures, the order is: 
        //                    Front, Back, Left, Right, Top, Bottom
        public Texture2D[] Textures;
        // Has multiple textures - if false, all faces use Textures[0]
        public bool HasMultipleTextures;
        // Is full -- if true, the faces facing this block will be rendered
        public bool IsNotFull;
        // Is Glass -- if true, the faces facing this block will not be rendered if the other block is the same
        public bool IsGlass;
    };

    public BlockEntry[] Blocks;
}
