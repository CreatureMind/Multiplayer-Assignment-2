using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;
    private TurnStatsSO _turnStats;
    private List<ClientManager> _clientManagers = new List<ClientManager>();
    // Authoritative transport for turn/action diffs via per-client input-authority RPCs.
    private TurnDiffBroadcaster _turnDiffBroadcaster;

    private const int maxPlayers = 8;
    [Networked, Capacity(maxPlayers)] private NetworkArray<PlayerActionData> PlayerActions => default;

    private int _currentTurnIndex;

    // Guards render-change callbacks from firing before turn dependencies are fully wired.
    private bool _isInstantiated;

    public static Action<PlayerActionData> OnPlayerActionChanged;
    public static Action OnTurnChanged;

    [Networked, OnChangedRender(nameof(CurrentTurnIndexChanged))]
    private int CurrentTurnIndex
    {
        get => _currentTurnIndex;
        set
        {
            // Wraps turns against live player count so indexing stays valid across authoritative progression.
            var playerCount = _clientManagers.Count;
            if (playerCount <= 0)
            {
                _currentTurnIndex = 0;
                return;
            }

            var wrapped = value % playerCount;
            _currentTurnIndex = wrapped < 0 ? wrapped + playerCount : wrapped;
        }
    }

    private void CurrentTurnIndexChanged()
    {
        if (!_isInstantiated || _clientManagers.Count == 0)
            return;
        Debug.Log($"Turn changed to player: {_clientManagers[CurrentTurnIndex].PlayerId}");
    }

    #region Lifetime Methods

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            Runner.Despawn(Object);
            return;
        }

        Instance = this;
    }

    public void InstantiateTurnManager(List<ClientManager> clientManagers, TurnStatsSO turnStats, TurnDiffBroadcaster turnDiffBroadcaster)
    {
        // Injects broadcaster and emits initial turn snapshot once setup is complete.
        _clientManagers = clientManagers;
        _turnStats = turnStats;
        _turnDiffBroadcaster = turnDiffBroadcaster;

        for (int i = 0; i < _clientManagers.Count; i++)
        {
            var playerActionData = new PlayerActionData(0, _clientManagers[i].PlayerId);
            PlayerActions.Set(i, playerActionData);
        }

        RandomizeTurnOrder();
        _isInstantiated = true;
        CurrentTurnIndexChanged();

        if (TryGetCurrentPlayerActionData(out var currentPlayingPlayer))
            _turnDiffBroadcaster?.BroadcastInstantiation(GetPlayerActionsSnapshot(), currentPlayingPlayer);
    }

    private void RandomizeTurnOrder()
    {
        CurrentTurnIndex = Random.Range(0, _clientManagers.Count);
    }

    #endregion

    // Methods for checking the condition of actions, amounts, and turns. These are server-side only checks to ensure game rules are followed.
    #region Server Checks

    public bool ValidatePlayerTurn(int playerId)
    {
        var playerIndex = _clientManagers.FindIndex(cm => cm.PlayerId == playerId);
        return playerIndex == CurrentTurnIndex;
    }

    public bool ValidatePlayerIntent(int playerId, MoveIntent intent)
    {
        switch (intent)
        {
            case MoveIntent.MoveSoldier:                // Handle move soldier intent
                return CanPlayerPlacePawn(playerId);
            
            case MoveIntent.PlaceBomb:                  // Handle place bomb intent
                return CanPlayerPlaceBomb(playerId);
            
            case MoveIntent.BuildBase:                  // Handle build base intent
                return CanPlayerBuildBase(playerId);
            
            default:
                Debug.LogWarning("Unknown move intent.");
                return false;
        }
    }

    private bool CanPlayerBuildBase(int playerId)
    {
        for (int i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId == playerId)
            {
                return PlayerActions[i].HasEnoughToBuildBase();
            }
        }

        return false;
    }

    private bool CanPlayerPlacePawn(int playerId)
    {
        for (int i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId == playerId)
            {
                return PlayerActions[i].HasEnoughToPlacePawn(_turnStats.PawnActionPrice);
            }
        }
        return false;
    }

    private bool CanPlayerPlaceBomb(int playerId)
    {
        for (int i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId == playerId)
            {
                return PlayerActions[i].HasEnoughToPlaceBomb(_turnStats.BombActionPrice);
            }
        }
        return false;
    }

    #endregion

    // Methods for changing the actions and turn state. These are server-side only methods that modify the game state and notify clients of changes.
    // **note** do not call unless checked with the server checks above to ensure game rules are followed.
    #region Server Action Methods

    public ActionResult PlayerPlacedPawn(int playerId)
    {
        if (!HasStateAuthority) return ActionResult.NotStateAuthority;

        for (var i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId != playerId) continue;
            
            var pad = PlayerActions[i];
            pad.UpdateCurrentActionAmount(_turnStats.PawnActionPrice);
            PlayerActions.Set(i, pad);
            // Mirrors updated active-player budget immediately after successful pawn placement.
            if (IsCurrentTurnPlayer(pad.PlayerId))
                _turnDiffBroadcaster?.BroadcastCurrentPlayingPlayer(pad);
            
            if (pad.TurnEnded())
            {
                return ActionResult.SuccessAndTurnEnded;
            }
            
            break;
        }

        return ActionResult.Success;
    }

    public ActionResult PlayerBuiltBase(int playerId)
    {
        if (!HasStateAuthority) return ActionResult.NotStateAuthority;

        for (var i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId != playerId) continue;
            
            var pad = PlayerActions[i];
            pad.UpdateMaxActionAmountPerTurn(_turnStats.ActionGainPerBase);
            PlayerActions.Set(i, pad);
            
            // Mirrors base gain adjustments (max/remaining actions) from authoritative state.
            if (IsCurrentTurnPlayer(pad.PlayerId))
                _turnDiffBroadcaster?.BroadcastCurrentPlayingPlayer(pad);
            
            if (pad.TurnEnded())
            {
                return ActionResult.SuccessAndTurnEnded;
            }
            
            break;
        }
        
        return ActionResult.Success;
    }

    public ActionResult PlayerPlacedBomb(int playerId)
    {
        if (!HasStateAuthority) return ActionResult.NotStateAuthority;

        for (int i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId == playerId)
            {
                var pad = PlayerActions[i];
                pad.UpdateCurrentActionAmount(_turnStats.BombActionPrice);
                PlayerActions.Set(i, pad);
                // Mirrors updated active-player budget immediately after successful bomb placement.
                if (IsCurrentTurnPlayer(pad.PlayerId))
                    _turnDiffBroadcaster?.BroadcastCurrentPlayingPlayer(pad);
                
                if (pad.TurnEnded())
                {
                    return ActionResult.SuccessAndTurnEnded;
                }
                
                break;
            }
        }
        
        return ActionResult.Success;
    }

    #endregion
    
    #region Server Turn Methods
    
    public void EndPlayerTurn(int playerId)
    {
        // Advances authoritative turn index and broadcasts the newly active player's turn payload.
        if (!HasStateAuthority) return;
        if (_clientManagers.Count == 0) return;
        if (!IsCurrentTurnPlayer(playerId)) return;

        CurrentTurnIndex++;

        var pad = PlayerActions[CurrentTurnIndex];
        pad.ResetCurrentActionAmount();
        PlayerActions.Set(CurrentTurnIndex, pad);

        _turnDiffBroadcaster?.BroadcastTurnChanged(pad);
        OnTurnChanged?.Invoke();
    }
    
    #endregion

    public bool TryGetPlayerActionData(int playerId, out PlayerActionData playerActionData)
    {
        // Exposes authoritative per-player action data to TurnDiffBroadcaster without leaking array internals.
        for (var i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId != playerId)
                continue;

            playerActionData = PlayerActions[i];
            return true;
        }

        playerActionData = default;
        return false;
    }

    public void SyncClientTurnState(ClientManager clientManager)
    {
        if (!HasStateAuthority || clientManager == null)
            return;

        if (!TryGetCurrentPlayerActionData(out var currentPlayingPlayer))
            return;

        _turnDiffBroadcaster?.SendInstantiationToClient(clientManager, GetPlayerActionsSnapshot(), currentPlayingPlayer);
    }

    private bool TryGetCurrentPlayerActionData(out PlayerActionData currentPlayingPlayer)
    {
        // Returns the authoritative action payload for whichever player currently owns the turn.
        if (_clientManagers.Count == 0)
        {
            currentPlayingPlayer = default;
            return false;
        }

        currentPlayingPlayer = PlayerActions[CurrentTurnIndex];
        return true;
    }

    private IReadOnlyList<PlayerActionData> GetPlayerActionsSnapshot()
    {
        // Builds a stable snapshot payload for initial turn-state broadcast.
        var snapshot = new List<PlayerActionData>(_clientManagers.Count);
        for (var i = 0; i < _clientManagers.Count; i++)
            snapshot.Add(PlayerActions[i]);
        return snapshot;
    }

    private bool IsCurrentTurnPlayer(int playerId)
    {
        // Centralised ownership check used to guard turn-only updates and end-turn requests.
        if (_clientManagers.Count == 0)
            return false;

        return _clientManagers[CurrentTurnIndex].PlayerId == playerId;
    }
}

public enum ActionResult
{
    NotStateAuthority,
    Success,
    SuccessAndTurnEnded
}