using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace JGUM.Calculators
{
    public static class StringBringCalculator
    {
        private static readonly Dictionary<string, int> _countsCache = new Dictionary<string, int>();

        public static string GetRandomStringId(string baseId)
        {
            if (!_countsCache.TryGetValue(baseId, out int count))
            {
                string countStr = new TextObject("{=" + baseId + "_count}1").ToString();
                if (!int.TryParse(countStr, out count))
                {
                    count = 1;
                }
                _countsCache[baseId] = count;
            }

            int randomVariant = MBRandom.RandomInt(1, count + 1);
            return "{=" + baseId + "_" + randomVariant + "}";
        }

        public static String GetRandomText(string baseId)
        {
            
            return new TextObject(GetRandomStringId(baseId)).ToString();
        }
    }
}