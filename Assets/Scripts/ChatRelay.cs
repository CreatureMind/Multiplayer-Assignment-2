using Events;
using Fusion;

public class ChatRelay : NetworkBehaviour
{
    public override void Spawned()
    {
        var manager = NetworkManager.Instance?.ChatNetworkManager;
        if (manager) manager.ChatRelay = this;

        if (!HasStateAuthority)
            RPC_RequestHistory(Runner.LocalPlayer);
        
        EventBus.Raise(new OnChatRelaySpawnedEvent());
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        var manager = NetworkManager.Instance?.ChatNetworkManager;
        if (manager && manager.ChatRelay == this) manager.ChatRelay = null;
        manager?.ResetSessionState();
        
        EventBus.Raise(new OnChatRelayDespawnedEvent());
    }

    [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable, TickAligned = false)]
    public void RPC_SendMessage(MessageData message)
    {
        EventBus.Raise(new NetworkMessageReceivedEvent
        {
            Sender = message.Sender.Value,
            Target = message.Target.Value,
            Message = message.Message.Value
        });
    }

    [Rpc(RpcSources.All, RpcTargets.All, Channel = RpcChannel.Reliable, TickAligned = false)]
    public void RPC_SendWhisper([RpcTarget] PlayerRef target, MessageData message)
    {
        EventBus.Raise(new NetworkMessageReceivedEvent
        {
            Sender = message.Sender.Value,
            Target = message.Target.Value,
            Message = message.Message.Value
        });
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
    public void RPC_RequestHistory(PlayerRef requester)
    {
        EventBus.Raise(new ChatHistoryRequestedEvent { Requester = requester });
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    public void RPC_SendHistoryEntry([RpcTarget] PlayerRef target, MessageData message)
    {
        EventBus.Raise(new NetworkMessageReceivedEvent
        {
            Sender = message.Sender.Value,
            Target = message.Target.Value,
            Message = message.Message.Value
        });
    }
}
