using Fusion;
using UnityEngine;

public class CubeMaterialChanger : NetworkBehaviour
{
    [SerializeField] private MeshRenderer renderer;
    
    [Networked, OnChangedRender(nameof(OnChangedMaterial))]
    private Color NetworkedColor { get; set; }

    public void InstantiateMaterialColor(Color color)
    {
        if (renderer != null)
        {
            renderer.material.color = color;
            NetworkedColor = color;
        }
    }
    
    private void OnChangedMaterial()
    {
        renderer.material.color = NetworkedColor;
    }
    
}
