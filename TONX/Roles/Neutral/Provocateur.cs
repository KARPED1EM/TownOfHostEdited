using AmongUs.GameOptions;
using System.Linq;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using System;
using System.Collections.Generic;
using Hazel;

namespace TONX.Roles.Neutral;

public sealed class Provocateur : RoleBase, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
       SimpleRoleInfo.Create(
            typeof(Provocateur),
            player => new Provocateur(player),
            CustomRoles.Provocateur,
         () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            75_1_2_0900,
            null,
            "prov|×Ô±¬¿¨³µ|×Ô±¬",
            "#74ba43",
           true

        );
    public Provocateur(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {

    }
    private bool IsKilled;
    public override void Add()
    {
        var playerId = Player.PlayerId;
        IsKilled = false;
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(IsKilled);
    }
    public override void ReceiveRPC(MessageReader reader)
    {

        IsKilled = reader.ReadBoolean();
    }
    public bool IsNK { get; private set; } = true;
    public bool IsNE { get; private set; } = false;
    public float CalculateKillCooldown() => 1f;
    public bool CanUseKillButton() => true;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        target.RpcSnapToForced(target.GetTruePosition());
        target.RpcMurderPlayerV2(killer);
        killer.SetRealKiller(target);
        IsKilled = true;
        SendRPC();
        return true;
    }
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (Player.IsAlive()) return false;
        if (CustomWinnerHolder.WinnerIds.Contains(Player?.GetRealKiller()?.PlayerId ?? 255) || !IsKilled) return false;
        return true;

    }
}
