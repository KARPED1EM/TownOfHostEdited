using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using TONX.Modules;
using TONX.Roles.Core;
using static TONX.Translator;

namespace TONX.Roles.Crewmate;
public sealed class Medium : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Medium),
            player => new Medium(player),
            CustomRoles.Medium,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            22500,
            SetupOptionItem,
            "me|通靈師|通灵",
            "#a200ff"
        );
    public Medium(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CustomRoleManager.ReceiveMessage.Add(OnReceiveMessage);
    }

    static OptionItem OptionContactNums;
    static OptionItem OptionOnlyReceiveMsgFromCrew;
    enum OptionName
    {
        MediumContactLimit,
        MediumOnlyReceiveMsgFromCrew,
    }

    private int ContactLimit;
    private byte ContactPlayer;
    private static void SetupOptionItem()
    {
        OptionContactNums = IntegerOptionItem.Create(RoleInfo, 10, OptionName.MediumContactLimit, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        OptionOnlyReceiveMsgFromCrew = BooleanOptionItem.Create(RoleInfo, 11, OptionName.MediumOnlyReceiveMsgFromCrew, true, false);
    }
    public override void Add()
    {
        ContactLimit = OptionContactNums.GetInt();
        ContactPlayer = byte.MaxValue;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        ContactPlayer = byte.MaxValue;
        if (target == null || Player.PlayerId == target.PlayerId || !Player.IsAlive() || ContactLimit < 1) return;

        ContactLimit--;
        ContactPlayer = target.PlayerId;

        Logger.Info($"通灵师建立联系：{Player.GetNameWithRole()} => {target.PlayerName}", "Medium.OnReportDeadBody");
    }
    public override void NotifyOnMeetingStart(ref List<(string, byte, string)> msgToSend)
    {
        var target = Utils.GetPlayerById(ContactPlayer);
        if (ContactPlayer == byte.MaxValue || target == null) return;
        msgToSend.Add((
            string.Format(GetString("MediumshipNotifySelf"), target.GetRealName(), ContactLimit),
            Player.PlayerId,
            Utils.ColorString(RoleInfo.RoleColor, GetString("MediumshipTitle"))
            ));

        if (OptionOnlyReceiveMsgFromCrew.GetBool() && !target.IsCrew()) return;
        msgToSend.Add((
            string.Format(GetString("MediumshipNotifyTarget"), Player.GetRealName()),
            target.PlayerId,
            Utils.ColorString(RoleInfo.RoleColor, GetString("MediumshipTitle"))
            ));
    }
    private static void OnReceiveMessage(MessageControl mc)
    {
        var (player, msg) = (mc.Player, mc.Message);

        if (player.IsAlive()) return;
        foreach (var medium in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Medium)))
        {
            var roleClass = medium.GetRoleClass() as Medium;
            if (roleClass.ContactPlayer != player.PlayerId) continue;
            if (OptionOnlyReceiveMsgFromCrew.GetBool() && !player.IsCrew()) continue;

            msg = msg.ToLower().Trim();
            if (!CheckCommond(ref msg, "通灵|ms|mediumship|medium", false)) return;

            bool ans;
            if (msg.Contains('n') || msg.Contains(GetString("No")) || msg.Contains('错') || msg.Contains("不是")) ans = false;
            else if (msg.Contains('y') || msg.Contains(GetString("Yes")) || msg.Contains('对')) ans = true;
            else
            {
                Utils.SendMessage(GetString("MediumshipHelp"), player.PlayerId);
                return;
            }

            Utils.SendMessage(GetString("Mediumship" + (ans ? "Yes" : "No")), medium.PlayerId, Utils.ColorString(RoleInfo.RoleColor, GetString("MediumshipTitle")));
            Utils.SendMessage(GetString("MediumshipDone"), player.PlayerId, Utils.ColorString(RoleInfo.RoleColor, GetString("MediumshipTitle")));

            roleClass.ContactPlayer = byte.MaxValue;
        }
    }
    public static bool CheckCommond(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        for (int i = 0; i < comList.Count(); i++)
        {
            if (exact)
            {
                if (msg == "/" + comList[i]) return true;
            }
            else
            {
                if (msg.StartsWith("/" + comList[i]))
                {
                    msg = msg.Replace("/" + comList[i], string.Empty);
                    return true;
                }
            }
        }
        return false;
    }
}