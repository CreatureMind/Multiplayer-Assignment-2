using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

namespace GameControllers {

  public class InGameMenuController : SimulationBehaviour {

    public Canvas MenuCanvas;
    public TextMeshProUGUI RoomInfoText;

    private IEnumerator Start() {

      MenuCanvas.enabled = false;

      while (NetworkRunner.Instances.Count == 0 || NetworkRunner.Instances[0] == null)
        yield return null;

      NetworkRunner.Instances[0].AddGlobal(this);

      MenuCanvas.enabled = true;

      RoomInfoText.text = $"CurrentConnectionType: {Runner.CurrentConnectionType}\nSession ID: {Runner.SessionInfo.Name}";
    }

    public void ShutdownRunner() {
      Runner.Shutdown();
      SceneManager.LoadScene((byte)SceneDefs.MENU);
    }
  }
}