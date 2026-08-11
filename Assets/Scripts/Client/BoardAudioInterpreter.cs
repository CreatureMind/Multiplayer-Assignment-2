using System.Collections.Generic;
using UnityEngine;

public sealed class BoardAudioInterpreter
{
    private readonly ClientBoardCache _board;
    private readonly byte _localPlayerId;

    private readonly bool _suppressSpawnOnMajorEvent;

    public BoardAudioInterpreter(ClientBoardCache board, byte localPlayerId, bool suppressSpawnOnMajorEvent = true)
    {
        _board = board;
        _localPlayerId = localPlayerId;
        _suppressSpawnOnMajorEvent = suppressSpawnOnMajorEvent;
    }
    
    private bool IsMine(byte owner) => owner == _localPlayerId;
    private bool IsEnemy(byte owner) => owner != _localPlayerId && owner != TileState.NoOwner;

    public void Interpret(IReadOnlyList<CellDiff> diffs)
    {
        Debug.Log($"[AUDIO] Trying to interpret {diffs.Count} diffs...");
        if (diffs == null || diffs.Count == 0)
            return;

        bool explosion = false, placed = false, iAte = false, iWasEaten = false, iBuiltBase = false, iCapturedBase = false, myBaseCaptured = false;

        foreach (var diff in diffs)
        {
            if (diff.Generation > 0)
            {
                Debug.Log("[AUDIO] Has explosion");
                explosion = true;
            }
            
            if (!_board.TryGet(diff.Cell, out var oldView))
            {
                Debug.Log("[AUDIO] No oldView");
                continue;
            }

            var newView = diff.ToView();
            var old = oldView.VisualType;
            var nue = newView.VisualType;
            
            if (old == TileType.Empty && nue == TileType.Soldier && IsMine(newView.OwnerId))
            {
                Debug.Log("[AUDIO] Soldier placed");
                placed = true;
            }
            
            if (old == TileType.Soldier && nue == TileType.Soldier)
            {
                if (IsEnemy(oldView.OwnerId) && IsMine(newView.OwnerId))
                {
                    Debug.Log("[AUDIO] I ate another's soldier");
                    iAte = true;
                }
                else if (IsMine(oldView.OwnerId) && IsEnemy(newView.OwnerId))
                {
                    Debug.Log("[AUDIO] Another player ate my soldier");
                    iWasEaten = true;
                }
            }
            
            if (old is TileType.Soldier or TileType.Bomb
                && nue == TileType.Base && IsMine(newView.OwnerId) && IsMine(newView.OwnerId))
            {
                Debug.Log("[AUDIO] I built a base");
                iBuiltBase = true;
            }

            if (old == TileType.Base && nue == TileType.Base)
            {
                if (IsEnemy(oldView.OwnerId) && IsMine(newView.OwnerId))
                {
                    Debug.Log("[AUDIO] I captured a base");
                    iCapturedBase = true;
                }
                else if (IsMine(oldView.OwnerId) && IsEnemy(newView.OwnerId))
                {
                    Debug.Log("[AUDIO] My base was captured");
                    myBaseCaptured = true;
                }
            }
        }
        
        var major = explosion || iBuiltBase || iCapturedBase || myBaseCaptured;
        var wantsSpawn = placed || iAte;
        
        if (explosion)
        {
            Debug.Log("[AUDIO] Raised explosion");
            Raise(SoundEffectEnum.PAWN_EXPLOSION);
        }
        if (iCapturedBase)
        {
            Debug.Log("[AUDIO] Raised base conquer");
            Raise(SoundEffectEnum.ENEMY_BASE_CONQUERED);
        }
        if (myBaseCaptured)
        {
            Debug.Log("[AUDIO] Raised base lost");
            Raise(SoundEffectEnum.YOUR_BASE_CONQUERED);
        }
        if (iBuiltBase)
        {
            Debug.Log("[AUDIO] Raised base built");
            Raise(SoundEffectEnum.BASE_PLACEMENT);
        }
        if (iWasEaten)
        {
            Debug.Log("[AUDIO] Raised soldier eaten");
            Raise(SoundEffectEnum.PAWN_EATEN);
        }
        if (wantsSpawn && !(_suppressSpawnOnMajorEvent && major))
        {
            Debug.Log("[AUDIO] Raised soldier spawned");
            Raise(SoundEffectEnum.PAWN_SPAWN);
        }
    }

    private static void Raise(SoundEffectEnum sound)
    {
        Debug.LogError($"Played sound with type {sound.ToString()}");
        EventBus.Raise(new Events.PlaySoundEvent { SoundName = sound });
    }
}