using UnityEngine;

namespace Utils
{
    public class FPSLimiter : MonoBehaviour
    {
        [SerializeField, Range(30,120)] private int targetFPS = 30;
    
        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFPS;
            
            DontDestroyOnLoad(gameObject);
        }
    }
}
