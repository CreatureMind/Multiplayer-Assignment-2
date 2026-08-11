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
        if (diffs == null || diffs.Count == 0)
            return;

        bool explosion = false, placed = false, iAte = false, iWasEaten = false, iBuiltBase = false, iCapturedBase = false, myBaseCaptured = false;

        foreach (var diff in diffs)
        {
            if (diff.Generation > 0)
                explosion = true;
            
            if (!_board.TryGet(diff.Cell, out var oldView))
                continue;
            
            if (oldView.VisualType == TileType.None)
                continue;

            var newView = diff.ToView();
            var old = oldView.VisualType;
            var nue = newView.VisualType;
            
            if (old == TileType.Empty && nue == TileType.Soldier && IsMine(newView.OwnerId))
                placed = true;
            
            if (old == TileType.Soldier && nue == TileType.Soldier)
            {
                if (IsEnemy(oldView.OwnerId) && IsMine(newView.OwnerId))
                    iAte = true;
                else if (IsMine(oldView.OwnerId) && IsEnemy(newView.OwnerId))
                    iWasEaten = true;
            }
            
            if (old is TileType.Soldier or TileType.Bomb
                && nue == TileType.Base && IsMine(newView.OwnerId) && IsMine(newView.OwnerId))
                iBuiltBase = true;

            if (old == TileType.Base && nue == TileType.Base)
            {
                if (IsEnemy(oldView.OwnerId) && IsMine(newView.OwnerId))
                    iCapturedBase = true;
                else if (IsMine(oldView.OwnerId) && IsEnemy(newView.OwnerId))
                    myBaseCaptured = true;
            }
        }
        
        var major = explosion || iBuiltBase || iCapturedBase || myBaseCaptured;
        var wantsSpawn = placed || iAte;
        
        if (explosion)
            Raise(SoundEffectEnum.PAWN_EXPLOSION);
        if (iCapturedBase)
            Raise(SoundEffectEnum.ENEMY_BASE_CONQUERED);
        if (myBaseCaptured)
            Raise(SoundEffectEnum.YOUR_BASE_CONQUERED);
        if (iBuiltBase)
            Raise(SoundEffectEnum.BASE_PLACEMENT);
        if (iWasEaten)
            Raise(SoundEffectEnum.PAWN_EATEN);
        if (wantsSpawn && !(_suppressSpawnOnMajorEvent && major))
            Raise(SoundEffectEnum.PAWN_SPAWN);
    }

    private static void Raise(SoundEffectEnum sound)
    {
        Debug.LogError($"Played sound with type {sound.ToString()}");
        EventBus.Raise(new Events.PlaySoundEvent { SoundName = sound });
    }
}