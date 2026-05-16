namespace TrenchAftermath.Domain;

public sealed record TraumaEntry(string Range, string Title, string Description);

public static class TraumaTable
{
    public static readonly IReadOnlyList<TraumaEntry> Entries = new TraumaEntry[]
    {
        new("11", "Dead",
            "The wound proved to be fatal. Remove the model and its Battlekit from your Warband Roster."),

        new("12", "Captured",
            "The enemy captures the model. Before continuing the Trauma Step, you and your opponent from the game can negotiate a ransom price in Ducats for the release of the model. If the ransom is not paid, the captured model is executed — remove them from your Warband Roster. If the ransom is paid, transfer the Ducats from your Stronghold to your opponent's, and treat this result as a Full Recovery. Continue with the Trauma Step after resolving the outcome of the ransom."),

        new("13", "Severe Nerve Damage",
            "All Success Rolls you take for this model are treated as being Risky Success Rolls, unless they are Risky Success Rolls already, in which case there is no additional penalty."),

        new("14", "Hand Wound",
            "Randomly determine which hand has been injured. Add -1 DICE to rolls for attacks made for this model with a Melee Weapon that is held (or jointly held) by the injured hand."),

        new("15", "Lost an Eye",
            "Add -1 DICE to rolls for Ranged Attacks made for this model. If this model receives this injury for a second time, they are blinded and you must remove them from your Warband Roster instead of re-rolling the result. Treat this injury as a Full Recovery if it is inflicted on a Sniper Priest."),

        new("16", "Chest Wound",
            "Add +1 INJURY DICE to Injury Rolls for attacks that target this model."),

        new("21", "Insomniac",
            "This model must always be the first model you deploy in any game it takes part in, and loses the INFILTRATOR Keyword if it has it."),

        new("22", "Head Wound",
            "This model can no longer gain Experience Points. You can assign Promotion Dice to this model as if it were a Troop in the Promotions and Experience Step. If one of its assigned Promotion Dice rolls a \"6\", it regains the ability to gain Experience Points, although the Battle Scar remains."),

        new("23", "Shell-shocked",
            "Roll a D6 the first time this model is deployed during a game. On a 1-2, add -1 DICE to rolls for this model for the rest of the game."),

        new("24", "Dark Memory",
            "Write down the name of the Warband from the game where this injury was received. Add -1 DICE to rolls for Melee Attacks made by this model if the target is a model from the Warband you have written down."),

        new("25", "Paranoid",
            "This model cannot be deployed within 8\" of a friendly model. Friendly models can be deployed within 8\" of this model after it has been deployed."),

        new("26", "Lost Arm",
            "This model cannot use Battlekit that requires 2 hands, and can only use one piece of Battlekit that requires 1 hand."),

        new("31", "Leg Wound",
            "Subtract 2\" from this model's Movement Characteristic. In addition, add -1 DICE to the Risky Success Roll for this model when it takes a Dash ACTION."),

        new("32", "Expensive Treatment",
            "The model's wounds require constant treatment. Before you can deploy this model, you must deduct 10 Ducats from your Warband's Stronghold. This payment does not count towards your Warband's Threshold Value."),

        new("33", "Possessed",
            "When this model is Activated, if it is more than 1\" from any enemy models the first ACTION that it takes must be a Dash ACTION, even if another rule states that it cannot take a Dash ACTION. In addition, the first 3\" of this move must be in a straight line directly away from its starting position, if it is possible for it to do so. If the model is Down at the start of the Activation, it will stand up if it can do so and must then attempt to move 3\" in a straight line away from its starting position."),

        new("34", "Muscle Damage",
            "This model cannot have Battlekit that has the HEAVY Keyword. Any that it has when the Injury is suffered is lost."),

        new("35", "Minor Wound",
            "This model cannot be used in the next game."),

        new("36", "Robbed",
            "All of the model's Battlekit is lost, unless it is Battlekit that cannot be lost or removed during a campaign. It does not receive an Injury or a Battle Scar."),

        new("41-63", "Full Recovery",
            "The model has survived the battle with no ill effects. It does not receive an Injury or a Battle Scar."),

        new("64", "Hardened",
            "This model gains the NEGATE FEAR Keyword. It does not receive an Injury or a Battle Scar."),

        new("65", "Bitter Lessons",
            "This model gains D3 extra Experience Points. It does not receive an Injury or a Battle Scar."),

        new("66", "Prominent Scar",
            "Write down the name of the Warband from the game where this injury was received. Add +1 DICE to rolls for Melee Attacks made by this model if the target is a model from the Warband you have written down. It does not receive an Injury or a Battle Scar."),
    };

    public static TraumaEntry Lookup(int d66)
    {
        if (d66 is < 11 or > 66) throw new ArgumentOutOfRangeException(nameof(d66), $"D66 result must be 11-66 (got {d66}).");
        // Tens must be 1-6 and ones 1-6.
        var tens = d66 / 10;
        var ones = d66 % 10;
        if (tens is < 1 or > 6 || ones is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(d66), $"D66 digits must each be 1-6 (got {d66}).");

        // 41..63 collapses to one entry. Everything else is an exact match.
        if (d66 is >= 41 and <= 63)
            return Entries.First(e => e.Range == "41-63");

        var key = d66.ToString();
        return Entries.First(e => e.Range == key);
    }
}
