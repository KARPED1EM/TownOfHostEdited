using System.Collections.Generic;
using System.Linq;
using TONX.Roles.Core;
using static TONX.Translator;

namespace TONX;

public static class ConfirmEjections
{
    // 参考：https://github.com/music-discussion/TownOfHost-TheOtherRoles

    public static void Apply(NetworkedPlayerInfo exiledPlayer, bool decidedWinner, List<string> winDescriptionText)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (exiledPlayer == null) return;
        var exileId = exiledPlayer.PlayerId;
        if (exileId is < 0 or > 254) return;

        var player = exiledPlayer.Object;
        var playerName = player.GetRealName(isMeeting: true);
        var coloredPlayerName = Utils.ColorString(Main.PlayerColors[player.PlayerId], playerName);
        var role = exiledPlayer.GetCustomRole();
        var roleName = GetRoleString(role.ToString());
        var coloredRoleName = Utils.GetTrueRoleName(exileId, false);
        var roleType = player.Is(CustomRoles.Madmate) ? CustomRoleTypes.Impostor
            : player.Is(CustomRoles.Charmed) ? CustomRoleTypes.Neutral
            : role.GetCustomRoleTypes();
        var coloredTeamName = GetString($"Team{roleType}").Color(Utils.GetCustomRoleTypeColor(roleType));

        string text = string.Empty;
        int impNum = Main.AllAlivePlayerControls.Count(p => p.Is(CustomRoleTypes.Impostor) || p.Is(CustomRoles.Madmate));
        int neutralNum = Main.AllAlivePlayerControls.Count(p => p.Is(CustomRoleTypes.Neutral) || p.Is(CustomRoles.Charmed));

        if (CustomRoles.Bard.IsExist()) // 吟游诗人创作
        {
            try { text = ModUpdater.Get("https://v1.hitokoto.cn/?encode=text"); }
            catch { text = GetString("ByBardGetFailed"); }
            text += "\n\t\t——" + GetString("ByBard");
            goto EndOfSession;
        }
        else if (decidedWinner) // 已经决定胜利者
        {
            text = string.Format(GetString("ExiledWrongPerson"), coloredPlayerName, coloredRoleName);
            winDescriptionText.Do(t => text += $"\n{t}");
        }
        else // 没有胜利者，游戏继续
        {
            switch (Options.CEMode.GetInt())
            {
                case 0: // 不确认身份
                    text = string.Format(GetString("PlayerExiled"), coloredPlayerName);
                    break;
                case 1: // 确认阵营
                    text = roleType is CustomRoleTypes.Crewmate
                        ? string.Format(GetString("IsGood"), coloredPlayerName)
                        : string.Format(GetString("BelongTo"), coloredPlayerName, coloredTeamName);
                    break;
                case 2: // 确认职业
                    text = string.Format(GetString("PlayerIsRole"), coloredPlayerName, coloredRoleName);
                    if (Options.ShowTeamNextToRoleNameOnEject.GetBool())
                        text += $" ({coloredTeamName})";
                    break;
            }
        }

        if (Options.ShowImpRemainOnEject.GetBool() && !decidedWinner)
        {
            text += "\n";
            string comma = neutralNum > 0 ? "，" : "";
            if (impNum == 0) text += GetString("NoImpRemain") + comma;
            else text += string.Format(GetString("ImpRemain"), impNum) + comma;
            if (Options.ShowNKRemainOnEject.GetBool() && neutralNum > 0)
                text += string.Format(GetString("NeutralRemain"), neutralNum);
        }

    EndOfSession:

        text += "<size=0>";
        _ = new LateTask(() =>
        {
            Main.DoBlockNameChange = true;
            if (GameStates.IsInGame) player.RpcSetName(text);
        }, 3.0f, "Change Exiled Player Name");
        _ = new LateTask(() =>
        {
            if (GameStates.IsInGame && !player.Data.Disconnected)
            {
                player.RpcSetName(playerName);
                Main.DoBlockNameChange = false;
            }
        }, 11.5f, "Change Exiled Player Name Back");
    }
}