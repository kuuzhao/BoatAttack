using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AutoStreamer
{
    public class AsTextureConfig : ScriptableObject {
	    [SerializeField]
        List<AsTextureTreeDataItem> m_TextureItems = new List<AsTextureTreeDataItem>();

        public List<AsTextureTreeDataItem> textureItems
        {
            get { return m_TextureItems; }
            set {  m_TextureItems = value; }
        }

        void Awake()
        {
            if (m_TextureItems.Count == 0)
            {
                var root = new AsTextureTreeDataItem("Root", -1, 0);
                m_TextureItems.Add(root);
            }
        }
    }
}