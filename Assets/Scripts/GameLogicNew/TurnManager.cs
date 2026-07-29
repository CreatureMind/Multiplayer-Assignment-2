using System;
using System.Collections.Generic;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;
    ServerGameManager _serverGameManager;
    [Networked] public NetworkBool TraceLogsEnabled { get; private set; }
    private TurnStatsSO _turnStats;
    private List<ClientManager> _clientManagers = new List<ClientManager>();
    private readonly Dictionary<int, ClientManager> _clientManagersByPlayerId = new Dictionary<int, ClientManager>();
    private int _highestPlayerId;
    // Authoritative transport for turn/action diffs via per-client input-authority RPCs.
    private TurnDiffBroadcaster _turnDiffBroadcaster;

    private const int maxPlayers = 8;
    [Networked, Capacity(maxPlayers)] private NetworkArray<PlayerActionData> PlayerActions => default;

    private int _currentTurnKey;

    // Guards render-change callbacks from firing before turn dependencies are fully wired.
    private bool _isInstantiated;

    [Networked, OnChangedRender(nameof(CurrentTurnKeyChanged))]
    private int CurrentTurnKey
    {
        get => _currentTurnKey;
        set
        {
            var initialTurnKey = _currentTurnKey;
            // Stores turn owner by player id and resolves to the next valid id (with wrap) when needed.
            if (_clientManagersByPlayerId.Count == 0)
            {
                _currentTurnKey = 0;
                return;
            }

            if (value == initialTurnKey)
                return;

            if (_clientManagersByPlayerId.ContainsKey(value))
            {
                DoesPlayerIdHaveSufficientActionToPlay(value);

                _currentTurnKey = value;
                return;
            }

            var candidate = value < 0 ? 0 : value;

            for (var playerId = candidate; playerId <= _highestPlayerId; playerId++)
            {
                if (_clientManagersByPlayerId.ContainsKey(playerId))
                {
                    if (!DoesPlayerIdHaveSufficientActionToPlay(playerId))
                    {
                        continue;
                    }
                    _currentTurnKey = playerId;
                    return;
                }
            }

            for (var playerId = 0; playerId <= _highestPlayerId; playerId++)
            {
                if (_clientManagersByPlayerId.ContainsKey(playerId))
                {
                    if (DoesPlayerIdHaveSufficientActionToPlay(playerId))
                    {
                        _currentTurnKey = playerId;
                        break;
                    }
                }
            }

            if (initialTurnKey == _currentTurnKey && initialTurnKey != 0)
            {
                GameTraceLogger.Turn(TraceLogsEnabled, $"The only valid player key that can play is {value}.");
                _serverGameManager.EndGame((byte)initialTurnKey);
            }

            _currentTurnKey = 0;
            GameTraceLogger.Turn(TraceLogsEnabled, $"CurrentTurnKey fallback applied: no valid player id found up to highest id {_highestPlayerId}.");
            GameTraceLogger.Turn(TraceLogsEnabled, $"Initial turn key was {initialTurnKey}.");
        }
    }

    private bool DoesPlayerIdHaveSufficientActionToPlay(int playerId)
    {
        if (TryGetPlayerActionData(playerId, out var currentPlayingPlayer))
        {
            if (currentPlayingPlayer.MaxActionAmountPerTurn > 0)
            {
                return true;
            }
        }
        return false;
    }

    private void CurrentTurnKeyChanged()
    {
        if (!_isInstantiated || _clientManagersByPlayerId.Count == 0)
            return;

        if (!_clientManagersByPlayerId.ContainsKey(CurrentTurnKey))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"Turn changed to unresolved player id {CurrentTurnKey}.");
            return;
        }

        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn changed to player {CurrentTurnKey}.");
    }

    #region Lifetime Methods

    public override void Spawned()
    {
        if (Instance && Instance != this)
        {
            Runner.Despawn(Object);
            return;
        }

        Instance = this;
    }

    public void InstantiateTurnManager(ServerGameManager gm, List<ClientManager> clientManagers, TurnStatsSO turnStats, TurnDiffBroadcaster turnDiffBroadcaster)
    {
        // Injects broadcaster and emits initial turn snapshot once setup is complete.
        _serverGameManager = gm;
        _clientManagers = clientManagers;
        _clientManagersByPlayerId.Clear();
        _highestPlayerId = 0;
        _turnStats = turnStats;
        _turnDiffBroadcaster = turnDiffBroadcaster;

        for (var i = 0; i < _clientManagers.Count; i++)
        {
            var playerId = _clientManagers[i].PlayerId;
            var playerActionData = new PlayerActionData(0, playerId);
            PlayerActions.Set(i, playerActionData);
            _clientManagersByPlayerId[playerId] = _clientManagers[i];
            if (i == 0 || playerId > _highestPlayerId)
                _highestPlayerId = playerId;
        }
        
        Debug.Log($"Instantiated turn manager with {clientManagers.Count} players.");
        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn manager instantiated with {clientManagers.Count} clients.");

        _isInstantiated = true;
        NextTurnKey();

        if (TryGetCurrentPlayerActionData(out var currentPlayingPlayer))
            _turnDiffBroadcaster?.BroadcastInstantiation(GetPlayerActionsSnapshot(), currentPlayingPlayer);
    }

    private void NextTurnKey()
    {
        CurrentTurnKey++;
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
        if (!TryGetPlayerIndex(playerId, out var playerIndex))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerTurn failed: player {playerId} not found.");
            return false;
        }

        var isValid = playerId == CurrentTurnKey;
        GameTraceLogger.Turn(TraceLogsEnabled, $"ValidatePlayerTurn player={playerId}, playerIndex={playerIndex}, currentTurnPlayerId={CurrentTurnKey}, result={isValid}.");
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
        if (TryReadPlayerActionData(playerId, out _, out var playerActionData))
            return playerActionData.HasEnoughToBuildBase();

        GameTraceLogger.Turn(TraceLogsEnabled, $"CanPlayerBuildBase failed: player {playerId} not found.");
        return false;
    }

    private bool CanPlayerPlacePawn(int playerId)
    {
        if (TryReadPlayerActionData(playerId, out _, out var playerActionData))
            return playerActionData.HasEnoughToPlacePawn(_turnStats.PawnActionPrice);

        GameTraceLogger.Turn(TraceLogsEnabled, $"CanPlayerPlacePawn failed: player {playerId} not found.");
        return false;
    }

    private bool CanPlayerPlaceBomb(int playerId)
    {
        if (TryReadPlayerActionData(playerId, out _, out var playerActionData))
            return playerActionData.HasEnoughToPlaceBomb(_turnStats.BombActionPrice);

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

        if (!TryReadPlayerActionData(playerId, out var playerIndex, out var playerActionData))
            return ActionResult.Success;

        var previousBudget = playerActionData.CurrentActionAmount;
        playerActionData.UpdateCurrentActionAmount(_turnStats.PawnActionPrice);
        WritePlayerActionData(playerIndex, playerActionData);
        GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerPlacedPawn applied for player={playerId}: budget {previousBudget}->{playerActionData.CurrentActionAmount}.");
        // Mirrors updated active-player budget immediately after successful pawn placement.
        if (IsCurrentTurnPlayer(playerActionData.PlayerId))
            _turnDiffBroadcaster?.BroadcastCurrentPlayingPlayer(playerActionData);

        if (playerActionData.TurnEnded())
            return ActionResult.SuccessAndTurnEnded;

        return ActionResult.Success;
    }

    public ActionResult PlayerBuiltBase(int playerId)
    {
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerBuiltBase rejected for player={playerId}: no state authority.");
            return ActionResult.NotStateAuthority;
        }

        if (!TryReadPlayerActionData(playerId, out var playerIndex, out var playerActionData))
            return ActionResult.Success;

        var previousMax = playerActionData.MaxActionAmountPerTurn;
        playerActionData.UpdateMaxActionAmountPerTurn(_turnStats.ActionGainPerBase);
        WritePlayerActionData(playerIndex, playerActionData);
        GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerBuiltBase applied for player={playerId}: max budget {previousMax}->{playerActionData.MaxActionAmountPerTurn}.");
        
        // Mirrors base gain adjustments (max/remaining actions) from authoritative state.
        if (IsCurrentTurnPlayer(playerActionData.PlayerId))
            _turnDiffBroadcaster?.BroadcastCurrentPlayingPlayer(playerActionData);
        
        if (playerActionData.TurnEnded())
            return ActionResult.SuccessAndTurnEnded;
        
        return ActionResult.Success;
    }

    public ActionResult PlayerPlacedBomb(int playerId)
    {
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerPlacedBomb rejected for player={playerId}: no state authority.");
            return ActionResult.NotStateAuthority;
        }

        if (!TryReadPlayerActionData(playerId, out var playerIndex, out var playerActionData))
            return ActionResult.Success;

        var previousBudget = playerActionData.CurrentActionAmount;
        playerActionData.UpdateCurrentActionAmount(_turnStats.BombActionPrice);
        WritePlayerActionData(playerIndex, playerActionData);
        GameTraceLogger.Turn(TraceLogsEnabled, $"PlayerPlacedBomb applied for player={playerId}: budget {previousBudget}->{playerActionData.CurrentActionAmount}.");
        // Mirrors updated active-player budget immediately after successful bomb placement.
        if (IsCurrentTurnPlayer(playerActionData.PlayerId))
            _turnDiffBroadcaster?.BroadcastCurrentPlayingPlayer(playerActionData);
        
        if (playerActionData.TurnEnded())
            return ActionResult.SuccessAndTurnEnded;
        
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

        if (_clientManagersByPlayerId.Count == 0)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn ignored for player={playerId}: no clients.");
            return;
        }

        if (!IsCurrentTurnPlayer(playerId))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn ignored for player={playerId}: not current turn owner.");
            return;
        }

        GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn processing for player={playerId}, currentTurnIndex={CurrentTurnKey}.");

        NextTurnKey();

        var nextPlayerId = CurrentTurnKey;
        if (!TryReadPlayerActionData(nextPlayerId, out var nextPlayerIndex, out var nextPlayerActionData))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn failed: could not read action data for next player={nextPlayerId}.");
            return;
        }

        var previousBudget = nextPlayerActionData.CurrentActionAmount;
        nextPlayerActionData.ResetCurrentActionAmount();
        WritePlayerActionData(nextPlayerIndex, nextPlayerActionData);
        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn advanced to player={nextPlayerActionData.PlayerId}, index={CurrentTurnKey}, budget reset {previousBudget}->{nextPlayerActionData.CurrentActionAmount}.");

        _turnDiffBroadcaster?.BroadcastTurnChanged(nextPlayerActionData);
    }
    
    #endregion

    public bool TryGetPlayerActionData(int playerId, out PlayerActionData playerActionData)
    {
        // Exposes authoritative per-player action data to TurnDiffBroadcaster without leaking array internals.
        return TryReadPlayerActionData(playerId, out _, out playerActionData);
    }

    public void SyncClientTurnState(ClientManager clientManager)
    {
        if (!HasStateAuthority || !clientManager)
            return;

        if (!TryGetCurrentPlayerActionData(out var currentPlayingPlayer))
            return;

        GameTraceLogger.Turn(TraceLogsEnabled, $"SyncClientTurnState -> client P{clientManager.PlayerId}, current player P{currentPlayingPlayer.PlayerId}.");
        _turnDiffBroadcaster?.SendInstantiationToClient(clientManager, GetPlayerActionsSnapshot(), currentPlayingPlayer);
    }

    private bool TryGetCurrentPlayerActionData(out PlayerActionData currentPlayingPlayer)
    {
        // Returns the authoritative action payload for whichever player currently owns the turn.
        if (_clientManagersByPlayerId.Count == 0)
        {
            currentPlayingPlayer = default;
            return false;
        }

        return TryReadPlayerActionData(CurrentTurnKey, out _, out currentPlayingPlayer);
    }

    private IReadOnlyList<PlayerActionData> GetPlayerActionsSnapshot()
    {
        // Builds a stable snapshot payload for initial turn-state broadcast.
        var snapshot = new List<PlayerActionData>(_clientManagers.Count);
        for (var i = 0; i < _clientManagers.Count; i++)
        {
            if (TryReadPlayerActionData(_clientManagers[i].PlayerId, out _, out var playerActionData))
                snapshot.Add(playerActionData);
        }
        return snapshot;
    }

    private bool IsCurrentTurnPlayer(int playerId)
    {
        // Centralised ownership check used to guard turn-only updates and end-turn requests.
        if (_clientManagersByPlayerId.Count == 0)
            return false;

        return CurrentTurnKey == playerId;
    }


    private bool TryReadPlayerActionData(int playerId, out int playerIndex, out PlayerActionData playerActionData)
    {
        if (!TryGetPlayerIndex(playerId, out playerIndex))
        {
            playerActionData = default;
            return false;
        }

        playerActionData = PlayerActions[playerIndex];
        return playerActionData.PlayerId == playerId;
    }

    private bool TryGetPlayerIndex(int playerId, out int playerIndex)
    {
        for (int i = 0; i < _clientManagers.Count; i++)
        {
            if (_clientManagers[i].PlayerId == playerId)
            {
                playerIndex = i;
                return true;
            }
        }
        playerIndex = -1;
        return false;
    }

    private void WritePlayerActionData(int playerIndex, in PlayerActionData playerActionData)
    {
        PlayerActions.Set(playerIndex, playerActionData);
    }
}

public enum ActionResult
{
    NotStateAuthority,
    Success,
    SuccessAndTurnEnded
}