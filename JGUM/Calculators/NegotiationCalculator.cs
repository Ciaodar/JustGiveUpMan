using System;
using System.Collections.Generic;
using System.Linq;
using JGUM.Config;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace JGUM.Calculators
{
    public static class NegotiationCalculator
    {
        public static float GetPowerRatio(Settlement settlement)
        {
            var siegeEvent = settlement.SiegeEvent;
            if (siegeEvent == null)
                return 0f;

            var besiegerParties = siegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege);
            float attackerStrength = besiegerParties.Sum(p => p.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.PlainBattle));

            var defenderParties = settlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege);
            float defenderStrength = defenderParties.Sum(p => p.GetCustomStrength(BattleSideEnum.Defender, MapEvent.PowerCalculationContext.PlainBattle));

            if (defenderStrength <= 0f)
                return 99f;

            return attackerStrength / defenderStrength;
        }

        public static PersuasionArgumentStrength GetBaseStrengthFromPowerRatio(float powerRatio)
        {
            float easyThreshold = Math.Max(0f, JgumSettingsManager.SiegeNegotiationEasyThreshold);
            float normalThreshold = Math.Min(easyThreshold, Math.Max(0f, JgumSettingsManager.SiegeNegotiationNormalThreshold));
            float hardThreshold = Math.Min(normalThreshold, Math.Max(0f, JgumSettingsManager.SiegeNegotiationHardThreshold));

            if (powerRatio >= easyThreshold)
                return PersuasionArgumentStrength.Easy;
            if (powerRatio >= normalThreshold)
                return PersuasionArgumentStrength.Normal;
            if (powerRatio >= hardThreshold)
                return PersuasionArgumentStrength.Hard;
            return PersuasionArgumentStrength.ExtremelyHard;
        }

        public static int GetRequiredSuccessScore(Settlement? settlement)
        {
            return settlement?.IsTown == true ? 4 : 3;
        }

        public static PersuasionArgumentStrength ShiftStrength(PersuasionArgumentStrength value, int delta)
        {
            int shifted = (int)value + delta;
            if (shifted > 3)
                shifted = 3;
            if (shifted < -3)
                shifted = -3;
            return (PersuasionArgumentStrength)shifted;
        }

        public static List<int> BuildRoundRandomBiases(int optionCount)
        {
            List<int> biases = new List<int>();
            if (optionCount <= 0)
                return biases;

            int rngRange = GetRngPresetRange();
            if (rngRange <= 0)
            {
                for (int i = 0; i < optionCount; i++)
                    biases.Add(0);
                return biases;
            }

            if (optionCount == 1)
            {
                biases.Add(0);
                return biases;
            }

            if (optionCount == 2)
            {
                biases.Add(-rngRange);
                biases.Add(rngRange);
            }
            else
            {
                biases.Add(-rngRange);
                biases.Add(0);
                biases.Add(rngRange);
                while (biases.Count < optionCount)
                    biases.Add(MBRandom.RandomInt(-rngRange, rngRange + 1));
            }

            for (int i = biases.Count - 1; i > 0; i--)
            {
                int swapIndex = MBRandom.RandomInt(i + 1);
                int tmp = biases[i];
                biases[i] = biases[swapIndex];
                biases[swapIndex] = tmp;
            }

            return biases;
        }

        public static int GetSlotStrengthBias(int optionIndex, int optionCount)
        {
            if (optionCount < 3)
                return 0;

            if (optionIndex == 0)
                return 1;
            if (optionIndex == optionCount - 1)
                return -1;

            return 0;
        }

        private static int GetRngPresetRange()
        {
            // 0 = Off, 1 = Medium (+/-2), 2 = High (+/-3)
            int preset = JgumSettingsManager.SiegeNegotiationRngPreset;
            if (preset <= 0)
                return 0;

            return preset == 1 ? 2 : 3;
        }
    }
}

