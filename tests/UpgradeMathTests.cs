using System;
using RiskUpgrader;

internal static class UpgradeMathTests
{
    private static int passed;
    private static void Near(double expected, double actual, double tolerance, string name)
    {
        if (Math.Abs(expected - actual) > tolerance) throw new Exception(name + ": " + actual);
        passed++;
    }
    private static void Equal(int expected, int actual, string name)
    {
        if (expected != actual) throw new Exception(name + ": " + actual);
        passed++;
    }
    public static void Main()
    {
        Near(0.30d, UpgradeMath.Chance(1d, 1, 3d, 1), 0.00001d, "one white to green");
        Near(0.90d, UpgradeMath.Chance(1d, 3, 3d, 1), 0.00001d, "three whites to green");
        Near(0.18d, UpgradeMath.Chance(3d, 1, 15d, 1), 0.00001d, "one green to red");
        Near(0.90d, UpgradeMath.Chance(3d, 5, 15d, 1), 0.00001d, "five greens to red");
        Near(0.90d, UpgradeMath.Chance(15d, 100, 1d, 1), 0.00001d, "cap");
        Near(0.001d, UpgradeMath.Chance(1d, 1, 10000d, 1), 0.00001d, "floor");
        Equal(3, UpgradeMath.RequiredInput(0.70d, 1d, 3d, 1), "required 70 percent");
        Equal(5, UpgradeMath.RequiredInput(0.90d, 3d, 15d, 1), "required green red");
        Near(1d, UpgradeMath.TierValue("Tier1"), 0d, "tier1");
        Near(3d, UpgradeMath.TierValue("Tier2"), 0d, "tier2");
        Near(15d, UpgradeMath.TierValue("Tier3"), 0d, "tier3");
        Near(4d, UpgradeMath.TierValue("Hex"), 0d, "hex");
        Near(6d, UpgradeMath.TierValue("Page"), 0d, "page");
        Near(8d, UpgradeMath.TierValue("Cursed"), 0d, "cursed");
        Near(0.5d, UpgradeMath.TierValue("FoodTier"), 0d, "food");
        Console.WriteLine("PASS: " + passed + " assertions");
    }
}
