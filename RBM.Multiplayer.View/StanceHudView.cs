using RBM.Multiplayer.Network;
using RBMStance;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.View
{
    // RBM's posture/stamina HUD (RBM's UI/StanceVisualLogic.cs, same prefab and view model), drawn from the values the
    // server sends (StanceNetworkClient) instead of RBM's own posture store, which only exists on the server. Also
    // shows RBM's crush-through messages, which the server can't display to a player itself.
    public class StanceHudView : StanceClientView<StanceVisualVM>
    {
        private static readonly Color MessageColor = Color.FromUint(4282569842u);

        // RBM's own order for this HUD (StanceVisualLogic).
        private const int LayerOrder = -1;

        private StanceHudUpdate _shown;

        public StanceHudView() : base("RBMStanceHud", LayerOrder, "CombatUI")
        {
        }

        protected override StanceVisualVM CreateDataSource()
        {
            return new StanceVisualVM { ShowPlayerPostureStatus = false };
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (DataSource == null || Client == null)
            {
                return;
            }
            StanceHudUpdate hud = Client.Hud;
            Agent main = Agent.Main;
            bool show = hud != null && main != null && main.IsActive();
            DataSource.ShowPlayerPostureStatus = show;
            if (!show)
            {
                DataSource.ShowEnemyStatus = false;
                _shown = null;
                return;
            }
            if (hud == _shown)
            {
                return;
            }
            _shown = hud;
            SetPlayerBars(hud);
            SetEnemyBars(hud);
        }

        // As RBM's StanceLogic.PushPlayerStanceBars: texts are rebuilt only when the numbers change.
        private void SetPlayerBars(StanceHudUpdate hud)
        {
            if (DataSource.PlayerPosture != hud.Posture)
            {
                DataSource.PlayerPosture = hud.Posture;
                DataSource.PlayerPostureText = hud.Posture.ToString();
            }
            if (DataSource.PlayerPostureMax != hud.PostureMax)
            {
                DataSource.PlayerPostureMax = hud.PostureMax;
                DataSource.PlayerPostureMaxText = hud.PostureMax.ToString();
            }
            if (DataSource.PlayerStamina != hud.Stamina)
            {
                DataSource.PlayerStamina = hud.Stamina;
                DataSource.PlayerStaminaText = hud.Stamina.ToString();
            }
            if (DataSource.PlayerStaminaMax != hud.StaminaMax)
            {
                DataSource.PlayerStaminaMax = hud.StaminaMax;
                DataSource.PlayerStaminaMaxText = hud.StaminaMax.ToString();
            }
        }

        private void SetEnemyBars(StanceHudUpdate hud)
        {
            Agent target = hud.HasTarget ? Mission.MissionNetworkHelper.GetAgentFromIndex(hud.TargetAgentIndex, canBeNull: true) : null;
            if (target == null)
            {
                DataSource.ShowEnemyStatus = false;
                return;
            }
            DataSource.EnemyPosture = hud.TargetPosture;
            DataSource.EnemyPostureMax = hud.TargetPostureMax;
            DataSource.EnemyStamina = hud.TargetStamina;
            DataSource.EnemyStaminaMax = hud.TargetStaminaMax;
            DataSource.EnemyHealth = hud.TargetHealth;
            DataSource.EnemyHealthMax = hud.TargetHealthMax;
            DataSource.EnemyName = target.IsMount
                ? target.RiderAgent?.Name + " (" + new TextObject("{=mountnoun}Mount").ToString() + ")"
                : target.Name;
            DataSource.ShowEnemyStatus = true;
        }

        // RBM's messages (MeleeBlowPatch.CrushThroughBlow, HandleDefenderChamberBlock), with RBM's string ids.
        protected override void OnStanceEvent(StanceEvent stanceEvent)
        {
            TextObject message;
            switch (stanceEvent.EventType)
            {
                case StanceEventType.OwnCrushThrough:
                    message = new TextObject("{=RBM_AI_033}Posture break: Posture depleted, {DMG} damage crushed through to the {PART}");
                    break;
                case StanceEventType.EnemyCrushThrough:
                    message = new TextObject("{=RBM_AI_034}Enemy Posture break: Posture depleted, {DMG} damage crushed through to the {PART}");
                    break;
                case StanceEventType.ChamberBlockPostureBreak:
                    message = new TextObject("{=RBM_AI_018}Posture break: Posture depleted, chamber block");
                    break;
                default:
                    return;
            }
            message.SetTextVariable("DMG", stanceEvent.Damage);
            message.SetTextVariable("PART", BodyPartName(stanceEvent.BodyPart));
            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), MessageColor));
        }

        // RBM's CrushThroughBodyPartName.
        private static TextObject BodyPartName(BoneBodyPartType bodyPart)
        {
            switch (bodyPart)
            {
                case BoneBodyPartType.Head:
                    return new TextObject("{=RBM_AI_035}head");
                case BoneBodyPartType.Neck:
                    return new TextObject("{=RBM_AI_036}neck");
                case BoneBodyPartType.Chest:
                    return new TextObject("{=RBM_AI_037}chest");
                case BoneBodyPartType.ShoulderLeft:
                    return new TextObject("{=RBM_AI_040}left shoulder");
                case BoneBodyPartType.ShoulderRight:
                    return new TextObject("{=RBM_AI_041}right shoulder");
                case BoneBodyPartType.Abdomen:
                    return new TextObject("{=RBM_AI_038}abdomen");
                default:
                    return new TextObject("{=RBM_AI_039}legs");
            }
        }
    }
}
