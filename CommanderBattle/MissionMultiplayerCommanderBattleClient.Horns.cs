using CommanderBattle.Network;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace CommanderBattle
{
    // A general's horn call (docs/plans/horn-signals.md): his side and spectators hear it and see the call; the enemy
    // only hears it, from where he stands, which gives him away.
    public partial class MissionMultiplayerCommanderBattleClient
    {
        private void HandleServerEventHornSignal(GameNetworkMessage baseMessage)
        {
            HornSignal signal = (HornSignal)baseMessage;
            BattleSideEnum mySide = CommanderBattleTeams.MySide;
            int soundId = HornSignals.GetSoundId(signal.Kind, signal.Side);
            bool isEnemy = mySide != BattleSideEnum.None && mySide != signal.Side;
            if (isEnemy)
            {
                if (soundId >= 0)
                {
                    MBSoundEvent.PlaySound(soundId, signal.Position);
                }
                return;
            }
            PlayHorn(soundId);
            string text = $"The general sounds: {HornSignals.GetCallText(signal.Kind)}";
            MBInformationManager.AddQuickInformation(new TextObject("{=!}{TEXT}").SetTextVariable("TEXT", text));
            InformationManager.DisplayMessage(new InformationMessage(text, SuggestionColor));
        }

        // A missing horn falls back to the alert, so the call is never silent.
        private static void PlayHorn(int soundId)
        {
            if (soundId >= 0)
            {
                SoundEvent.PlaySound2D(soundId);
            }
            else
            {
                SoundEvent.PlaySound2D(SuggestionSound);
            }
        }
    }
}
