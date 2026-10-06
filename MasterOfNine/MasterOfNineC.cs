using BlueprintCore.Actions.Builder;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using BlueprintCore.Utils.Types;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using System;
using VoidHeadWOTRNineSwords.Common;
using VoidHeadWOTRNineSwords.Swordsage;
using VoidHeadWOTRNineSwords.Warblade;

namespace VoidHeadWOTRNineSwords.MasterOfNine
{
  public static class MasterOfNineC
  {
    public const string Guid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E01";
    public const string PerfectFormGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E05";
    public const string AdaptiveStyleGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E07";

    const string ProgressionGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E02";
    const string DisciplinesGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E03";
    const string DuelingStanceGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E04";
    const string StrikeGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E06";
    const string AdaptiveAbilityGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E08";
    const string AdaptiveResourceGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E09";
    const string PreparedSlotGuid = "7C4E1A90-2B6D-4F18-9A55-0C1E8A7B6E0A";

    public static void ConfigureClass()
    {
      Main.Logger.Info($"{nameof(MasterOfNineC)} configuring");
      ConfigureAdaptiveStyle();

      var icon = FeatureRefs.Dodge.Reference.Get().Icon;
      var disciplines = FeatureConfigurator.New("MasterOfNineDisciplines", DisciplinesGuid)
        .SetDisplayName("MasterOfNineDisciplines.Name")
        .SetDescription("MasterOfNineDisciplines.Desc")
        .SetIcon(icon)
        .SetIsClassFeature()
        .AddFacts(new()
        {
          DisciplineProficencies.DesertWindProficencyGuid,
          DisciplineProficencies.DiamondMindProficencyGuid,
          DisciplineProficencies.IronHeartProficencyGuid,
          DisciplineProficencies.ShadowHandProficencyGuid,
          DisciplineProficencies.StoneDragonProficencyGuid,
          DisciplineProficencies.TigerClawProficencyGuid,
          DisciplineProficencies.WhiteRavenProficencyGuid,
          DisciplineProficencies.RivenHourglassProficencyGuid
        })
        .Configure();

      var preparedSlot = FeatureConfigurator.New("MasterPreparedSlot", PreparedSlotGuid)
        .SetDisplayName("MasterPreparedSlot.Name")
        .SetDescription("MasterPreparedSlot.Desc")
        .SetIcon(icon)
        .SetIsClassFeature()
        .SetRanks(5)
        .Configure();

      var dueling = FeatureConfigurator.New("DuelingStance", DuelingStanceGuid)
        .SetDisplayName("DuelingStance.Name")
        .SetDescription("DuelingStance.Desc")
        .SetIcon(icon)
        .SetIsClassFeature()
        .AddIncreaseActivatableAbilityGroupSize(Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityGroup.CombatStyle)
        .Configure();

      var perfect = FeatureConfigurator.New("PerfectForm", PerfectFormGuid)
        .SetDisplayName("PerfectForm.Name")
        .SetDescription("PerfectForm.Desc")
        .SetIcon(icon)
        .SetIsClassFeature()
        .Configure();

      var strike = FeatureConfigurator.New("MasterOfNineStrike", StrikeGuid)
        .SetDisplayName("MasterOfNineStrike.Name")
        .SetDescription("MasterOfNineStrike.Desc")
        .SetIcon(icon)
        .SetIsClassFeature()
        .AddComponent<MasterOfNineStrikeBonus>()
        .Configure();

      var bonusFeat = FeatureSelectionRefs.FighterFeatSelection.Reference.Guid;
      var entries = LevelEntryBuilder.New()
        .AddEntry(1, disciplines.AssetGuid, preparedSlot.AssetGuid)
        .AddEntry(2, dueling.AssetGuid, bonusFeat, preparedSlot.AssetGuid)
        .AddEntry(3, perfect.AssetGuid, preparedSlot.AssetGuid)
        .AddEntry(4, bonusFeat, preparedSlot.AssetGuid)
        .AddEntry(5, strike.AssetGuid, preparedSlot.AssetGuid);

      var progression = ProgressionConfigurator.New("MasterOfNineProgression", ProgressionGuid)
        .SetRanks(1)
        .SetLevelEntries(entries)
        .SetClasses(Guid)
        .Configure();

      var master = CharacterClassConfigurator.New("MasterOfNine", Guid)
        .SetLocalizedName("MasterOfNineC.Name")
        .SetLocalizedDescription("MasterOfNineC.Desc")
        .SetIcon(icon)
        .SetSkillPoints(6)
        .SetHitDie(DiceType.D8)
        .SetPrestigeClass(true)
        .SetHideIfRestricted(false)
        .SetIsArcaneCaster(false)
        .SetIsDivineCaster(false)
        .SetBaseAttackBonus(StatProgressionRefs.BABMedium.Reference.Get())
        .SetFortitudeSave(StatProgressionRefs.SavesLow.Reference.Get())
        .SetReflexSave(StatProgressionRefs.SavesLow.Reference.Get())
        .SetWillSave(StatProgressionRefs.SavesHigh.Reference.Get())
        .AddToClassSkills(StatType.SkillAthletics, StatType.SkillMobility, StatType.SkillPersuasion, StatType.SkillPerception, StatType.SkillKnowledgeWorld, StatType.SkillStealth)
        .SetProgression(progression)
        .AddToRecommendedAttributes(StatType.Strength, StatType.Dexterity, StatType.Wisdom)
        .AddToNotRecommendedAttributes(StatType.Intelligence, StatType.Charisma)
        .AddPrerequisiteIsPet(not: true)
        .AddPrerequisiteCharacterLevel(15)
        .AddPrerequisiteClassLevel(WarbladeC.Guid, 1, group: Prerequisite.GroupType.Any)
        .AddPrerequisiteClassLevel(SwordsageC.Guid, 1, group: Prerequisite.GroupType.Any)
        .AddPrerequisiteFeaturesFromList(amount: 6, features: new()
        {
          DisciplineProficencies.DesertWindProficencyGuid,
          DisciplineProficencies.DiamondMindProficencyGuid,
          DisciplineProficencies.IronHeartProficencyGuid,
          DisciplineProficencies.ShadowHandProficencyGuid,
          DisciplineProficencies.StoneDragonProficencyGuid,
          DisciplineProficencies.TigerClawProficencyGuid,
          DisciplineProficencies.WhiteRavenProficencyGuid,
          DisciplineProficencies.RivenHourglassProficencyGuid
        })
        .AddPrerequisiteFeature(FeatureRefs.Dodge.ToString())
        .AddPrerequisiteFeature(FeatureRefs.Improved_Initiative.ToString())
        .AddPrerequisiteFeature(FeatureRefs.ImprovedUnarmedStrike.ToString())
        .AddPrerequisiteFeature(FeatureRefs.BlindFight.ToString())
        .AddPrerequisiteFeature(AdaptiveStyleGuid)
        .AddPrerequisiteStatValue(StatType.SkillAthletics, 5)
        .AddPrerequisiteStatValue(StatType.SkillMobility, 5)
        .AddPrerequisiteStatValue(StatType.SkillPersuasion, 5)
        .AddPrerequisiteStatValue(StatType.SkillKnowledgeWorld, 5)
        .AddComponent(new PrerequisiteInitiatorLevel { Level = 6 })
        .SetStartingGold(0)
        .SetPrimaryColor(11)
        .SetSecondaryColor(47)
        .SetDifficulty(6)
        .AddToMaleEquipmentEntities("65e7ae8b40be4d64ba07d50871719259", "04244d527b8a1f14db79374bc802aaaa")
        .AddToFemaleEquipmentEntities("11266d19b35cb714d96f4c9de08df48e", "64abd9c4d6565de419f394f71a2d496f")
        .Configure();

      var levelCap = new PrerequisiteClassLevel
      {
        Level = 5,
        Not = true
      };
      levelCap.m_CharacterClass = master.ToReference<BlueprintCharacterClassReference>();
      master.AddComponents(levelCap);

      var root = BlueprintTool.Get<BlueprintRoot>("2d77316c72b9ed44f888ceefc2a131f6");
      root.Progression.m_CharacterClasses = CommonTool.Append(root.Progression.m_CharacterClasses, master.ToReference<BlueprintCharacterClassReference>());
      Main.Logger.Info($"{nameof(MasterOfNineC)} done");
    }

    static void ConfigureAdaptiveStyle()
    {
      var icon = AbilityRefs.Restoration.Reference.Get().Icon;
      var resource = AbilityResourceConfigurator.New("AdaptiveStyleResource", AdaptiveResourceGuid)
        .SetMaxAmount(new BlueprintAbilityResource.Amount { BaseValue = 1 })
        .Configure();

      var ability = AbilityConfigurator.New("AdaptiveStyleAbility", AdaptiveAbilityGuid)
        .SetDisplayName("AdaptiveStyle.Name")
        .SetDescription("AdaptiveStyle.Desc")
        .SetIcon(icon)
        .SetRange(AbilityRange.Personal)
        .SetCanTargetSelf(true)
        .SetActionType(UnitCommand.CommandType.Standard)
        .SetIsFullRoundAction(true)
        .AddAbilityEffectRunAction(ActionsBuilder.New().Add<AdaptiveStyleAction>())
        .AddAbilityResourceLogic(1, requiredResource: AdaptiveResourceGuid, isSpendResource: true)
        .Configure();

      FeatureConfigurator.New("AdaptiveStyle", AdaptiveStyleGuid, FeatureGroup.Feat, FeatureGroup.CombatFeat)
        .SetDisplayName("AdaptiveStyle.Name")
        .SetDescription("AdaptiveStyle.Desc")
        .SetIcon(icon)
        .SetIsClassFeature(false)
        .AddFeatureTagsComponent(FeatureTag.Attack | FeatureTag.Melee)
        .AddPrerequisiteClassLevel(WarbladeC.Guid, 1, group: Prerequisite.GroupType.Any)
        .AddPrerequisiteClassLevel(SwordsageC.Guid, 1, group: Prerequisite.GroupType.Any)
        .AddFacts(new() { ability })
        .AddAbilityResources(resource: resource, restoreAmount: true)
        .AddToFeatureSelection(
          FeatureSelectionRefs.BasicFeatSelection.ToString(),
          FeatureSelectionRefs.FighterFeatSelection.ToString())
        .Configure();
    }
  }

  [AllowedOn(typeof(BlueprintCharacterClass))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d37")]
  public class PrerequisiteInitiatorLevel : Prerequisite
  {
    public int Level;

    public override bool CheckInternal(FeatureSelectionState selectionState, UnitDescriptor unit, LevelUpState state)
    {
      var entity = unit?.Unit;
      if (entity == null)
        return false;

      return MartialAdeptMulticlass.InitiatorLevel(entity) >= Level;
    }

    public override string GetUITextInternal(UnitDescriptor unit)
    {
      return "Initiator Level " + Level;
    }
  }

  [AllowedOn(typeof(BlueprintAbility))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d39")]
  public class AdaptiveStyleAction : ContextAction
  {
    public override string GetCaption()
    {
      return "Adaptive style";
    }

    public override void RunAction()
    {
      var caster = Context?.MaybeCaster;
      if (caster == null)
        return;

      ManeuverPreparation.ClearPrepared(caster);
    }
  }

  [AllowedOn(typeof(BlueprintFeature))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d38")]
  public class MasterOfNineStrikeBonus : UnitFactComponentDelegate, IInitiatorRulebookHandler<RuleAttackRoll>, IInitiatorRulebookSubscriber
  {
    public void OnEventAboutToTrigger(RuleAttackRoll evt)
    {
      if (IsManeuverStrike(evt))
        evt.AttackBonusPenalty -= 2;
    }

    public void OnEventDidTrigger(RuleAttackRoll evt)
    {
      if (evt == null || !evt.IsHit || evt.Target == null || !IsManeuverStrike(evt))
        return;

      int bonus = ManeuverPreparation.PreparedDisciplineCount(evt.Initiator);
      if (bonus <= 0)
        return;

      Game.Instance.Rulebook.TriggerEvent(new RuleDealDamage(evt.Initiator, evt.Target, new DirectDamage(new DiceFormula(0, DiceType.D6), bonus)));
    }

    static bool IsManeuverStrike(RulebookEvent evt)
    {
      BlueprintAbility ability = null;
      var data = evt?.Reason?.Ability;
      if (data?.Fact?.Blueprint is BlueprintAbility fromFact)
        ability = fromFact;
      else if (evt?.Reason?.Context?.SourceAbility is BlueprintAbility fromContext)
        ability = fromContext;

      return ManeuverPreparation.SpendsManeuverPoints(ability);
    }
  }
}
