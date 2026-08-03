using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Events;
using Fusion;
using UnityEngine;

/// <summary>
/// Lives on the Lobby Hub session. Server-authoritative registry of open rooms,
/// replicated to every connected client, plus the create-room request endpoint.
/// </summary>
public class LobbyHubService : NetworkBehaviour
{
    public static LobbyHubService Instance { get; private set; }

    [Networked, Capacity(32)]
    private NetworkDictionary<int, RoomInfo> Rooms => default;

    [Networked] private int TotalPlayers { get; set; }

    [Networked, OnChangedRender(nameof(OnRegistryChanged))]
    private int Version { get; set; }

    public override void Spawned()
    {
        Instance = this;
        Debug.Log($"[Net] LobbyHubService.Spawned (HasStateAuthority={HasStateAuthority}) -> Instance set.");
        OnRegistryChanged(); // seed clients that spawn after rooms already exist
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    private void OnRegistryChanged()
    {
        var list = new List<RoomInfo>(Rooms.Count);
        foreach (var kvp in Rooms)
            list.Add(kvp.Value);

        EventBus.Raise(new RoomListChangedEvent { Rooms = list, TotalPlayers = TotalPlayers });
    }

    // ---------------- Server-side registry mutation ----------------

    public void AddRoom(RoomInfo info)
    {
        if (!HasStateAuthority) return;
        // Private rooms should not appear in the public lobby list.
        if (!info.IsPublic) return;
        Rooms.Set(info.RoomId, info);
        Touch();
    }

    public void RemoveRoom(int roomId)
    {
        if (!HasStateAuthority) return;
        Rooms.Remove(roomId);
        Touch();
    }

    public void SetRoomPlayerCount(int roomId, int count)
    {
        if (!HasStateAuthority) return;
        if (!Rooms.TryGet(roomId, out var info)) return;
        info.PlayerCount = (byte)count;
        Rooms.Set(roomId, info);
        Touch();
    }

    public void SetRoomOpen(int roomId, bool isOpen)
    {
        if (!HasStateAuthority) return;
        if (!Rooms.TryGet(roomId, out var info)) return;
        info.IsOpen = isOpen;
        Rooms.Set(roomId, info);
        Touch();
    }

    public void SetRoomOwner(int roomId, PlayerRef owner)
    {
        if (!HasStateAuthority) return;
        if (!Rooms.TryGet(roomId, out var info)) return;
        info.Owner = owner;
        Rooms.Set(roomId, info);
        Touch();
    }

    private void Touch()
    {
        var total = 0;
        foreach (var kvp in Rooms)
            total += kvp.Value.PlayerCount;
        TotalPlayers = total;
        Version++;
        
        if (HasStateAuthority)
        {
            OnRegistryChanged();
        }
    }

    // ---------------- Client -> Server: create room ----------------

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RequestCreateRoom(NetworkString<_32> roomName, NetworkString<_16> mode,
        NetworkString<_16> map, int maxPlayers, NetworkBool isPublic, PlayerRef requester)
    {
        Debug.Log($"[Server] Hub received create request from {requester}: '{roomName.Value}' ({mode.Value}/{map.Value}, max={maxPlayers}).");

        if (!RoomManager.Instance)
        {
            Debug.LogError("[Server] RoomManager.Instance is NULL — cannot create room.");
            Rpc_CreateRoomResult(requester, false, "", "", "Server unavailable.");
            return;
        }

        _ = CreateFlow(roomName.Value, mode.Value, map.Value, maxPlayers, isPublic, requester);
    }

    private async Task CreateFlow(string roomName, string mode, string map, int maxPlayers, bool isPublic, PlayerRef requester)
    {
        try
        {
            var result = await RoomManager.Instance.CreateRoomAsync(roomName, mode, map, maxPlayers, isPublic, requester);

            if (result.Ok)
                AddRoom(result.Info);

            Debug.Log($"[Server] CreateFlow done: Ok={result.Ok} session='{result.SessionName}' reason='{result.Reason}'. Registry count={Rooms.Count}");
            Rpc_CreateRoomResult(requester, result.Ok, result.SessionName, result.OwnerToken, result.Reason);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Rpc_CreateRoomResult(requester, false, "", "", "Server error while creating room.");
        }
    }

    // ---------------- Server -> requesting client: result ----------------

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_CreateRoomResult([RpcTarget] PlayerRef requester, NetworkBool ok,
        NetworkString<_32> sessionName, NetworkString<_32> ownerToken, NetworkString<_32> reason)
    {
        Debug.Log($"[Client] Create result received: ok={ok} session='{sessionName.Value}' reason='{reason.Value}'.");

        if (!NetworkManager.Instance) return;

        if (ok)
            NetworkManager.Instance.OnRoomCreationApproved(sessionName.Value, ownerToken.Value);
        else
            NetworkManager.Instance.OnRoomCreationRejected(reason.Value);
    }
}
