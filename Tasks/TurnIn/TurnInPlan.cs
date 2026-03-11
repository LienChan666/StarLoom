namespace StarLoom.Tasks.TurnIn;

public static class TurnInPlan
{
    public static List<TurnInEntry> BuildQueue(IEnumerable<TurnInCandidate> candidates)
    {
        return candidates
            .Where(candidate => candidate.isCollectable && candidate.quantity > 0)
            .Select(candidate => new TurnInEntry(candidate.itemId, candidate.itemName, candidate.quantity, candidate.jobId))
            .ToList();
    }
}
