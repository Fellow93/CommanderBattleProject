using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace CommanderBattle
{
    // The general's horn calls (docs/plans/horn-signals.md).
    public enum HornSignalKind
    {
        Advance,
        Hold,
        FallBack
    }

    public static class HornSignals
    {
        // Side-wide calls, so the general can't drown the field in horns.
        public const float Cooldown = 8f;

        // The stock bark wheel's entries (SkinVoiceManager.VoiceType.MpBarks) that double as horn calls for the general.
        private const int BarkDefend = 0;
        private const int BarkAttack = 1;
        private const int BarkRegroup = 8;

        // The singleplayer team AI's tactic horns (TacticComponent). The MP siege retreat horns differ per side.
        private const string AttackHorn = "event:/ui/mission/horns/attack";
        private const string MoveHorn = "event:/ui/mission/horns/move";
        private const string RetreatHorn = "event:/ui/mission/horns/retreat";
        private const string AttackerRetreatHorn = "event:/multiplayer/retreat_horn_attacker";
        private const string DefenderRetreatHorn = "event:/multiplayer/retreat_horn_defender";

        private static readonly Dictionary<string, int> SoundIds = new Dictionary<string, int>();

        public static bool TryGetKindOfBark(int indexOfBark, out HornSignalKind kind)
        {
            switch (indexOfBark)
            {
                case BarkAttack:
                    kind = HornSignalKind.Advance;
                    return true;
                case BarkDefend:
                    kind = HornSignalKind.Hold;
                    return true;
                case BarkRegroup:
                    kind = HornSignalKind.FallBack;
                    return true;
                default:
                    kind = HornSignalKind.Hold;
                    return false;
            }
        }

        public static HornSignalKind GetKindOfSuggestion(GeneralSuggestionKind suggestion)
        {
            switch (suggestion)
            {
                case GeneralSuggestionKind.Charge:
                case GeneralSuggestionKind.Advance:
                    return HornSignalKind.Advance;
                case GeneralSuggestionKind.FallBack:
                case GeneralSuggestionKind.Retreat:
                    return HornSignalKind.FallBack;
                default:
                    return HornSignalKind.Hold;
            }
        }

        public static string GetCallText(HornSignalKind kind)
        {
            switch (kind)
            {
                case HornSignalKind.Advance:
                    return "Advance!";
                case HornSignalKind.FallBack:
                    return "Fall back!";
                default:
                    return "Hold!";
            }
        }

        // Client: the horn's sound id, or -1 when the game has none (logged once per event).
        public static int GetSoundId(HornSignalKind kind, BattleSideEnum side)
        {
            switch (kind)
            {
                case HornSignalKind.Advance:
                    return GetSoundId(AttackHorn);
                case HornSignalKind.FallBack:
                    int sided = GetSoundId(side == BattleSideEnum.Attacker ? AttackerRetreatHorn : DefenderRetreatHorn);
                    return sided >= 0 ? sided : GetSoundId(RetreatHorn);
                default:
                    return GetSoundId(MoveHorn);
            }
        }

        private static int GetSoundId(string eventName)
        {
            if (!SoundIds.TryGetValue(eventName, out int id))
            {
                id = SoundEvent.GetEventIdFromString(eventName);
                SoundIds[eventName] = id;
                if (id < 0)
                {
                    CommanderBattleLog.Write($"Missing sound {eventName}");
                }
            }
            return id;
        }
    }
}
