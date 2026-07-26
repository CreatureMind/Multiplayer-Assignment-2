using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;
    private TurnStatsSO _turnStats;
    private List<ClientManager> _clientManagers = new List<ClientManager>();

    [Networked, Capacity(8)]
    private NetworkArray<PlayerActionData> _playerActions { get; set; } = new NetworkArray<PlayerActionData>();

    private int _currentTurnIndex;

    private bool isInstantiated = false;

    public static Action<PlayerActionData> OnPlayerActionChanged;
    public static Action OnTurnChanged;

    [Networked, OnChangedRender(nameof(CurrentTurnIndexChanged))]
    private int CurrentTurnIndex
    {
        get => _currentTurnIndex;
        set => _currentTurnIndex = value % _playerActions.Length;
    }

    private void CurrentTurnIndexChanged()
    {
        //all classes get notified in the change of the turn
        // this should prompt the client to update the UI and allow the player to make a move
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

    public void InstantiateTurnManager(List<ClientManager> clientManagers, TurnStatsSO turnStats)
    {
        _clientManagers = clientManagers;
        _turnStats = turnStats;

        for (int i = 0; i < _clientManagers.Count; i++)
        {
            var playerActionData = new PlayerActionData(_turnStats.InitialActionAmount, _clientManagers[i].PlayerId);
            _playerActions.Set(i, playerActionData);
        }

        RandomizeTurnOrder();
        CurrentTurnIndexChanged();
        isInstantiated = true;
    }

    private void RandomizeTurnOrder()
    {
        CurrentTurnIndex = Random.Range(0, _clientManagers.Count);
    }

    #endregion

    // Methods for checking the condition of actions, amounts, and turns. These are server-side only checks to ensure game rules are followed.
    #region Server Checks

    public bool ValidatePlayerTurn(int playerPlayerId)
    {
        var playerIndex = _clientManagers.FindIndex(cm => cm.PlayerId == playerPlayerId);
        return playerIndex == CurrentTurnIndex;
    }

    public bool CanPlayerBuildBase(int playerId)
    {
        for (int i = 0; i < _playerActions.Length; i++)
        {
            if (_playerActions[i].PlayerId == playerId)
            {
                return _playerActions[i].HasEnoughToBuildBase();
            }
        }

        return false;
    }

    public bool CanPlayerPlacePawn(int playerId)
    {
        for (int i = 0; i < _playerActions.Length; i++)
        {
            if (_playerActions[i].PlayerId == playerId)
            {
                return _playerActions[i].HasEnoughToPlacePawn(_turnStats.PawnActionPrice);
            }
        }
        return false;
    }

    public bool CanPlayerPlaceBomb(int playerId)
    {
        for (int i = 0; i < _playerActions.Length; i++)
        {
            if (_playerActions[i].PlayerId == playerId)
            {
                return _playerActions[i].HasEnoughToPlaceBomb(_turnStats.BombActionPrice);
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

        for (var i = 0; i < _playerActions.Length; i++)
        {
            if (_playerActions[i].PlayerId != playerId) continue;
            
            var pad = _playerActions[i];
            pad.UpdateCurrentActionAmount(_turnStats.PawnActionPrice);
            _playerActions.Set(i, pad);
            
            if (pad.TurnEnded())
            {
                return ActionResult.SuccessAndTurnEnded;
            }
            
            break;
        }

        NotifyPlayersOfActionChangeRPC(playerId);
        return ActionResult.Success;
    }

    public ActionResult PlayerBuiltBase(int playerId)
    {
        if (!HasStateAuthority) return ActionResult.NotStateAuthority;

        for (var i = 0; i < _playerActions.Length; i++)
        {
            if (_playerActions[i].PlayerId != playerId) continue;
            
            var pad = _playerActions[i];
            pad.UpdateMaxActionAmountPerTurn(_turnStats.ActionGainPerBase);
            _playerActions.Set(i, pad);
            
            if (pad.TurnEnded())
            {
                return ActionResult.SuccessAndTurnEnded;
            }
            
            break;
        }
        
        NotifyPlayersOfActionChangeRPC(playerId);
        return ActionResult.Success;
    }

    public ActionResult PlayerPlacedBomb(int playerId)
    {
        if (!HasStateAuthority) return ActionResult.NotStateAuthority;

        for (int i = 0; i < _playerActions.Length; i++)
        {
            if (_playerActions[i].PlayerId == playerId)
            {
                var pad = _playerActions[i];
                pad.UpdateCurrentActionAmount(_turnStats.BombActionPrice);
                _playerActions.Set(i, pad);
                
                if (pad.TurnEnded())
                {
                    return ActionResult.SuccessAndTurnEnded;
                }
                
                break;
            }
        }
        
        NotifyPlayersOfActionChangeRPC(playerId);
        return ActionResult.Success;
    }

    #endregion
    
    #region Server Turn Methods
    
    public void EndPlayerTurn(int playerId)
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < _playerActions.Length; i++)
        {
            if (_playerActions[i].PlayerId == playerId)
            {
                var pad = _playerActions[i];
                pad.ResetCurrentActionAmount();
                _playerActions.Set(i, pad);
                break;
            }
        }

        CurrentTurnIndex++;
        
        NotifyPlayersOfTurnEnd(_clientManagers[CurrentTurnIndex].PlayerId);
        OnTurnChanged?.Invoke();
    }
    
    #endregion

    #region RPC Methods Server --> All

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void NotifyPlayersOfActionChangeRPC(int playerId)
    {
        foreach (var pad in _playerActions.Where(pad => pad.PlayerId == playerId))
        {
            OnPlayerActionChanged?.Invoke(pad);
            break;
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void NotifyPlayersOfTurnEnd(int playerIdToPlay)
    {
        
    }

    #endregion
}

public enum ActionResult
{
    NotStateAuthority,
    Success,
    SuccessAndTurnEnded
}