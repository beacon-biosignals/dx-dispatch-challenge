using System.Text.Json;
using static dx_dispatch_challenge.Models;

namespace dx_dispatch_challenge;

internal class Program
{
    private const int TickMinutes = 30;
    private const int HorizonMinutes = 1440;

    // Choose a deterministic "AtRisk" threshold (minutes)
    private const int AtRiskThresholdMinutes = 60;

    public static void Main(string[] args)
    {
        var json = args[0];
        Run(json);
    }


    public static DispatchResult Run(string json)
    {
        var input = JsonSerializer.Deserialize<InputRoot>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("Invalid JSON.");

        var technicians = input.Technicians
            .Select(t => new Technician(
                t.TechId,
                t.AvailabilityBlocks.Select(b => new AvailabilityBlock(b.StartMinute, b.EndMinute)).ToList()
            ))
            .OrderBy(t => t.TechId, StringComparer.Ordinal)
            .ToList();

        var studies = input.Studies
            .Select(s =>
            {
                int? deadline = s.SlaMinutes.HasValue ? s.UploadedAtMinute + s.SlaMinutes.Value : null;
                return new Study(s.StudyId, s.CustomerId, s.UploadedAtMinute, deadline, s.PriorityFlag, s.RequiresDoubleScoring);
            })
            .OrderBy(s => s.StudyId, StringComparer.Ordinal)
            .ToList();

        // Initialize queue with Primary work items.
        var queue = new List<WorkItem>();
        foreach (var s in studies)
        {
            var primary = new WorkItem(
                WorkItemId: $"WI:{s.StudyId}:P",
                StudyId: s.StudyId,
                Type: WorkType.Primary,
                ReadyAtMinute: Math.Max(s.UploadedAtMinute, 0),
                DeadlineMinute: s.DeadlineMinute,
                PriorityFlag: s.PriorityFlag,
                UploadedAtMinute: s.UploadedAtMinute
            );
            queue.Add(primary);
        }

        // Track state for each study.
        var completedWorkItemIds = new HashSet<string>(StringComparer.Ordinal);
        var primaryScorerByStudy = new Dictionary<string, string>(StringComparer.Ordinal);
        var completedPrimary = new HashSet<string>(StringComparer.Ordinal);
        var completedReview = new HashSet<string>(StringComparer.Ordinal);

        var result = new DispatchResult();

        // Simulate ticks
        for (int t = 0; t < HorizonMinutes; t += TickMinutes)
        {
            // Stop early if nothing eligible now or in future:
            if (!HasAnyRemainingWork(queue, completedWorkItemIds))
                break;

            // Per-tick SLA risk snapshot at start of tick
            var snapshot = ComputeTickRiskSnapshot(
                tickStart: t,
                studies: studies,
                completedPrimary: completedPrimary,
                completedReview: completedReview,
                completedWorkItemIds: completedWorkItemIds,
                primaryScorerByStudy: primaryScorerByStudy
            );
            result.TickRiskSnapshots.Add(snapshot);

            // Determine available techs for this tick
            var availableTechs = technicians
                .Where(tech => IsTechAvailableForTick(tech, t))
                .OrderBy(tech => tech.TechId, StringComparer.Ordinal)
                .ToList();

            if (availableTechs.Count == 0)
                continue;

            // Eligible work items (ready and not completed)
            var eligible = queue
                .Where(w => w.ReadyAtMinute <= t && !completedWorkItemIds.Contains(w.WorkItemId))
                .ToList();

            if (eligible.Count == 0)
                continue;

            // We'll assign at most one item per available tech.
            // We choose the best eligible item per tech in TechId order.
            var assignmentsThisTick = new List<Assignment>();

            foreach (var tech in availableTechs)
            {
                var candidate = PickBestEligibleForTech(eligible, tech.TechId, primaryScorerByStudy);
                if (candidate is null)
                    continue;

                // Assign it
                assignmentsThisTick.Add(new Assignment(t, tech.TechId, candidate.StudyId, candidate.Type));

                // Remove from eligible for this tick so it can't be assigned twice
                eligible.Remove(candidate);

                // Mark completion at end of tick
                completedWorkItemIds.Add(candidate.WorkItemId);

                // Update study-level state
                if (candidate.Type == WorkType.Primary)
                {
                    completedPrimary.Add(candidate.StudyId);
                    primaryScorerByStudy[candidate.StudyId] = tech.TechId;

                    // If study needs review, enqueue review item ready next tick
                    var s = studies.First(st => st.StudyId == candidate.StudyId);
                    if (s.RequiresDoubleScoring)
                    {
                        var review = new WorkItem(
                            WorkItemId: $"WI:{s.StudyId}:R",
                            StudyId: s.StudyId,
                            Type: WorkType.Review,
                            ReadyAtMinute: t + TickMinutes,
                            DeadlineMinute: s.DeadlineMinute,
                            PriorityFlag: s.PriorityFlag,
                            UploadedAtMinute: s.UploadedAtMinute
                        );
                        // Only enqueue once
                        if (!queue.Any(w => w.WorkItemId == review.WorkItemId))
                            queue.Add(review);
                    }
                    else
                    {
                        // Study completes at end of this tick
                        result.StudyCompletionMinute[s.StudyId] = t + TickMinutes;
                    }
                }
                else // Review
                {
                    completedReview.Add(candidate.StudyId);
                    // Study completes at end of this tick
                    result.StudyCompletionMinute[candidate.StudyId] = t + TickMinutes;
                }
            }

            result.Assignments.AddRange(assignmentsThisTick);
        }

        // Final SLA report (based on actual completion in simulation)
        BuildFinalReports(studies, result);

        return result;
    }

    // ---------- Helpers ----------

    private static bool HasAnyRemainingWork(List<WorkItem> queue, HashSet<string> completedWorkItemIds)
        => queue.Any(w => !completedWorkItemIds.Contains(w.WorkItemId));

    private static bool IsTechAvailableForTick(Technician tech, int tickStartMinute)
    {
        int tickEnd = tickStartMinute + TickMinutes;
        foreach (var b in tech.AvailabilityBlocks)
        {
            if (b.StartMinute <= tickStartMinute && tickEnd <= b.EndMinute)
                return true;
        }
        return false;
    }

    private static WorkItem? PickBestEligibleForTech(
        List<WorkItem> eligible,
        string techId,
        Dictionary<string, string> primaryScorerByStudy)
    {
        // Filter out review items that would violate "different tech" constraint
        var filtered = eligible.Where(w =>
        {
            if (w.Type != WorkType.Review) return true;
            return !primaryScorerByStudy.TryGetValue(w.StudyId, out var primaryTech) || !string.Equals(primaryTech, techId, StringComparison.Ordinal);
        });

        return filtered
            .OrderBy(w => w.DeadlineMinute.HasValue ? 0 : 1)                  // SLA first
            .ThenBy(w => w.DeadlineMinute ?? int.MaxValue)                    // earliest deadline
            .ThenBy(w => w.Type == WorkType.Review ? 0 : 1)                  // Review before Primary
            .ThenBy(w => w.PriorityFlag ? 0 : 1)                              // priority first
            .ThenBy(w => w.UploadedAtMinute)                                  // older first
            .ThenBy(w => w.StudyId, StringComparer.Ordinal)
            .ThenBy(w => w.WorkItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static TickRiskSnapshot ComputeTickRiskSnapshot(
        int tickStart,
        List<Study> studies,
        HashSet<string> completedPrimary,
        HashSet<string> completedReview,
        HashSet<string> completedWorkItemIds,
        Dictionary<string, string> primaryScorerByStudy)
    {
        int onTrack = 0, atRisk = 0, willMiss = 0;
        var worst = new List<(int key, string studyId)>();

        foreach (var s in studies)
        {
            if (s.DeadlineMinute is null)
                continue; // only SLA-bound studies

            bool isComplete = IsStudyComplete(s, completedPrimary, completedReview);
            if (isComplete)
                continue;

            // remaining work items optimistic estimate
            int remainingWorkItems = RemainingWorkItems(s, completedPrimary, completedReview);
            int earliestCompletion = tickStart + TickMinutes * remainingWorkItems;

            int deadline = s.DeadlineMinute.Value;
            string status;
            if (earliestCompletion > deadline)
            {
                status = "WillMiss";
                willMiss++;
                worst.Add((deadline, s.StudyId));
            }
            else if (deadline - earliestCompletion <= AtRiskThresholdMinutes)
            {
                status = "AtRisk";
                atRisk++;
                worst.Add((deadline, s.StudyId));
            }
            else
            {
                status = "OnTrack";
                onTrack++;
            }
        }

        // Include a few urgent study ids (lowest deadline first)
        var worstIds = worst
            .OrderBy(x => x.key)
            .ThenBy(x => x.studyId, StringComparer.Ordinal)
            .Take(3)
            .Select(x => x.studyId)
            .ToList();

        return new TickRiskSnapshot(tickStart, onTrack, atRisk, willMiss, worstIds);
    }

    private static bool IsStudyComplete(Study s, HashSet<string> completedPrimary, HashSet<string> completedReview)
    {
        if (!completedPrimary.Contains(s.StudyId))
            return false;

        if (!s.RequiresDoubleScoring)
            return true;

        return completedReview.Contains(s.StudyId);
    }

    private static int RemainingWorkItems(Study s, HashSet<string> completedPrimary, HashSet<string> completedReview)
    {
        if (!completedPrimary.Contains(s.StudyId))
            return s.RequiresDoubleScoring ? 2 : 1;

        if (!s.RequiresDoubleScoring)
            return 0;

        return completedReview.Contains(s.StudyId) ? 0 : 1;
    }

    private static void BuildFinalReports(List<Study> studies, DispatchResult result)
    {
        // Study-level SLA results
        foreach (var s in studies.Where(st => st.DeadlineMinute.HasValue))
        {
            int deadline = s.DeadlineMinute!.Value;
            result.StudyCompletionMinute.TryGetValue(s.StudyId, out int completion);
            int? completionOrNull = result.StudyCompletionMinute.ContainsKey(s.StudyId) ? completion : (int?)null;

            string status;
            if (completionOrNull is null)
            {
                status = "WillMiss";
            }
            else if (completionOrNull.Value > deadline)
            {
                status = "WillMiss";
            }
            else if (deadline - completionOrNull.Value <= AtRiskThresholdMinutes)
            {
                status = "AtRisk";
            }
            else
            {
                status = "OnTrack";
            }

            result.FinalStudySla.Add(new StudySlaResult(s.StudyId, s.CustomerId, deadline, completionOrNull, status));
        }

        // Customer summary
        var grouped = result.FinalStudySla.GroupBy(r => r.CustomerId, StringComparer.Ordinal);
        foreach (var g in grouped.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            int onTrack = g.Count(x => x.Status == "OnTrack");
            int atRisk = g.Count(x => x.Status == "AtRisk");
            int willMiss = g.Count(x => x.Status == "WillMiss");
            result.CustomerSummary.Add(new CustomerSlaSummary(g.Key, onTrack, atRisk, willMiss));
        }
    }

    // ---------- Optional: pretty print ----------
    public static void Print(DispatchResult r)
    {
        Console.WriteLine("=== Dispatch Assignments ===");
        foreach (var a in r.Assignments.OrderBy(x => x.TickStartMinute).ThenBy(x => x.TechId, StringComparer.Ordinal))
        {
            Console.WriteLine($"t={a.TickStartMinute,4}  {a.TechId}  {a.StudyId}  {a.Type}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Tick Risk Snapshots (SLA only) ===");
        foreach (var s in r.TickRiskSnapshots)
        {
            var worst = s.WorstStudyIds.Count > 0 ? $" worst=[{string.Join(",", s.WorstStudyIds)}]" : "";
            Console.WriteLine($"t={s.TickStartMinute,4}  OnTrack={s.OnTrack}  AtRisk={s.AtRisk}  WillMiss={s.WillMiss}{worst}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Final SLA Results ===");
        foreach (var x in r.FinalStudySla.OrderBy(x => x.DeadlineMinute).ThenBy(x => x.StudyId, StringComparer.Ordinal))
        {
            Console.WriteLine($"{x.StudyId} ({x.CustomerId})  deadline={x.DeadlineMinute,4}  completion={(x.CompletionMinute?.ToString() ?? "null"),4}  status={x.Status}");
        }

        Console.WriteLine();
        Console.WriteLine("=== Customer Summary ===");
        foreach (var c in r.CustomerSummary)
        {
            Console.WriteLine($"{c.CustomerId}: OnTrack={c.OnTrack} AtRisk={c.AtRisk} WillMiss={c.WillMiss}");
        }
    }
}

/*
USAGE (example):

var json = File.ReadAllText("dataset.json");
var result = DispatchReference.Run(json);
DispatchReference.Print(result);

*/
