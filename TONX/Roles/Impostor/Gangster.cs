using AmongUs.GameOptions;
using Hazel;

using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using UnityEngine;
using static TONX.Translator;

namespace TONX.Roles.Impostor;
public sealed class Gangster : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Gangster),
            player => new Gangster(player),
            CustomRoles.Gangster,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            3200,
            SetupOptionItem,
            "ga"
        );
    public Gangster(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem OptionRecruitLimit;
    static OptionItem OptionKillCooldown;
    static OptionItem OptionSheriffCanBeMadmate;
    static OptionItem OptionMayorCanBeMadmate;
    static OptionItem OptionNGuesserCanBeMadmate;
    static OptionItem OptionJudgeCanBeMadmate;
    enum OptionName
    {
        GangsterRecruitCooldown,
        GangsterRecruitLimit,
        GanSheriffCanBeMadmate,
        GanMayorCanBeMadmate,
        GanNGuesserCanBeMadmate,
        GanJudgeCanBeMadmate,
    }

    private int RecruitLimit;
    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.GangsterRecruitCooldown, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRecruitLimit = IntegerOptionItem.Create(RoleInfo, 11, OptionName.GangsterRecruitLimit, new(1, 15, 1), 2, false)
            .SetValueFormat(OptionFormat.Times);

        OptionSheriffCanBeMadmate = BooleanOptionItem.Create(RoleInfo, 12, OptionName.GanSheriffCanBeMadmate, false, false);
        OptionMayorCanBeMadmate = BooleanOptionItem.Create(RoleInfo, 13, OptionName.GanMayorCanBeMadmate, false, false);
        OptionNGuesserCanBeMadmate = BooleanOptionItem.Create(RoleInfo, 14, OptionName.GanNGuesserCanBeMadmate, false, false);
        OptionJudgeCanBeMadmate = BooleanOptionItem.Create(RoleInfo, 15, OptionName.GanJudgeCanBeMadmate, false, false);

    }
    public override void Add()
    {
        RecruitLimit = OptionRecruitLimit.GetInt();
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(RecruitLimit);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        
        RecruitLimit = reader.ReadInt32();
    }
    public float CalculateKillCooldown() => RecruitLimit >= 1 ? OptionKillCooldown.GetFloat() : Options.DefaultKillCooldown;
    public override string GetProgressText(bool comms = false) => Utils.ColorString(RecruitLimit >= 1 ? Color.red : Color.gray, $"({RecruitLimit})");
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("GangsterButtonText");
        return RecruitLimit >= 1;
    }
    public bool OverrideKillButtonSprite(out string buttonName)
    {
        buttonName = "Sidekick";
        return RecruitLimit >= 1;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (RecruitLimit < 1) return true;
        var (killer, target) = info.AttemptTuple;

        if (CanBeRecruited(target))
        {
            RecruitLimit--;
            SendRPC();

            target.RpcSetCustomRole(CustomRoles.Madmate);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("GangsterSuccessfullyRecruited")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("BeRecruitedByGangster")));
            Utils.NotifyRoles();

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RpcProtectedMurderPlayer(target);
            target.RpcProtectedMurderPlayer(killer);
            target.RpcProtectedMurderPlayer(target);

            Logger.Info($"注册附加职业：{target.GetNameWithRole()} => {CustomRoles.Madmate}", "AssignCustomSubRoles");
            Logger.Info($"{killer.GetNameWithRole()} : 剩余{RecruitLimit}次招募机会", "Gangster");
            return false;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("GangsterRecruitmentFailure")));
        Logger.Info($"{killer.GetNameWithRole()} : 剩余{RecruitLimit}次招募机会", "Gangster");
        return true;
    }
    private static bool CanBeRecruited(PlayerControl pc)
    {
        return pc != null && pc.GetCustomRole().IsCrewmate() && !pc.Is(CustomRoles.Madmate)
        && !(
            (pc.Is(CustomRoles.Sheriff) && !OptionSheriffCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Mayor) && !OptionMayorCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.NiceGuesser) && !OptionNGuesserCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Judge) && !OptionJudgeCanBeMadmate.GetBool()) ||
            pc.Is(CustomRoles.Snitch) ||
            pc.Is(CustomRoles.LazyGuy) ||
            pc.Is(CustomRoles.Celebrity) ||
            pc.Is(CustomRoles.Egoist)
            );
    }
}