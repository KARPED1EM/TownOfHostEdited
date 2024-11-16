using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using TONX.Roles.Core;

namespace TONX.Modules;

public class MessageControl
{
    public string Message { get; set; }
    public string Args { get; set; }
    public bool HasValidArgs { get; set; }

    public PlayerControl Player { get; set; }
    public bool IsAlive { get => Player.IsAlive(); }
    public bool IsFromMod { get => Player.IsModClient(); }
    public bool IsFromSelf { get => Player.AmOwner; }

    public bool IsCommand { get; set; } = false;
    public bool ForceSend { get; set; } = false;
    public MsgRecallMode RecallMode { get; set; } = MsgRecallMode.None;

    public MessageControl(PlayerControl player, string message)
    {
        Player = player;
        Message = message;

        ChatCommand.InitRoleCommands();

        MsgRecallMode recallMode = MsgRecallMode.None;
        // Check if it is a role command
        IsCommand = Player.GetRoleClass()?.OnSendMessage(Message, out recallMode) ?? false;
        if (IsCommand && !AmongUsClient.Instance.AmHost) ForceSend = true;
        CustomRoleManager.ReceiveMessage.Do(a => a.Invoke(this));

        if (ChatCommand.AllCommands == null || !ChatCommand.AllCommands.Any())
            ChatCommand.Init();

        RecallMode = recallMode;
        if (IsCommand || !AmongUsClient.Instance.AmHost) return;

        if (!IsCommand)
        {
            // Not a role command, check for command list
            foreach (var command in ChatCommand.AllCommands)
            {
                if (command.Access switch
                {
                    CommandAccess.All => false,
                    CommandAccess.LocalMod => !IsFromSelf,
                    CommandAccess.Host => !AmongUsClient.Instance.AmHost || !IsFromSelf,
                    CommandAccess.Debugger => !DebugModeManager.AmDebugger,
                    _ => true,
                }) continue;

                string keyword = command.KeyWords.Find(k => Message.ToLower().StartsWith("/" + k.ToLower()));
                if (string.IsNullOrEmpty(keyword)) continue;

                Args = Message[(keyword.Length + 1)..].Trim();
                HasValidArgs = !string.IsNullOrWhiteSpace(Args);

                Logger.Info($"Command: /{keyword}, Args: {Args}", "ChatControl");

                (RecallMode, string msg) = command.Command(this);
                if (!string.IsNullOrEmpty(msg)) Utils.SendMessage(msg, Player.PlayerId);
                IsCommand = true;
                return;
            }
        }
    }

    public static List<MessageControl> History;
    public static MessageControl Create(PlayerControl player, string message)
    {
        var mc = new MessageControl(player, message);
        if (!AmongUsClient.Instance.AmHost) return mc;
        History ??= new();
        if (!message.EndsWith('\0') && !(player?.Data?.PlayerName?.EndsWith('\0') ?? true)) History.Add(mc);
        if (History?.Count > 50) History.RemoveAt(0);
        return mc;
    }

    public static void TryHideMessage(bool includeHost, bool includeModded)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        SendHistoryMessages(includeHost, includeModded);
    }

    public static void SendHistoryMessages(bool includeHost = false, bool includeModded = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var sendList = History.Where(m => m.IsAlive && m.RecallMode == MsgRecallMode.None);
        for (int i = 0; i <= 20 - sendList.Count(); i++)
        {
            // The number of historical messages is not enough to cover all messages
            var player = Main.AllAlivePlayerControls.ToArray()[IRandom.Instance.Next(0, Main.AllAlivePlayerControls.Count())];
            SendMessageAsPlayerImmediately(player, "Hello " + Main.ModName, includeHost, includeModded);
        }
        foreach (var mc in sendList)
        {
            SendMessageAsPlayerImmediately(mc.Player, mc.Message, includeHost, includeModded);
        }
    }

    public static void SpamFakeCommands(bool includeHost = false, bool includeModded = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        List<CustomRoles> roles = Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x is not CustomRoles.NotAssigned).ToList();
        var rd = IRandom.Instance;
        string msg;
        string[] command = new string[] { "bet", "bt", "guess", "gs", "shoot", "st", "赌", "猜", "审判", "tl", "判", "审" };
        for (int i = 0; i < 20; i++)
        {
            msg = "/";
            if (rd.Next(1, 100) < 20)
            {
                msg += "id";
            }
            else
            {
                msg += command[rd.Next(0, command.Length - 1)];
                msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                msg += rd.Next(0, 15).ToString();
                msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                CustomRoles role = roles[rd.Next(0, roles.Count)];
                msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                msg += Utils.GetRoleName(role);
            }
            var player = Main.AllAlivePlayerControls.ToArray()[rd.Next(0, Main.AllAlivePlayerControls.Count())];
            SendMessageAsPlayerImmediately(player, msg, includeHost, includeModded);
        }
    }

    public static void SendMessageAsPlayerImmediately(PlayerControl player, string text, bool hostCanSee = true, bool sendToModded = true)
    {
        if (hostCanSee) DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, text);
        if (!sendToModded) text += "\0";

        var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
        writer.StartMessage(-1);
        writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
            .Write(text)
            .EndRpc();
        writer.EndMessage();
        writer.SendMessage();
    }
}

public enum MsgRecallMode
{
    None,
    Block, // Cancel sending msg for modded client
    Spam, // Spam lot of messages to hide original message
}