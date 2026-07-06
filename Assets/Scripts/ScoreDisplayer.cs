using System.Collections.Generic;
using Events;
using Fusion;
using TMPro;
using UnityEngine;

public class ScoreDisplayer : MonoBehaviour
{
    private Dictionary<PlayerRef, PlayerData> _playerDataMap;
    private readonly Dictionary<PlayerRef, TMP_Text> _scoreTexts = new();
    [SerializeField] private TMP_Text _scoreTextPrefab;

    private void OnEnable()
    {
        ScoreManager.OnScoresChanged += UpdateScoreDisplay;
        EventBus.Subscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        EventBus.Subscribe<PlayerLeftEvent>(OnPlayerLeft);

        RefreshPlayerDataMap();
        AddMissingScoreRows();
    }

    private void OnDisable()
    {
        ScoreManager.OnScoresChanged -= UpdateScoreDisplay;
        EventBus.Unsubscribe<PlayerListChangedEvent>(OnPlayerListChanged);
        EventBus.Unsubscribe<PlayerLeftEvent>(OnPlayerLeft);
    }

    private void RefreshPlayerDataMap()
    {
        _playerDataMap = NetworkManager.Instance.GetPlayerDataMap();
    }

    private void OnPlayerListChanged(PlayerListChangedEvent _)
    {
        RefreshPlayerDataMap();
        AddMissingScoreRows();
        UpdateScoreDisplay();
    }

    private void AddMissingScoreRows()
    {
        if (_scoreTextPrefab == null || _playerDataMap == null) return;

        foreach (var kvp in _playerDataMap)
        {
            if (_scoreTexts.ContainsKey(kvp.Key)) continue;

            var playerData = kvp.Value;
            if (playerData == null || !playerData.Object || !playerData.Object.IsValid) continue;

            var textGo = Instantiate(_scoreTextPrefab, transform);
            textGo.text = $"{playerData.DisplayName.Value}: {ScoreManager.DefaultScore}";
            _scoreTexts[kvp.Key] = textGo;
        }
    }

    private void UpdateScoreDisplay()
    {
        if (_playerDataMap == null || ScoreManager.Instance == null) return;

        foreach (var kvp in _playerDataMap)
        {
            if (!_scoreTexts.TryGetValue(kvp.Key, out var text)) continue;

            var playerData = kvp.Value;
            if (playerData == null || !playerData.Object || !playerData.Object.IsValid) continue;

            var score = ScoreManager.Instance.GetScore(kvp.Key);
            text.text = $"{playerData.DisplayName.Value}: {score}";
        }
    }

    private void OnPlayerLeft(PlayerLeftEvent e)
    {
        if (!_scoreTexts.TryGetValue(e.Player, out var text)) return;

        Destroy(text.gameObject);
        _scoreTexts.Remove(e.Player);
    }
}
