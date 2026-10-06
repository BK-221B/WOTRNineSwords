using BlueprintCore.Blueprints.Configurators.Facts;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using System;
using VoidHeadWOTRNineSwords.MasterOfNine;
using VoidHeadWOTRNineSwords.Swordsage;
using VoidHeadWOTRNineSwords.Warblade;

namespace VoidHeadWOTRNineSwords.Common
{
  static class MartialAdeptMulticlass
  {
    public const string WarbladeStyleGuid = "4A7C1E2B-9F30-4C1A-8B6D-1E5A0C7D2F11";
    public const string SwordsageStyleGuid = "4A7C1E2B-9F30-4C1A-8B6D-1E5A0C7D2F12";
    const string PoolBonusGuid = "4A7C1E2B-9F30-4C1A-8B6D-1E5A0C7D2F13";
    const string PoolPenaltyGuid = "4A7C1E2B-9F30-4C1A-8B6D-1E5A0C7D2F14";

    static readonly int[] WarbladeExtraLevels = { 4, 10, 15, 20 };
    static readonly int[] SwordsageExtraLevels = { 1, 3, 5, 8, 10, 13, 15, 18, 20 };
    static readonly string[] InitiatorTierGuids =
    {
      InitiatorLevels.Lvl1Guid,
      InitiatorLevels.Lvl2Guid,
      InitiatorLevels.Lvl3Guid,
      InitiatorLevels.Lvl4Guid,
      InitiatorLevels.Lvl5Guid,
      InitiatorLevels.Lvl6Guid,
      InitiatorLevels.Lvl7Guid,
      InitiatorLevels.Lvl8Guid,
      InitiatorLevels.Lvl9Guid
    };

    static BlueprintUnitFact _warbladeStyle;
    static BlueprintUnitFact _swordsageStyle;
    static BlueprintFeature _poolBonus;
    static BlueprintFeature _poolPenalty;
    static BlueprintGuid[] _initiatorTierIds;
    static bool _syncing;

    public static void Configure()
    {
      Main.Logger.Info($"Configuring {nameof(MartialAdeptMulticlass)}");

      _warbladeStyle = UnitFactConfigurator.New("RecoveryStyleWarblade", WarbladeStyleGuid).Configure();
      _swordsageStyle = UnitFactConfigurator.New("RecoveryStyleSwordsage", SwordsageStyleGuid).Configure();

      _poolBonus = FeatureConfigurator.New("ManeuverPoolBonus", PoolBonusGuid)
        .SetHideInUI(true)
        .SetRanks(20)
        .Configure();

      _poolPenalty = FeatureConfigurator.New("ManeuverPoolPenalty", PoolPenaltyGuid)
        .SetHideInUI(true)
        .SetRanks(20)
        .Configure();

      _initiatorTierIds = new BlueprintGuid[InitiatorTierGuids.Length];
      for (int i = 0; i < InitiatorTierGuids.Length; i++)
        _initiatorTierIds[i] = BlueprintGuid.Parse(InitiatorTierGuids[i]);
    }

    public static void TryPatchInitiatorPrerequisite(Harmony harmony)
    {
      try
      {
        var method = AccessTools.Method(typeof(PrerequisiteFeature), "CheckInternal")
                     ?? AccessTools.Method(typeof(PrerequisiteFeature), "Check");
        if (method == null)
        {
          Main.Logger.Error("Initiator prerequisite method was not found. Combined initiator level will rely on granted features.");
          return;
        }

        harmony.Patch(method, postfix: new HarmonyMethod(typeof(MartialAdeptMulticlass), nameof(InitiatorPrerequisitePostfix)));
        Main.Logger.Info("Patched initiator level prerequisites for multiclass.");
      }
      catch (Exception e)
      {
        Main.Logger.Error("Failed to patch initiator prerequisites", e);
      }
    }

    public static void InitiatorPrerequisitePostfix(PrerequisiteFeature __instance, UnitEntityData unit, ref bool __result)
    {
      if (__result || unit == null || __instance == null || __instance.m_Feature == null)
        return;

      int initiator = InitiatorLevel(unit);
      if (initiator <= 0)
        return;

      int requiredTier = TierOf(__instance.m_Feature.Guid);
      if (requiredTier > 0 && ManeuverTier(initiator) >= requiredTier)
        __result = true;
    }

    public static void EnsureRecoveryStyle(UnitEntityData unit, bool warblade)
    {
      if (unit == null || HasRecoveryStyle(unit))
        return;

      unit.AddFact(warblade ? _warbladeStyle : _swordsageStyle);
    }

    public static void Sync(UnitEntityData unit)
    {
      if (_syncing || unit?.Progression == null)
        return;

      _syncing = true;
      try
      {
        EnsureInitiatorFeatures(unit);
        SyncPool(unit);
      }
      catch (Exception e)
      {
        Main.Logger.Error("Failed to sync multiclass martial adept", e);
      }
      finally
      {
        _syncing = false;
      }
    }

    static bool HasRecoveryStyle(UnitEntityData unit)
    {
      return unit.HasFact(_warbladeStyle) || unit.HasFact(_swordsageStyle);
    }

    static int ClassLevel(UnitEntityData unit, string classGuid)
    {
      var characterClass = BlueprintTool.Get<BlueprintCharacterClass>(classGuid);
      return unit.Progression.GetClassLevel(characterClass);
    }

    public static int InitiatorLevel(UnitEntityData unit)
    {
      return ClassLevel(unit, WarbladeC.Guid) + ClassLevel(unit, SwordsageC.Guid) + ClassLevel(unit, MasterOfNineC.Guid);
    }

    public static int ManeuverTier(int initiatorLevel)
    {
      if (initiatorLevel <= 0)
        return 0;
      return Math.Min(9, (initiatorLevel + 1) / 2);
    }

    static int TierOf(BlueprintGuid guid)
    {
      if (_initiatorTierIds == null)
        return 0;

      for (int i = 0; i < _initiatorTierIds.Length; i++)
      {
        if (_initiatorTierIds[i] == guid)
          return i + 1;
      }
      return 0;
    }

    static void EnsureInitiatorFeatures(UnitEntityData unit)
    {
      int warbladeLevel = ClassLevel(unit, WarbladeC.Guid);
      int swordsageLevel = ClassLevel(unit, SwordsageC.Guid);
      int initiator = InitiatorLevel(unit);
      if (initiator <= 0)
        return;

      int fromClasses = 0;
      if (warbladeLevel > 0)
        fromClasses = Math.Max(fromClasses, ManeuverTier(warbladeLevel));
      if (swordsageLevel > 0)
        fromClasses = Math.Max(fromClasses, ManeuverTier(swordsageLevel));
      int combined = ManeuverTier(initiator);
      for (int tier = fromClasses + 1; tier <= combined; tier++)
      {
        var feature = BlueprintTool.Get<BlueprintFeature>(InitiatorTierGuids[tier - 1]);
        if (!unit.HasFact(feature))
          unit.AddFact(feature);
      }
    }

    static void SyncPool(UnitEntityData unit)
    {
      int warbladeLevel = ClassLevel(unit, WarbladeC.Guid);
      int swordsageLevel = ClassLevel(unit, SwordsageC.Guid);
      if (warbladeLevel <= 0 || swordsageLevel <= 0)
        return;
      if (!unit.HasFact(_warbladeStyle) && !unit.HasFact(_swordsageStyle))
        return;

      int totalLevel = warbladeLevel + swordsageLevel;
      bool warbladeFirst = unit.HasFact(_warbladeStyle);
      int desiredExtra = ExtraUses(warbladeFirst ? WarbladeExtraLevels : SwordsageExtraLevels, totalLevel);
      int progressionExtra = Rank(unit, BlueprintTool.Get<BlueprintFeature>(ManeuverResources.IncreaseManeuverUsesGuid));
      int delta = desiredExtra - progressionExtra;

      SetAdjustmentRank(unit, _poolBonus, Math.Max(0, delta));
      SetAdjustmentRank(unit, _poolPenalty, Math.Max(0, -delta));
    }

    public static int ExtraPool(UnitEntityData unit)
    {
      if (unit?.Progression == null)
        return 0;

      int warbladeLevel = ClassLevel(unit, WarbladeC.Guid);
      int swordsageLevel = ClassLevel(unit, SwordsageC.Guid);
      if (warbladeLevel <= 0 && swordsageLevel <= 0)
        return 0;

      if (warbladeLevel > 0 && swordsageLevel > 0)
      {
        bool warbladeFirst = unit.HasFact(_warbladeStyle) || !unit.HasFact(_swordsageStyle);
        return ExtraUses(warbladeFirst ? WarbladeExtraLevels : SwordsageExtraLevels, warbladeLevel + swordsageLevel);
      }

      if (warbladeLevel > 0)
        return ExtraUses(WarbladeExtraLevels, warbladeLevel);
      return ExtraUses(SwordsageExtraLevels, swordsageLevel);
    }

    static int ExtraUses(int[] levels, int totalLevel)
    {
      int count = 0;
      for (int i = 0; i < levels.Length; i++)
      {
        if (totalLevel >= levels[i])
          count++;
      }
      return count;
    }

    static int Rank(UnitEntityData unit, BlueprintFeature feature)
    {
      if (unit.GetFact(feature) is Feature fact)
        return fact.Rank;
      return 0;
    }

    static void SetAdjustmentRank(UnitEntityData unit, BlueprintFeature feature, int rank)
    {
      var fact = unit.GetFact(feature) as Feature;
      if (rank <= 0)
      {
        if (fact != null)
          unit.RemoveFact(fact);
        return;
      }

      if (fact == null)
        fact = unit.AddFact(feature) as Feature;
      if (fact == null || fact.Rank == rank)
        return;

      fact.TurnOff();
      fact.SetRank(rank);
      fact.TurnOn();
    }
  }

  [AllowedOn(typeof(BlueprintAbility))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d23")]
  public class ShowRecoveryForStyle : BlueprintComponent, IAbilityVisibilityProvider
  {
    public bool UseWarbladeRecovery;

    public bool IsAbilityVisible(AbilityData ability)
    {
      var caster = ability?.Caster;
      if (caster == null)
        return true;

      bool warblade = caster.HasFact(BlueprintTool.Get<BlueprintUnitFact>(MartialAdeptMulticlass.WarbladeStyleGuid));
      bool swordsage = caster.HasFact(BlueprintTool.Get<BlueprintUnitFact>(MartialAdeptMulticlass.SwordsageStyleGuid));
      if (!warblade && !swordsage)
        return true;

      return UseWarbladeRecovery ? warblade : swordsage;
    }
  }

  [AllowedOn(typeof(BlueprintUnitFact))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d21")]
  public class LockRecoveryStyle : UnitFactComponentDelegate
  {
    public bool UseWarbladeRecovery;

    public override void OnTurnOn()
    {
      MartialAdeptMulticlass.EnsureRecoveryStyle(Owner, UseWarbladeRecovery);
      MartialAdeptMulticlass.Sync(Owner);
    }
  }

  [AllowedOn(typeof(BlueprintUnitFact))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d3a")]
  public class ManeuverPoolAmount : UnitFactComponentDelegate, IResourceAmountBonusHandler
  {
    public void CalculateMaxResourceAmount(BlueprintAbilityResource resource, ref int amount)
    {
      if (resource == null || resource.AssetGuid != BlueprintGuid.Parse(ManeuverResources.ManeuverResourceGuid))
        return;

      amount += MartialAdeptMulticlass.ExtraPool(Owner);
    }

    public override void OnTurnOn()
    {
      var resource = ManeuverResources.ManeuverResource;
      if (resource == null || Owner?.Resources == null || Owner.IsInCombat)
        return;
      if (!Owner.Resources.HasMaxAmount(resource))
        Owner.Resources.Restore(resource);
    }
  }

  [AllowedOn(typeof(BlueprintUnitFact))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d22")]
  public class MartialAdeptSync : UnitFactComponentDelegate, IOwnerGainLevelHandler
  {
    public override void OnTurnOn()
    {
      MartialAdeptMulticlass.Sync(Owner);
    }

    public void HandleUnitGainLevel()
    {
      MartialAdeptMulticlass.Sync(Owner);
    }
  }
}
