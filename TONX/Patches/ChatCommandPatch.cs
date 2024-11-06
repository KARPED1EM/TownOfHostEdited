using Assets.CoreScripts;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TONX.Modules;
using TONX.Roles.Core;
using UnityEngine;
using static TONX.Translator;

namespace TONX;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
internal class ChatCommands
{
    public static List<string> SentHistory = new();
    public static bool Prefix(ChatController __instance)
    {
        // クイックチャットなら横流し
        if (__instance.quickChatField.Visible)
        {
            return true;
        }
        // 入力欄に何も書かれてなければブロック
        if (string.IsNullOrWhiteSpace(__instance.freeChatField.textArea.text))
        {
            return false;
        }

        __instance.timeSinceLastMessage = 3f;

        var text = __instance.freeChatField.textArea.text;
        if (SentHistory.Count == 0 || SentHistory[^1] != text) SentHistory.Add(text);
        ChatControllerUpdatePatch.CurrentHistorySelection = SentHistory.Count;

        Logger.Info(text, "SendChat");
        var mc = MessageControl.Create(PlayerControl.LocalPlayer, text);

        if (mc.RecallMode != MsgRecallMode.None && !mc.ForceSend)
        {
            Logger.Info("Message Sendding Canceled", "SendChat");
            __instance.freeChatField.textArea.Clear();
            return false;
        }
        else if (SendTargetPatch.SendTarget != SendTargetPatch.SendTargets.Default)
        {
            switch (SendTargetPatch.SendTarget)
            {
                case SendTargetPatch.SendTargets.All:
                    Utils.SendMessage(text, title: $"<color=#ff0000>{GetString("MessageFromTheHost")}</color>");
                    break;
                case SendTargetPatch.SendTargets.Dead:
                    Main.AllPlayerControls.Where(p => p.AmOwner || !p.IsAlive()).Do(p => Utils.SendMessage(text, p.PlayerId, $"<color=#ff0000>{GetString("MessageFromTheHost")}</color>"));
                    break;
            }
            __instance.freeChatField.textArea.Clear();
            return false;
        }
        return true;
    }
    public static void OnReceiveChat(PlayerControl player, string text, out bool blockForLocalPlayer)
    {
        blockForLocalPlayer = false;

        if (text.StartsWith("\n")) text = text[1..].Trim();
        if (text.EndsWith('\0'))
        {
            blockForLocalPlayer = true;
            return;
        }

        ChatUpdatePatch.DoBlockChat = true;

        var mc = MessageControl.Create(player, text);
        if (mc.RecallMode == MsgRecallMode.Spam)
        {
            blockForLocalPlayer = true;
            MessageControl.TryHideMessage(false, false);
        }

        ChatUpdatePatch.DoBlockChat = false;

        if (!mc.IsCommand && SpamManager.CheckSpam(player, text))
            blockForLocalPlayer = true;
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
        if (clientId == -1)
        {
            player.SetName(title);
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            player.SetName(name);
        }
        var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
        writer.StartMessage(clientId);

        writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
            .Write(player.Data.NetId)
            .Write(title)
            .EndRpc();
        writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
            .Write(msg)
            .EndRpc();
        writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
                        .Write(player.Data.NetId)
.Write(player.Data.PlayerName)
            .EndRpc();
        writer.EndMessage();
        writer.SendMessage();
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