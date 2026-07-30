using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TaleWorlds.ObjectSystem;
using JGUM.Calculators;

namespace JGUM.Data
{
    public class AiSurrenderLogEntry : LogEntry, IEncyclopediaLog, IChatNotification
    {
        [SaveableField(1)]
        private Hero _winner;

        [SaveableField(2)]
        private Hero _loser;

        public Hero Winner => _winner;
        public Hero Loser => _loser;

        public AiSurrenderLogEntry(Hero winner, Hero loser)
        {
            _winner = winner;
            _loser = loser;
        }

        public override CampaignTime KeepInHistoryTime => CampaignTime.WeeksFromNow(8f);

        public bool IsVisibleNotification => true;

        public override ChatNotificationType NotificationType => ChatNotificationType.Default;

        public bool IsVisibleInEncyclopediaPageOf<T>(T obj) where T : MBObjectBase
        {
            return obj == Winner || obj == Loser;
        }

        public TextObject GetEncyclopediaText()
        {
            return GetNotificationText();
        }

        public TextObject GetNotificationText()
        {
            MBTextManager.SetTextVariable("WINNER_NAME", Winner.Name);
            MBTextManager.SetTextVariable("LOSER_NAME", Loser.Name);
            return new TextObject(StringCalculator.GetString("jgum_ai_surrender_log", "{LOSER_NAME} surrendered to the overwhelming forces of {WINNER_NAME} without a fight."));
        }

        public override string ToString()
        {
            return GetNotificationText().ToString();
        }
    }
}
