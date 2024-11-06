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
public sealed class Medic : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Medic),
            player => new Medic(player),
            CustomRoles.Medic,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Crewmate,
            22100,
            SetupOptionItem,
            "me|醫生",
            "#00a4ff",
            true
        );
    public Medic(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        ProtectList = new();

        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        CustomRoleManager.OnCheckMurderPlayerOthers_Before.Add(OnCheckMurderPlayerOthers_Before);
    }

    static OptionItem OptionProtectNums;
    static OptionItem OptionProtectCooldown;
    static OptionItem OptionTargetCanSeeProtect;
    static OptionItem OptionKnowTargetShieldBroken;
    enum OptionName
    {
        MedicCooldown,
        MedicSkillLimit,
        MedicTargetCanSeeProtect,
        MedicKnowTargetShieldBroken,
    }

    private int ProtectLimit;
    private static List<byte> ProtectList;
    public bool IsKiller { get; private set; } = false;
    private static void SetupOptionItem()
    {
        OptionProtectCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.MedicCooldown, new(2.5f, 180f, 2.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionProtectNums = IntegerOptionItem.Create(RoleInfo, 11, OptionName.MedicSkillLimit, new(1, 99, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptionTargetCanSeeProtect = BooleanOptionItem.Create(RoleInfo, 12, OptionName.MedicTargetCanSeeProtect, true, false);
        OptionKnowTargetShieldBroken = BooleanOptionItem.Create(RoleInfo, 13, OptionName.MedicKnowTargetShieldBroken, true, false);
    }
    public override void Add()
    {
        ProtectLimit = OptionProtectNums.GetInt();
    }
    private void SendRPC_SyncLimit()
    {
        using var sender = CreateSender();
        sender.Writer.Write(ProtectLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        ProtectLimit = reader.ReadInt32();
    }
    private static void SendRPC_SyncList()
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMedicProtectList, SendOption.Reliable, -1);
        writer.Write(ProtectList.Count);
        for (int i = 0; i < ProtectList.Count; i++)
            writer.Write(ProtectList[i]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC_SyncList(MessageReader reader)
    {
        int count = reader.ReadInt32();
        ProtectList = new();
        for (int i = 0; i < count; i++)
            ProtectList.Add(reader.ReadByte());
    }
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("MedicButtonText");
        return true;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Shield";
        return true;
    }
    public float CalculateKillCooldown() => CanUseKillButton() ? OptionProtectCooldown.GetFloat() : 255f;
    public bool CanUseKillButton()
       => Player.IsAlive()
       && ProtectLimit > 0;
    public bool CanUseSabotageButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public override string GetProgressText(bool comms = false) => Utils.ColorString(CanUseKillButton() ? RoleInfo.RoleColor : Color.gray, $"({ProtectLimit})");
    public static bool InProtect(byte id) => ProtectList.Contains(id) && !(PlayerState.GetByPlayerId(id)?.IsDead ?? true);
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        var (killer, target) = info.AttemptTuple;
        if (ProtectList.Contains(target.PlayerId)) return false;

        ProtectLimit--;
        SendRPC_SyncLimit();
        ProtectList.Add(target.PlayerId);
        SendRPC_SyncList();

        killer.SetKillCooldownV2(target: target);
        killer.RPCPlayCustomSound("Shield");
        target.RPCPlayCustomSound("Shield");

        Utils.NotifyRoles(killer);
        Utils.NotifyRoles(target);

        Logger.Info($"{killer.GetNameWithRole()} : 将护盾发送给 {target.GetNameWithRole()}", "Medic.OnCheckMurderAsKiller");
        Logger.Info($"{killer.GetNameWithRole()} : 剩余{ProtectLimit}个护盾", "Medic.OnCheckMurderAsKiller");
        return false;
    }
    private static bool OnCheckMurderPlayerOthers_Before(MurderInfo info)
    {
        if (info.IsSuicide) return true;
        var (killer, target) = info.AttemptTuple;
        if (!ProtectList.Contains(target.PlayerId)) return true;

        ProtectList.Remove(target.PlayerId);
        SendRPC_SyncList();

        killer.SetKillCooldownV2(target: target, forceAnime: true);
        killer.RpcProtectedMurderPlayer(target);

        if (OptionTargetCanSeeProtect.GetBool())
            target.RpcProtectedMurderPlayer(target);

        Utils.NotifyRoles(target);

        if (OptionKnowTargetShieldBroken.GetBool())
            Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Medic) && x.PlayerId != target.PlayerId).Do(x => x.Notify(Translator.GetString("MedicTargetShieldBroken")));
        else
            Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Medic)).Do(x => Utils.NotifyRoles(x));

        Logger.Info($"{target.GetNameWithRole()} : 来自医生的盾破碎", "Medic.OnCheckMurderPlayerOthers_Before");

        info.CanKill = false;

        return false;
    }
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!InProtect(seen.PlayerId)) return "";
        return (seer.Is(CustomRoles.Medic)
            || (seer == seen && OptionTargetCanSeeProtect.GetBool())
            ) ? Utils.ColorString(RoleInfo.RoleColor, "●") : "";
    }
}