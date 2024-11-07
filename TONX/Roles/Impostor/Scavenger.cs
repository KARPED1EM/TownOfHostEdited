using AmongUs.GameOptions;

using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Impostor;
public sealed class Scavenger : RoleBase, IImpostor
{

    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Scavenger),
            player => new Scavenger(player),
            CustomRoles.Scavenger,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            1800,
            SetupOptionItem,
            "sc|清道|"
        );
    public Scavenger(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem OptionKillCooldown;
    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 40f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
    public void BeforeMurderPlayerAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        killer.RpcSnapToForced(target.GetTruePosition());
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        target.RpcSnapToForced(Utils.GetBlackRoomPS());
        target.SetRealKiller(killer);
        target.RpcMurderPlayerV2(target);
        killer.SetKillCooldownV2();
        NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), Translator.GetString("KilledByScavenger")));

        info.DoKill = false;
    }
}