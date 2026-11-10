namespace Roulette.Server.Game;

// Pure roulette rules: no state and no I/O, so they are easy to read and to test.
public static class BetRules
{
    public const string Red = "Red";
    public const string Black = "Black";
    public const string Green = "Green";

    private static readonly HashSet<int> RedNumbers =
        [1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36];

    public static string ColorOf(int number) =>
        number == 0 ? Green : RedNumbers.Contains(number) ? Red : Black;

    // CONCEPT: Never trust the client. The browser only offers valid options, but anyone can call
    // the hub from the dev console with any value. (see docs/03-casino-realtime-patterns.md#never-trust-the-client)
    public static bool TryNormalizeTarget(string? raw, out string target)
    {
        target = "";
        var value = raw?.Trim() ?? "";

        if (value.Equals(Red, StringComparison.OrdinalIgnoreCase)) { target = Red; return true; }
        if (value.Equals(Black, StringComparison.OrdinalIgnoreCase)) { target = Black; return true; }

        if (int.TryParse(value, out var number) && number is >= 0 and <= 36)
        {
            target = number.ToString();
            return true;
        }

        return false;
    }

    // Returns the total paid back to the player for one bet (stake included).
    // Red/Black pays 2x, a single number pays 36x. 0 is green, so every colour bet loses on 0.
    public static int Payout(string target, int amount, int number)
    {
        if (target is Red or Black)
            return ColorOf(number) == target ? amount * 2 : 0;

        return int.Parse(target) == number ? amount * 36 : 0;
    }
}
