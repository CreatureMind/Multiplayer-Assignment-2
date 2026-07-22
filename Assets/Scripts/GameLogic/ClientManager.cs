using Fusion;
using UnityEngine;

public class ClientManager : NetworkBehaviour
{
    [Networked] public PlayerRef Player { get; private set; }
    private int _renderVersion;
    private static ServerGameManager _serverGameManager;
    
    public void InstantiateClientManager(ServerGameManager serverGameManager)
    {
        Player = Object.InputAuthority;
        _serverGameManager = serverGameManager;
        transform.name = $"ClientManager_{Player.PlayerId}";
        _renderVersion = 0;
        
        BoardManager.Instance.RegisterVisualRenderer(OnBoardChanged);
    }
    
    private void OnBoardChanged(int version)
    {
        if (version <= _renderVersion) return;
        _renderVersion = version;
        
        // Update visuals here
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RequestBoardChange_RPC(Vector2Int gridPosition, TileType targetType)
    {
        if(!Object.HasStateAuthority) return;
        
        _serverGameManager.RequestBoardChange(Player, gridPosition, targetType);
    }
    
}
