using System.Collections.Generic;

// Server-side transport for turn state. One job: fan authoritative turn updates to each player's ClientManager.
public sealed class TurnDiffBroadcaster
{
    private readonly TurnManager _turnManager;
    private readonly IReadOnlyList<ClientManager> _clients;

    public TurnDiffBroadcaster(TurnManager turnManager, IReadOnlyList<ClientManager> clients)
    {
        _turnManager = turnManager;
        _clients = clients;
    }

    public void BroadcastInstantiation(IReadOnlyList<PlayerActionData> allPlayerActions, PlayerActionData currentPlayingPlayer)
    {
        // Sends initial turn snapshot and current actor to every player's input-authority client.
        if (allPlayerActions == null || allPlayerActions.Count == 0)
            return;

        foreach (var client in _clients)
            SendInstantiationToClient(client, allPlayerActions, currentPlayingPlayer);
    }

    public void SendInstantiationToClient(ClientManager client, IReadOnlyList<PlayerActionData> allPlayerActions, PlayerActionData currentPlayingPlayer)
    {
        if (!_turnManager || !client || allPlayerActions == null || allPlayerActions.Count == 0)
            return;

        var payload = new PlayerActionData[allPlayerActions.Count];
        for (var i = 0; i < allPlayerActions.Count; i++)
            payload[i] = allPlayerActions[i];

        client.RPC_InitialisePlayerActions(payload, payload.Length);
    }

    public void BroadcastCurrentPlayingPlayer(PlayerActionData currentPlayingPlayer)
    {
        // Mirrors action-budget changes for the currently active player to every client.
        foreach (var client in _clients)
        {
            if (!client)
                continue;
            client.RPC_CurrentPlayingPlayerChanged(currentPlayingPlayer);
        }
    }

    public void BroadcastTurnChanged(PlayerActionData upcomingPlayer)
    {
        // Broadcasts end-turn/start-turn transition using the next player's authoritative action data.
        foreach (var client in _clients)
        {
            if (!client)
                continue;
            client.RPC_TurnChanged(upcomingPlayer);
        }
    }
}
