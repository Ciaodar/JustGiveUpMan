using System;
using System.Collections.Generic;
using System.Linq;
using JGUM.Actions;
using JGUM.Calculators;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace JGUM.Behaviors.SiegeNegotiationBehavior
{
    public partial class SiegeNegotiationBehavior : CampaignBehaviorBase
    {
        private const string SiegeStrategiesMenuId = "menu_siege_strategies";
        private const string OptionId = "jgum_offer_negotiation_meeting";
        private const float RequestResponseDelayHours = 1f;
        private const float RequestButtonCooldownHours = 24f;
        private const float FailedRetryCooldownHours = 72f;
        private const int MaxPersuasionRounds = 4;

        private static readonly Dictionary<string, CampaignTime> RequestCooldownBySettlement = new Dictionary<string, CampaignTime>();
        private static readonly Dictionary<string, CampaignTime> FailedRetryCooldownBySettlement = new Dictionary<string, CampaignTime>();
        private static readonly List<List<PersuasionLineTemplate>> PersuasionRoundLinePools = new List<List<PersuasionLineTemplate>>
        {
            new List<PersuasionLineTemplate>
            {
                new PersuasionLineTemplate("jgum_proactive_option_t1_1", "Your people should not die in a hopeless defense.", NegotiationSkill.Leadership, NegotiationTrait.Mercy, TraitEffect.Positive, 1, true),
                new PersuasionLineTemplate("jgum_proactive_option_t1_2", "Look at the numbers. This siege is already decided.", NegotiationSkill.Tactics, NegotiationTrait.Calculating, TraitEffect.Positive, 0, false),
                new PersuasionLineTemplate("jgum_proactive_option_t1_3", "Choose the honorable path and spare your people.", NegotiationSkill.Charm, NegotiationTrait.Honor, TraitEffect.Positive, 0, true)
            },
            new List<PersuasionLineTemplate>
            {
                new PersuasionLineTemplate("jgum_proactive_option_t2_1", "Your walls are breaking. Prolonging this only wastes lives.", NegotiationSkill.Engineering, NegotiationTrait.Calculating, TraitEffect.Positive, -1, false),
                new PersuasionLineTemplate("jgum_proactive_option_t2_2", "If this turns into an assault, you lose all leverage.", NegotiationSkill.Tactics, NegotiationTrait.Calculating, TraitEffect.Positive, 0, true),
                new PersuasionLineTemplate("jgum_proactive_option_t2_3", "Open the gates and I will keep order over your civilians.", NegotiationSkill.Leadership, NegotiationTrait.Mercy, TraitEffect.Positive, 1, true)
            },
            new List<PersuasionLineTemplate>
            {
                new PersuasionLineTemplate("jgum_proactive_option_t3_1", "Surrender by treaty protects your honor before all Calradia.", NegotiationSkill.Charm, NegotiationTrait.Honor, TraitEffect.Positive, 1, true),
                new PersuasionLineTemplate("jgum_proactive_option_t3_2", "Your supplies and morale decline while my advantage grows.", NegotiationSkill.Trade, NegotiationTrait.Calculating, TraitEffect.Positive, -1, false),
                new PersuasionLineTemplate("jgum_proactive_option_t3_3", "Do not trade lives for pride. End this with mercy.", NegotiationSkill.Leadership, NegotiationTrait.Mercy, TraitEffect.Positive, 0, true)
            }
        };

        private static readonly List<PersuasionLineTemplate> CityFourthRoundLinePool = new List<PersuasionLineTemplate>
        {
            new PersuasionLineTemplate("jgum_proactive_option_t4_1", "A city survives by preserving people, not ruins.", NegotiationSkill.Leadership, NegotiationTrait.Mercy, TraitEffect.Positive, 0, true),
            new PersuasionLineTemplate("jgum_proactive_option_t4_2", "Yield now and the markets reopen instead of burning.", NegotiationSkill.Trade, NegotiationTrait.Calculating, TraitEffect.Positive, 0, false),
            new PersuasionLineTemplate("jgum_proactive_option_t4_3", "Choose surrender, and history will remember restraint over slaughter.", NegotiationSkill.Charm, NegotiationTrait.Honor, TraitEffect.Positive, 1, true)
        };

        private readonly List<PendingNegotiationRequest> _pendingRequests = new List<PendingNegotiationRequest>();
        private Dictionary<string, int> _savedSuccessfulRoundsBySettlement = new Dictionary<string, int>();
        private readonly Dictionary<string, PersuasionLineTemplate> _templateByResolvedText = new Dictionary<string, PersuasionLineTemplate>();
        private Settlement? _activeSettlement;
        private string? _activeSettlementKey;
        private List<PersuasionTask>? _activeTasks;
        private PersuasionArgumentStrength _activeBaseStrength;
        private int _activeRequiredRounds;
        private int _startingSuccessfulRounds;
        private int _successfulRoundsThisAttempt;

        private float _maxScore;
        private float _successValue;
        private float _failValue;
        private float _criticalSuccessValue;
        private float _criticalFailValue;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddSiegeMenuOption(starter);
            AddNegotiationDialogs(starter);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("JGUM_SavedSuccessfulRoundsBySettlement", ref _savedSuccessfulRoundsBySettlement);
        }

        private void ResetActiveNegotiationState()
        {
            _activeSettlement = null;
            _activeSettlementKey = null;
            _activeTasks = null;
            _templateByResolvedText.Clear();
        }

        private static SkillObject ResolveSkill(NegotiationSkill skill)
        {
            switch (skill)
            {
                case NegotiationSkill.Leadership:
                    return DefaultSkills.Leadership ?? DefaultSkills.Charm;
                case NegotiationSkill.Tactics:
                    return DefaultSkills.Tactics ?? DefaultSkills.Charm;
                case NegotiationSkill.Trade:
                    return DefaultSkills.Trade ?? DefaultSkills.Charm;
                case NegotiationSkill.Engineering:
                    return DefaultSkills.Engineering ?? DefaultSkills.Charm;
                default:
                    return DefaultSkills.Charm;
            }
        }

        private static TraitObject ResolveTrait(NegotiationTrait trait)
        {
            switch (trait)
            {
                case NegotiationTrait.Mercy:
                    return DefaultTraits.Mercy ?? DefaultTraits.Honor;
                case NegotiationTrait.Honor:
                    return DefaultTraits.Honor ?? DefaultTraits.Mercy;
                default:
                    return DefaultTraits.Calculating ?? DefaultTraits.Honor;
            }
        }

        private static CharacterObject? FindSettlementRepresentative(Settlement settlement)
        {
            var garrisonParty = settlement.Town?.GarrisonParty;
            CharacterObject? governor = settlement.Town?.Governor?.CharacterObject;

            Hero? defenderLeader = settlement.SiegeEvent != null
                ? Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(settlement.SiegeEvent, BattleSideEnum.Defender)
                : null;

            CharacterObject? randomGarrisonTroop = GetRandomGarrisonTroopRepresentative(garrisonParty);

            return governor
                   ?? defenderLeader?.CharacterObject
                   ?? settlement.Parties.FirstOrDefault(p => p.LeaderHero != null && p.LeaderHero.IsLord)?.LeaderHero?.CharacterObject
                   ?? settlement.Town?.GetDefenderParties(MapEvent.BattleTypes.None).FirstOrDefault(p => p.LeaderHero != null && p.LeaderHero.IsLord)?.LeaderHero?.CharacterObject
                   ?? randomGarrisonTroop
                   ?? settlement.Notables.FirstOrDefault()?.CharacterObject;
        }

        private static CharacterObject? GetRandomGarrisonTroopRepresentative(MobileParty? garrisonParty)
        {
            var garrisonRoster = garrisonParty?.MemberRoster;
            if (garrisonRoster == null)
                return null;

            var troopPool = new List<CharacterObject>();
            for (int i = 0; i < garrisonRoster.Count; i++)
            {
                var element = garrisonRoster.GetElementCopyAtIndex(i);
                if (element.Number <= 0 || element.Character == null || element.Character.IsHero)
                    continue;

                troopPool.Add(element.Character);
            }

            if (troopPool.Count == 0)
                return null;

            return troopPool[MBRandom.RandomInt(troopPool.Count)];
        }

        private sealed class PendingNegotiationRequest
        {
            public Settlement Settlement { get; set; } = null!;
            public CampaignTime ResolveAt { get; set; }
        }

        private sealed class PersuasionLineTemplate
        {
            public PersuasionLineTemplate(
                string id,
                string fallback,
                NegotiationSkill skill,
                NegotiationTrait trait,
                TraitEffect traitEffect,
                int strengthOffset,
                bool canMoveToTheNextReservation)
            {
                Id = id;
                Fallback = fallback;
                Skill = skill;
                Trait = trait;
                TraitEffect = traitEffect;
                StrengthOffset = strengthOffset;
                CanMoveToTheNextReservation = canMoveToTheNextReservation;
            }

            public string Id { get; }
            public string Fallback { get; }
            public NegotiationSkill Skill { get; }
            public NegotiationTrait Trait { get; }
            public TraitEffect TraitEffect { get; }
            public int StrengthOffset { get; }
            public bool CanMoveToTheNextReservation { get; }
        }

        private enum NegotiationSkill
        {
            Charm,
            Leadership,
            Tactics,
            Trade,
            Engineering
        }

        private enum NegotiationTrait
        {
            Mercy,
            Honor,
            Calculating
        }
    }
}

