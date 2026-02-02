using UnityEngine;
using System.Collections.Generic;
using Geneforge.Gameplay.Items;
using Geneforge.Gameplay.Abilities;
using System;
using System.Linq;

namespace Geneforge.UI
{
    [CreateAssetMenu(menuName = "Geneforge/UI/RewardStatConfig", fileName = "RewardStatConfig")]
    public class RewardStatConfig : ScriptableObject
    {
        [Header("Icons")]
        [SerializeField] private Sprite upgradeArrow;
        [SerializeField] private Sprite downgradeArrow;

        [SerializeField] private List<RunStatIconMapping> runStatIcons;
        [SerializeField] private List<WeaponStatIconMapping> weaponStatIcons;

        [Serializable]
        public struct RunStatIconMapping
        {
            public StatType stat;
            public Sprite icon;
        }

        [Serializable]
        public struct WeaponStatIconMapping
        {
            public WeaponStatId stat;
            public Sprite icon;
        }

        public Sprite UpgradeArrow => upgradeArrow;
        public Sprite DowngradeArrow => downgradeArrow;

        public Sprite GetRunStatIcon(StatType stat)
        {
            var match = runStatIcons.FirstOrDefault(x => x.stat == stat);
            return match.icon;
        }

        public Sprite GetWeaponStatIcon(WeaponStatId stat)
        {
            var match = weaponStatIcons.FirstOrDefault(x => x.stat == stat);
            return match.icon;
        }
    }
}
