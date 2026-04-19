using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace JGUM.Behaviors.SiegeNegotiationBehavior
{
    public partial class SiegeNegotiationBehavior
    {
        private void CleanupInvalidNegotiationState()
        {
            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                PendingNegotiationRequest request = _pendingRequests[i];
                if (request.Settlement.SiegeEvent == null || !IsPlayerBesieger(request.Settlement))
                    _pendingRequests.RemoveAt(i);
            }

            PruneSettlementTimeDictionary(RequestCooldownBySettlement);
            PruneSettlementTimeDictionary(FailedRetryCooldownBySettlement);
            PruneSavedRoundsDictionary();

            if (_activeSettlement != null && (_activeSettlement.SiegeEvent == null || !IsPlayerBesieger(_activeSettlement)))
                ResetActiveNegotiationState();
        }

        private static void PruneSettlementTimeDictionary(Dictionary<string, CampaignTime> dictionary)
        {
            List<string> keysToRemove = new List<string>();
            foreach (string key in dictionary.Keys)
            {
                Settlement? settlement = GetSettlementByKey(key);
                if (settlement?.SiegeEvent == null)
                    keysToRemove.Add(key);
            }

            foreach (string key in keysToRemove)
                dictionary.Remove(key);
        }

        private void PruneSavedRoundsDictionary()
        {
            List<string> keysToRemove = new List<string>();
            foreach (string key in _savedSuccessfulRoundsBySettlement.Keys)
            {
                Settlement? settlement = GetSettlementByKey(key);
                if (settlement?.SiegeEvent == null || !IsPlayerBesieger(settlement))
                    keysToRemove.Add(key);
            }

            foreach (string key in keysToRemove)
                _savedSuccessfulRoundsBySettlement.Remove(key);
        }

        private static Settlement? GetSettlementByKey(string key)
        {
            return Campaign.Current?.Settlements?.FirstOrDefault(x => x.StringId == key);
        }
    }
}

