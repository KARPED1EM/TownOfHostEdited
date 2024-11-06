using AmongUs.GameOptions;

using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Impostor;
public sealed class KillingMachine : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(KillingMachine),
            player => new KillingMachine(player),
            CustomRoles.KillingMachine,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4000,
            SetupOptionItem,
            "km|殺戮機器|杀戮|机器|杀戮兵器|杀人机器"
        );
    public KillingMachine(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem KillCooldown;
    private static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public float CalculateKillCooldown() => KillCooldown.GetFloat();
    public override bool CanUseAbilityButton() => false;
    public bool CanUseSabotageButton() => false;
    public override bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => Is(reporter);
    public bool CanUseImpostorVentButton { get; } = false;
}