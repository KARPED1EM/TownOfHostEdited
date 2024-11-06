using AmongUs.GameOptions;
using System.Collections.Generic;

using TONX.Roles.Core;
using static TONX.Translator;

namespace TONX.Roles.Crewmate;
public sealed class Detective : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Detective),
            player => new Detective(player),
            CustomRoles.Detective,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            21300,
            SetupOptionItem,
            "de|偵探",
            "#7160e8"
        );
    public Detective(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        MsgToSend = null;
    }

    static OptionItem OptionKnowKiller;
    enum OptionName
    {
        DetectiveCanknowKiller,
    }

    private string MsgToSend;
    private static void SetupOptionItem()
    {
        OptionKnowKiller = BooleanOptionItem.Create(RoleInfo, 10, OptionName.DetectiveCanknowKiller, true, false);
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        var tpc = target.Object;
        if (reporter == null || !Is(reporter) || target == null || tpc == null || reporter.PlayerId == target.PlayerId) return;
        {
            string msg;
            msg = string.Format(GetString("DetectiveNoticeVictim"), tpc.GetRealName(), tpc.GetTrueRoleName());
            if (OptionKnowKiller.GetBool())
            {
                var realKiller = tpc.GetRealKiller();
                if (realKiller == null) msg += "；" + GetString("DetectiveNoticeKillerNotFound");
                else msg += "；" + string.Format(GetString("DetectiveNoticeKiller"), realKiller.GetTrueRoleName());
            }
            MsgToSend = msg;
        }
    }
    public override void NotifyOnMeetingStart(ref List<(string, byte, string)> msgToSend)
    {
        if (MsgToSend != null)
            msgToSend.Add((MsgToSend, Player.PlayerId, Utils.ColorString(RoleInfo.RoleColor, GetString("DetectiveNoticeTitle"))));
        MsgToSend = null;
    }
}