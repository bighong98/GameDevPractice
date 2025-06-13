using System;
using UnityEngine;

public class RadialBar : MonoBehaviour
{
    private SpriteRenderer barRenderer; // serialize for debug
    private MaterialPropertyBlock propertyBlock;
    private static readonly int Ratio = Shader.PropertyToID("_Ratio");
    
    private void Awake()
    {
        if (barRenderer == null)
            barRenderer = GetComponent<SpriteRenderer>();

        propertyBlock = new MaterialPropertyBlock();
    }
    
    public void MoveBar(float ratio)
    {
        barRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(Ratio, ratio);
        barRenderer.SetPropertyBlock(propertyBlock);
    }
}
