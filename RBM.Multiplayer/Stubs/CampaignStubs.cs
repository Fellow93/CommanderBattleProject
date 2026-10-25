using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

// Compile-only stand-ins for the campaign types RBM's AI and Stance code mentions. In multiplayer there is no
// campaign: Campaign.Current is null, no agent's Character is a CharacterObject and no party or map event exists,
// so RBM's own null checks take the non-campaign path and nothing here is ever really used. Members return the
// neutral value (no perk bonus, factor 1) in case a code path reaches them anyway.

namespace TaleWorlds.CampaignSystem
{
    public sealed class Campaign
    {
        public static Campaign Current => null;

        public CampaignModels Models { get; } = new CampaignModels();
    }

    public sealed class CampaignModels
    {
        public CharacterDevelopmentModel CharacterDevelopmentModel { get; } = new CharacterDevelopmentModel();
    }

    public sealed class CharacterDevelopmentModel
    {
        public int MinSkillRequiredForEpicPerkBonus => int.MaxValue;
    }

    public static class CampaignOptions
    {
        public enum Difficulty
        {
            VeryEasy,
            Easy,
            Realistic
        }

        public static Difficulty CombatAIDifficulty => Difficulty.Realistic;
    }

    // Never instantiated: `agent.Character as CharacterObject` is always null in multiplayer.
    public class CharacterObject : BasicCharacterObject
    {
        public int Tier => 0;

        public Hero HeroObject => null;
    }

    public class Hero
    {
        public int GetTraitLevel(TraitObject trait)
        {
            return 0;
        }
    }

    public interface IFaction
    {
        bool IsBanditFaction { get; }
    }

    public struct ExplainedNumber
    {
        public ExplainedNumber(float baseNumber = 0f, bool includeDescriptions = false, TextObject baseText = null)
        {
            ResultNumber = baseNumber;
            IncludeDescriptions = includeDescriptions;
        }

        public float ResultNumber { get; private set; }

        public bool IncludeDescriptions { get; }

        public void Add(float value, TextObject description = null, TextObject variable = null)
        {
            ResultNumber += value;
        }

        public void AddFactor(float value, TextObject description = null)
        {
            ResultNumber += ResultNumber * value;
        }
    }
}

namespace TaleWorlds.CampaignSystem.CharacterDevelopment
{
    public sealed class PerkObject
    {
    }

    public sealed class TraitObject
    {
    }

    public enum SkillEffectRole
    {
        None
    }

    public sealed class SkillEffect
    {
        public EffectIncrementType IncrementType => EffectIncrementType.Add;

        public SkillEffectRole Role => SkillEffectRole.None;

        public float GetSkillEffectValue(int skillLevel)
        {
            return 0f;
        }
    }

    public static class DefaultSkillEffects
    {
        public static SkillEffect OneHandedSpeed { get; } = new SkillEffect();

        public static SkillEffect TwoHandedSpeed { get; } = new SkillEffect();

        public static SkillEffect PolearmSpeed { get; } = new SkillEffect();

        public static SkillEffect CrossbowReloadSpeed { get; } = new SkillEffect();

        public static SkillEffect ThrowingSpeed { get; } = new SkillEffect();

        public static SkillEffect CrouchedSpeed { get; } = new SkillEffect();
    }

    public static class DefaultTraits
    {
        public static TraitObject Valor { get; } = new TraitObject();

        public static TraitObject Calculating { get; } = new TraitObject();
    }

    public static class DefaultPerks
    {
        public static class Throwing
        {
            public static PerkObject PerfectTechnique { get; } = new PerkObject();

            public static PerkObject UnstoppableForce { get; } = new PerkObject();
        }

        public static class Athletics
        {
            public static PerkObject Fury { get; } = new PerkObject();
        }

        public static class OneHanded
        {
            public static PerkObject WrappedHandles { get; } = new PerkObject();

            public static PerkObject SteelCoreShields { get; } = new PerkObject();

            public static PerkObject ShieldWall { get; } = new PerkObject();
        }

        public static class TwoHanded
        {
            public static PerkObject StrongGrip { get; } = new PerkObject();
        }

        public static class Polearm
        {
            public static PerkObject CounterWeight { get; } = new PerkObject();
        }

        public static class Engineering
        {
            public static PerkObject Scaffolds { get; } = new PerkObject();
        }
    }
}

namespace TaleWorlds.CampaignSystem.Party
{
    public class PartyBase
    {
        public IFaction MapFaction => null;

        public MobileParty MobileParty => null;
    }

    public class MobileParty
    {
        public static MobileParty MainParty => null;

        public MapEvent MapEvent => null;

        public bool IsBandit => false;
    }
}

namespace TaleWorlds.CampaignSystem.MapEvents
{
    public class MapEvent
    {
        public static MapEvent PlayerMapEvent => null;

        public MapEventSide DefenderSide => null;

        public MapEventSide AttackerSide => null;
    }

    public class MapEventSide
    {
        public TaleWorlds.CampaignSystem.Party.PartyBase LeaderParty => null;
    }
}

namespace Helpers
{
    public static class PerkHelper
    {
        public static void AddPerkBonusForCharacter(PerkObject perk, BattleEnvironment battleEnvironment, CharacterObject character, bool isPrimaryBonus, ref ExplainedNumber bonuses)
        {
        }

        public static void AddPerkBonusFromCaptain(PerkObject perk, BattleEnvironment battleEnvironment, CharacterObject captainCharacter, ref ExplainedNumber bonuses)
        {
        }

        public static void AddEpicPerkBonusForCharacter(PerkObject perk, BattleEnvironment battleEnvironment, CharacterObject character, SkillObject skillType, bool applyPrimaryBonus, ref ExplainedNumber bonuses, int skillRequired)
        {
        }
    }

    public static class SkillHelper
    {
        public static void AddSkillBonusForSkillLevel(SkillEffect skillEffect, ref ExplainedNumber explainedNumber, int skillLevel)
        {
        }
    }
}

namespace SandBox.GameComponents
{
    // Only the target of an RBM patch class that RBM.Multiplayer never applies.
    public abstract class SandboxAgentStatCalculateModel : AgentStatCalculateModel
    {
    }
}

namespace SandBox.Missions.MissionLogics
{
    // Never added to a multiplayer mission.
    public class CampaignSiegeStateHandler : MissionLogic
    {
    }
}
