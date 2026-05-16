namespace TrenchAftermath.Domain;

public enum PromotionsPhase
{
    NeedsImport,
    PoolSetup,
    Assignment,
    SlowRoll,
    Done,
}

public enum DieStatus
{
    Unassigned,
    Assigned,
    Rolled,
    Skipped, // model promoted before this die was rolled
}

public sealed class PromotionDie
{
    public int Id { get; }
    public DieStatus Status { get; set; } = DieStatus.Unassigned;
    public int? AssignedTroopIndex { get; set; }
    public int? RolledValue { get; set; }
    public bool WasPityTriggered { get; set; }

    public PromotionDie(int id) { Id = id; }
}

// State + rules for the Promotions flow. Pure C# — no Blazor refs — so the
// page can re-render off this without worrying about reactivity.
public sealed class PromotionsFlow
{
    public WarbandSession Warband { get; }
    public PromotionsPhase Phase { get; private set; } = PromotionsPhase.PoolSetup;

    public int GloriousDeeds { get; set; }
    public int ManualBonus { get; set; }

    public List<PromotionDie> Pool { get; private set; } = new();
    public int PityCounter { get; private set; }
    public List<int> PromotedTroopIndexes { get; } = new();

    // Troops eligible for promotion. Per pg 104, Head Wound Battle Scar holders
    // are also assignable as if they were Troops — out of scope for v1.
    public IReadOnlyList<ModelEntry> Troops =>
        Warband.Models.Where(m => !m.IsElite).ToList();

    public PromotionsFlow(WarbandSession warband)
    {
        Warband = warband;
        PityCounter = warband.ConsecutiveFailedPromotionDice;
    }

    public int PoolSize => 1 + GloriousDeeds + ManualBonus;

    public void GeneratePool()
    {
        if (Phase != PromotionsPhase.PoolSetup) throw new InvalidOperationException();
        Pool = Enumerable.Range(0, PoolSize).Select(i => new PromotionDie(i)).ToList();
        Phase = PromotionsPhase.Assignment;
    }

    // The n+1 assignment rule from pg 104: you can't assign an Nth die to any
    // single troop until every troop has at least N-1 dice.
    public bool CanAssignTo(int troopIndex)
    {
        if (Troops.Count == 0) return false;
        if (Pool.All(d => d.Status != DieStatus.Unassigned)) return false; // pool empty

        var counts = CountsByTroop();
        var thisCount = counts.GetValueOrDefault(troopIndex, 0);
        var minCount = Troops.Count > 0 ? counts.Values.DefaultIfEmpty(0).Min() : 0;
        // Make sure every troop is counted even at 0.
        foreach (var t in Troops)
            if (!counts.ContainsKey(t.Index))
                minCount = 0;

        // Assigning makes thisCount = thisCount + 1. Rule: thisCount + 1 <= min + 1,
        // i.e. thisCount <= min. (You may add to a troop only if it currently has
        // the lowest number of dice — or is tied for lowest.)
        return thisCount <= minCount;
    }

    public bool TryAssignNext(int troopIndex)
    {
        if (Phase != PromotionsPhase.Assignment) return false;
        if (!CanAssignTo(troopIndex)) return false;

        var die = Pool.FirstOrDefault(d => d.Status == DieStatus.Unassigned);
        if (die is null) return false;

        die.AssignedTroopIndex = troopIndex;
        die.Status = DieStatus.Assigned;
        return true;
    }

    public bool TryUnassignLast(int troopIndex)
    {
        if (Phase != PromotionsPhase.Assignment) return false;
        var die = Pool.LastOrDefault(d =>
            d.Status == DieStatus.Assigned && d.AssignedTroopIndex == troopIndex);
        if (die is null) return false;
        die.AssignedTroopIndex = null;
        die.Status = DieStatus.Unassigned;
        return true;
    }

    public int AssignedCount(int troopIndex) =>
        Pool.Count(d => d.AssignedTroopIndex == troopIndex
            && d.Status is DieStatus.Assigned or DieStatus.Rolled or DieStatus.Skipped);

    public int UnassignedCount =>
        Pool.Count(d => d.Status == DieStatus.Unassigned);

    public bool CanBeginRolling()
    {
        if (Phase != PromotionsPhase.Assignment) return false;
        // Allow rolling even if some dice are left unassigned — those are simply
        // lost per pg 104. We just need at least one Troop with at least one die.
        return Pool.Any(d => d.Status == DieStatus.Assigned);
    }

    public void BeginRolling()
    {
        if (!CanBeginRolling()) throw new InvalidOperationException();
        Phase = PromotionsPhase.SlowRoll;
    }

    // Returns the next die that should be rolled for a given troop, if any.
    public PromotionDie? NextDieFor(int troopIndex) =>
        Pool.FirstOrDefault(d =>
            d.AssignedTroopIndex == troopIndex && d.Status == DieStatus.Assigned);

    // Roll one die. Honors the pity timer: if PityCounter is already at 5, this
    // roll is forced to a 6 (auto-promote). Otherwise rolls D6 randomly (or uses
    // the optional manualValue for the manual-override UI).
    public void RollDie(PromotionDie die, int? manualValue = null)
    {
        if (Phase != PromotionsPhase.SlowRoll) throw new InvalidOperationException();
        if (die.Status != DieStatus.Assigned) throw new InvalidOperationException("Die not assignable to roll.");
        if (die.AssignedTroopIndex is not int troopIndex) throw new InvalidOperationException();

        bool pityFires = PityCounter >= 5;
        int result;
        if (pityFires)
        {
            result = 6;
            die.WasPityTriggered = true;
        }
        else if (manualValue is int mv)
        {
            if (mv is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(manualValue));
            result = mv;
        }
        else
        {
            result = Dice.D6();
        }

        die.RolledValue = result;
        die.Status = DieStatus.Rolled;

        if (result == 6)
        {
            // Promote: stop rolling for this troop; skip remaining dice.
            PromotedTroopIndexes.Add(troopIndex);
            foreach (var d in Pool)
            {
                if (d.AssignedTroopIndex == troopIndex && d.Status == DieStatus.Assigned)
                    d.Status = DieStatus.Skipped;
            }
            PityCounter = 0;
        }
        else
        {
            PityCounter++;
        }

        // If nothing more is rollable, advance to Done.
        if (!Pool.Any(d => d.Status == DieStatus.Assigned))
        {
            Phase = PromotionsPhase.Done;
        }
    }

    // Mutates the underlying JSON: adds ELITE keyword to each promoted troop,
    // writes the new pity counter, and stamps an advancement entry for traceability.
    public void ApplyToWarband()
    {
        foreach (var idx in PromotedTroopIndexes)
        {
            var model = Warband.Models.First(m => m.Index == idx);
            model.AddKeyword("ELITE", "kw_elite");
            model.RecordAdvancement(
                "Promoted to ELITE",
                $"Promoted during the Promotions & Experience Step on {DateTime.UtcNow:yyyy-MM-dd}.");
        }
        Warband.ConsecutiveFailedPromotionDice = PityCounter;
    }

    private Dictionary<int, int> CountsByTroop()
    {
        var d = new Dictionary<int, int>();
        foreach (var die in Pool)
        {
            if (die.AssignedTroopIndex is int ti && die.Status != DieStatus.Unassigned)
                d[ti] = d.GetValueOrDefault(ti, 0) + 1;
        }
        return d;
    }
}
