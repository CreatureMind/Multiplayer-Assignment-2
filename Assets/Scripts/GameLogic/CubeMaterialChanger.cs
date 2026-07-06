using Fusion;
using UnityEngine;

public class CubeMaterialChanger : NetworkBehaviour
{
    [SerializeField] private new MeshRenderer renderer;
    
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

    public override void Spawned()
    {
        InstantiateMaterialColor(NetworkedColor);
    }

    public void RequestDestroy(PlayerRef destroyer)
    {
        if (!Object || !Object.IsValid) return;

        // Input-authority self-invoke of an InputAuthority-targeted RPC throws when
        // called from input callbacks. Only external players use this destroy path.
        if (Object.HasInputAuthority) return;

        Rpc_RequestDestroy(destroyer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestDestroy(PlayerRef destroyer)
    {
        ScoreManager.Instance?.Rpc_AddScore(destroyer);

        Runner.Despawn(Object);
        
    }
}
