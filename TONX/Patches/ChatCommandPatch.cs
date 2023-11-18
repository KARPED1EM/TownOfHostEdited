using Assets.CoreScripts;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TONX.Modules;
using TONX.Roles.Core;
using TONX.Roles.Crewmate;
using UnityEngine;
using static TONX.Translator;

namespace TONX;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
internal class ChatCommands
{
    public static List<string> ChatHistory = new();
    private static Dictionary<CustomRoles, List<string>> roleCommands;

    public static bool Prefix(ChatController __instance)
    {
        if (roleCommands == null) InitRoleCommands();

        // クイックチャットなら横流し
        if (__instance.quickChatField.Visible)
        {
            return true;
        }
        // 入力欄に何も書かれてなければブロック
        if (__instance.freeChatField.textArea.text == "")
        {
            return false;
        }
        __instance.timeSinceLastMessage = 3f;
        var text = __instance.freeChatField.textArea.text;
        if (ChatHistory.Count == 0 || ChatHistory[^1] != text) ChatHistory.Add(text);
        ChatControllerUpdatePatch.CurrentHistorySelection = ChatHistory.Count;
        string[] args = text.Split(' ');
        string subArgs = "";
        var canceled = false;
        var cancelVal = "";
        Main.isChatCommand = true;
        Logger.Info(text, "SendChat");
        ChatManager.GetMessage(PlayerControl.LocalPlayer, text);
        if (ChatManager.cancel == true) { canceled = true; ChatManager.cancel = false; }
        if (ChatManager.end == true) { ChatManager.end = false; goto End; }
        if (text.Length >= 3) if (text[..2] == "/r" && text[..3] != "/rn") args[0] = "/r";
        if (text.Length >= 4) if (text[..3] == "/up") args[0] = "/up";
        foreach (var func in CustomRoleManager.ReceiveMessage)
        {
            if (!func(PlayerControl.LocalPlayer, text))
            {
                canceled = true;
                goto End;
            }
        }
        switch (args[0])
        {
            case "/dump":
                canceled = true;
                Utils.DumpLog();
                break;
            case "/v":
            case "/version":
                canceled = true;
                string version_text = "";
                foreach (var kvp in Main.playerVersion.OrderBy(pair => pair.Key))
                {
                    version_text += $"{kvp.Key}:{Main.AllPlayerNames[kvp.Key]}:{kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n";
                }
                if (version_text != "") Utils.AddChatMessage(version_text);
                break;
            default:
                Main.isChatCommand = false;
                break;
        }
        if (AmongUsClient.Instance.AmHost)
        {
            Main.isChatCommand = true;
            switch (args[0])
            {
                case "/win":
                case "/winner":
                    canceled = true;
                    if (Main.winnerNameList.Count < 1) Utils.SendMessage(GetString("NoInfoExists"));
                    else Utils.SendMessage("Winner: " + string.Join(",", Main.winnerNameList));
                    break;

                case "/l":
                case "/lastresult":
                    canceled = true;
                    Utils.ShowKillLog();
                    Utils.ShowLastResult();
                    break;

                case "/rn":
                case "/rename":
                    canceled = true;
                    if (args.Length < 1) break;
                    if (args[1].Length is > 10 or < 1)
                        Utils.SendMessage(GetString("Message.AllowNameLength"), PlayerControl.LocalPlayer.PlayerId);
                    else Main.nickName = args[1];
                    break;

                case "/hn":
                case "/hidename":
                    canceled = true;
                    Main.HideName.Value = args.Length > 1 ? args.Skip(1).Join(delimiter: " ") : Main.HideName.DefaultValue.ToString();
                    GameStartManagerPatch.HideName.text = Main.HideName.Value;
                    break;

                case "/level":
                    canceled = true;
                    subArgs = args.Length < 2 ? "" : args[1];
                    Utils.SendMessage(string.Format(GetString("Message.SetLevel"), subArgs), PlayerControl.LocalPlayer.PlayerId);
                    int.TryParse(subArgs, out int input);
                    if (input is < 1 or > 999)
                    {
                        Utils.SendMessage(GetString("Message.AllowLevelRange"), PlayerControl.LocalPlayer.PlayerId);
                        break;
                    }
                    var number = Convert.ToUInt32(input);
                    PlayerControl.LocalPlayer.RpcSetLevel(number - 1);
                    break;

                case "/n":
                case "/now":
                    canceled = true;
                    subArgs = args.Length < 2 ? "" : args[1];
                    switch (subArgs)
                    {
                        case "r":
                        case "roles":
                            Utils.ShowActiveRoles();
                            break;
                        default:
                            Utils.ShowActiveSettings();
                            break;
                    }
                    break;

                case "/dis":
                case "/disconnect":
                    canceled = true;
                    subArgs = args.Length < 2 ? "" : args[1];
                    switch (subArgs)
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
                            cancelVal = "/dis";
                            break;
                    }
                    break;

                case "/r":
                    canceled = true;
                    subArgs = text.Remove(0, 2);
                    SendRolesInfo(subArgs, PlayerControl.LocalPlayer.PlayerId);
                    break;

                case "/up":
                    canceled = true;
                    subArgs = text.Remove(0, 3);
                    SpecifyRole(subArgs, PlayerControl.LocalPlayer.PlayerId);
                    break;

                case "/h":
                case "/help":
                    canceled = true;
                    Utils.ShowHelp(PlayerControl.LocalPlayer.PlayerId);
                    break;

                case "/m":
                case "/myrole":
                    canceled = true;
                    if (GameStates.IsInGame)
                    {
                        var role = PlayerControl.LocalPlayer.GetCustomRole();
                        Utils.SendMessage(
                            role.GetRoleInfo()?.Description?.GetFullFormatHelpWithAddons(PlayerControl.LocalPlayer) ??
                            // roleInfoがない役職
                            GetString(role.ToString()) + PlayerControl.LocalPlayer.GetRoleInfo(true),
                            PlayerControl.LocalPlayer.PlayerId);
                    }
                    else
                    {
                        Utils.SendMessage(GetString("Message.CanNotUseInLobby"), PlayerControl.LocalPlayer.PlayerId);
                    }
                    break;

                case "/t":
                case "/template":
                    canceled = true;
                    if (args.Length > 1) TemplateManager.SendTemplate(args[1]);
                    else Utils.AddChatMessage($"{GetString("ForExample")}:\n{args[0]} test");
                    break;

                case "/mw":
                case "/messagewait":
                    canceled = true;
                    if (args.Length > 1 && int.TryParse(args[1], out int sec))
                    {
                        Main.MessageWait.Value = sec;
                        Utils.SendMessage(string.Format(GetString("Message.SetToSeconds"), sec), 0);
                    }
                    else Utils.SendMessage($"{GetString("Message.MessageWaitHelp")}\n{GetString("ForExample")}:\n{args[0]} 3", 0);
                    break;

                case "/exe":
                    canceled = true;
                    if (GameStates.IsLobby)
                    {
                        Utils.SendMessage(GetString("Message.CanNotUseInLobby"), PlayerControl.LocalPlayer.PlayerId);
                        break;
                    }
                    if (args.Length < 2 || !int.TryParse(args[1], out int id)) break;
                    var player = Utils.GetPlayerById(id);
                    if (player != null)
                    {
                        player.Data.IsDead = true;
                        var state = PlayerState.GetByPlayerId(player.PlayerId);
                        state.DeathReason = CustomDeathReason.etc;
                        player.RpcExileV2();
                        state.SetDead();
                        if (player.AmOwner) Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
                        else Utils.SendMessage(string.Format(GetString("Message.Executed"), player.Data.PlayerName));
                    }
                    break;

                case "/kill":
                    canceled = true;
                    if (GameStates.IsLobby)
                    {
                        Utils.SendMessage(GetString("Message.CanNotUseInLobby"), PlayerControl.LocalPlayer.PlayerId);
                        break;
                    }
                    if (args.Length < 2 || !int.TryParse(args[1], out int id2)) break;
                    var target = Utils.GetPlayerById(id2);
                    if (target != null)
                    {
                        target.RpcMurderPlayer(target);
                        if (target.AmOwner) Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
                        else Utils.SendMessage(string.Format(GetString("Message.Executed"), target.Data.PlayerName));
                    }
                    break;

                case "/colour":
                case "/color":
                    canceled = true;
                    if (GameStates.IsInGame)
                    {
                        Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), PlayerControl.LocalPlayer.PlayerId);
                        break;
                    }
                    subArgs = args.Length < 2 ? "" : args[1];
                    var color = Utils.MsgToColor(subArgs, true);
                    if (color == byte.MaxValue)
                    {
                        Utils.SendMessage(GetString("IllegalColor"), PlayerControl.LocalPlayer.PlayerId);
                        break;
                    }
                    PlayerControl.LocalPlayer.RpcSetColor(color);
                    Utils.SendMessage(string.Format(GetString("Message.SetColor"), subArgs), PlayerControl.LocalPlayer.PlayerId);
                    break;

                case "/quit":
                case "/qt":
                    canceled = true;
                    Utils.SendMessage(GetString("Message.CanNotUseByHost"), PlayerControl.LocalPlayer.PlayerId);
                    break;

                case "/id":
                    canceled = true;
                    string msgText = GetString("PlayerIdList");
                    foreach (var pc in Main.AllPlayerControls)
                        msgText += "\n" + pc.PlayerId.ToString() + " → " + Main.AllPlayerNames[pc.PlayerId];
                    Utils.SendMessage(msgText, PlayerControl.LocalPlayer.PlayerId);
                    break;

                case "/qq":
                    canceled = true;
                    if (Main.newLobby) Cloud.ShareLobby(true);
                    else Utils.SendMessage("很抱歉，每个房间车队姬只会发一次", PlayerControl.LocalPlayer.PlayerId);
                    break;

                case "/end":
                    canceled = true;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                    GameManager.Instance.LogicFlow.CheckEndCriteria();
                    break;

                case "/mt":
                case "/hy":
                    canceled = true;
                    if (GameStates.IsMeeting) MeetingHud.Instance.RpcClose();
                    else PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true);
                    break;

                case "/cs":
                    canceled = true;
                    subArgs = text.Remove(0, 3);
                    PlayerControl.LocalPlayer.RPCPlayCustomSound(subArgs.Trim());
                    break;

                case "/sd":
                    canceled = true;
                    subArgs = text.Remove(0, 3);
                    if (subArgs.Length < 1 || !int.TryParse(subArgs, out int sound1)) break;
                    RPC.PlaySoundRPC(PlayerControl.LocalPlayer.PlayerId, (Sounds)sound1);
                    break;

                case "/cosid":
                    canceled = true;
                    var of = PlayerControl.LocalPlayer.Data.DefaultOutfit;
                    Logger.Warn($"ColorId: {of.ColorId}", "Get Cos Id");
                    Logger.Warn($"PetId: {of.PetId}", "Get Cos Id");
                    Logger.Warn($"HatId: {of.HatId}", "Get Cos Id");
                    Logger.Warn($"SkinId: {of.SkinId}", "Get Cos Id");
                    Logger.Warn($"VisorId: {of.VisorId}", "Get Cos Id");
                    Logger.Warn($"NamePlateId: {of.NamePlateId}", "Get Cos Id");
                    break;

                default:
                    Main.isChatCommand = false;
                    break;
            }
        }
    End:
        if (canceled)
        {
            Logger.Info("Command Canceled", "ChatCommand");
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(cancelVal);
        }
        else if (SendTargetPatch.SendTarget != SendTargetPatch.SendTargets.Default)
        {
            switch (SendTargetPatch.SendTarget)
            {
                case SendTargetPatch.SendTargets.All:
                    Utils.SendMessage(text, title: $"<color=#ff0000>{GetString("MessageFromTheHost")}</color>");
                    break;
                case SendTargetPatch.SendTargets.Dead:
                    Main.AllPlayerControls.Where(p => p.AmOwner || !p.IsAlive()).Do(p =>
                        Utils.SendMessage(text, p.PlayerId, $"<color=#ff0000>{GetString("MessageFromTheHost")}</color>")
                        );
                    break;
            }
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(cancelVal);
            return false;
        }
        return !canceled;
    }
    public static bool GetRoleByInputName(string input, out CustomRoles output, bool includeVanilla = false)
    {
        output = new();
        input = Regex.Replace(input, @"[0-9]+", string.Empty); //清除数字
        input = Regex.Replace(input, @"\s", string.Empty); //清除空字符
        input = Regex.Replace(input, @"[\x01-\x1F,\x7F]", string.Empty); //清除无效字符
        input = input.ToLower().Trim().Replace("是", string.Empty).Replace("着", "者");
        if (input == "") return false;
        foreach (CustomRoles role in Enum.GetValues(typeof(CustomRoles)))
        {
            if (!includeVanilla && role.IsVanilla()) continue;
            if (input == GetString(Enum.GetName(typeof(CustomRoles), role)).TrimStart('*').ToLower().Trim().Replace(" ", string.Empty).RemoveHtmlTags() //匹配到翻译文件中的职业原名
                || (roleCommands.TryGetValue(role, out var com) && com.Any(c => input == c.Trim().ToLower())) //匹配到职业缩写
                )
            {
                output = role;
                return true;
            }
        }
        return false;
    }
    public static void InitRoleCommands()
    {
        // 初回のみ処理
#pragma warning disable IDE0028  // Dictionary初期化の簡素化をしない
        roleCommands = new();

        // GM
        roleCommands.Add(CustomRoles.GM, new() { "gm", "管理" });

        // RoleClass
        ConcatCommands(CustomRoleTypes.Impostor);
        ConcatCommands(CustomRoleTypes.Crewmate);
        ConcatCommands(CustomRoleTypes.Neutral);

        // SubRoles
        roleCommands.Add(CustomRoles.Lovers, new() { "lo", "情人", "愛人", "链子" });
        roleCommands.Add(CustomRoles.Watcher, new() { "wat", "窺視者", "窥视" });
        roleCommands.Add(CustomRoles.Workhorse, new() { "wh", "加班" });
        roleCommands.Add(CustomRoles.Avanger, new() { "av", "復仇者", "复仇" });
        roleCommands.Add(CustomRoles.Bait, new() { "ba", "誘餌", "大奖", "头奖" });
        roleCommands.Add(CustomRoles.Bewilder, new() { "bwd", "迷幻", "迷惑者" });
        roleCommands.Add(CustomRoles.Brakar, new() { "br", "破平" });
        roleCommands.Add(CustomRoles.DualPersonality, new() { "sp", "雙重人格", "双重", "双人格", "人格" });
        roleCommands.Add(CustomRoles.Egoist, new() { "ego", "利己主義者", "利己主义", "利己", "野心" });
        roleCommands.Add(CustomRoles.Flashman, new() { "fl", "閃電俠", "闪电" });
        roleCommands.Add(CustomRoles.Fool, new() { "fo", "蠢蛋", "笨蛋", "蠢狗", "傻逼" });
        roleCommands.Add(CustomRoles.Lighter, new() { "li", "執燈人", "执灯", "灯人", "小灯人" });
        roleCommands.Add(CustomRoles.Ntr, new() { "np", "ntr", "渣男" });
        roleCommands.Add(CustomRoles.Oblivious, new() { "pb", "膽小鬼", "胆小" });
        roleCommands.Add(CustomRoles.Reach, new() { "re", "持槍", "手长" });
        roleCommands.Add(CustomRoles.Seer, new() { "se", "靈媒" });
        roleCommands.Add(CustomRoles.Trapper, new() { "tra", "陷阱師", "陷阱", "小奖" });
        roleCommands.Add(CustomRoles.Youtuber, new() { "yt", "up" });
        roleCommands.Add(CustomRoles.Mimic, new() { "mi", "寶箱怪", "宝箱" });
        roleCommands.Add(CustomRoles.TicketsStealer, new() { "ts", "竊票者", "偷票", "偷票者", "窃票师", "窃票" });
#pragma warning restore IDE0028
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
            Utils.SendMessage(role.GetRoleInfo().Description.FullFormatHelp, playerId);
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
            roleCommands[role.RoleName] = new();
            coms.DoIf(c => c.Trim() != "", roleCommands[role.RoleName].Add);
        }
    }
    public static void OnReceiveChat(PlayerControl player, string text, out bool canceled)
    {
        if (roleCommands == null) InitRoleCommands();

        canceled = false;
        if (!AmongUsClient.Instance.AmHost) return;
        if (text.StartsWith("\n")) text = text[1..];
        if (SpamManager.CheckSpam(player, text)) return;
        if (!text.StartsWith("/")) return;
        string[] args = text.Split(' ');
        string subArgs = "";
        ChatManager.GetMessage(player, text);
        if (ChatManager.cancel == true) { canceled = true; ChatManager.cancel = false; }
        if (ChatManager.end == true) { ChatManager.end = false; return; }
        if (text.Length >= 3) if (text[..2] == "/r" && text[..3] != "/rn") args[0] = "/r";
        foreach (var func in CustomRoleManager.ReceiveMessage)
        {
            if (!func(player, text))
            {
                canceled = true;
                return;
            }
        }
        switch (args[0])
        {
            case "/l":
            case "/lastresult":
                Utils.ShowKillLog(player.PlayerId);
                Utils.ShowLastResult(player.PlayerId);
                break;

            case "/n":
            case "/now":
                subArgs = args.Length < 2 ? "" : args[1];
                switch (subArgs)
                {
                    case "r":
                    case "roles":
                        Utils.ShowActiveRoles(player.PlayerId);
                        break;
                    default:
                        Utils.ShowActiveSettings(player.PlayerId);
                        break;
                }
                break;

            case "/r":
                subArgs = text.Remove(0, 2);
                SendRolesInfo(subArgs, player.PlayerId);
                break;

            case "/h":
            case "/help":
                Utils.ShowHelpToClient(player.PlayerId);
                break;

            case "/m":
            case "/myrole":
                if (GameStates.IsInGame)
                {
                    var role = player.GetCustomRole();
                    if (role.GetRoleInfo()?.Description is { } description)
                    {
                        Utils.SendMessage(description.GetFullFormatHelpWithAddons(player), player.PlayerId, removeTags: false);
                    }
                    // roleInfoがない役職
                    else
                    {
                        Utils.SendMessage(GetString(role.ToString()) + player.GetRoleInfo(true), player.PlayerId);
                    }
                }
                else
                {
                    Utils.SendMessage(GetString("Message.CanNotUseInLobby"), player.PlayerId);
                }
                break;

            case "/t":
            case "/template":
                if (args.Length > 1) TemplateManager.SendTemplate(args[1], player.PlayerId);
                else Utils.SendMessage($"{GetString("ForExample")}:\n{args[0]} test", player.PlayerId);
                break;

            case "/colour":
            case "/color":
                if (Options.PlayerCanSetColor.GetBool())
                {
                    if (GameStates.IsInGame)
                    {
                        Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId);
                        break;
                    }
                    subArgs = args.Length < 2 ? "" : args[1];
                    var color = Utils.MsgToColor(subArgs);
                    if (color == byte.MaxValue)
                    {
                        Utils.SendMessage(GetString("IllegalColor"), player.PlayerId);
                        break;
                    }
                    player.RpcSetColor(color);
                    Utils.SendMessage(string.Format(GetString("Message.SetColor"), subArgs), player.PlayerId);
                }
                else
                {
                    Utils.SendMessage(GetString("DisableUseCommand"), player.PlayerId);
                }
                break;

            case "/quit":
            case "/qt":
                subArgs = args.Length < 2 ? "" : args[1];
                var cid = player.PlayerId.ToString();
                cid = cid.Length != 1 ? cid.Substring(1, 1) : cid;
                if (subArgs.Equals(cid))
                {
                    string name = player.GetRealName();
                    Utils.SendMessage(string.Format(GetString("Message.PlayerQuitForever"), name));
                    Utils.KickPlayer(player.GetClientId(), true, "VoluntarilyQuit");
                }
                else
                {
                    Utils.SendMessage(string.Format(GetString("SureUse.quit"), cid), player.PlayerId);
                }
                break;

            case "/say":
            case "/s":
                if (player.IsDev() && args.Length > 1)
                    Utils.SendMessage(args.Skip(1).Join(delimiter: " "), title: $"<color={Main.ModColor}>{GetString("MessageFromDev")}</color>");
                break;

            default:
                break;
        }
    }
}
[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal class ChatUpdatePatch
{
    public static bool Active = false;
    public static bool DoBlockChat = false;
    public static void Postfix(ChatController __instance)
    {
        Active = __instance.IsOpenOrOpening;

        __instance.freeChatField.textArea.AllowPaste = true;
        __instance.chatBubblePool.Prefab.Cast<ChatBubble>().TextArea.overrideColorTags = false;

        if (!AmongUsClient.Instance.AmHost || Main.MessagesToSend.Count < 1 || (Main.MessagesToSend[0].Item2 == byte.MaxValue && Main.MessageWait.Value > __instance.timeSinceLastMessage)) return;
        if (DoBlockChat) return;
        var player = Main.AllAlivePlayerControls.OrderBy(x => x.PlayerId).FirstOrDefault() ?? Main.AllPlayerControls.OrderBy(x => x.PlayerId).FirstOrDefault();
        if (player == null) return;
        (string msg, byte sendTo, string title) = Main.MessagesToSend[0];
        Main.MessagesToSend.RemoveAt(0);
        int clientId = sendTo == byte.MaxValue ? -1 : Utils.GetPlayerById(sendTo).GetClientId();
        var name = player.Data.PlayerName;

        Dictionary<int, bool> receiver = new();
        if (clientId == -1)
        {
            if (msg.RemoveHtmlTags() == msg) receiver.TryAdd(-1, false);
            else if (Main.AllPlayerControls.All(p => !p.AmOwner && !p.IsModClient())) receiver.TryAdd(-1, false);
            else if (Main.AllPlayerControls.All(p => p.IsModClient())) receiver.TryAdd(-1, true);
            else Main.AllPlayerControls.Do(p => receiver.TryAdd(p.GetClientId(), p.IsModClient()));
        }
        else
        {
            Main.AllPlayerControls.DoIf(p => p.GetClientId() == clientId, p => receiver.TryAdd(p.GetClientId(), p.IsModClient()));
            receiver.Remove(-1);
        }

        foreach (var kvp in receiver)
        {
            var (id, isMod) = kvp;

            if (id == -1)
            {
                player.SetName(title);
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
                player.SetName(name);
            }

            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
            writer.StartMessage(id);
            writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
                .Write(title)
                .EndRpc();
            writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
                .Write(isMod ? msg : msg.RemoveHtmlTags())
                .EndRpc();
            writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
                .Write(player.Data.PlayerName)
                .EndRpc();
            writer.EndMessage();
            writer.SendMessage();
        }

        __instance.timeSinceLastMessage = 0f;
    }
}
[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
internal class UpdateCharCountPatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        int length = __instance.textArea.text.Length;
        __instance.charCountText.SetText($"{length}/{__instance.textArea.characterLimit}");
        if (length < (AmongUsClient.Instance.AmHost ? 888 : 250))
            __instance.charCountText.color = Color.black;
        else if (length < (AmongUsClient.Instance.AmHost ? 999 : 300))
            __instance.charCountText.color = new Color(1f, 1f, 0f, 1f);
        else
            __instance.charCountText.color = Color.red;
    }
}
[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal class AddChatPatch
{
    public static void Postfix(string chatText)
    {
        switch (chatText)
        {
            default:
                break;
        }
        if (!AmongUsClient.Instance.AmHost) return;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal class RpcSendChatPatch
{
    public static bool Prefix(PlayerControl __instance, string chatText, ref bool __result)
    {
        if (string.IsNullOrWhiteSpace(chatText))
        {
            __result = false;
            return false;
        }
        int return_count = PlayerControl.LocalPlayer.name.Count(x => x == '\n');
        chatText = new StringBuilder(chatText).Insert(0, "\n", return_count).ToString();
        if (AmongUsClient.Instance.AmClient && DestroyableSingleton<HudManager>.Instance)
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(__instance, chatText);
        if (chatText.Contains("who", StringComparison.OrdinalIgnoreCase))
            DestroyableSingleton<UnityTelemetry>.Instance.SendWho();
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpc(__instance.NetId, (byte)RpcCalls.SendChat, SendOption.None);
        messageWriter.Write(chatText);
        messageWriter.EndMessage();
        __result = true;
        return false;
    }
}
public static class ChatManager
{
    public static bool cancel = false;
    public static bool end = false;
    private static List<string> chatHistory = new();
    private const int maxHistorySize = 20;
    private static Dictionary<string, bool> ismatch = new();
    #region 检查
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
    #endregion
    public static void GetMessage(PlayerControl player, string message)
    {
        if (!player.IsAlive() || !AmongUsClient.Instance.AmHost) return;
        if (!Options.BlockMsgPlus.GetBool()) return;
        int operate;
        string msg = message;
        message = message.ToLower().TrimStart().TrimEnd();
        if (!GameStates.IsInGame) operate = 1;
        else if (CustomRoleManager.GetByPlayerId(player.PlayerId)?.OnSendMessage(message) ?? false) operate = 2;
        else if (CheckCommond(ref message, "up", false)) operate = 3;
        else if (CheckCommond(ref message, "r|role|m|myrole|n|now")) operate = 4;
        else if (CheckCommond(ref message, "", false)) operate = 5;
        else operate = 1;
        if (operate == 1)
        {
            message = msg;
            string chatEntry = $"{player.PlayerId}: {message}";
            chatHistory.Add(chatEntry);
            if (chatHistory.Count > maxHistorySize)
            {
                chatHistory.RemoveAt(0);
            }
            cancel = false;
        }
        else if (operate == 2)
        {
            Logger.Info($"指令{msg}，不记录", "ChatManager");
            message = msg;
            cancel = true;
            end = true;
        }
        else if (operate == 3)
        {
            Logger.Info($"指令{msg}，不记录", "ChatManager");
            message = msg;
            cancel = false;
        }
        else if (operate == 4)
        {
            Logger.Info($"指令{msg}，不记录", "ChatManager");
            message = msg;
            SendPreviousMessagesToAll();
            cancel = false;
        }
        else if (operate == 5)
        {
            Logger.Info($"包含特殊信息，不记录", "ChatManager");
            message = msg;
            SendPreviousMessagesToAll();
            cancel = true;
        }
    }
    public static void SendPreviousMessagesToAll()
    {
        ismatch = new();
        List<PlayerControl> playerControls = new();
        for (int i = 0; i < 20; i++) 
        {
            Utils.SendMessage(GetString("HideMessage"), 255);
        }
        foreach (var reviveplayer in Main.AllPlayerControls)
        {
            //新base的获取死因不知道怎么整，可能要麻烦咔哥了qwq
            playerControls.Add(reviveplayer);
            reviveplayer.Data.IsDead = false;
            reviveplayer.Revive();
        }
        AntiBlackout.SendGameData();
        foreach (var entry in chatHistory)
        {
            var entryParts = entry.Split(':');
            var senderId = entryParts[0].Trim();
            var senderMessage = entryParts[1].Trim();
            //CustomDeathReason deathreason = 0;
            ismatch.Add(senderId, false);
            foreach (var senderPlayer in Main.AllPlayerControls)
            {
                if (senderPlayer.PlayerId.ToString() == senderId)
                {
                    if (!senderPlayer.Data.IsDead)
                    {
                        ismatch[senderPlayer.PlayerId.ToString()] = true;
                        DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);
                        var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                        writer.StartMessage(-1);
                        writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
                            .Write(senderMessage)
                            .EndRpc();
                        writer.EndMessage();
                        writer.SendMessage();
                    }
                    else
                    {
                        Utils.SendMessage(senderMessage, 255, GetString("PlayerIdNF"));
                    }
                }
                if (!ismatch[senderPlayer.PlayerId.ToString()])
                {
                    Utils.SendMessage(senderMessage, 255, GetString("PlayerIdNF"));
                }
            }
        }
        foreach (var deadplayer in playerControls)
        {
            deadplayer.Data.IsDead = true;
            deadplayer.RpcExileV2();
            //deadplayer.SetDeathReason(deathreason);
        }
        AntiBlackout.SendGameData();
    }
   
}