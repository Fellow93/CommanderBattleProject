using RBM.Multiplayer;
using TaleWorlds.MountAndBlade;

namespace RBM.Multiplayer.View
{
    public class RbmMpViewSubModule : MBSubModuleBase
    {
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (!GameNetwork.IsClient || !RbmMpSubModule.IsCommanderBattle(mission) || !RBMConfig.RBMConfig.rbmStanceEnabled)
            {
                return;
            }
            mission.AddMissionBehavior(new StanceHudView());
            mission.AddMissionBehavior(new ExhaustionView());
        }
    }
}
