using AmongUs.GameOptions;
using Hazel;
using System;
using System.Linq;
using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Neutral;
public sealed class Follower : RoleBase, IKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Follower),
            player => new Follower(player),
            CustomRoles.Follower,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            51000,
            SetupOptionItem,
            "fo|賭徒",
            "#ff9409",
            true
        );
    public Follower(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    private static OptionItem OptionMaxBetTimes;
    public static OptionItem OptionBetCooldown;
    private static OptionItem OptionBetCooldownIncrese;
    private static OptionItem OptionMaxBetCooldown;
    private static OptionItem OptionKnowTargetRole;
    private static OptionItem OptionBetTargetKnowFollower;
    enum OptionName
    {
        FollowerMaxBetTimes,
        FollowerBetCooldown,
        FollowerMaxBetCooldown,
        FollowerBetCooldownIncrese,
        FollowerKnowTargetRole,
        FollowerBetTargetKnowFollower,
    }


    private int BetLimit;
    private byte BetTarget;

    private static void SetupOptionItem()
    {
        OptionMaxBetTimes = IntegerOptionItem.Create(RoleInfo, 10, OptionName.FollowerMaxBetTimes, new(1, 99, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptionBetCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.FollowerBetCooldown, new(2.5f, 180f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionMaxBetCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.FollowerMaxBetCooldown, new(0f, 990f, 2.5f), 50f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionBetCooldownIncrese = FloatOptionItem.Create(RoleInfo, 13, OptionName.FollowerBetCooldownIncrese, new(0f, 60f, 1f), 4f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionKnowTargetRole = BooleanOptionItem.Create(RoleInfo, 14, OptionName.FollowerKnowTargetRole, false, false);
        OptionBetTargetKnowFollower = BooleanOptionItem.Create(RoleInfo, 15, OptionName.FollowerBetTargetKnowFollower, false, false);
    }
    public override void Add()
    {
        BetLimit = OptionMaxBetTimes.GetInt();
        BetTarget = byte.MaxValue;
    }
    public bool IsKiller => false;
    public float CalculateKillCooldown()
    {
        if (BetLimit < 1) return 255f;
        float cd = OptionBetCooldown.GetFloat();
        cd += Main.AllPlayerControls.Count(x => !x.IsAlive()) * OptionBetCooldownIncrese.GetFloat();
        cd = Math.Min(cd, OptionMaxBetCooldown.GetFloat());
        return cd;
    }
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool CanUseSabotageButton() => false;
    private void SendRPC()
    {
        var sender = CreateSender();
        sender.Writer.Write(BetLimit);
        sender.Writer.Write(BetTarget);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        BetLimit = reader.ReadInt32();
        BetTarget = reader.ReadByte();
    }
    public bool CanUseKillButton() => Player.IsAlive() && BetLimit >= 1;
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText)
    {
        if (!OptionKnowTargetRole.GetBool()) return;
        if (seen.PlayerId == BetTarget) enabled = true;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (info.IsSuicide) return true;
        if (BetTarget == target.PlayerId || BetLimit < 1) return false;

        BetLimit--;
        var beforeTarget = Utils.GetPlayerById(BetTarget);
        if (beforeTarget != null) Utils.NotifyRoles(beforeTarget);

        BetTarget = target.PlayerId;
        SendRPC();

        killer.ResetKillCooldown();
        killer.SetKillCooldownV2();
        killer.RPCPlayCustomSound("Bet");

        killer.Notify(Translator.GetString("FollowerBetPlayer"));
        if (OptionBetTargetKnowFollower.GetBool())
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Follower), Translator.GetString("FollowerBetOnYou")));

        Logger.Info($"赌徒下注：{killer.GetNameWithRole()} => {target.GetNameWithRole()}", "Follower");

        return false;
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        return seen.PlayerId == BetTarget ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Follower), "♦") : "";
    }
    private static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (seen == null || !OptionBetTargetKnowFollower.GetBool()) return "";
        return (seen.GetRoleClass() is Follower roleClass && roleClass.BetTarget == seer.PlayerId)
            ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Follower), "♦") : "";
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(CanUseKillButton() ? Utils.ShadeColor(RoleInfo.RoleColor, 0.25f) : Color.gray, $"({BetLimit})");
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (BetTarget == byte.MaxValue) return false;
        var targetPs = PlayerState.GetByPlayerId(BetTarget);
        return (CustomWinnerHolder.WinnerIds?.Contains(BetTarget) ?? false)
            || (targetPs != null && (CustomWinnerHolder.WinnerRoles?.Contains(targetPs.MainRole) ?? false));
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("FollowerKillButtonText");
        return true;
    }
}