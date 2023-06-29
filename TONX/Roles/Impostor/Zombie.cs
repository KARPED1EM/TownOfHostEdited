﻿using AmongUs.GameOptions;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Impostor;
public sealed class Zombie : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Zombie),
            player => new Zombie(player),
            CustomRoles.Zombie,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            4100,
            SetupOptionItem,
            "zb|殭屍|丧尸",
            experimental: true
        );
    public Zombie(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionSpeedReduce;
    enum OptionName
    {
        ZombieSpeedReduce
    }

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(2.5f, 180f, 2.5f), 12f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSpeedReduce = FloatOptionItem.Create(RoleInfo, 11, OptionName.ZombieSpeedReduce, new(0.01f, 1f, 0.01f), 0.03f, false)
            .SetValueFormat(OptionFormat.Multiplier);
    }
    public float CalculateKillCooldown()
    {
        Main.AllPlayerSpeed[Player.PlayerId] -= OptionSpeedReduce.GetFloat();
        return OptionKillCooldown.GetFloat();
    }
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0.2f);
    public override bool OnVote(byte voterId, byte sourceVotedForId, ref byte roleVoteFor, ref int roleNumVotes, ref bool clearVote)
    {
        if (sourceVotedForId == Player.PlayerId) roleNumVotes = 0;
        return true;
    }
}