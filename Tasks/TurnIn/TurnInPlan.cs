namespace StarLoom.Tasks.TurnIn;

public static class TurnInPlan
{
    public static List<TurnInEntry> BuildQueue(IEnumerable<TurnInCandidate> candidates)
    {
        return candidates
            .Where(candidate => candidate.isCollectable && candidate.quantity > 0 && candidate.jobId > 0)
            .GroupBy(candidate => new { candidate.itemId, candidate.itemName, candidate.jobId })
            .Select(group => new TurnInEntry(
                group.Key.itemId,
                group.Key.itemName,
                group.Sum(candidate => candidate.quantity),
                group.Key.jobId))
            .ToList();
    }
}
