// Assets/Scripts/Gameplay/WorldGen/LockedDoor.cs
using UnityEngine;

namespace Geneforge.Gameplay.WorldGen
{
    public class LockedDoor : MonoBehaviour
    {
        public Doorway8 doorway;
        public bool openWhenKey = true;

        void Start()
        {
            if (!doorway) doorway = GetComponentInChildren<Doorway8>(true);
            if (doorway) doorway.SetOpen(false);
            if (KeyManager.I) KeyManager.I.OnKeyPicked += HandleKeyPicked;
        }

        void OnDestroy()
        {
            if (KeyManager.I) KeyManager.I.OnKeyPicked -= HandleKeyPicked;
        }

        void HandleKeyPicked()
        {
            if (doorway && openWhenKey) doorway.SetOpen(true);
            // Hook VFX/SFX here if you want.
        }
    }
}
