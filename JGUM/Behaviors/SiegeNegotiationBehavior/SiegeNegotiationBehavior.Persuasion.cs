using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using JGUM.Actions;
using JGUM.Calculators;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace JGUM.Behaviors.SiegeNegotiationBehavior
{
    public partial class SiegeNegotiationBehavior
    {
        private void AddNegotiationDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("jgum_proactive_intro", "start", "jgum_proactive_player_open",
                StringCalculator.GetString("jgum_proactive_intro", "We are listening. Speak your terms.[ib:nervous][if:convo_grave]"),
                ProactiveConversationCondition, null, 10001);

            starter.AddPlayerLine("jgum_proactive_player_open", "jgum_proactive_player_open", "jgum_proactive_persuasion_options",
                StringCalculator.GetString("jgum_proactive_player_open", "There is no need for more bloodshed. Hear me out."),
                ProactiveConversationCondition, StartPersuasionOnConsequence, 10001);

            starter.AddDialogLine("jgum_proactive_task_line", "jgum_proactive_persuasion_options", "jgum_proactive_persuasion_options",
                "{=!}{JGUM_PERSUASION_TASK_LINE}", ShowCurrentTaskLineCondition, null, 10001);

            starter.AddPlayerLine("jgum_proactive_option_1", "jgum_proactive_persuasion_options", "jgum_proactive_reaction",
                "{=!}{JGUM_PERSUASION_OPTION_1}",
                () => SetupPersuasionOptionText(0, "JGUM_PERSUASION_OPTION_1"),
                () => BlockCurrentOption(0),
                10001,
                ProactiveOption1Clickable,
                SetupPersuasionOption1);

            starter.AddPlayerLine("jgum_proactive_option_2", "jgum_proactive_persuasion_options", "jgum_proactive_reaction",
                "{=!}{JGUM_PERSUASION_OPTION_2}",
                () => SetupPersuasionOptionText(1, "JGUM_PERSUASION_OPTION_2"),
                () => BlockCurrentOption(1),
                10001,
                ProactiveOption2Clickable,
                SetupPersuasionOption2);

            starter.AddPlayerLine("jgum_proactive_option_3", "jgum_proactive_persuasion_options", "jgum_proactive_reaction",
                "{=!}{JGUM_PERSUASION_OPTION_3}",
                () => SetupPersuasionOptionText(2, "JGUM_PERSUASION_OPTION_3"),
                () => BlockCurrentOption(2),
                10001,
                ProactiveOption3Clickable,
                SetupPersuasionOption3);

            starter.AddDialogLine("jgum_proactive_reaction", "jgum_proactive_reaction", "jgum_proactive_result",
                "{=!}{JGUM_PERSUASION_REACTION}", ProactiveReactionCondition, ProactiveReactionConsequence, 10001);

            starter.AddDialogLine("jgum_proactive_result_success", "jgum_proactive_result", "close_window",
                StringCalculator.GetString("jgum_proactive_success", "You are right, there's no need for blood to spill."),
                ProactiveSuccessCondition, OnProactivePersuasionSuccess, 10001);

            starter.AddDialogLine("jgum_proactive_result_fail", "jgum_proactive_result", "close_window",
                StringCalculator.GetString("jgum_proactive_fail", "No. We will not yield."),
                ProactiveFailCondition, OnProactivePersuasionFailure, 10001);

            starter.AddDialogLine("jgum_proactive_result_continue", "jgum_proactive_result", "jgum_proactive_persuasion_options",
                StringCalculator.GetString("jgum_proactive_continue", "I need to hear more."),
                ProactiveContinueCondition, null, 10001);
        }

        private bool ProactiveConversationCondition() => _activeSettlement != null;

        private void StartPersuasionOnConsequence()
        {
            ConversationManager.StartPersuasion(
                _maxScore,
                _successValue,
                _failValue,
                _criticalSuccessValue,
                _criticalFailValue,
                _startingSuccessfulRounds * _successValue);
        }

        private bool ShowCurrentTaskLineCondition()
        {
            PersuasionTask task = GetCurrentPersuasionTask();
            MBTextManager.SetTextVariable("JGUM_PERSUASION_TASK_LINE", task.SpokenLine);
            return !ConversationManager.GetPersuasionProgressSatisfied() && !IsPersuasionFailed();
        }

        private bool SetupPersuasionOptionText(int optionIndex, string variableName)
        {
            PersuasionTask task = GetCurrentPersuasionTask();
            if (task.Options.Count <= optionIndex)
                return false;

            TextObject line = new TextObject("{=bSo9hKwr}{PERSUASION_OPTION_LINE} {SUCCESS_CHANCE}");
            line.SetTextVariable("PERSUASION_OPTION_LINE", task.Options[optionIndex].Line);
            line.SetTextVariable("SUCCESS_CHANCE", PersuasionHelper.ShowSuccess(task.Options[optionIndex]));
            MBTextManager.SetTextVariable(variableName, line);
            return true;
        }

        private void BlockCurrentOption(int optionIndex)
        {
            PersuasionTask task = GetCurrentPersuasionTask();
            if (task.Options.Count > optionIndex)
                task.Options[optionIndex].BlockTheOption(true);
        }

        private PersuasionOptionArgs SetupPersuasionOption1() => GetCurrentPersuasionTask().Options[0];
        private PersuasionOptionArgs SetupPersuasionOption2() => GetCurrentPersuasionTask().Options[1];
        private PersuasionOptionArgs SetupPersuasionOption3() => GetCurrentPersuasionTask().Options[2];

        private bool ProactiveOption1Clickable(out TextObject hintText) => ProactiveOptionClickable(0, out hintText);
        private bool ProactiveOption2Clickable(out TextObject hintText) => ProactiveOptionClickable(1, out hintText);
        private bool ProactiveOption3Clickable(out TextObject hintText) => ProactiveOptionClickable(2, out hintText);

        private bool ProactiveOptionClickable(int optionIndex, out TextObject hintText)
        {
            PersuasionTask task = GetCurrentPersuasionTask();
            if (task.Options.Count > optionIndex)
            {
                PersuasionOptionArgs option = task.Options[optionIndex];
                if (TryGetTraitLockHint(task, optionIndex, option, out hintText))
                    return false;

                if (option.IsBlocked)
                {
                    hintText = new TextObject(StringCalculator.GetString(
                        "jgum_proactive_option_locked_used",
                        "This argument is no longer available."));
                    return false;
                }

                hintText = TextObject.GetEmpty();
                return true;
            }

            hintText = new TextObject("{=9ACJsI6S}Blocked");
            return false;
        }

        private bool ProactiveReactionCondition()
        {
            if (!ConversationManager.GetPersuasionChosenOptions().Any())
                return false;

            PersuasionOptionResult result = ConversationManager.GetPersuasionChosenOptions().Last().Item2;
            MBTextManager.SetTextVariable("JGUM_PERSUASION_REACTION", PersuasionHelper.GetDefaultPersuasionOptionReaction(result));
            return true;
        }

        private void ProactiveReactionConsequence()
        {
            Tuple<PersuasionOptionArgs, PersuasionOptionResult> chosen = ConversationManager.GetPersuasionChosenOptions().Last();

            float difficulty = Campaign.Current.Models.PersuasionModel.GetDifficulty(PersuasionDifficulty.Hard);
            float moveToNextStageChance;
            Campaign.Current.Models.PersuasionModel.GetEffectChances(
                chosen.Item1,
                out moveToNextStageChance,
                out _,
                difficulty);

            PersuasionTask task = GetCurrentPersuasionTask();
            task.ApplyEffects(moveToNextStageChance, 0f);

            string? selectedTemplateId = null;
            if (_templateByResolvedText.TryGetValue(chosen.Item1.Line.ToString(), out PersuasionLineTemplate selectedTemplate))
                selectedTemplateId = selectedTemplate.Id;

            if (chosen.Item2 == PersuasionOptionResult.Success || chosen.Item2 == PersuasionOptionResult.CriticalSuccess)
                _successfulRoundsThisAttempt = Math.Min(_successfulRoundsThisAttempt + 1, _activeRequiredRounds);

            RebuildNextTaskWithCarryOver(task, chosen.Item1, selectedTemplateId);

            foreach (PersuasionOptionArgs option in task.Options)
                option.BlockTheOption(true);
        }

        private bool ProactiveSuccessCondition() => ConversationManager.GetPersuasionProgressSatisfied();
        private bool ProactiveFailCondition() => IsPersuasionFailed();
        private bool ProactiveContinueCondition() => !ConversationManager.GetPersuasionProgressSatisfied() && !IsPersuasionFailed();

        private void OnProactivePersuasionSuccess()
        {
            ConversationManager.EndPersuasion();

            Settlement? settlement = _activeSettlement;
            string? settlementKey = _activeSettlementKey;
            ResetActiveNegotiationState();

            if (!string.IsNullOrEmpty(settlementKey))
                _savedSuccessfulRoundsBySettlement.Remove(settlementKey!);

            if (settlement == null)
                return;

            CaptureSettlementByNegotiationAction.Apply(settlement);
        }

        private void OnProactivePersuasionFailure()
        {
            ConversationManager.EndPersuasion();

            Settlement? settlement = _activeSettlement;
            string? settlementKey = _activeSettlementKey;
            ResetActiveNegotiationState();

            if (settlement != null)
                FailedRetryCooldownBySettlement[settlement.StringId] = CampaignTime.HoursFromNow(FailedRetryCooldownHours);

            if (!string.IsNullOrEmpty(settlementKey))
            {
                int decayed = _successfulRoundsThisAttempt - 1;
                if (decayed < 0)
                    decayed = 0;

                int maxCarry = _activeRequiredRounds - 1;
                if (decayed > maxCarry)
                    decayed = maxCarry;

                _savedSuccessfulRoundsBySettlement[settlementKey!] = decayed;
            }
        }

        private void BuildPersuasionTasks(float powerRatio)
        {
            _successValue = 1f;
            _failValue = 0f;
            _criticalSuccessValue = 2f;
            _criticalFailValue = 2f;

            int requiredSuccessScore = Math.Min(NegotiationCalculator.GetRequiredSuccessScore(_activeSettlement), MaxPersuasionRounds);
            _activeRequiredRounds = requiredSuccessScore;
            _activeBaseStrength = NegotiationCalculator.GetBaseStrengthFromPowerRatio(powerRatio);

            int carryRounds = 0;
            if (!string.IsNullOrEmpty(_activeSettlementKey) && _savedSuccessfulRoundsBySettlement.TryGetValue(_activeSettlementKey!, out int savedRounds))
                carryRounds = savedRounds;

            if (carryRounds < 0)
                carryRounds = 0;
            if (carryRounds >= requiredSuccessScore)
                carryRounds = requiredSuccessScore - 1;

            _startingSuccessfulRounds = carryRounds;
            _successfulRoundsThisAttempt = carryRounds;
            _templateByResolvedText.Clear();

            _activeTasks = new List<PersuasionTask>();
            for (int i = 0; i < requiredSuccessScore; i++)
                _activeTasks.Add(CreatePersuasionTask(i, BuildRoundTemplates(i, null, null)));

            _maxScore = requiredSuccessScore * _successValue;
        }

        private PersuasionTask CreatePersuasionTask(int reservationIndex, List<PersuasionLineTemplate> lineTemplates)
        {
            PersuasionArgumentStrength roundBaseStrength = NegotiationCalculator.ShiftStrength(_activeBaseStrength, -reservationIndex);
            PersuasionTask task = new PersuasionTask(reservationIndex)
            {
                SpokenLine = new TextObject(StringCalculator.GetString("jgum_proactive_task_line", "Convince me.")),
                ImmediateFailLine = new TextObject(StringCalculator.GetString("jgum_proactive_task_fail_immediate", "That is not convincing.")),
                FinalFailLine = new TextObject(StringCalculator.GetString("jgum_proactive_task_fail_final", "Your words are not enough. We refuse."))
            };

            List<int> roundRandomBiases = NegotiationCalculator.BuildRoundRandomBiases(lineTemplates.Count);
            for (int i = 0; i < lineTemplates.Count; i++)
                task.AddOptionToTask(CreateOption(lineTemplates[i], roundBaseStrength, i, lineTemplates.Count, roundRandomBiases[i]));

            return task;
        }

        private PersuasionOptionArgs CreateOption(PersuasionLineTemplate line, PersuasionArgumentStrength roundBaseStrength, int optionIndex, int optionCount, int randomBias)
        {
            string resolvedLine = StringCalculator.GetString(line.Id, line.Fallback);
            int slotBias = NegotiationCalculator.GetSlotStrengthBias(optionIndex, optionCount);
            PersuasionArgumentStrength strength = NegotiationCalculator.ShiftStrength(roundBaseStrength, line.StrengthOffset + slotBias + randomBias);
            _templateByResolvedText[resolvedLine] = line;

            return new PersuasionOptionArgs(
                ResolveSkill(line.Skill),
                ResolveTrait(line.Trait),
                line.TraitEffect,
                strength,
                false,
                new TextObject(resolvedLine),
                canMoveToTheNextReservation: line.CanMoveToTheNextReservation);
        }

        private void RebuildNextTaskWithCarryOver(PersuasionTask currentTask, PersuasionOptionArgs selectedOption, string? selectedTemplateId)
        {
            if (_activeTasks == null)
                return;

            int currentTaskIndex = _activeTasks.IndexOf(currentTask);
            int nextTaskIndex = currentTaskIndex + 1;
            if (currentTaskIndex < 0 || nextTaskIndex >= _activeTasks.Count)
                return;

            List<PersuasionLineTemplate> carryCandidates = new List<PersuasionLineTemplate>();
            foreach (PersuasionOptionArgs option in currentTask.Options)
            {
                if (option == selectedOption)
                    continue;

                if (_templateByResolvedText.TryGetValue(option.Line.ToString(), out PersuasionLineTemplate template))
                    carryCandidates.Add(template);
            }

            List<PersuasionLineTemplate> nextRoundTemplates = BuildRoundTemplates(nextTaskIndex, carryCandidates, selectedTemplateId);
            _activeTasks[nextTaskIndex] = CreatePersuasionTask(nextTaskIndex, nextRoundTemplates);
        }

        private List<PersuasionLineTemplate> BuildRoundTemplates(int reservationIndex, List<PersuasionLineTemplate>? carryCandidates, string? selectedTemplateId)
        {
            List<PersuasionLineTemplate> templates = new List<PersuasionLineTemplate>();

            if (carryCandidates != null && carryCandidates.Count > 0)
            {
                List<PersuasionLineTemplate> eligibleCarry = carryCandidates
                    .Where(x => x.Id != selectedTemplateId)
                    .Distinct()
                    .ToList();

                int carryCount = Math.Min(eligibleCarry.Count, MBRandom.RandomInt(1, 3));
                for (int i = 0; i < carryCount; i++)
                {
                    int randomIndex = MBRandom.RandomInt(eligibleCarry.Count);
                    templates.Add(eligibleCarry[randomIndex]);
                    eligibleCarry.RemoveAt(randomIndex);
                }
            }

            List<PersuasionLineTemplate> roundPool = GetRoundLinePool(reservationIndex)
                .Where(x => x.Id != selectedTemplateId && templates.All(t => t.Id != x.Id))
                .ToList();

            while (templates.Count < 3 && roundPool.Count > 0)
            {
                int randomIndex = MBRandom.RandomInt(roundPool.Count);
                templates.Add(roundPool[randomIndex]);
                roundPool.RemoveAt(randomIndex);
            }

            if (templates.Count < 3)
            {
                List<PersuasionLineTemplate> fallbackPool = GetRoundLinePool(reservationIndex)
                    .Where(x => templates.All(t => t.Id != x.Id))
                    .ToList();

                while (templates.Count < 3 && fallbackPool.Count > 0)
                {
                    int randomIndex = MBRandom.RandomInt(fallbackPool.Count);
                    templates.Add(fallbackPool[randomIndex]);
                    fallbackPool.RemoveAt(randomIndex);
                }
            }

            return templates;
        }

        private List<PersuasionLineTemplate> GetRoundLinePool(int reservationIndex)
        {
            if (_activeSettlement?.IsTown == true && reservationIndex == MaxPersuasionRounds - 1)
                return CityFourthRoundLinePool;

            if (reservationIndex >= 0 && reservationIndex < PersuasionRoundLinePools.Count)
                return PersuasionRoundLinePools[reservationIndex];

            return PersuasionRoundLinePools[PersuasionRoundLinePools.Count - 1];
        }

        private PersuasionTask GetCurrentPersuasionTask()
        {
            if (_activeTasks == null || _activeTasks.Count == 0)
                throw new InvalidOperationException("Persuasion task is not initialized.");

            foreach (PersuasionTask task in _activeTasks)
            {
                if (!task.Options.All(x => x.IsBlocked))
                    return task;
            }

            return _activeTasks[_activeTasks.Count - 1];
        }

        private bool IsPersuasionFailed()
        {
            PersuasionTask task = GetCurrentPersuasionTask();
            return !ConversationManager.GetPersuasionProgressSatisfied() && task.Options.All(x => x.IsBlocked);
        }

        private bool TryGetTraitLockHint(PersuasionTask task, int optionIndex, PersuasionOptionArgs option, out TextObject hintText)
        {
            if (!_templateByResolvedText.TryGetValue(option.Line.ToString(), out PersuasionLineTemplate optionTemplate))
            {
                hintText = TextObject.GetEmpty();
                return false;
            }

            if (Hero.MainHero.GetTraitLevel(ResolveTrait(optionTemplate.Trait)) >= 0)
            {
                hintText = TextObject.GetEmpty();
                return false;
            }

            int lockableBeforeCurrent = 0;
            for (int i = 0; i < task.Options.Count; i++)
            {
                PersuasionOptionArgs candidate = task.Options[i];
                if (!_templateByResolvedText.TryGetValue(candidate.Line.ToString(), out PersuasionLineTemplate candidateTemplate))
                    continue;

                if (Hero.MainHero.GetTraitLevel(ResolveTrait(candidateTemplate.Trait)) < 0 && i < optionIndex)
                    lockableBeforeCurrent++;
            }

            if (lockableBeforeCurrent >= 2)
            {
                hintText = TextObject.GetEmpty();
                return false;
            }

            if (optionTemplate.Trait == NegotiationTrait.Honor)
            {
                hintText = new TextObject(StringCalculator.GetString(
                    "jgum_proactive_option_locked_honor",
                    "Your Honor is too low to make this argument."));
            }
            else if (optionTemplate.Trait == NegotiationTrait.Mercy)
            {
                hintText = new TextObject(StringCalculator.GetString(
                    "jgum_proactive_option_locked_mercy",
                    "Your Mercy is too low to make this argument."));
            }
            else
            {
                hintText = new TextObject(StringCalculator.GetString(
                    "jgum_proactive_option_locked_calculating",
                    "You need a more calculating approach to make this argument."));
            }

            return true;
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> involvedCharacters)
        {
            CleanupInvalidNegotiationState();
            ResetActiveNegotiationState();
        }
    }
}

