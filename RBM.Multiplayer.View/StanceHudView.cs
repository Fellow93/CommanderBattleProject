using RBM.Multiplayer.Network;
using RBM.Multiplayer.Stance;
using RBMStance;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace RBM.Multiplayer.View
{
    // RBM's posture/stamina HUD (RBM's UI/StanceVisualLogic.cs, same prefab and view model), drawn from the values the
    // server sends (StanceNetworkClient) instead of RBM's own posture store, which only exists on the server. Also
    // shows RBM's crush-through messages, which the server can't display to a player itself.
    public class StanceHudView : MissionLogic
    {
        private static readonly Color MessageColor = Color.FromUint(4282569842u);

        private StanceNetworkClient _client;

        private MissionScreen _missionScreen;

        private GauntletLayer _layer;

        private StanceVisualVM _dataSource;

        private StanceHudUpdate _shown;

        public override void AfterStart()
        {
            base.AfterStart();
            _client = Mission.GetMissionBehavior<StanceNetworkClient>();
            if (_client != null)
            {
                _client.EventReceived += OnStanceEvent;
            }
            TryCreateLayer();
        }

        // The mission screen may not be on top yet when the mission starts; the layer is made once it is.
        private void TryCreateLayer()
        {
            if (_layer != null || !(ScreenManager.TopScreen is MissionScreen missionScreen))
            {
                return;
            }
            _missionScreen = missionScreen;
            _dataSource = new StanceVisualVM();
            _dataSource.ShowPlayerPostureStatus = false;
            _layer = new GauntletLayer("RBMStanceHud", -1);
            _missionScreen.AddLayer(_layer);
            _layer.LoadMovie("CombatUI", _dataSource);
        }

        public override void OnRemoveBehavior()
        {
            if (_client != null)
            {
                _client.EventReceived -= OnStanceEvent;
            }
            if (_missionScreen != null && _layer != null)
            {
                _missionScreen.RemoveLayer(_layer);
            }
            _layer = null;
            _missionScreen = null;
            _dataSource?.OnFinalize();
            _dataSource = null;
            base.OnRemoveBehavior();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            TryCreateLayer();
            if (_dataSource == null || _client == null)
            {
                return;
            }
            StanceHudUpdate hud = _client.Hud;
            Agent main = Agent.Main;
            bool show = hud != null && main != null && main.IsActive();
            _dataSource.ShowPlayerPostureStatus = show;
            if (!show)
            {
                _dataSource.ShowEnemyStatus = false;
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
            if (_dataSource.PlayerPosture != hud.Posture)
            {
                _dataSource.PlayerPosture = hud.Posture;
                _dataSource.PlayerPostureText = hud.Posture.ToString();
            }
            if (_dataSource.PlayerPostureMax != hud.PostureMax)
            {
                _dataSource.PlayerPostureMax = hud.PostureMax;
                _dataSource.PlayerPostureMaxText = hud.PostureMax.ToString();
            }
            if (_dataSource.PlayerStamina != hud.Stamina)
            {
                _dataSource.PlayerStamina = hud.Stamina;
                _dataSource.PlayerStaminaText = hud.Stamina.ToString();
            }
            if (_dataSource.PlayerStaminaMax != hud.StaminaMax)
            {
                _dataSource.PlayerStaminaMax = hud.StaminaMax;
                _dataSource.PlayerStaminaMaxText = hud.StaminaMax.ToString();
            }
        }

        private void SetEnemyBars(StanceHudUpdate hud)
        {
            Agent target = hud.HasTarget ? Mission.MissionNetworkHelper.GetAgentFromIndex(hud.TargetAgentIndex, canBeNull: true) : null;
            if (target == null)
            {
                _dataSource.ShowEnemyStatus = false;
                return;
            }
            _dataSource.EnemyPosture = hud.TargetPosture;
            _dataSource.EnemyPostureMax = hud.TargetPostureMax;
            _dataSource.EnemyStamina = hud.TargetStamina;
            _dataSource.EnemyStaminaMax = hud.TargetStaminaMax;
            _dataSource.EnemyHealth = hud.TargetHealth;
            _dataSource.EnemyHealthMax = hud.TargetHealthMax;
            _dataSource.EnemyName = target.IsMount
                ? target.RiderAgent?.Name + " (" + new TextObject("{=mountnoun}Mount").ToString() + ")"
                : target.Name;
            _dataSource.ShowEnemyStatus = true;
        }

        // RBM's messages (MeleeBlowPatch.CrushThroughBlow, HandleDefenderChamberBlock), with RBM's string ids.
        private void OnStanceEvent(StanceEvent stanceEvent)
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
