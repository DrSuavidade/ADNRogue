// Assets/Scripts/Gameplay/WorldGen/KeyManager.cs
using UnityEngine;
using System;

namespace Geneforge.Gameplay.WorldGen
{
    public class KeyManager : MonoBehaviour
    {
        public static KeyManager I { get; private set; }
        public bool HasKey { get; private set; }
        public event Action OnKeyPicked;

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ResetKey() => HasKey = false;

        public void PickupKey()
        {
            if (HasKey) return;
            HasKey = true;
            OnKeyPicked?.Invoke();
        }
    }
}
