using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TONX.Roles.Core;
using TONX.Roles.Core.Descriptions;
using UnityEngine;
using static TONX.Translator;

namespace TONX.Modules;

public class ChatCommand(List<string> keywords, CommandAccess access, Func<MessageControl, (MsgRecallMode, string)> command)
{
    public List<string> KeyWords { get; set; } = keywords;

    public CommandAccess Access { get; set; } = access;

    public Func<MessageControl, (MsgRecallMode, string)> Command { get; set; } = command;

    public static List<ChatCommand> AllCommands;

    public static void Init()
    {
        InitRoleCommands();
        AllCommands = new()
        {
            new(["dump"], CommandAccess.LocalMod, mc =>
            {
                Utils.DumpLog();
                return (MsgRecallMode.Block, null);
            }),
            new(["v", "ver", "version"], CommandAccess.LocalMod, mc =>
            {
                StringBuilder sb = new();
                foreach (var kvp in Main.playerVersion.OrderBy(pair => pair.Key))
                    sb.Append($"{kvp.Key}:{Main.AllPlayerNames[kvp.Key]}:{kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n");
                return (MsgRecallMode.Block, sb.ToString());
            }),
            new(["win", "winner"], CommandAccess.All, mc =>
            {
                string text = GetString("NoInfoExists");
                if (Main.winnerNameList.Any())
                    text = "Winner: " + string.Join(",", Main.winnerNameList);
                return (MsgRecallMode.Block, text);
            }),
             new(["level"], CommandAccess.Host, mc =>
            {
                string text = GetString("Message.AllowLevelRange");
                if (int.TryParse(mc.Args, out int level) && level is >= 1 and <= 999)
                {
                    text = string.Format(GetString("Message.SetLevel"), level);
                    mc.Player.RpcSetLevel(Convert.ToUInt32(level) - 1);
                }
                return (MsgRecallMode.Block, text);
            }),new(["l", "lastresult"], CommandAccess.All, mc =>
            {
                Utils.ShowKillLog(mc.Player.PlayerId);
                Utils.ShowLastResult(mc.Player.PlayerId);
                return (MsgRecallMode.Block, null);
            }),
            new(["rn", "rename"], CommandAccess.Host, mc =>
            {
                string text = mc.Args.Length is > 10 or < 1 ? GetString("Message.AllowNameLength") : null;
                if (text == null) Main.HostNickName = mc.Args;
                return (MsgRecallMode.Block, text);
            }),
                        new(["role", "r"], CommandAccess.All, mc =>
            {
                SendRolesInfo(mc.Args, mc.Player.PlayerId);
                return (MsgRecallMode.Block, null);
            }),
            new(["hn", "hidename"], CommandAccess.Host, mc =>
            {
                Main.HideName.Value = mc.HasValidArgs ? mc.Args : Main.HideName.DefaultValue.ToString();
                GameStartManagerPatch.HideName.text = Main.HideName.Value;
                return (MsgRecallMode.Block, null);
            }),

            new(["now", "n" ], CommandAccess.All, mc =>
            {
                switch (mc.Args)
                {
                    case "r":
                    case "roles":
                        Utils.ShowActiveRoles(mc.Player.PlayerId);
                        break;
                    default:
                        Utils.ShowActiveSettings(mc.Player.PlayerId);
                        break;
                }
                return (MsgRecallMode.Block, null);
            }),
            new(["dis", "disconnect"], CommandAccess.Host, mc =>
            {
                switch (mc.Args)
                {
                    case "crew":
                        GameManager.Instance.enabled = false;
                        GameManager.Instance.RpcEndGame(GameOverReason.HumansDisconnect, false);
                        break;
                    case "imp":
                        GameManager.Instance.enabled = false;
                        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                        break;
                    default:
                        Utils.AddChatMessage("crew | imp");
                        break;
                }
                return (MsgRecallMode.Block, null);
            }),

            new(["up", "specify"], CommandAccess.Host, mc =>
            {
                SpecifyRole(mc.Args, mc.Player.PlayerId);
                return (MsgRecallMode.Block, null);
            }),
            new(["h", "help"], CommandAccess.All, mc =>
            {
                Utils.ShowHelp(mc.Player.PlayerId);
                return (MsgRecallMode.Block, null);
            }),
            new(["m", "myrole"], CommandAccess.All, mc =>
            {
                string text = GetString("Message.CanNotUseInLobby");
                if (GameStates.IsInGame)
                {
                    var role = mc.Player.GetCustomRole();
                    text = role.GetRoleInfo()?.Description?.GetFullFormatHelpWithAddonsByPlayer(mc.Player) ??
                        // roleInfoがない役職
                        GetString(role.ToString()) + mc.Player.GetRoleInfo(true);
                }
                return (MsgRecallMode.Block, text);
            }),
            new(["t", "template"], CommandAccess.LocalMod, mc =>
            {
                if (mc.HasValidArgs) TemplateManager.SendTemplate(mc.Args);
                else Utils.AddChatMessage($"{GetString("ForExample")}:\nt test");
                return (MsgRecallMode.Block, null);
            }),
            new(["mw", "messagewait"], CommandAccess.Host, mc =>
            {
                string text = $"{GetString("Message.MessageWaitHelp")}\n{GetString("ForExample")}:\nmw 3";
                if (int.TryParse(mc.Args, out int sec))
                {
                    Main.MessageWait.Value = sec;
                    text = string.Format(GetString("Message.SetToSeconds"), sec);
                }
                return (MsgRecallMode.Block, text);
            }),
            new(["exe", "execute"], CommandAccess.Host, mc =>
            {
                string text = GetString("Message.CanNotUseInLobby");
                if (GameStates.IsInGame)
                {
                    if (!mc.HasValidArgs || !int.TryParse(mc.Args, out int id)) return (MsgRecallMode.Block, null);
                    var target = Utils.GetPlayerById(id);
                    if (target != null)
                    {
                        target.Data.IsDead = true;
                        var state = PlayerState.GetByPlayerId(target.PlayerId);
                        state.DeathReason = CustomDeathReason.etc;
                        target.RpcExileV2();
                        state.SetDead();
                        text = target.AmOwner
                            ? Utils.ColorString(Color.red, GetString("HostKillSelfByCommand"))
                            : string.Format(GetString("Message.Executed"), target.Data.PlayerName);
                    }
                }
                return (MsgRecallMode.Block, text);
            }),
            new(["kill"], CommandAccess.Host, mc =>
            {
                string text = GetString("Message.CanNotUseInLobby");
                if (GameStates.IsInGame)
                {
                    if (!mc.HasValidArgs || !int.TryParse(mc.Args, out int id)) return (MsgRecallMode.Block, null);
                    var target = Utils.GetPlayerById(id);
                    if (target != null)
                    {
                        var state = PlayerState.GetByPlayerId(target.PlayerId);
                        state.DeathReason = CustomDeathReason.etc;
                        target.RpcMurderPlayer(target);
                        text = target.AmOwner
                            ? Utils.ColorString(Color.red, GetString("HostKillSelfByCommand"))
                            : string.Format(GetString("Message.Executed"), target.Data.PlayerName);
                    }
                }
                return (MsgRecallMode.Block, text);
            }),
            new(["color", "colour"], Options.PlayerCanSetColor.GetBool() ?CommandAccess.All : CommandAccess.Host, mc =>
            {
                string text = GetString("Message.OnlyCanUseInLobby");
                if (GameStates.IsLobby )
                {
                    text = GetString("IllegalColor");
                    var color = Utils.MsgToColor(mc.Args, mc.IsFromSelf);
                    if (color != byte.MaxValue)
                    {
                        mc.Player.RpcSetColor(color);
                        text = string.Format(GetString("Message.SetColor"), mc.Args);
                    }
                }
                return (MsgRecallMode.Block, text);
            }),
            new(["qt", "quit"], CommandAccess.All, mc =>
            {
                string text = GetString("Message.CanNotUseByHost");
                if (!mc.IsFromSelf)
                {
                    var cid = mc.Player.PlayerId.ToString();
                    cid = cid.Length != 1 ? cid.Substring(1, 1) : cid;
                    if (mc.Args.Equals(cid))
                    {
                        string name = mc.Player.GetRealName();
                        text = string.Format(GetString("Message.PlayerQuitForever"), name);
                        Utils.KickPlayer(mc.Player.GetClientId(), true, "VoluntarilyQuit");
                    }
                    else
                    {
                        text = string.Format(GetString("SureUse.quit"), cid);
                    }
                }
                return (MsgRecallMode.Block, text);
            }),
            new(["id"], CommandAccess.All, mc =>
            {
                string text = GetString("PlayerIdList");
                foreach (var pc in Main.AllPlayerControls)
                    text += "\n" + pc.PlayerId.ToString() + " → " + Main.AllPlayerNames[pc.PlayerId];
                return (MsgRecallMode.Block, text);
            }),
            new(["end", "endgame"], CommandAccess.Host, mc =>
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                GameManager.Instance.LogicFlow.CheckEndCriteria();
                return (MsgRecallMode.Block, null);
            }),
            new(["hy", "mt", "meeting"], CommandAccess.Host, mc =>
            {
                if (GameStates.IsMeeting) MeetingHud.Instance.RpcClose();
                else mc.Player.NoCheckStartMeeting(null, true);
                return (MsgRecallMode.Block, null);
            }),
            new(["cosid"], CommandAccess.Host, mc =>
            {
                var of =mc.Player.Data.DefaultOutfit;
                Logger.Warn($"ColorId: {of.ColorId}", "Get Cos Id");
                Logger.Warn($"PetId: {of.PetId}", "Get Cos Id");
                Logger.Warn($"HatId: {of.HatId}", "Get Cos Id");
                Logger.Warn($"SkinId: {of.SkinId}", "Get Cos Id");
                Logger.Warn($"VisorId: {of.VisorId}", "Get Cos Id");
                Logger.Warn($"NamePlateId: {of.NamePlateId}", "Get Cos Id");
                return (MsgRecallMode.Block, null);
            }),
        };
    }

    private static Dictionary<CustomRoles, List<string>> RoleCommands;
    public static void InitRoleCommands()
    {
        // 初回のみ処理
        RoleCommands = new();

        // GM
        RoleCommands.Add(CustomRoles.GM, new() { "gm", "管理" });

        // RoleClass
        ConcatCommands(CustomRoleTypes.Impostor);
        ConcatCommands(CustomRoleTypes.Crewmate);
        ConcatCommands(CustomRoleTypes.Neutral);

        // SubRoles
        RoleCommands.Add(CustomRoles.Lovers, new() { "lo", "情人", "愛人", "链子" });
        RoleCommands.Add(CustomRoles.Watcher, new() { "wat", "窺視者", "窥视" });
        RoleCommands.Add(CustomRoles.Workhorse, new() { "wh", "加班" });
        RoleCommands.Add(CustomRoles.Avenger, new() { "av", "復仇者", "复仇" });
        RoleCommands.Add(CustomRoles.Bait, new() { "ba", "誘餌", "大奖", "头奖" });
        RoleCommands.Add(CustomRoles.Bewilder, new() { "bwd", "迷幻", "迷惑者" });
        RoleCommands.Add(CustomRoles.Tiebreaker, new() { "br", "破平" });
        RoleCommands.Add(CustomRoles.Schizophrenic, new() { "sp", "雙重人格", "双重", "双人格", "人格" });
        RoleCommands.Add(CustomRoles.Egoist, new() { "ego", "利己主義者", "利己主义", "利己", "野心" });
        RoleCommands.Add(CustomRoles.Flashman, new() { "fl", "閃電俠", "闪电" });
        RoleCommands.Add(CustomRoles.Fool, new() { "fo", "蠢蛋", "笨蛋", "蠢狗", "傻逼" });
        RoleCommands.Add(CustomRoles.Lighter, new() { "li", "執燈人", "执灯", "灯人", "小灯人" });
        RoleCommands.Add(CustomRoles.Neptune, new() { "np", "ntr", "渣男" });
        RoleCommands.Add(CustomRoles.Oblivious, new() { "pb", "膽小鬼", "胆小" });
        RoleCommands.Add(CustomRoles.Reach, new() { "re", "持槍", "手长" });
        RoleCommands.Add(CustomRoles.Seer, new() { "se", "靈媒" });
        RoleCommands.Add(CustomRoles.Beartrap, new() { "tra", "陷阱師", "陷阱", "小奖" });
        RoleCommands.Add(CustomRoles.YouTuber, new() { "yt", "up" });
        RoleCommands.Add(CustomRoles.Mimic, new() { "mi", "寶箱怪", "宝箱" });
        RoleCommands.Add(CustomRoles.TicketsStealer, new() { "ts", "竊票者", "偷票", "偷票者", "窃票师", "窃票" });
    }
    public static void SendRolesInfo(string input, byte playerId)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Utils.ShowActiveRoles(playerId);
            return;
        }
        else if (!GetRoleByInputName(input, out var role))
        {
            Utils.SendMessage(GetString("Message.CanNotFindRoleThePlayerEnter"), playerId);
            return;
        }
        else
        {
            var roleinfo = role.GetRoleInfo();
            if (!role.IsAddon())
            {

                var roleDescription = roleinfo.Description;
                var rff = roleDescription.FullFormatHelp;
                Utils.SendMessage(rff, playerId);
            }
            Utils.SendMessage(AddonDescription.FullFormatHelpBySubRole(role) ??
        // roleInfoがない役職
        $"<size=130%><color={Utils.GetRoleColor(role)}>{GetString(role.ToString())}</color></size>:\n\n{AddonDescription.FullFormatHelpBySubRole(role)}", playerId);
        }
    }
    public static void SpecifyRole(string input, byte playerId)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Utils.ShowActiveRoles(playerId);
            return;
        }
        else if (!GetRoleByInputName(input, out var role))
        {
            Utils.SendMessage(GetString("Message.DirectorModeCanNotFindRoleThePlayerEnter"), playerId);
            return;
        }
        else if (!Options.EnableDirectorMode.GetBool())
        {
            Utils.SendMessage(string.Format(GetString("Message.DirectorModeDisabled"), GetString("EnableDirectorMode")));
        }
        else if (!GameStates.IsLobby)
        {
            Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), playerId);
        }
        else
        {
            string roleName = GetString(Enum.GetName(typeof(CustomRoles), role));
            if (
                !role.IsEnable()
                || role.IsAddon()
                || role.IsVanilla()
                || role is CustomRoles.GM or CustomRoles.NotAssigned
                || !Options.CustomRoleSpawnChances.ContainsKey(role))
            {
                Utils.SendMessage(string.Format(GetString("Message.DirectorModeSelectFailed"), roleName), playerId);
            }
            else
            {
                byte pid = playerId == byte.MaxValue ? byte.MinValue : playerId;
                Main.DevRole.Remove(pid);
                Main.DevRole.Add(pid, role);

                Utils.SendMessage(string.Format(GetString("Message.DirectorModeSelected"), roleName), playerId);
            }
        }
    }
    private static void ConcatCommands(CustomRoleTypes roleType)
    {
        var roles = CustomRoleManager.AllRolesInfo.Values.Where(role => role.CustomRoleType == roleType);
        foreach (var role in roles)
        {
            if (role.ChatCommand is null) continue;
            var coms = role.ChatCommand.Split('|');
            RoleCommands[role.RoleName] = new();
            coms.DoIf(c => c.Trim() != "", RoleCommands[role.RoleName].Add);
        }
    }
    public static bool GetRoleByInputName(string input, out CustomRoles output, bool includeVanilla = false)
    {
        output = new();
        input = Regex.Replace(input, @"[0-9]+", string.Empty); //清除数字
        input = Regex.Replace(input, @"\s", string.Empty); //清除空字符
        input = Regex.Replace(input, @"[\x01-\x1F,\x7F]", string.Empty); //清除无效字符
        input = input.ToLower().Trim().Replace("是", string.Empty).Replace("着", "者");
        if (string.IsNullOrEmpty(input)) return false;
        foreach (CustomRoles role in Enum.GetValues(typeof(CustomRoles)))
        {
            if (!includeVanilla && role.IsVanilla()) continue;
            if (input == GetString(Enum.GetName(typeof(CustomRoles), role)).TrimStart('*').ToLower().Trim().Replace(" ", string.Empty).RemoveHtmlTags() //匹配到翻译文件中的职业原名
                || (RoleCommands.TryGetValue(role, out var com) && com.Any(c => input == c.Trim().ToLower())) //匹配到职业缩写
                )
            {
                output = role;
                return true;
            }
        }
        return false;
    }
}

public enum CommandAccess
{
    All, // Everyone Can use this command
    LocalMod, // Command won't received by host
    Host, // Only host can use this comand
    Debugger,
}