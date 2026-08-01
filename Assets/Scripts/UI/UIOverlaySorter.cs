using UnityEngine;

namespace UI
{
    public class UIOverlaySorter
    {
        private const int BASE_OVERLAY_SORT_ORDER = 0;
        private static int _currentSortOrder = BASE_OVERLAY_SORT_ORDER;

        /// <summary>
        /// Returns an incremented sorting order to ensure the newly opened overlay sits on top of all others.
        /// </summary>
        public static int PushOverlay()
        {
            _currentSortOrder++;
            return _currentSortOrder;
        }
        
        public static int PopOverlay()
        {
            _currentSortOrder = Mathf.Max(0, _currentSortOrder - 1);
            return BASE_OVERLAY_SORT_ORDER + _currentSortOrder;
        }

        /// <summary>
        /// Resets the overlay counter back to baseline (e.g., when returning to Main Menu).
        /// </summary>
        public static void Reset()
        {
            _currentSortOrder = BASE_OVERLAY_SORT_ORDER;
        }
    }
}