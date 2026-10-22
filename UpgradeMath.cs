using System;

namespace RiskUpgrader
{
    public static class UpgradeMath
    {
        public static double Chance(double inputValue, int inputCount, double targetValue, int outputCount)
        {
            if (inputValue <= 0d || inputCount <= 0 || targetValue <= 0d || outputCount <= 0) return 0d;
            double chance = 0.9d * (inputValue * inputCount) / (targetValue * outputCount);
            return Math.Max(0.001d, Math.Min(0.9d, chance));
        }

        public static int RequiredInput(double desiredChance, double inputValue, double targetValue, int outputCount)
        {
            if (inputValue <= 0d || targetValue <= 0d || outputCount <= 0) return 0;
            desiredChance = Math.Max(0.001d, Math.Min(0.9d, desiredChance));
            return Math.Max(1, (int)Math.Ceiling(desiredChance * targetValue * outputCount / (0.9d * inputValue)));
        }

        public static double TierValue(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return 0d;
            string t = tier.ToLowerInvariant();
            if (t == "notier" || t.Contains("assigned")) return 0d;
            if (t.Contains("food")) return 0.5d;
            if (t.Contains("hex")) return 4d;
            if (t.Contains("page")) return 6d;
            if (t.Contains("cursed")) return 8d;
            if (t.Contains("voidboss")) return 15d;
            if (t.Contains("voidtier3")) return 18d;
            if (t.Contains("voidtier2")) return 4d;
            if (t.Contains("voidtier1")) return 1.25d;
            if (t.Contains("boss")) return 12d;
            if (t.Contains("lunar")) return 9d;
            if (t.Contains("tier3")) return 15d;
            if (t.Contains("tier2")) return 3d;
            if (t.Contains("tier1")) return 1d;
            if (t.Contains("scrap")) return 0.8d;
            return 6d;
        }
    }
}
