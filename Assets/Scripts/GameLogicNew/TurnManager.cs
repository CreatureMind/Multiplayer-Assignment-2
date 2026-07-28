using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;
    [Networked] public NetworkBool TraceLogsEnabled { get; private set; }
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
        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn changed to player {_clientManagers[CurrentTurnIndex].PlayerId} (index={CurrentTurnIndex}).");
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
        
        Debug.Log($"Instantiated turn manager with {clientManagers.Count} players.");
        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn manager instantiated with {clientManagers.Count} clients.");

        RandomizeTurnOrder();
        _isInstantiated = true;
        CurrentTurnIndexChanged();

        if (TryGetCurrentPlayerActionData(out var currentPlayingPlayer))
            _turnDiffBroadcaster?.BroadcastInstantiation(GetPlayerActionsSnapshot(), currentPlayingPlayer);
    }

    private void RandomizeTurnOrder()
    {
        CurrentTurnIndex = Random.Range(0, _clientManagers.Count);
        GameTraceLogger.Turn(TraceLogsEnabled, $"Randomized starting turn index to {CurrentTurnIndex}.");
    }

    public void SetTraceLoggingEnabled(NetworkBool enabled)
    {
        if (!HasStateAuthority)
            return;

        TraceLogsEnabled = enabled;
    }

    #endregion

    // Methods for checking the condition of actions, amounts, and turns. These are server-side only checks to ensure game rules are followed.
    #region Server Checks

    public bool ValidatePlayerTurn(int playerId)
    {
        var playerIndex = _clientManagers.FindIndex(cm => cm.PlayerId == playerId);
        var isValid = playerIndex == CurrentTurnIndex;
        GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerTurn player={playerId}, playerIndex={playerIndex}, currentTurnIndex={CurrentTurnIndex}, result={isValid}.");
        return isValid;
    }

    public bool ValidatePlayerIntent(int playerId, MoveIntent intent)
    {
        GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerIntent player={playerId}, intent={intent}.");
        switch (intent)
        {
            case MoveIntent.MoveSoldier:                // Handle move soldier intent
            {
                var result = CanPlayerPlacePawn(playerId);
                GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerIntent result for MoveSoldier and player={playerId}: {result}.");
                return result;
            }
             
            case MoveIntent.PlaceBomb:                  // Handle place bomb intent
            {
                var result = CanPlayerPlaceBomb(playerId);
                GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerIntent result for PlaceBomb and player={playerId}: {result}.");
                return result;
            }
             
            case MoveIntent.BuildBase:                  // Handle build base intent
            {
                var result = CanPlayerBuildBase(playerId);
                GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerIntent result for BuildBase and player={playerId}: {result}.");
                return result;
            }
             
            default:
                Debug.LogWarning("Unknown move intent.");
                GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerIntent failed: unknown intent {intent} for player={playerId}.");
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

        GameTraceLogger.Turn(TraceLogsEnabled, $"CanPlayerBuildBase failed: player {playerId} not found.");
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

        GameTraceLogger.Turn(TraceLogsEnabled, $"CanPlayerPlacePawn failed: player {playerId} not found.");
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

        GameTraceLogger.Turn(TraceLogsEnabled, $"CanPlayerPlaceBomb failed: player {playerId} not found.");
        return false;
    }

    #endregion

    // Methods for changing the actions and turn state. These are server-side only methods that modify the game state and notify clients of changes.
    // **note** do not call unless checked with the server checks above to ensure game rules are followed.
    #region Server Action Methods

    public ActionResult PlayerPlacedPawn(int playerId)
    {
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerPlacedPawn rejected for player={playerId}: no state authority.");
            return ActionResult.NotStateAuthority;
        }

        for (var i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId != playerId) continue;
            
            var pad = PlayerActions[i];
            var previousBudget = pad.CurrentActionAmount;
            pad.UpdateCurrentActionAmount(_turnStats.PawnActionPrice);
            PlayerActions.Set(i, pad);
            GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerPlacedPawn applied for player={playerId}: budget {previousBudget}->{pad.CurrentActionAmount}.");
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
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerBuiltBase rejected for player={playerId}: no state authority.");
            return ActionResult.NotStateAuthority;
        }

        for (var i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId != playerId) continue;
            
            var pad = PlayerActions[i];
            var previousMax = pad.MaxActionAmountPerTurn;
            pad.UpdateMaxActionAmountPerTurn(_turnStats.ActionGainPerBase);
            PlayerActions.Set(i, pad);
            GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerBuiltBase applied for player={playerId}: max budget {previousMax}->{pad.MaxActionAmountPerTurn}.");
            
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
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerPlacedBomb rejected for player={playerId}: no state authority.");
            return ActionResult.NotStateAuthority;
        }

        for (int i = 0; i < _clientManagers.Count; i++)
        {
            if (PlayerActions[i].PlayerId == playerId)
            {
                var pad = PlayerActions[i];
                var previousBudget = pad.CurrentActionAmount;
                pad.UpdateCurrentActionAmount(_turnStats.BombActionPrice);
                PlayerActions.Set(i, pad);
                GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerPlacedBomb applied for player={playerId}: budget {previousBudget}->{pad.CurrentActionAmount}.");
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
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn ignored for player={playerId}: no state authority.");
            return;
        }

        if (_clientManagers.Count == 0)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn ignored for player={playerId}: no clients.");
            return;
        }

        if (!IsCurrentTurnPlayer(playerId))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn ignored for player={playerId}: not current turn owner.");
            return;
        }

        GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn processing for player={playerId}, currentTurnIndex={CurrentTurnIndex}.");

        CurrentTurnIndex++;

        var pad = PlayerActions[CurrentTurnIndex];
        var previousBudget = pad.CurrentActionAmount;
        pad.ResetCurrentActionAmount();
        PlayerActions.Set(CurrentTurnIndex, pad);
        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn advanced to player={pad.PlayerId}, index={CurrentTurnIndex}, budget reset {previousBudget}->{pad.CurrentActionAmount}.");

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

        GameTraceLogger.Turn(TraceLogsEnabled, $"SyncClientTurnState -> client P{clientManager.PlayerId}, current player P{currentPlayingPlayer.PlayerId}.");
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