using System;
using TMPro;
using UnityEngine;

namespace PharmaLabVR.UI
{
    public sealed class HoldPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text reasonLabel;
        public bool IsHeld { get; private set; }
        public bool CanContinue { get; private set; }
        public event Action ContinueRequested;

        public void Present(bool held, bool neutral, bool baselineFresh, string reason)
        {
            IsHeld = held;
            CanContinue = held && neutral && baselineFresh;
            gameObject.SetActive(held);
            if (reasonLabel != null) reasonLabel.text = reason ?? string.Empty;
        }

        public void RequestContinue()
        {
            if (CanContinue) ContinueRequested?.Invoke();
        }
    }
}
