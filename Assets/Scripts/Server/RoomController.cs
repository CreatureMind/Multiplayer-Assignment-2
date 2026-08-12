using Events;
using Fusion;
using UnityEngine.SceneManagement;
using Utils;

/// <summary>
/// Server-authoritative controller spawned into each room session. Holds the room
/// owner and match state and validates owner-only actions (kick / start) coming
/// from clients before executing them on the server.
/// </summary>
public class RoomController : NetworkBehaviour
{
    public static RoomController Instance { get; private set; }

    [Networked] public PlayerRef Owner { get; private set; }

    [Networked, OnChangedRender(nameof(OnMatchStartedChanged))]
    public NetworkBool MatchStarted { get; private set; }

    private int _roomId;

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    // ---------------- Server-side ----------------

    public void ServerInit(int roomId)
    {
        if (!HasStateAuthority) return;
        _roomId = roomId;
    }

    public void AssignOwner(PlayerRef owner)
    {
        if (!HasStateAuthority) return;
        Owner = owner;
    }

    // ---------------- Client -> Server requests ----------------

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RequestKick(PlayerRef target, PlayerRef requester)
    {
        if (requester != Owner) return; // only the owner may kick
        if (target == Owner) return; // owner can't kick themselves

        //Tell the player to leave explicitly, then disconnect them server-side.
        Rpc_NotifyKicked(target, "Kicked by room owner.");
        Runner.Disconnect(target);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void Rpc_NotifyKicked([RpcTarget] PlayerRef target, NetworkString<_32> reason)
    {
        // Client-only.
        if (!NetworkManager.Instance) return;
        _ = NetworkManager.Instance.LeaveRoom(NetworkManager.Instance.CurrentLobbyId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RequestStartMatch(PlayerRef requester)
    {
        if (requester != Owner) return; // only the owner may start
        if (MatchStarted) return;

        MatchStarted = true;

        // Closing the room so no new players join a match in progress.
        if (RoomManager.Instance)
            RoomManager.Instance.SetRoomOpen(_roomId, false);
        Runner.SessionInfo.IsOpen = false;
        
        Runner.LoadScene(SceneRef.FromIndex((int)SceneDefs.GAME));
    }

    private void OnMatchStartedChanged()
    {
        if (MatchStarted)
            EventBus.Raise(new MatchStartedEvent());
    }
}
