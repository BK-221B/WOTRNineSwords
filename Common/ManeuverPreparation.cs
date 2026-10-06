using BlueprintCore.Actions.Builder;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.Blueprints.Classes.Selection;
using HarmonyLib;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using System;
using System.Collections.Generic;
using VoidHeadWOTRNineSwords.Components;
using VoidHeadWOTRNineSwords.MasterOfNine;
using VoidHeadWOTRNineSwords.Swordsage;
using VoidHeadWOTRNineSwords.Warblade;

namespace VoidHeadWOTRNineSwords.Common
{
  static class ManeuverPreparation
  {
    public const string CountFeatureGuid = "C3A91E70-6B24-4F18-9D55-0C1E8A7B6D40";
    const string PrepareAbilityGuid = "C3A91E70-6B24-4F18-9D55-0C1E8A7B6D30";
    static readonly int[] WarbladePrepared = { 0, 3, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };
    static readonly RestHandler Handler = new RestHandler();
    static readonly List<PreparedManeuver> Entries = new List<PreparedManeuver>();
    static bool _subscribed;
    static bool _ranking;

    public static void ConfigureCountFeature()
    {
      FeatureConfigurator.New("PreparedManeuverCount", CountFeatureGuid)
        .SetDisplayName("PreparedManeuverCount.Name")
        .SetDescription("PreparedManeuverCount.Desc")
        .SetIsClassFeature()
        .SetRanks(40)
        .AddFacts([ManeuverResources.ManeuverResourceFactGuid])
        .AddComponent<SyncPreparedManeuverCount>()
        .Configure();
    }

    public static void Subscribe()
    {
      if (_subscribed)
        return;

      _subscribed = true;
      EventBus.Subscribe(Handler);
    }

    public static void Configure()
    {
      Main.Logger.Info($"Configuring {nameof(ManeuverPreparation)}");
      Entries.Clear();

      int features = 0;
      int withFacts = 0;
      int refs = 0;
      int resolved = 0;
      foreach (var source in KnownFeatures())
      {
        features++;
        var feature = BlueprintTool.Get<BlueprintFeature>(source.ToString());
        AddFacts granted = null;
        if (feature?.ComponentsArray != null)
        {
          foreach (var component in feature.ComponentsArray)
          {
            if (component is AddFacts facts)
              granted = facts;
          }
        }
        if (granted?.m_Facts == null)
          continue;

        withFacts++;
        foreach (var factRef in granted.m_Facts)
        {
          if (factRef == null)
            continue;

          refs++;
          BlueprintUnitFact grantedFact = factRef.Get();
          if (grantedFact == null)
          {
            try
            {
              var guid = factRef.Guid != BlueprintGuid.Empty ? factRef.Guid : factRef.deserializedGuid;
              if (guid == BlueprintGuid.Empty)
                continue;
              grantedFact = BlueprintTool.Get<BlueprintUnitFact>(guid.ToString());
            }
            catch (InvalidOperationException)
            {
              continue;
            }
          }

          resolved++;
          if (grantedFact is BlueprintAbility spending && SpendsManeuverPoints(spending))
            Register(feature, spending);
          else if (grantedFact is BlueprintActivatableAbility activatable && !IsStance(feature))
            Register(feature, activatable);
        }
      }

      Main.Logger.Info($"preparation scan features={features} withFacts={withFacts} refs={refs} resolved={resolved} spenders={Entries.Count}");

      if (Entries.Count == 0)
      {
        Main.Logger.Error("No maneuvers were found for preparation.");
        return;
      }

      var variants = new List<BlueprintAbilityReference>();
      var prepareIcon = AbilityRefs.Heroism.Reference.Get().Icon;
      var parent = AbilityConfigurator.New("PrepareManeuvers", PrepareAbilityGuid)
        .SetDisplayName("PrepareManeuvers.Name")
        .SetDescription("PrepareManeuvers.Desc")
        .SetIcon(prepareIcon)
        .SetType(AbilityType.Extraordinary)
        .SetRange(AbilityRange.Personal)
        .SetActionType(UnitCommand.CommandType.Free)
        .SetCanTargetSelf()
        .SetCanTargetEnemies(false)
        .SetCanTargetFriends(false)
        .Configure();
      parent.ShowNameForVariant = false;

      foreach (var entry in Entries)
      {
        var variant = AbilityConfigurator.New("PrepareManeuver_" + Compact(entry.Granted.AssetGuid.ToString()), entry.VariantGuid)
          .SetDisplayName("PrepareManeuvers.Name")
          .SetDescription("PrepareManeuvers.Desc")
          .SetIcon(prepareIcon)
          .SetType(AbilityType.Extraordinary)
          .SetRange(AbilityRange.Personal)
          .SetActionType(UnitCommand.CommandType.Free)
          .SetCanTargetSelf()
          .SetCanTargetEnemies(false)
          .SetCanTargetFriends(false)
          .AddAbilityEffectRunAction(ActionsBuilder.New().Add<PrepareManeuverAction>(action => action.MarkerGuid = entry.MarkerGuid))
          .Configure();

        variant.m_Parent = parent.ToReference<BlueprintAbilityReference>();
        CopyPresentation(entry.Granted, variant);
        variant.ActionBarAutoFillIgnored = true;
        variant.m_AutoUseIsForbidden = true;
        variant.AddComponents(new ShowIfManeuverCanBePrepared
        {
          SourceGuid = entry.Source.AssetGuid.ToString(),
          MarkerGuid = entry.MarkerGuid
        });
        variants.Add(variant.ToReference<BlueprintAbilityReference>());
      }

      parent.AddComponents(
        new AbilityVariants { m_Variants = variants.ToArray() },
        new ShowPrepareManeuversAbility(),
        new CasterNotInCombat());

      var resourceFact = BlueprintTool.Get<BlueprintUnitFact>(ManeuverResources.ManeuverResourceFactGuid);
      resourceFact.AddComponents(new GrantPrepareManeuvers());

      Main.Logger.Info($"Maneuver preparation covers {Entries.Count} maneuvers.");
    }

    public static int MaxPrepared(UnitEntityData unit)
    {
      if (unit == null)
        return 0;

      int master = ClassLevel(unit, MasterOfNineC.Guid);
      if (master > 5)
        master = 5;
      return WarbladeSlots(ClassLevel(unit, WarbladeC.Guid)) + SwordsageSlots(ClassLevel(unit, SwordsageC.Guid)) + master;
    }

    public static void SyncPreparedCap(UnitEntityData unit)
    {
      if (_ranking || unit == null)
        return;

      int rank = MaxPrepared(unit);
      if (rank <= 0)
        return;

      _ranking = true;
      try
      {
        var feature = BlueprintTool.Get<BlueprintFeature>(CountFeatureGuid);
        var fact = unit.GetFact(feature) as Feature;
        if (fact == null)
          fact = unit.AddFact(feature) as Feature;
        if (fact != null && fact.Rank != rank)
        {
          if (fact.Activating || fact.Deactivating)
            fact.Rank = ClampRank(fact, rank);
          else
            fact.SetRank(rank);
        }
        RemoveExtraCountFacts(unit, feature, fact);
        DropIllegalPrepared(unit);
      }
      finally
      {
        _ranking = false;
      }
    }

    static int ClampRank(Feature fact, int rank)
    {
      int max = fact.Blueprint is BlueprintFeature blueprint && blueprint.Ranks > 0 ? blueprint.Ranks : 40;
      if (rank > max)
        return max;
      if (rank < 1)
        return 1;
      return rank;
    }

    static void DropIllegalPrepared(UnitEntityData unit)
    {
      if (unit == null)
        return;

      foreach (var entry in Entries)
      {
        if (!unit.HasFact(entry.Marker))
          continue;
        if (!Qualifies(unit, entry))
          RemoveMarker(unit, entry);
      }
    }

    static void RemoveExtraCountFacts(UnitEntityData unit, BlueprintFeature feature, Feature keep)
    {
      var facts = unit.Descriptor?.Facts?.List;
      if (facts == null)
        return;

      for (int i = facts.Count - 1; i >= 0; i--)
      {
        if (facts[i].Blueprint != feature || facts[i] == keep)
          continue;
        unit.RemoveFact(facts[i]);
      }
    }

    public static int PreparedCount(UnitEntityData unit)
    {
      if (unit == null)
        return 0;

      int count = 0;
      foreach (var entry in Entries)
      {
        if (unit.HasFact(entry.Marker))
          count++;
      }
      return count;
    }

    public static void Prepare(UnitEntityData unit, string markerGuid)
    {
      var entry = Entries.Find(item => item.MarkerGuid == markerGuid);
      if (entry == null || !CanPrepare(unit, entry.Source, entry.Marker))
        return;

      bool addedAbility = !unit.HasFact(entry.Granted) || IsTemporarilyDisabled(unit, entry.Granted);
      if (addedAbility)
        unit.AddFact(entry.Granted);
      if (!unit.HasFact(entry.Marker))
        unit.AddFact(entry.Marker);

      if (!unit.HasFact(entry.Marker) || PreparedCount(unit) > MaxPrepared(unit))
      {
        RemoveMarker(unit, entry);
        if (addedAbility && !unit.HasFact(entry.Source))
          RemoveGranted(unit, entry.Granted);
        return;
      }

      EnsureTurnedOn(unit.GetFact(entry.Granted));

      if (entry.Granted is BlueprintAbility preparedAbility)
      {
        ReleaseBarSlot(unit, preparedAbility);
        NotifyAbilityShown(unit, preparedAbility);
      }
    }

    public static void ClearPrepared(UnitEntityData unit)
    {
      if (unit == null)
        return;

      foreach (var entry in Entries)
        RemoveMarker(unit, entry);
    }

    public static bool CanPrepareAny(UnitEntityData unit)
    {
      if (unit == null || unit.IsInCombat || PreparedCount(unit) >= MaxPrepared(unit))
        return false;

      foreach (var entry in Entries)
      {
        if (!unit.HasFact(entry.Marker) && Qualifies(unit, entry))
          return true;
      }
      return false;
    }

    public static bool CanPrepare(UnitEntityData unit, BlueprintFeature source, BlueprintUnitFact marker)
    {
      if (unit == null || marker == null || unit.IsInCombat || unit.HasFact(marker))
        return false;

      var entry = Entries.Find(item => item.Marker == marker);
      if (entry == null || !Qualifies(unit, entry))
        return false;
      return PreparedCount(unit) < MaxPrepared(unit);
    }

    static bool Qualifies(UnitEntityData unit, PreparedManeuver entry)
    {
      if (entry.Source == null || !unit.HasFact(entry.Source))
        return false;
      if (entry.Discipline != null && !unit.HasFact(entry.Discipline))
        return false;

      return MartialAdeptMulticlass.ManeuverTier(MartialAdeptMulticlass.InitiatorLevel(unit)) >= entry.RequiredTier;
    }

    static void RemoveMarker(UnitEntityData unit, PreparedManeuver entry)
    {
      var fact = unit.GetFact(entry.Marker);
      if (fact != null)
        unit.RemoveFact(fact);

      if (!unit.HasFact(entry.Source))
        RemoveGranted(unit, entry.Granted);
    }

    static void RemoveGranted(UnitEntityData unit, BlueprintUnitFact blueprint)
    {
      var granted = unit.GetFact(blueprint);
      if (granted == null)
        return;

      EnsureTurnedOn(granted);
      unit.RemoveFact(granted);
    }

    public static bool IsTemporarilyDisabled(UnitEntityData unit, BlueprintUnitFact blueprint)
    {
      return unit?.GetFact(blueprint) is Ability ability && ability.Data != null && ability.Data.TemporarilyDisabled;
    }

    public static void EnsureTurnedOn(EntityFact fact)
    {
      if (fact == null || fact.IsTurnedOn || fact.Activating || fact.Deactivating)
        return;

      if (fact.IsActive)
        fact.TurnOn();
      else if (fact.IsAttached)
        fact.Activate();
    }

    static void Register(BlueprintFeature feature, BlueprintUnitFact granted)
    {
      var grantedGuid = granted.AssetGuid.ToString();
      var markerGuid = DeriveGuid(grantedGuid, 0x5A);
      var marker = FeatureConfigurator.New("ManeuverPrepared_" + Compact(grantedGuid), markerGuid)
        .SetHideInUI(true)
        .AddComponent<RestorePreparedAbility>(component => component.GrantedGuid = grantedGuid)
        .Configure();

      if (granted is BlueprintAbility ability)
        ability.AddComponents(new ShowPreparedManeuver { MarkerGuid = markerGuid });

      ReadRequirements(feature, out var discipline, out int tier);
      Entries.Add(new PreparedManeuver
      {
        Source = feature,
        Granted = granted,
        Marker = marker,
        MarkerGuid = markerGuid,
        VariantGuid = DeriveGuid(grantedGuid, 0xA5),
        Discipline = discipline,
        RequiredTier = tier
      });
    }

    static void CopyPresentation(BlueprintUnitFact granted, BlueprintAbility variant)
    {
      if (granted?.m_Icon != null)
        variant.m_Icon = granted.m_Icon;

      if (granted is BlueprintAbility ability)
      {
        variant.m_DisplayName = ability.m_DisplayName;
        variant.m_Description = ability.m_Description;
        return;
      }

      if (granted is BlueprintActivatableAbility activatable)
      {
        variant.m_DisplayName = activatable.m_DisplayName;
        variant.m_Description = activatable.m_Description;
      }
    }

    public static void ReleaseBarSlot(UnitEntityData unit, BlueprintAbility ability)
    {
      var settings = unit?.UISettings;
      if (settings == null || ability == null || HasGoodSlot(settings, ability))
        return;

      settings.RemoveFromAlreadyAutomaticallyAdded(ability);
    }

    static void NotifyAbilityShown(UnitEntityData unit, BlueprintAbility ability)
    {
      if (unit?.GetFact(ability) is Ability fact)
        EventBus.RaiseEvent<IPlayerAbilitiesHandler>(handler => handler.HandleAbilityAdded(fact));
    }

    static bool HasGoodSlot(UnitUISettings settings, BlueprintAbility ability)
    {
      var slots = settings.m_Slots;
      if (slots == null)
        return false;

      foreach (var slot in slots)
      {
        if (slot is not MechanicActionBarSlotAbility abilitySlot || abilitySlot.Ability?.Blueprint != ability)
          continue;
        if (!abilitySlot.IsBad())
          return true;
      }

      return false;
    }

    static bool IsStance(BlueprintFeature feature)
    {
      return StanceGuids().Contains(Norm(feature.AssetGuid.ToString()));
    }

    static HashSet<string> _stanceGuids;

    static HashSet<string> StanceGuids()
    {
      if (_stanceGuids != null)
        return _stanceGuids;

      _stanceGuids = new HashSet<string>();
      CollectStances(VoidHeadWOTRNineSwords.Warblade.SwordsageStanceSelection.Guid);
      CollectStances(VoidHeadWOTRNineSwords.Swordsage.SwordsageStanceSelection.Guid);
      return _stanceGuids;
    }

    static void CollectStances(string selectionGuid)
    {
      var selection = BlueprintTool.Get<BlueprintFeatureSelection>(selectionGuid);
      if (selection?.m_AllFeatures == null)
        return;

      foreach (var feature in selection.m_AllFeatures)
      {
        if (feature != null)
          _stanceGuids.Add(RefId(feature));
      }
    }

    public static int PreparedDisciplineCount(UnitEntityData unit)
    {
      if (unit == null)
        return 0;

      var seen = new System.Collections.Generic.HashSet<string>();
      foreach (var entry in Entries)
      {
        if (entry.Discipline == null || entry.Marker == null || !unit.HasFact(entry.Marker))
          continue;

        seen.Add(Norm(entry.Discipline.AssetGuid.ToString()));
      }

      return seen.Count;
    }

    public static bool SpendsManeuverPoints(BlueprintAbility ability)
    {
      if (ability?.ComponentsArray == null)
        return false;

      foreach (var component in ability.ComponentsArray)
      {
        if (component is not AbilityResourceLogic logic || !logic.IsSpendResource || logic.m_RequiredResource == null)
          continue;

        if (string.Equals(RefId(logic.m_RequiredResource), Norm(ManeuverResources.ManeuverResourceGuid), StringComparison.Ordinal))
          return true;
      }

      return false;
    }

    static void ReadRequirements(BlueprintFeature feature, out BlueprintFeature discipline, out int tier)
    {
      discipline = null;
      tier = 1;
      if (feature?.ComponentsArray == null)
        return;

      int foundTier = 0;
      foreach (var component in feature.ComponentsArray)
      {
        if (component is not PrerequisiteFeature prerequisite || prerequisite.m_Feature == null)
          continue;

        var id = RefId(prerequisite.m_Feature);
        int initiatorTier = InitiatorTier(id);
        if (initiatorTier > foundTier)
          foundTier = initiatorTier;

        var disciplineFeature = DisciplineFeature(id);
        if (disciplineFeature != null)
          discipline = disciplineFeature;
      }

      if (foundTier > 0)
        tier = foundTier;
    }

    static int InitiatorTier(string normalizedGuid)
    {
      if (normalizedGuid == Norm(InitiatorLevels.Lvl1Guid)) return 1;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl2Guid)) return 2;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl3Guid)) return 3;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl4Guid)) return 4;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl5Guid)) return 5;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl6Guid)) return 6;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl7Guid)) return 7;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl8Guid)) return 8;
      if (normalizedGuid == Norm(InitiatorLevels.Lvl9Guid)) return 9;
      return 0;
    }

    static BlueprintFeature DisciplineFeature(string normalizedGuid)
    {
      string guid = null;
      if (normalizedGuid == Norm(DisciplineProficencies.DesertWindProficencyGuid)) guid = DisciplineProficencies.DesertWindProficencyGuid;
      else if (normalizedGuid == Norm(DisciplineProficencies.DiamondMindProficencyGuid)) guid = DisciplineProficencies.DiamondMindProficencyGuid;
      else if (normalizedGuid == Norm(DisciplineProficencies.IronHeartProficencyGuid)) guid = DisciplineProficencies.IronHeartProficencyGuid;
      else if (normalizedGuid == Norm(DisciplineProficencies.ShadowHandProficencyGuid)) guid = DisciplineProficencies.ShadowHandProficencyGuid;
      else if (normalizedGuid == Norm(DisciplineProficencies.StoneDragonProficencyGuid)) guid = DisciplineProficencies.StoneDragonProficencyGuid;
      else if (normalizedGuid == Norm(DisciplineProficencies.TigerClawProficencyGuid)) guid = DisciplineProficencies.TigerClawProficencyGuid;
      else if (normalizedGuid == Norm(DisciplineProficencies.WhiteRavenProficencyGuid)) guid = DisciplineProficencies.WhiteRavenProficencyGuid;
      else if (normalizedGuid == Norm(DisciplineProficencies.RivenHourglassProficencyGuid)) guid = DisciplineProficencies.RivenHourglassProficencyGuid;
      if (guid == null)
        return null;

      try
      {
        return BlueprintTool.Get<BlueprintFeature>(guid);
      }
      catch (InvalidOperationException)
      {
        return null;
      }
    }

    static int ClassLevel(UnitEntityData unit, string classGuid)
    {
      if (unit?.Progression == null)
        return 0;

      var characterClass = BlueprintTool.Get<BlueprintCharacterClass>(classGuid);
      return unit.Progression.GetClassLevel(characterClass);
    }

    static int WarbladeSlots(int level)
    {
      if (level <= 0)
        return 0;
      if (level >= WarbladePrepared.Length)
        return WarbladePrepared[WarbladePrepared.Length - 1];
      return WarbladePrepared[level];
    }

    static int SwordsageSlots(int level)
    {
      if (level <= 0)
        return 0;
      return 5 + Math.Min(level, 20);
    }

    static string RefId(BlueprintReferenceBase reference)
    {
      if (reference == null)
        return "";

      var guid = reference.Guid;
      if (guid != BlueprintGuid.Empty)
        return Norm(guid.ToString());
      return Norm(reference.deserializedGuid.ToString());
    }

    static string Norm(string value)
    {
      if (string.IsNullOrEmpty(value))
        return "";
      return value.Replace("-", "").Replace("{", "").Replace("}", "").Trim().ToLowerInvariant();
    }

    static IEnumerable<Blueprint<BlueprintFeatureReference>> KnownFeatures()
    {
      return Concat(
        AllManeuversAndStances.DiamondMindGuids,
        AllManeuversAndStances.DesertWindGuids,
        AllManeuversAndStances.IronHeartGuids,
        AllManeuversAndStances.ShadowHandGuids,
        AllManeuversAndStances.StoneDragonGuids,
        AllManeuversAndStances.TigerClawGuids,
        AllManeuversAndStances.WhiteRavenGuids,
        AllManeuversAndStances.RivenHourglassGuids);
    }

    static IEnumerable<Blueprint<BlueprintFeatureReference>> Concat(params IEnumerable<Blueprint<BlueprintFeatureReference>>[] lists)
    {
      foreach (var list in lists)
      {
        foreach (var item in list)
          yield return item;
      }
    }

    static string Compact(string guid)
    {
      return guid.Replace("-", "").Replace("{", "").Replace("}", "");
    }

    static string DeriveGuid(string source, byte salt)
    {
      var bytes = Guid.Parse(source).ToByteArray();
      bytes[0] ^= salt;
      bytes[1] ^= (byte)(salt * 3);
      return new Guid(bytes).ToString("D");
    }

    sealed class PreparedManeuver
    {
      public BlueprintFeature Source;
      public BlueprintUnitFact Granted;
      public BlueprintFeature Marker;
      public string MarkerGuid;
      public string VariantGuid;
      public BlueprintFeature Discipline;
      public int RequiredTier;
    }

    [HarmonyPatch(typeof(UnitUISettings), nameof(UnitUISettings.UpdateBadSlots))]
    static class RefillClearedPreparedSlots
    {
      static void Postfix(UnitUISettings __instance)
      {
        var unit = __instance?.Owner;
        if (unit == null)
          return;

        foreach (var entry in Entries)
        {
          if (entry.Granted is not BlueprintAbility ability || !unit.HasFact(entry.Marker))
            continue;
          if (!HasGoodSlot(__instance, ability))
            __instance.RemoveFromAlreadyAutomaticallyAdded(ability);
        }
      }
    }

    sealed class RestHandler : IUnitRestHandler
    {
      public void HandleUnitRest(UnitEntityData unit)
      {
        ClearPrepared(unit);
      }
    }
  }

  public class PrepareManeuverAction : ContextAction
  {
    public string MarkerGuid;

    public override string GetCaption()
    {
      return "Prepare maneuver";
    }

    public override void RunAction()
    {
      var caster = Context?.MaybeCaster;
      if (caster == null)
        return;

      ManeuverPreparation.Prepare(caster, MarkerGuid);
    }
  }

  [AllowedOn(typeof(BlueprintFeature))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d3b")]
  public class RestorePreparedAbility : UnitFactComponentDelegate
  {
    public string GrantedGuid;

    public override void OnTurnOn()
    {
      if (Owner == null || string.IsNullOrEmpty(GrantedGuid))
        return;

      BlueprintUnitFact granted;
      try
      {
        granted = BlueprintTool.Get<BlueprintUnitFact>(GrantedGuid);
      }
      catch (InvalidOperationException)
      {
        return;
      }

      if (granted == null)
        return;
      if (!Owner.HasFact(granted) || ManeuverPreparation.IsTemporarilyDisabled(Owner, granted))
        Owner.AddFact(granted);
      ManeuverPreparation.EnsureTurnedOn(Owner.GetFact(granted));
      if (granted is BlueprintAbility ability)
        ManeuverPreparation.ReleaseBarSlot(Owner, ability);
    }
  }

  [AllowedOn(typeof(BlueprintFeature))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d36")]
  public class SyncPreparedManeuverCount : UnitFactComponentDelegate, IOwnerGainLevelHandler
  {
    public override void OnTurnOn()
    {
      ManeuverPreparation.SyncPreparedCap(Owner);
    }

    public void HandleUnitGainLevel()
    {
      ManeuverPreparation.SyncPreparedCap(Owner);
    }
  }

  [AllowedOn(typeof(BlueprintUnitFact))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d35")]
  public class GrantPrepareManeuvers : UnitFactComponentDelegate, IOwnerGainLevelHandler
  {
    public override void OnTurnOn()
    {
      var ability = BlueprintTool.Get<BlueprintAbility>("C3A91E70-6B24-4F18-9D55-0C1E8A7B6D30");
      if (Owner != null && ability != null && !Owner.HasFact(ability))
        Owner.AddFact(ability);

      ManeuverPreparation.SyncPreparedCap(Owner);
    }

    public void HandleUnitGainLevel()
    {
      ManeuverPreparation.SyncPreparedCap(Owner);
    }
  }

  [AllowedOn(typeof(BlueprintAbility))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d31")]
  public class ShowPreparedManeuver : BlueprintComponent, IAbilityVisibilityProvider
  {
    public string MarkerGuid;

    public bool IsAbilityVisible(AbilityData ability)
    {
      var caster = ability?.Caster?.Unit;
      if (caster == null || string.IsNullOrEmpty(MarkerGuid))
        return false;

      return caster.HasFact(BlueprintTool.Get<BlueprintUnitFact>(MarkerGuid));
    }
  }

  [AllowedOn(typeof(BlueprintAbility))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d32")]
  public class ShowIfManeuverCanBePrepared : BlueprintComponent, IAbilityVisibilityProvider
  {
    public string SourceGuid;
    public string MarkerGuid;

    public bool IsAbilityVisible(AbilityData ability)
    {
      var caster = ability?.Caster?.Unit;
      if (caster == null)
        return false;

      var source = BlueprintTool.Get<BlueprintFeature>(SourceGuid);
      var marker = BlueprintTool.Get<BlueprintUnitFact>(MarkerGuid);
      return ManeuverPreparation.CanPrepare(caster, source, marker);
    }
  }

  [AllowedOn(typeof(BlueprintAbility))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d33")]
  public class ShowPrepareManeuversAbility : BlueprintComponent, IAbilityVisibilityProvider
  {
    public bool IsAbilityVisible(AbilityData ability)
    {
      var caster = ability?.Caster?.Unit;
      if (caster == null || caster.IsInCombat)
        return false;
      return ManeuverPreparation.MaxPrepared(caster) > 0;
    }
  }

  [AllowedOn(typeof(BlueprintAbility))]
  [TypeId("8f3c2a10-6b14-4e7a-9d55-0c1e8a7b6d34")]
  public class CasterNotInCombat : BlueprintComponent, IAbilityCasterRestriction
  {
    public bool IsCasterRestrictionPassed(UnitEntityData caster)
    {
      return caster != null && !caster.IsInCombat;
    }

    public string GetAbilityCasterRestrictionUIText()
    {
      return "";
    }
  }
}
