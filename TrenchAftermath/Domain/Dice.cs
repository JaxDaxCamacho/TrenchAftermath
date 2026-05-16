namespace TrenchAftermath.Domain;

public static class Dice
{
    public static int D6() => Random.Shared.Next(1, 7);

    // D66: roll two D6 and read as tens + ones (e.g. 3 and 5 = 35).
    // Range: 11..16, 21..26, ... 61..66. Total 36 outcomes.
    public static int D66()
    {
        var tens = D6();
        var ones = D6();
        return tens * 10 + ones;
    }
}
