namespace dx_dispatch_challenge
{
    public class Models
    {
        public sealed class InputRoot
        {
            public List<TechnicianDto> Technicians { get; set; } = [];
            public List<StudyDto> Studies { get; set; } = [];
        }

        public sealed class TechnicianDto
        {
            public string TechId { get; set; } = "";
            public List<AvailabilityBlockDto> AvailabilityBlocks { get; set; } = [];
        }

        public sealed class AvailabilityBlockDto
        {
            public int StartMinute { get; set; }
            public int EndMinute { get; set; }
        }

        public sealed class StudyDto
        {
            public string StudyId { get; set; } = "";
            public string CustomerId { get; set; } = "";
            public int UploadedAtMinute { get; set; }
            public int? SlaMinutes { get; set; }
            public bool PriorityFlag { get; set; }
            public bool RequiresDoubleScoring { get; set; }
        }

        // ---------- Domain ----------
        public sealed record Study(
            string StudyId,
            string CustomerId,
            int UploadedAtMinute,
            int? DeadlineMinute,
            bool PriorityFlag,
            bool RequiresDoubleScoring
        );

        public enum WorkType { Primary, Review }

        public sealed record WorkItem(
            string WorkItemId,
            string StudyId,
            WorkType Type,
            int ReadyAtMinute,
            int? DeadlineMinute,
            bool PriorityFlag,
            int UploadedAtMinute
        );

        public sealed record AvailabilityBlock(int StartMinute, int EndMinute);

        public sealed record Technician(
            string TechId,
            IReadOnlyList<AvailabilityBlock> AvailabilityBlocks
        );

        public sealed record Assignment(
            int TickStartMinute,
            string TechId,
            string StudyId,
            WorkType Type
        );

        public sealed record StudySlaResult(
            string StudyId,
            string CustomerId,
            int DeadlineMinute,
            int? CompletionMinute,
            string Status
        );

        public sealed record CustomerSlaSummary(
            string CustomerId,
            int OnTrack,
            int AtRisk,
            int WillMiss
        );

        public sealed record TickRiskSnapshot(
            int TickStartMinute,
            int OnTrack,
            int AtRisk,
            int WillMiss,
            IReadOnlyList<string> WorstStudyIds // optional: a few urgent misses/risks
        );

        public sealed class DispatchResult
        {
            public List<Assignment> Assignments { get; } = [];
            public Dictionary<string, int> StudyCompletionMinute { get; } = new(); // when study completes (review if required)
            public List<TickRiskSnapshot> TickRiskSnapshots { get; } = [];
            public List<StudySlaResult> FinalStudySla { get; } = [];
            public List<CustomerSlaSummary> CustomerSummary { get; } = [];
        }
    }
}
