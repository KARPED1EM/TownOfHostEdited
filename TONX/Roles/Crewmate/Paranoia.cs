using AmongUs.GameOptions;
using System.Collections.Generic;
using TONX.Roles.Core;

namespace TONX.Roles.Crewmate;
public sealed class Paranoia : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Paranoia),
            player => new Paranoia(player),
            CustomRoles.Paranoia,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            20800,
            SetupOptionItem,
            "pa|被害妄想|被迫害妄想症|被害|妄想|妄想症",
            "#c993f5"
        );
    public Paranoia(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    static OptionItem OptionSkillNums;
    static OptionItem OptionSkillCooldown;
    enum OptionName
    {
        ParanoiaNumOfUseButton,
        ParanoiaVentCooldown,
    }

    private int SkillLimit;
    private static void SetupOptionItem()
    {
        OptionSkillNums = IntegerOptionItem.Create(RoleInfo, 10, OptionName.ParanoiaNumOfUseButton, new(1, 99, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptionSkillCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.ParanoiaVentCooldown, new(2.5f, 180f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void Add() => SkillLimit = OptionSkillNums.GetInt();
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown =
            SkillLimit < 1
            ? 255f
            : OptionSkillCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1;
    }
    public override bool GetAbilityButtonText(out string text)
    {
        text = Translator.GetString("ParanoiaVetnButtonText");
        return true;
    }
    public override bool GetAbilityButtonSprite(out string buttonName)
    {
        buttonName = "Paranoid";
        return true;
    }
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (SkillLimit >= 1)
        {
            var user = physics.myPlayer;
            physics.RpcBootFromVent(ventId);
            user?.NoCheckStartMeeting(user?.Data);
            SkillLimit--;
        }
        else
        {
            Player.Notify(Translator.GetString("SkillMaxUsage"));
        }
        return false;
    }
    public override void NotifyOnMeetingStart(ref List<(string, byte, string)> msgToSend)
    {
        msgToSend.Add((Translator.GetString("SkillUsedLeft") + SkillLimit.ToString(), Player.PlayerId, null));
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner) => Player.RpcResetAbilityCooldown();
}