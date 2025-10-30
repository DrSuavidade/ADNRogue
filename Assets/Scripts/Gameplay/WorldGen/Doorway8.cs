// Assets/Scripts/Gameplay/WorldGen/Doorway8.cs
using UnityEngine;

namespace Geneforge.Gameplay.WorldGen
{
    public class Doorway8 : MonoBehaviour
    {
        public Dir8 direction;
        [Tooltip("Closed when enabled. Leave null if your door is open-hole only.")]
        public GameObject blocker;
        [Tooltip("Attach/corridor alignment point; defaults to this transform.")]
        public Transform socket;

        public Transform Socket => socket ? socket : transform;

        public void SetOpen(bool open)
        {
            if (blocker) blocker.SetActive(!open);
        }
    }
}
