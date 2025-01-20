using AmongUs.GameOptions;

using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Impostor;
public sealed class Bard : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Bard),
            player => new Bard(player),
            CustomRoles.Bard,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4900,
            null,
            "ba|吟游詩人|诗人"
            #if RELEASE
,
            Hidden: true //For Debug
#endif

        );

    public Bard(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    private float KillCooldown;
    public override void Add() => KillCooldown = Options.DefaultKillCooldown;
    public float CalculateKillCooldown() => KillCooldown;
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled != null) KillCooldown /= 2;
    }
}