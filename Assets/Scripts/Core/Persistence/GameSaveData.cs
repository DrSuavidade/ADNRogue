using System;
using System.Collections.Generic;

namespace Geneforge.Core.Persistence
{
    [Serializable]
    public class GameSaveData
    {
        // Run State
        public List<string> collectedItemNames = new List<string>();
        public List<string> unlockedEssenceIDs = new List<string>();
        public int currentTimelineId;
        
        // You can add more here like Seed, Health, etc. for a full run restore
        
        public void Clear()
        {
            collectedItemNames.Clear();
            currentTimelineId = 0;
        }
    }
}
