using AmongUs.GameOptions;
using System.Linq;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using System;
using System.Collections.Generic;
using Hazel;

namespace TONX.Roles.Neutral;

public sealed class Innocent : RoleBase
{ 
    public static readonly SimpleRoleInfo RoleInfo =
       SimpleRoleInfo.Create(
            typeof(Innocent),
            player => new Innocent(player),
            CustomRoles.Innocent,
         () =>  RoleTypes.Impostor ,
            CustomRoleTypes.Neutral,
            75_1_2_0800,
            null,
            "inno|дЉзя|д­зя",
            "#8f815e",
           true
           
        );
    public Innocent(PlayerControl player)
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
    public bool IsNK { get; private set; } = false;

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
        return false;
    }
    public override Action CheckExile(NetworkedPlayerInfo exiled, ref bool DecidedWinner, ref List<string> WinDescriptionText)
    {
        if (!AmongUsClient.Instance.AmHost || Player.GetRealKiller().PlayerId != exiled.PlayerId ||!IsKilled) return null;

        DecidedWinner = true;
        WinDescriptionText.Add(Translator.GetString("ExiledInnocentTarget"));
        return () =>
        {
            CustomWinnerHolder.SetWinnerOrAdditonalWinner(CustomWinner.Innocent);
            CustomWinnerHolder.WinnerIds.Add(Player.PlayerId);
        };
    }
}
