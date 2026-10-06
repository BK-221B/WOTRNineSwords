using BlueprintCore.Actions.Builder;
using BlueprintCore.Actions.Builder.ContextEx;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using VoidHeadWOTRNineSwords.Common;
using VoidHeadWOTRNineSwords.Components;

namespace VoidHeadWOTRNineSwords.Warblade
{
  static class WarbladeRecoverManeuvers
  {
    public const string Guid = "B2DB2652-A131-4A9A-B50F-5C8D5C0C8D63";
    const string Name = "RecoverManeuvers.Name";

    public static void Configure()
    {
      Main.Logger.Info($"Configuring {nameof(WarbladeRecoverManeuvers)}");

      UnityEngine.Sprite icon = AbilityRefs.Restoration.Reference.Get().Icon;

      var ability = AbilityConfigurator.New("RecoverManeuversAbility", "32F6007B-8056-46B3-AE85-D5A8FAED67FF")
        .SetDisplayName(Name)
        .SetDescription("RecoverManeuvers.Desc")
        .SetIcon(icon)
        .SetAnimation(Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionCastSpell.CastAnimationStyle.Special)
        .SetCanTargetEnemies()
        .SetRange(Kingmaker.UnitLogic.Abilities.Blueprints.AbilityRange.Weapon)
        .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Move)
        .AddAbilityEffectRunAction(ActionsBuilder.New().Add<MeleeAttackExtended>(mae => mae.OnHit = ActionsBuilder.New().OnContextCaster(ActionsBuilder.New().RestoreResource(ManeuverResources.ManeuverResourceGuid, value: 2)).Build()))
        .AddComponent(new ShowRecoveryForStyle { UseWarbladeRecovery = true })
        .Configure();

      var feature = FeatureConfigurator.New("RecoverManeuvers", Guid)
        .SetDisplayName(Name)
        .SetDescription("RecoverManeuvers.Desc")
        .AddComponent(new LockRecoveryStyle { UseWarbladeRecovery = true })
        .AddFacts([ability])
        .Configure();
    }
  }
}