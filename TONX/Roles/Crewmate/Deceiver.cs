using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Crewmate;
public sealed class Deceiver : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Deceiver),
            player => new Deceiver(player),
            CustomRoles.Deceiver,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Crewmate,
            21700,
            SetupOptionItem,
            "de|贗品商|赝品",
            "#e0e0e0",
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate)
        );
    public Deceiver(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        Customers = new();

        CustomRoleManager.OnCheckMurderPlayerOthers_Before.Add(OnCheckMurderPlayerOthers_Before);
    }

    static OptionItem OptionSellCooldown;
    static OptionItem OptionSellNums;
    enum OptionName
    {
        DeceiverSkillCooldown,
        DeceiverSkillLimitTimes,
    }

    private int SellLimit;
    private Dictionary<byte, bool> Customers;
    public bool IsKiller { get; private set; } = false;
    private static void SetupOptionItem()
    {
        OptionSellCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.DeceiverSkillCooldown, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionSellNums = IntegerOptionItem.Create(RoleInfo, 11, OptionName.DeceiverSkillLimitTimes, new(1, 15, 1), 2, false)
            .SetValueFormat(OptionFormat.Times);
    }
    public override void Add()
    {
        SellLimit = OptionSellNums.GetInt();
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(SellLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        SellLimit = reader.ReadInt32();
    }
    public float CalculateKillCooldown() => CanUseKillButton() ? OptionSellCooldown.GetFloat() : 255f;
    public bool CanUseKillButton() => Player.IsAlive() && SellLimit >= 1;
    public bool CanUseSabotageButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("DeceiverButtonText");
        return true;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (SellLimit < 1) return false;
        var (killer, target) = info.AttemptTuple;

        if (Customers.ContainsKey(target.PlayerId))
        {
            killer.Notify(Translator.GetString("DeceiverRepeatSell"));
            return false;
        }

        SellLimit--;
        SendRPC();

        killer.ResetKillCooldown();
        killer.SetKillCooldownV2();
        killer.RPCPlayCustomSound("Bet");

        Customers.Add(target.PlayerId, false);

        Logger.Info($"{killer.GetNameWithRole()}：将赝品售卖给 => {target.GetNameWithRole()}", "Deceiver.OnCheckMurderAsKille");
        Logger.Info($"{killer.GetNameWithRole()}：剩余{SellLimit}个赝品", "Deceiver.OnCheckMurderAsKille");
        return false;
    }
    private static bool OnCheckMurderPlayerOthers_Before(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        foreach (var deceiver in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Deceiver)))
        {
            if (deceiver.GetRoleClass() is not Deceiver roleClass) continue;
            if (roleClass.Customers.TryGetValue(killer.PlayerId, out var x) && x)
            {
                killer.SetRealKiller(deceiver);
                killer.SetDeathReason(CustomDeathReason.Misfire);
                killer.RpcMurderPlayerV2(killer);
                Logger.Info($"{deceiver.GetNameWithRole()} 的客户：{killer.GetNameWithRole()} 因使用赝品走火自杀", "Deceiver.OnCheckMurderPlayerOthers_Before");
                return false;
            }
        }
        return true;
    }
    public override void OnStartMeeting()
    {
        var keys = Customers.Keys;
        keys.Do(x => Customers[x] = true);
        foreach (var pcId in Customers.Keys)
        {
            var target = Utils.GetPlayerById(pcId);
            if (target == null || !target.IsAlive()) continue;
            if (target.GetRoleClass() is IKiller x && x.IsKiller && x.CanKill) continue;
            MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Misfire, target.PlayerId);
            target.SetRealKiller(Player);
            Logger.Info($"赝品商 {Player.GetRealName()} 的客户 {target.GetRealName()} 因不带刀将在会议结束后自杀", "Deceiver.OnStartMeeting");
        }
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (seen == null) return "";
        return Customers.ContainsKey(seen.PlayerId) ? Utils.ColorString(RoleInfo.RoleColor, "▲") : "";
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(CanUseKillButton() ? RoleInfo.RoleColor : Color.gray, $"({SellLimit})");
}