using AmongUs.GameOptions;
using Hazel;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Neutral;
public sealed class Succubus : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Succubus),
            player => new Succubus(player),
            CustomRoles.Succubus,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            51100,
            SetupOptionItem,
            "su",
            "#ff00ff",
            true,
            countType: CountTypes.Succubus
        );
    public Succubus(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    { }

    static OptionItem OptionCharmCooldown;
    static OptionItem OptionCharmCooldownIncrese;
    static OptionItem OptionCharmMax;
    static OptionItem OptionKnowTargetRole;
    public static OptionItem OptionTargetKnowOtherTarget;
    public static OptionItem OptionCharmedCountMode;
    static readonly string[] charmedCountMode =
    {
        "CharmedCountMode.None",
        "CharmedCountMode.Succubus",
        "CharmedCountMode.Original",
    };
    enum OptionName
    {
        SuccubusCharmCooldown,
        SuccubusCharmCooldownIncrese,
        SuccubusCharmMax,
        SuccubusKnowTargetRole,
        SuccubusTargetKnowOtherTarget,
        CharmedCountMode,
    }

    private int CharmLimit;

    private static void SetupOptionItem()
    {
        OptionCharmCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.SuccubusCharmCooldown, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCharmCooldownIncrese = FloatOptionItem.Create(RoleInfo, 11, OptionName.SuccubusCharmCooldownIncrese, new(0f, 180f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCharmMax = IntegerOptionItem.Create(RoleInfo, 12, OptionName.SuccubusCharmMax, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        OptionKnowTargetRole = BooleanOptionItem.Create(RoleInfo, 13, OptionName.SuccubusKnowTargetRole, true, false);
        OptionTargetKnowOtherTarget = BooleanOptionItem.Create(RoleInfo, 14, OptionName.SuccubusTargetKnowOtherTarget, true, false);
        OptionCharmedCountMode = StringOptionItem.Create(RoleInfo, 15, OptionName.CharmedCountMode, charmedCountMode, 0, false);
    }
    public override void Add() => CharmLimit = OptionCharmMax.GetInt();
    public bool IsKiller => false;
    public float CalculateKillCooldown()
    {
        if (CharmLimit < 1) return 255f;
        return OptionCharmCooldown.GetFloat() + (OptionCharmMax.GetInt() - CharmLimit) * OptionCharmCooldownIncrese.GetFloat();
    }
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool CanUseSabotageButton() => false;
    private void SendRPC()
    {
        var sender = CreateSender();
        sender.Writer.Write(CharmLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        CharmLimit = reader.ReadInt32();
    }
    public bool CanUseKillButton() => Player.IsAlive() && CharmLimit >= 1;
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;
        if (info.IsSuicide) return true;
        if (CharmLimit < 1) return false;
        if (CanBeCharmed(target))
        {
            CharmLimit--;
            SendRPC();
            target.RpcSetCustomRole(CustomRoles.Charmed);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), Translator.GetString("SuccubusCharmedPlayer")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), Translator.GetString("CharmedBySuccubus")));
            Utils.NotifyRoles();

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcProtectedMurderPlayer(target);
            target.RpcProtectedMurderPlayer(killer);
            target.RpcProtectedMurderPlayer(target);

            Logger.Info($"注册附加职业：{target.GetNameWithRole()} => {CustomRoles.Charmed}", "AssignCustomSubRoles");
            Logger.Info($"{killer.GetNameWithRole()} : 剩余{CharmLimit}次魅惑机会", "Succubus");
            return false;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Succubus), Translator.GetString("SuccubusInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole()} : 剩余{CharmLimit}次魅惑机会", "Succubus");
        return false;
    }
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText)
    {
        if (OptionKnowTargetRole.GetBool() && seen.Is(CustomRoles.Charmed)) enabled = true;
    }
    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText)
    {
        if (seer.Is(CustomRoles.Charmed)) enabled = true;
    }
    public override string GetProgressText(bool comms = false) => Utils.ColorString(CanUseKillButton() ? Utils.ShadeColor(RoleInfo.RoleColor, 0.25f) : Color.gray, $"({CharmLimit})");
    public static bool CanBeCharmed(PlayerControl pc) => pc != null && (pc.GetCustomRole().IsCrewmate() || pc.GetCustomRole().IsImpostor()) && !pc.Is(CustomRoles.Charmed);
    public bool OverrideKillButtonText(out string text)
    {
        text = Translator.GetString("SuccubusKillButtonText");
        return true;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Subbus";
        return true;
    }
}
