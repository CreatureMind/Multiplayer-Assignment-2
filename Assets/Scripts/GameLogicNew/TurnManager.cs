using System.Collections.Generic;
using System.Linq;
using Events;
using Fusion;
using UnityEngine;

public class TurnManager : NetworkBehaviour
{
    ServerGameManager _serverGameManager;
    [Networked] public NetworkBool TraceLogsEnabled { get; private set; }
    private TurnStatsSO _turnStats;
    private List<ClientManager> _clientManagers = new List<ClientManager>();
    private readonly Dictionary<byte, ClientManager> _clientManagersByPlayerId = new Dictionary<byte, ClientManager>();
    private byte _highestPlayerId;
    // Authoritative transport for turn/action diffs via per-client input-authority RPCs.
    private TurnDiffBroadcaster _turnDiffBroadcaster;

    private const int maxPlayers = 8;
    [Networked, Capacity(maxPlayers)] private NetworkArray<PlayerActionData> PlayerActions => default;

    // Guards render-change callbacks from firing before turn dependencies are fully wired.
    private bool _isInstantiated;
    private bool _gameBegun;
    
    private byte _currentTurnKey;
    [Networked, OnChangedRender(nameof(CurrentTurnKeyChanged))]
    private byte CurrentTurnKey
    {
        get => _currentTurnKey;
        set
        {
            if (_clientManagersByPlayerId.Count == 0)
            {
                _currentTurnKey = 0;
                return;
            }

            if (value == _currentTurnKey)
                return;

            if (value == 0)
            {
                _currentTurnKey = 0;
                return;
            }

            _currentTurnKey = IsPlayerTurnSelectable(value) ? value : (byte)0;
            if (_currentTurnKey != 0)
                _gameBegun = true;
        }
    }

    #region Lifetime Methods

    public override void Spawned()
    {
        EventBus.Subscribe<PlayerLeftEvent>(OnPlayerLeft);
    }
    
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        EventBus.Unsubscribe<PlayerLeftEvent>(OnPlayerLeft);
    }

    private void OnPlayerLeft(PlayerLeftEvent playerRefToPullOut)
    {
        RemovePlayerFromTurnManager(playerRefToPullOut.Player.PlayerId);
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
        
        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn manager instantiated with {clientManagers.Count} clients.");

        _isInstantiated = true;
        NextTurnKey();
        _turnDiffBroadcaster?.BroadcastInstantiation(GetPlayerActionsSnapshot(), default);
    }

    private void NextTurnKey()
    {
        AdvanceTurnKeyFrom(CurrentTurnKey);
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

    public bool IsPlayerTurnEnded(int playerId)
    {
        if (!TryReadPlayerActionData(playerId, out _, out var playerActionData))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"IsPlayerTurnEnded fallback=true: player {playerId} not found.");
            return true;
        }

        var turnEnded = playerActionData.TurnEnded();
        GameTraceLogger.Turn(TraceLogsEnabled, $"IsPlayerTurnEnded player={playerId}, current={playerActionData.CurrentActionAmount}, max={playerActionData.MaxActionAmountPerTurn}, result={turnEnded}.");
        return turnEnded;
    }

    private bool CanPlayerBuildBase(int playerId)
    {
        if (TryReadPlayerActionData(playerId, out _, out var playerActionData))
            return playerActionData.HasEnoughToBuildBase(_turnStats.PawnActionPrice);

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

        if (CurrentTurnKey == 0 || !IsPlayerTurnSelectable(CurrentTurnKey))
        {
            var previousTurnKey = CurrentTurnKey;
            NextTurnKey();
            if (CurrentTurnKey != 0 &&
                CurrentTurnKey != previousTurnKey &&
                TryGetCurrentPlayerActionData(out var currentPlayingPlayer))
            {
                _turnDiffBroadcaster?.BroadcastTurnChanged(currentPlayingPlayer);
            }
        }
        
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
    
    #region Interanal Checks
    
     private bool IsPlayerTurnSelectable(int playerId)
    {
        if (!TryGetPlayerIndex(playerId, out var playerIndex))
            return false;

        var playerActionData = PlayerActions[playerIndex];
        return playerActionData.PlayerId == playerId && playerActionData.MaxActionAmountPerTurn > 0;
    }

    private bool TryGetNextTurnPlayerId(byte startExclusive, out byte nextPlayerId)
    {
        nextPlayerId = 0;
        if (_clientManagersByPlayerId.Count == 0 || _highestPlayerId == 0)
            return false;

        var firstCandidate = Mathf.Clamp(startExclusive + 1, 1, _highestPlayerId + 1);
        for (var playerId = firstCandidate; playerId <= _highestPlayerId; playerId++)
        {
            if (IsPlayerTurnSelectable(playerId))
            {
                nextPlayerId = (byte)playerId;
                return true;
            }
        }

        for (var playerId = 1; playerId < firstCandidate; playerId++)
        {
            if (IsPlayerTurnSelectable(playerId))
            {
                nextPlayerId = (byte)playerId;
                return true;
            }
        }

        return false;
    }

    private bool TryGetSoleSelectablePlayerId(out byte playerId)
    {
        playerId = 0;
        var selectableCount = 0;
        foreach (var entry in _clientManagersByPlayerId)
        {
            if (!IsPlayerTurnSelectable(entry.Key))
                continue;

            selectableCount++;
            playerId = entry.Key;
            if (selectableCount > 1)
                return false;
        }

        return selectableCount == 1;
    }
    
    private void CurrentTurnKeyChanged()
    {
        if (!_isInstantiated || _clientManagersByPlayerId.Count == 0)
            return;

        if (CurrentTurnKey == 0)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, "Turn has no active player yet (CurrentTurnKey=0).");
            return;
        }

        if (!_clientManagersByPlayerId.ContainsKey(CurrentTurnKey))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"Turn changed to unresolved player id {CurrentTurnKey}.");
            return;
        }

        GameTraceLogger.Turn(TraceLogsEnabled, $"Turn changed to player {CurrentTurnKey}.");
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
        for (int i = 0; i < PlayerActions.Length; i++)
        {
            if (PlayerActions[i].PlayerId == playerId)
            {
                playerIndex = i;
                return true;
            }
        }
        playerIndex = -1;
        return false;
    }
    
    private bool IsCurrentTurnPlayer(int playerId)
    {
        // Centralised ownership check used to guard turn-only updates and end-turn requests.
        if (_clientManagersByPlayerId.Count == 0)
            return false;

        return CurrentTurnKey == playerId;
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
        if (nextPlayerId == 0)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"EndPlayerTurn finished for player={playerId}: no selectable next player.");
            return;
        }

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
    
    public void ReducePlayerMaxActions(int overriddenPlayerID)
    {
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"ReducePlayerMaxActions failed: no state authority.");
            return;
        }

        if (!TryReadPlayerActionData(overriddenPlayerID, out var playerIndex, out var playerActionData))
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"ReducePlayerMaxActions skipped: unresolved player id P{overriddenPlayerID}.");
            return;
        }

        var previousCurrent = playerActionData.CurrentActionAmount;
        var previousMax = playerActionData.MaxActionAmountPerTurn;
        playerActionData.CurrentActionAmount = Mathf.Max(0, playerActionData.CurrentActionAmount - _turnStats.ActionGainPerBase);
        playerActionData.ReduceMaxActionAmountPerTurn(_turnStats.ActionGainPerBase);
        WritePlayerActionData(playerIndex, playerActionData);

        if (IsCurrentTurnPlayer(overriddenPlayerID))
            _turnDiffBroadcaster?.BroadcastCurrentPlayingPlayer(playerActionData);

        if (playerActionData.MaxActionAmountPerTurn <= 0f)
        {
            RemovePlayerFromTurnManager(overriddenPlayerID);
            GameTraceLogger.Turn(TraceLogsEnabled, $"Player P{overriddenPlayerID} removed from turn manager due to max actions <= 0.");
        }

        GameTraceLogger.Turn(
            TraceLogsEnabled,
            $"Reduced actions for P{overriddenPlayerID}: current {previousCurrent}->{playerActionData.CurrentActionAmount}, max {previousMax}->{playerActionData.MaxActionAmountPerTurn}.");
    }

    private void RemovePlayerFromTurnManager(int overriddenPlayerID)
    {
        _clientManagersByPlayerId.Remove((byte)overriddenPlayerID);
        if (_clientManagersByPlayerId.Count == 1)
        {
            _serverGameManager.EndGame(_clientManagersByPlayerId.Keys.First());
        }
    }
    
    private void AdvanceTurnKeyFrom(byte startExclusive)
    {
        if (_gameBegun && TryGetSoleSelectablePlayerId(out var soleSelectablePlayerId))
        {
            CurrentTurnKey = 0;
            GameTraceLogger.Turn(TraceLogsEnabled, $"Only one selectable player remains after game start: P{soleSelectablePlayerId}. Ending game.");
            _serverGameManager?.EndGame(soleSelectablePlayerId);
            return;
        }

        if (TryGetNextTurnPlayerId(startExclusive, out var nextPlayerId))
        {
            CurrentTurnKey = nextPlayerId;
            return;
        }

        CurrentTurnKey = 0;
        GameTraceLogger.Turn(TraceLogsEnabled, $"No selectable player found after key {startExclusive}. CurrentTurnKey set to 0.");
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

    private void WritePlayerActionData(int playerIndex, in PlayerActionData playerActionData)
    {
        PlayerActions.Set(playerIndex, playerActionData);
    }

    public List<byte> GetKeyList()
    {
        var keyList = new List<byte>(_clientManagers.Count);
        for (var i = 0; i < _clientManagers.Count; i++)
        {
            keyList.Add(_clientManagers[i].PlayerId);
        }
        return keyList;
    }

    public PlayerActionData GetCurrentPlayingPlayer()
    {
        if (!HasStateAuthority)
        {
            GameTraceLogger.Turn(TraceLogsEnabled, $"GetCurrentPlayingPlayer failed: no state authority.");
            return default;
        }
        
        if (TryGetCurrentPlayerActionData(out var currentPlayingPlayer))
        {
            return currentPlayingPlayer;
        }
        return default;
    }
    
    #endregion
    
}

public enum ActionResult
{
    NotStateAuthority,
    Success,
    SuccessAndTurnEnded
}