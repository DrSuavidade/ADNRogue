using Geneforge.Gameplay.Map;

namespace Geneforge.Gameplay.Progression
{
    public static class RunState
    {
        public static bool HasTimelineOverride { get; set; }
        public static TimelineId CurrentTimeline { get; set; }
    }
}
