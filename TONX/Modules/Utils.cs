using AmongUs.Data;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TONX.Modules;
using TONX.Roles.AddOns.Crewmate;
using TONX.Roles.AddOns.Impostor;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;
using TONX.Roles.Crewmate;
using TONX.Roles.Impostor;
using TONX.Roles.Neutral;
using UnityEngine;
using static TONX.Translator;

namespace TONX;

public static class Utils
{
    private static readonly DateTime timeStampStartTime = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static long GetTimeStamp(DateTime? dateTime = null) => (long)((dateTime ?? DateTime.Now).ToUniversalTime() - timeStampStartTime).TotalSeconds;
    public static void ErrorEnd(string text)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            Logger.Fatal($"{text} 错误，触发防黑屏措施", "Anti-black");
            ChatUpdatePatch.DoBlockChat = true;
            Main.OverrideWelcomeMsg = GetString("AntiBlackOutNotifyInLobby");
            _ = new LateTask(() =>
            {
                Logger.SendInGame(GetString("AntiBlackOutLoggerSendInGame"), true);
            }, 3f, "Anti-Black Msg SendInGame");
            _ = new LateTask(() =>
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                GameManager.Instance.LogicFlow.CheckEndCriteria();
                RPC.ForceEndGame(CustomWinner.Error);
            }, 5.5f, "Anti-Black End Game");
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.AntiBlackout, SendOption.Reliable);
            writer.Write(text);
            writer.EndMessage();
            if (Options.EndWhenPlayerBug.GetBool())
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutRequestHostToForceEnd"), true);
                }, 3f, "Anti-Black Msg SendInGame");
            }
            else
            {
                _ = new LateTask(() =>
                {
                    Logger.SendInGame(GetString("AntiBlackOutHostRejectForceEnd"), true);
                }, 3f, "Anti-Black Msg SendInGame");
                _ = new LateTask(() =>
                {
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                    Logger.Fatal($"{text} 错误，已断开游戏", "Anti-black");
                }, 8f, "Anti-Black Exit Game");
            }
        }
    }
    public static void TPAll(Vector2 location)
    {
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            TP(pc.NetTransform, location);
    }

    public static void TP(CustomNetworkTransform nt, Vector2 location)
    {
        location += new Vector2(0, 0.3636f);
        if (AmongUsClient.Instance.AmHost) nt.SnapTo(location);
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(nt.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
        //nt.WriteVector2(location, writer);
        NetHelpers.WriteVector2(location, writer);
        writer.Write(nt.lastSequenceId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static ClientData GetClientById(int id)
    {
        try
        {
            var client = AmongUsClient.Instance.allClients.ToArray().Where(cd => cd.Id == id).FirstOrDefault();
            return client;
        }
        catch
        {
            return null;
        }
    }
    public static bool IsActive(SystemTypes type)
    {
        // ないものはfalse
        if (!ShipStatus.Instance.Systems.ContainsKey(type))
        {
            return false;
        }
        int mapId = Main.NormalOptions.MapId;
        switch (type)
        {
            case SystemTypes.Electrical:
                {
                    var SwitchSystem = ShipStatus.Instance.Systems[type].Cast<SwitchSystem>();
                    return SwitchSystem != null && SwitchSystem.IsActive;
                }
            case SystemTypes.Reactor:
                {
                    if (mapId == 2) return false;
                    else
                    {
                        var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                        return ReactorSystemType != null && ReactorSystemType.IsActive;
                    }
                }
            case SystemTypes.Laboratory:
                {
                    if (mapId != 2) return false;
                    var ReactorSystemType = ShipStatus.Instance.Systems[type].Cast<ReactorSystemType>();
                    return ReactorSystemType != null && ReactorSystemType.IsActive;
                }
            case SystemTypes.LifeSupp:
                {
                    if (mapId is 2 or 4) return false;
                    var LifeSuppSystemType = ShipStatus.Instance.Systems[type].Cast<LifeSuppSystemType>();
                    return LifeSuppSystemType != null && LifeSuppSystemType.IsActive;
                }
            case SystemTypes.Comms:
                {
                    if (mapId is 1 or 5)
                    {
                        var HqHudSystemType = ShipStatus.Instance.Systems[type].Cast<HqHudSystemType>();
                        return HqHudSystemType != null && HqHudSystemType.IsActive;
                    }
                    else
                    {
                        var HudOverrideSystemType = ShipStatus.Instance.Systems[type].Cast<HudOverrideSystemType>();
                        return HudOverrideSystemType != null && HudOverrideSystemType.IsActive;
                    }
                }
            case SystemTypes.HeliSabotage:
                {
                    var HeliSabotageSystem = ShipStatus.Instance.Systems[type].Cast<HeliSabotageSystem>();
                    return HeliSabotageSystem != null && HeliSabotageSystem.IsActive;
                }
            case SystemTypes.MushroomMixupSabotage:
                {
                    var mushroomMixupSabotageSystem = ShipStatus.Instance.Systems[type].TryCast<MushroomMixupSabotageSystem>();
                    return mushroomMixupSabotageSystem != null && mushroomMixupSabotageSystem.IsActive;
                }
            default:
                return false;
        }
    }
    public static SystemTypes GetCriticalSabotageSystemType() => (MapNames)Main.NormalOptions.MapId switch
    {
        MapNames.Polus => SystemTypes.Laboratory,
        MapNames.Airship => SystemTypes.HeliSabotage,
        _ => SystemTypes.Reactor,
    };
    public static void SetVision(this IGameOptions opt, bool HasImpVision)
    {
        if (HasImpVision)
        {
            opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.CrewLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod) * 5);
            }
            return;
        }
        else
        {
            opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.CrewLightMod));
            if (IsActive(SystemTypes.Electrical))
            {
                opt.SetFloat(
                FloatOptionNames.ImpostorLightMod,
                opt.GetFloat(FloatOptionNames.ImpostorLightMod) / 5);
            }
            return;
        }
    }
    //誰かが死亡したときのメソッド
    public static void TargetDies(MurderInfo info)
    {
        PlayerControl killer = info.AppearanceKiller, target = info.AttemptTarget;

        if (!target.Data.IsDead || GameStates.IsMeeting || !AmongUsClient.Instance.AmHost) return;
        foreach (var seer in Main.AllPlayerControls)
        {
            if (KillFlashCheck(info, seer))
            {
                seer.KillFlash();
                continue;
            }
        }
    }
    public static bool KillFlashCheck(MurderInfo info, PlayerControl seer)
    {
        PlayerControl killer = info.AppearanceKiller, target = info.AttemptTarget;

        if (seer.Is(CustomRoles.GM) || seer.Is(CustomRoles.Seer)) return true;
        if (seer.Data.IsDead || killer == seer || target == seer) return false;

        if (seer.GetRoleClass() is IKillFlashSeeable killFlashSeeable)
        {
            return killFlashSeeable.CheckKillFlash(info);
        }

        if (target.Is(CustomRoles.Celebrity) && (Celebrity.CanSeeKillFlash(seer) || target.Is(CustomRoles.Madmate))) return true;

        return false;
    }
    public static void KillFlash(this PlayerControl player)
    {
        //キルフラッシュ(ブラックアウト+リアクターフラッシュ)の処理
        bool ReactorCheck = IsActive(GetCriticalSabotageSystemType());

        var Duration = Options.KillFlashDuration.GetFloat();
        if (ReactorCheck) Duration += 0.2f; //リアクター中はブラックアウトを長くする

        //実行
        var state = PlayerState.GetByPlayerId(player.PlayerId);
        state.IsBlackOut = true; //ブラックアウト
        if (player.AmOwner)
        {
            FlashColor(new(1f, 0f, 0f, 0.3f));
            if (Constants.ShouldPlaySfx()) RPC.PlaySound(player.PlayerId, Sounds.KillSound);
        }
        else if (player.IsModClient())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.KillFlash, SendOption.Reliable, player.GetClientId());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else if (!ReactorCheck) player.ReactorFlash(0f); //リアクターフラッシュ
        player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            state.IsBlackOut = false; //ブラックアウト解除
            player.MarkDirtySettings();
        }, Options.KillFlashDuration.GetFloat(), "RemoveKillFlash");
    }
    public static void BlackOut(this IGameOptions opt, bool IsBlackOut)
    {
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
        if (IsBlackOut)
        {
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0);
            opt.SetFloat(FloatOptionNames.CrewLightMod, 0);
        }
        return;
    }
    /// <summary>
    /// seerが自分であるときのseenのRoleName + ProgressText
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <returns>RoleName + ProgressTextを表示するか、構築する色とテキスト(bool, Color, string)</returns>
    public static (bool enabled, string text) GetRoleNameAndProgressTextData(PlayerControl seer, PlayerControl seen = null)
    {
        var roleName = GetDisplayRoleName(seer, seen);
        var progressText = GetProgressText(seer, seen);
        var text = roleName + (roleName != "" ? " " : "") + progressText;
        return (text != "", text);
    }
    /// <summary>
    /// GetDisplayRoleNameDataからRoleNameを構築
    /// </summary>
    /// <param name="seer">見る側</param>
    /// <param name="seen">見られる側</param>
    /// <returns>構築されたRoleName</returns>
    public static string GetDisplayRoleName(PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        //デフォルト値
        bool enabled = seer == seen
                    || seen.Is(CustomRoles.GM)
                    || (seer.AmOwner && Main.GodMode.Value)
                    || (Main.VisibleTasksCount && !seer.IsAlive() && Options.GhostCanSeeOtherRoles.GetBool())

                    || (seer.Is(CustomRoles.Lovers) && seen.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool())

                    || (seer.Is(CustomRoleTypes.Impostor) && seen.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool())
                    || (seer.Is(CustomRoles.Madmate) && seen.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool())
                    || (seer.Is(CustomRoleTypes.Impostor) && seen.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool())
                    || (seer.Is(CustomRoles.Madmate) && seen.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool())

                    || (seer.Is(CustomRoles.Charmed) && seen.Is(CustomRoles.Charmed) && Succubus.OptionTargetKnowOtherTarget.GetBool());

        var (roleColor, roleText) = GetTrueRoleNameData(seen.PlayerId, seer == seen || !seer.IsAlive());

        //seen側による変更
        seen.GetRoleClass()?.OverrideDisplayRoleNameAsSeen(seer, ref enabled, ref roleColor, ref roleText);

        //seer側による変更
        seer.GetRoleClass()?.OverrideDisplayRoleNameAsSeer(seen, ref enabled, ref roleColor, ref roleText);

        return enabled ? ColorString(roleColor, roleText) : "";
    }
    /// <summary>
    /// 引数の指定通りのRoleNameを表示
    /// </summary>
    /// <param name="mainRole">表示する役職</param>
    /// <param name="subRolesList">表示する属性のList</param>
    /// <returns>RoleNameを構築する色とテキスト(Color, string)</returns>
    public static (Color color, string text) GetRoleNameData(CustomRoles mainRole, List<CustomRoles> subRolesList, bool showSubRoleMarks = true)
    {
        string roleText = "";
        Color roleColor = Color.white;

        if (mainRole < CustomRoles.NotAssigned)
        {
            roleText = GetRoleName(mainRole);
            roleColor = GetRoleColor(mainRole);
        }

        if (subRolesList != null)
        {
            foreach (var subRole in subRolesList)
            {
                if (subRole <= CustomRoles.NotAssigned) continue;
                switch (subRole)
                {
                    case CustomRoles.LastImpostor:
                        roleText = GetRoleString("Last-") + roleText;
                        break;
                    case CustomRoles.Madmate:
                        roleText = GetRoleString("Mad-") + roleText;
                        roleColor = GetRoleColor(CustomRoles.Madmate);
                        break;
                    case CustomRoles.Charmed:
                        roleText = GetRoleString("Charmed-") + roleText;
                        roleColor = GetRoleColor(CustomRoles.Charmed);
                        break;
                }
            }
        }

        string subRoleMarks = showSubRoleMarks ? GetSubRoleMarks(subRolesList) : "";

        return (roleColor, subRoleMarks + roleText);
    }
    public static string GetSubRoleMarks(List<CustomRoles> subRolesList)
    {
        var sb = new StringBuilder(100);
        if (subRolesList != null)
        {
            foreach (var subRole in subRolesList)
            {
                if (subRole <= CustomRoles.NotAssigned || subRole is CustomRoles.LastImpostor or CustomRoles.Madmate or CustomRoles.Charmed or CustomRoles.Lovers) continue;
                sb.Append(ColorString(GetRoleColor(subRole), GetString("Prefix." + subRole.ToString())));
            }
        }
        return sb.ToString();
    }
    /// <summary>
    /// 対象のRoleNameを全て正確に表示
    /// </summary>
    /// <param name="playerId">見られる側のPlayerId</param>
    /// <returns>RoleNameを構築する色とテキスト(Color, string)</returns>
    private static (Color color, string text) GetTrueRoleNameData(byte playerId, bool showSubRoleMarks = true)
    {
        var state = PlayerState.GetByPlayerId(playerId);
        var (color, text) = GetRoleNameData(state.MainRole, state.SubRoles, showSubRoleMarks);
        CustomRoleManager.GetByPlayerId(playerId)?.OverrideTrueRoleName(ref color, ref text);
        return (color, text);
    }
    /// <summary>
    /// 対象のRoleNameを全て正確に表示
    /// </summary>
    /// <param name="playerId">見られる側のPlayerId</param>
    /// <returns>構築したRoleName</returns>
    public static string GetTrueRoleName(byte playerId, bool showSubRoleMarks = true)
    {
        var (color, text) = GetTrueRoleNameData(playerId, showSubRoleMarks);
        return ColorString(color, text);
    }
    public static string GetRoleName(CustomRoles role, bool forUser = true)
    {
        return GetRoleString(Enum.GetName(typeof(CustomRoles), role), forUser);
    }
    public static string GetDeathReason(CustomDeathReason status)
    {
        return GetString("DeathReason." + Enum.GetName(typeof(CustomDeathReason), status));
    }
    public static Color GetRoleColor(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = role.GetRoleInfo()?.RoleColorCode;
        _ = ColorUtility.TryParseHtmlString(hexColor, out Color c);
        return c;
    }
    public static string GetRoleColorCode(CustomRoles role)
    {
        if (!Main.roleColors.TryGetValue(role, out var hexColor)) hexColor = role.GetRoleInfo()?.RoleColorCode;
        return hexColor;
    }
    public static Color GetRoleTeamColor(CustomRoles role)
        => role.IsValid()
        ? GetCustomRoleTypeColor(role.GetCustomRoleTypes())
        : new Color32(255, 255, 255, byte.MaxValue);
    public static string GetRoleTeamColorCode(CustomRoles role)
        => role.IsValid()
        ? GetCustomRoleTypeColorCode(role.GetCustomRoleTypes())
        : "#FFFFFF";
    public static Color GetCustomRoleTypeColor(CustomRoleTypes type)
    {
        return type switch
        {
            CustomRoleTypes.Crewmate => new Color32(140, 255, 255, byte.MaxValue),
            CustomRoleTypes.Impostor => new Color32(255, 25, 25, byte.MaxValue),
            CustomRoleTypes.Neutral => new Color32(255, 171, 27, byte.MaxValue),
            CustomRoleTypes.Addon => new Color32(255, 154, 206, byte.MaxValue),
            _ => new Color(1, 1, 1)
        };
    }
    public static string GetCustomRoleTypeColorCode(CustomRoleTypes type)
    {
        return type switch
        {
            CustomRoleTypes.Crewmate => "#8cffff",
            CustomRoleTypes.Impostor => "#f74631",
            CustomRoleTypes.Neutral => "#ffab1b",
            CustomRoleTypes.Addon => "#ff9ace",
            _ => "#FFFFFF"
        };
    }
    public static string GetKillCountText(byte playerId)
    {
        int count = PlayerState.GetByPlayerId(playerId)?.GetKillCount(true) ?? 0;
        if (count < 1) return "";
        return ColorString(new Color32(255, 69, 0, byte.MaxValue), string.Format(GetString("KillCount"), count));
    }
    public static string GetVitalText(byte playerId, bool RealKillerColor = false, bool summary = false)
    {
        var state = PlayerState.GetByPlayerId(playerId);
        string deathReason = state.IsDead ? GetString("DeathReason." + state.DeathReason) : (summary ? "" : GetString("Alive"));
        if (RealKillerColor)
        {
            var KillerId = state.GetRealKiller();
            Color color = KillerId != byte.MaxValue ? Main.PlayerColors[KillerId] : GetRoleColor(CustomRoles.Doctor);
            if (state.DeathReason is CustomDeathReason.Disconnected or CustomDeathReason.Vote) color = new Color32(255, 255, 255, 60);
            deathReason = ColorString(color, deathReason);
        }
        return deathReason;
    }
    public static string GetRoleDisplaySpawnMode(CustomRoles role, bool parentheses = true)
    {
        if (Options.HideGameSettings.GetBool() && Main.AllPlayerControls.Count() > 1)
            return string.Empty;
        string mode;
        if (role.IsVanilla()) return role.GetChance().ToString() + "%";
        else if (!Options.CustomRoleSpawnChances.ContainsKey(role)) mode = GetString("HidenRole");
        else mode = Options.CustomRoleSpawnChances[role].GetString().RemoveHtmlTags();
        return parentheses ? $"({mode})" : mode;
    }

    public static bool HasTasks(NetworkedPlayerInfo p, bool ForRecompute = true)
    {
        if (GameStates.IsLobby) return false;
        //Tasksがnullの場合があるのでその場合タスク無しとする
        if (p.Tasks == null) return false;
        if (p.Role == null) return false;
        if (p.Disconnected) return false;

        var hasTasks = true;
        var States = PlayerState.GetByPlayerId(p.PlayerId);
        if (p.Role.IsImpostor)
            hasTasks = false; //タスクはCustomRoleを元に判定する
        // 死んでいて，死人のタスク免除が有効なら確定でfalse
        if (p.IsDead && Options.GhostIgnoreTasks.GetBool())
        {
            return false;
        }
        var role = States.MainRole;
        var roleClass = CustomRoleManager.GetByPlayerId(p.PlayerId);
        if (roleClass != null)
        {
            switch (roleClass.HasTasks)
            {
                case HasTask.True:
                    hasTasks = true;
                    break;
                case HasTask.False:
                    hasTasks = false;
                    break;
                case HasTask.ForRecompute:
                    hasTasks = !ForRecompute;
                    break;
            }
        }
        switch (role)
        {
            case CustomRoles.GM:
                hasTasks = false;
                break;
            default:
                if (role.IsImpostor()) hasTasks = false;
                break;
        }

        foreach (var subRole in States.SubRoles)
            switch (subRole)
            {
                case CustomRoles.Madmate:
                case CustomRoles.Charmed:
                case CustomRoles.Lovers:
                    //ラバーズはタスクを勝利用にカウントしない
                    hasTasks &= !ForRecompute;
                    break;
            }

        return hasTasks;
    }
    public static bool CanBeMadmate(this PlayerControl pc)
    {
        return pc != null && (pc.GetRoleClass()?.CanBeMadmate ?? false) && !pc.Is(CustomRoles.Madmate)
        && !(
            (pc.Is(CustomRoles.Sheriff) && !Options.SheriffCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Mayor) && !Options.MayorCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.NiceGuesser) && !Options.NGuesserCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Snitch) && !Options.SnitchCanBeMadmate.GetBool()) ||
            (pc.Is(CustomRoles.Judge) && !Options.JudgeCanBeMadmate.GetBool()) ||
            pc.Is(CustomRoles.LazyGuy) ||
            pc.Is(CustomRoles.Egoist)
            );
    }
    private static string GetProgressText(PlayerControl seer, PlayerControl seen = null)
    {
        seen ??= seer;
        var comms = IsActive(SystemTypes.Comms) || Concealer.IsHidding;
        bool enabled = seer == seen
                    || (Main.VisibleTasksCount && !seer.IsAlive() && Options.GhostCanSeeOtherTasks.GetBool());
        string text = GetProgressText(seen.PlayerId, comms);

        //seer側による変更
        seer.GetRoleClass()?.OverrideProgressTextAsSeer(seen, ref enabled, ref text);

        return enabled ? text : "";
    }
    private static string GetProgressText(byte playerId, bool comms = false)
    {
        var ProgressText = new StringBuilder();
        var State = PlayerState.GetByPlayerId(playerId);
        var role = State.MainRole;
        var roleClass = CustomRoleManager.GetByPlayerId(playerId);
        ProgressText.Append(GetTaskProgressText(playerId, comms));
        if (roleClass != null)
        {
            ProgressText.Append(roleClass.GetProgressText(comms));
        }

        //SubRoles
        ProgressText.Append(TicketsStealer.GetProgressText(playerId, comms));

        return ProgressText.ToString();
    }
    public static string GetTaskProgressText(byte playerId, bool comms = false)
    {
        var state = PlayerState.GetByPlayerId(playerId);
        if (state == null || state.taskState == null || !state.taskState.hasTasks)
        {
            return "";
        }

        Color TextColor = Color.yellow;
        var info = GetPlayerInfoById(playerId);
        var TaskCompleteColor = HasTasks(info) ? Color.green : GetRoleColor(state.MainRole).ShadeColor(0.5f); //タスク完了後の色
        var NonCompleteColor = HasTasks(info) ? Color.yellow : Color.white; //カウントされない人外は白色

        if (Workhorse.IsThisRole(playerId))
            NonCompleteColor = Workhorse.RoleColor;

        var NormalColor = state.taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;

        TextColor = comms ? Color.gray : NormalColor;
        string Completed = comms ? "?" : $"{state.taskState.CompletedTasksCount}";
        return ColorString(TextColor, $"({Completed}/{state.taskState.AllTasksCount})");

    }
    public static void ShowActiveSettingsHelp(byte PlayerId = byte.MaxValue)
    {
        SendMessage(GetString("CurrentActiveSettingsHelp") + ":", PlayerId);

        if (Options.DisableDevices.GetBool()) { SendMessage(GetString("DisableDevicesInfo"), PlayerId); }
        if (Options.SyncButtonMode.GetBool()) { SendMessage(GetString("SyncButtonModeInfo"), PlayerId); }
        if (Options.SabotageTimeControl.GetBool()) { SendMessage(GetString("SabotageTimeControlInfo"), PlayerId); }
        if (Options.RandomMapsMode.GetBool()) { SendMessage(GetString("RandomMapsModeInfo"), PlayerId); }
        if (Options.EnableGM.GetBool()) { SendMessage(GetRoleName(CustomRoles.GM) + GetString("GMInfoLong"), PlayerId); }
        foreach (var role in CustomRolesHelper.AllStandardRoles)
        {
            if (role.IsEnable())
            {
                if (role.GetRoleInfo()?.Description is { } description)
                {
                    SendMessage(description.FullFormatHelp, PlayerId, removeTags: false);
                }
                // RoleInfoがない役職は従来処理
                else
                {
                    SendMessage(GetRoleName(role) + GetString(Enum.GetName(typeof(CustomRoles), role) + "InfoLong"), PlayerId);
                }
            }
        }

        if (Options.NoGameEnd.GetBool()) { SendMessage(GetString("NoGameEndInfo"), PlayerId); }
    }
    public static void ShowActiveSettings(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }
        if (Options.DIYGameSettings.GetBool())
        {
            SendMessage(GetString("Message.NowOverrideText"), PlayerId);
            return;
        }

        var sb = new StringBuilder().AppendFormat("<line-height={0}>", ActiveSettingsLineHeight);
        sb.AppendFormat("<size={0}>", ActiveSettingsSize);
        sb.Append("<size=100%>").Append(GetString("Settings")).Append('\n').Append("</size>");
        foreach (var opt in OptionItem.AllOptions.Where(x => x.Id is >= 2000000 and < 3000000 && !x.IsHiddenOn(Options.CurrentGameMode) && x.Parent == null))
        {
            if (opt.IsHeader) sb.Append('\n');
            if (opt.IsText) sb.Append($"   {opt.GetName()}\n");
            else sb.Append($"{opt.GetName()}: {opt.GetString()}\n");
            if (opt.GetBool()) OptionShower.ShowChildren(opt, ref sb, Color.white, 1);
        }
        foreach (var opt in OptionItem.AllOptions.Where(x => x.Id is >= 3000000 and < 5000000 && !x.IsHiddenOn(Options.CurrentGameMode) && x.Parent == null))
        {
            if (opt.IsHeader) sb.Append('\n');
            if (opt.IsText) sb.Append($"   {opt.GetName()}\n");
            else sb.Append($"{opt.GetName()}: {opt.GetString()}\n");
            if (opt.GetBool()) OptionShower.ShowChildren(opt, ref sb, Color.white, 1);
        }

        SendMessage(sb.ToString().TrimStart('\n'), PlayerId);
    }
    public static void CopyCurrentSettings()
    {
        var sb = new StringBuilder();
        if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
        {
            ClipboardHelper.PutClipboardString(GetString("Message.HideGameSettings"));
            return;
        }
        sb.Append($"━━━━━━━━━━━━【{GetString("Roles")}】━━━━━━━━━━━━");
        foreach (var role in Options.CustomRoleCounts)
        {
            if (!role.Key.IsEnable()) continue;
            sb.Append($"\n【{GetRoleName(role.Key)}:{GetRoleDisplaySpawnMode(role.Key, false)}×{role.Key.GetCount()}】\n");
            ShowChildrenSettings(Options.CustomRoleSpawnChances[role.Key], ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━【{GetString("Settings")}】━━━━━━━━━━━━");
        foreach (var opt in OptionItem.AllOptions.Where(x => x.GetBool() && x.Parent == null && x.Id >= 90000 && !x.IsHiddenOn(Options.CurrentGameMode)))
        {
            if (opt.IsText) sb.Append($"\n【{opt.GetName(true, true)}】\n");
            else sb.Append($"\n【{opt.GetName(true, true)}: {opt.GetString()}】\n");
            ShowChildrenSettings(opt, ref sb);
            var text = sb.ToString();
            sb.Clear().Append(text.RemoveHtmlTags());
        }
        sb.Append($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        ClipboardHelper.PutClipboardString(sb.ToString());
    }
    public static void ShowActiveRoles(byte PlayerId = byte.MaxValue)
    {
        if (Options.HideGameSettings.GetBool() && PlayerId != byte.MaxValue)
        {
            SendMessage(GetString("Message.HideGameSettings"), PlayerId);
            return;
        }
        var sb = new StringBuilder(GetString("Roles")).Append(':');
        sb.AppendFormat("\n{0}:{1}", GetRoleName(CustomRoles.GM), Options.EnableGM.GetString().RemoveHtmlTags());
        int headCount = -1;
        foreach (CustomRoles role in CustomRolesHelper.AllStandardRoles)
        {
            headCount++;
            if (role.IsImpostor() && headCount == 0) sb.Append("\n\n● " + GetString("TabGroup.ImpostorRoles"));
            else if (role.IsCrewmate() && headCount == 1) sb.Append("\n\n● " + GetString("TabGroup.CrewmateRoles"));
            else if (role.IsNeutral() && headCount == 2) sb.Append("\n\n● " + GetString("TabGroup.NeutralRoles"));
            else if (role.IsAddon() && headCount == 3) sb.Append("\n\n● " + GetString("TabGroup.Addons"));
            else headCount--;

            if (role.IsEnable()) sb.AppendFormat("\n{0}:{1}x{2}", GetRoleName(role), $"{Utils.GetRoleDisplaySpawnMode(role, false)}", role.GetCount());
        }
        SendMessage(sb.ToString(), PlayerId);
    }
    public static void ShowChildrenSettings(OptionItem option, ref StringBuilder sb, int deep = 0, bool forChat = false)
    {
        foreach (var opt in option.Children.Select((v, i) => new { Value = v, Index = i + 1 }))
        {
            if (forChat)
            {
                sb.Append("\n\n");
                forChat = false;
            }

            if (opt.Value.Name == "Maximum") continue; //Maximumの項目は飛ばす
            if (opt.Value.Name == "DisableSkeldDevices" && !Options.IsActiveSkeld) continue;
            if (opt.Value.Name == "DisableMiraHQDevices" && !Options.IsActiveMiraHQ) continue;
            if (opt.Value.Name == "DisablePolusDevices" && !Options.IsActivePolus) continue;
            if (opt.Value.Name == "DisableAirshipDevices" && !Options.IsActiveAirship) continue;
            if (opt.Value.Name == "PolusReactorTimeLimit" && !Options.IsActivePolus) continue;
            if (opt.Value.Name == "AirshipReactorTimeLimit" && !Options.IsActiveAirship) continue;
            if (deep > 0)
            {
                sb.Append(string.Concat(Enumerable.Repeat("┃", Mathf.Max(deep - 1, 0))));
                sb.Append(opt.Index == option.Children.Count ? "┗ " : "┣ ");
            }
            sb.Append($"{opt.Value.GetName(true).RemoveHtmlTags()}: {opt.Value.GetString()}\n");
            if (opt.Value.GetBool()) ShowChildrenSettings(opt.Value, ref sb, deep + 1);
        }
    }
    public static string LastResult = "";
    public static void ShowLastResult(byte PlayerId = byte.MaxValue)
    {
        if (AmongUsClient.Instance.IsGameStarted)
        {
            SendMessage(GetString("CantUse.lastresult"), PlayerId);
            return;
        }
        var sb = new StringBuilder();
        var winnerColor = ((CustomRoles)CustomWinnerHolder.WinnerTeam).GetRoleInfo()?.RoleColor ?? Palette.DisabledGrey;

        sb.Append("""<align="center">""");
        sb.Append("<size=150%>").Append(GetString("PlayerInfo")).Append("</size>");
        string winText = SetEverythingUpPatch.LastWinsText + (!string.IsNullOrEmpty(SetEverythingUpPatch.LastWinsReason)
            ? $" ({SetEverythingUpPatch.LastWinsReason})" : string.Empty);
        sb.Append('\n').Append(winText.Mark(winnerColor, false));
        sb.Append("</align>");

        sb.Append("<size=70%>\n");
        List<byte> cloneRoles = new(PlayerState.AllPlayerStates.Keys);
        foreach (var id in Main.winnerList.Where(i => !EndGamePatch.SummaryText[i].Contains("NotAssigned")))
        {
            sb.Append($"\n★ ".Color(winnerColor)).Append(SummaryTexts(id, true));
            cloneRoles.Remove(id);
        }
        foreach (var id in cloneRoles.Where(i => !EndGamePatch.SummaryText[i].Contains("NotAssigned")))
        {
            sb.Append($"\n　 ").Append(SummaryTexts(id, true));
        }
        SendMessage(sb.ToString(), PlayerId);
    }
    public static void ShowKillLog(byte PlayerId = byte.MaxValue)
    {
        if (GameStates.IsInGame)
        {
            SendMessage(GetString("CantUse.killlog"), PlayerId);
            return;
        }
        if (!string.IsNullOrEmpty(EndGamePatch.KillLog)) SendMessage(EndGamePatch.KillLog, PlayerId);
    }
    public static string GetSubRolesText(byte id, bool disableColor = false, bool intro = false, bool summary = false)
    {
        var SubRoles = PlayerState.GetByPlayerId(id).SubRoles;
        if (SubRoles.Count == 0 && intro == false) return "";
        var sb = new StringBuilder();
        foreach (var role in SubRoles)
        {
            if (role is CustomRoles.NotAssigned or
                        CustomRoles.LastImpostor) continue;
            if (summary && role is CustomRoles.Madmate or CustomRoles.Charmed) continue;

            var RoleText = disableColor ? GetRoleName(role) : ColorString(GetRoleColor(role), GetRoleName(role));
            sb.Append($"{ColorString(Color.white, " + ")}{RoleText}");
        }

        if (intro && !SubRoles.Contains(CustomRoles.Lovers) && !SubRoles.Contains(CustomRoles.Neptune) && CustomRoles.Neptune.IsExist())
        {
            var RoleText = disableColor ? GetRoleName(CustomRoles.Lovers) : ColorString(GetRoleColor(CustomRoles.Lovers), GetRoleName(CustomRoles.Lovers));
            sb.Append($"{ColorString(Color.white, " + ")}{RoleText}");
        }

        return sb.ToString();
    }

    public static byte MsgToColor(string text, bool isHost = false)
    {
        text = text.ToLowerInvariant();
        text = text.Replace("色", string.Empty);
        int color;
        try { color = int.Parse(text); } catch { color = -1; }
        switch (text)
        {
            case "0": case "红": case "紅": case "red": color = 0; break;
            case "1": case "蓝": case "藍": case "深蓝": case "blue": color = 1; break;
            case "2": case "绿": case "綠": case "深绿": case "green": color = 2; break;
            case "3": case "粉红": case "pink": color = 3; break;
            case "4": case "橘": case "orange": color = 4; break;
            case "5": case "黄": case "黃": case "yellow": color = 5; break;
            case "6": case "黑": case "black": color = 6; break;
            case "7": case "白": case "white": color = 7; break;
            case "8": case "紫": case "purple": color = 8; break;
            case "9": case "棕": case "brown": color = 9; break;
            case "10": case "青": case "cyan": color = 10; break;
            case "11": case "黄绿": case "黃綠": case "浅绿": case "lime": color = 11; break;
            case "12": case "红褐": case "紅褐": case "深红": case "maroon": color = 12; break;
            case "13": case "玫红": case "玫紅": case "浅粉": case "rose": color = 13; break;
            case "14": case "焦黄": case "焦黃": case "淡黄": case "banana": color = 14; break;
            case "15": case "灰": case "gray": color = 15; break;
            case "16": case "茶": case "tan": color = 16; break;
            case "17": case "珊瑚": case "coral": color = 17; break;
            case "18": case "隐藏": case "?": color = 18; break;
        }
        return !isHost && color == 18 ? byte.MaxValue : color is < 0 or > 18 ? byte.MaxValue : Convert.ToByte(color);
    }

    public static void ShowHelpToClient(byte ID)
    {
        SendMessage(
            "<color=#c06fe8>" + GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + "<color=#12bee4>" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
            , ID);
    }
    public static void ShowHelp(byte ID)
    {
        SendMessage(
            "<color=#c06fe8>" + GetString("CommandList")
            + $"\n  ○ /n {GetString("Command.now")}"
            + $"\n  ○ /r {GetString("Command.roles")}"
            + $"\n  ○ /m {GetString("Command.myrole")}"
            + $"\n  ○ /l {GetString("Command.lastresult")}"
            + $"\n  ○ /win {GetString("Command.winner")}"
            + "\n\n" + "<color=#12bee4>" + GetString("CommandOtherList")
            + $"\n  ○ /color {GetString("Command.color")}"
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /qt {GetString("Command.quit")}"
            + "\n\n" + "<color=#f14d57>" + GetString("CommandHostList")
            + $"\n  ○ /rn {GetString("Command.rename")}"
            + $"\n  ○ /mw {GetString("Command.mw")}"
            + $"\n  ○ /kill {GetString("Command.kill")}"
            + $"\n  ○ /exe {GetString("Command.exe")}"
            + $"\n  ○ /level {GetString("Command.level")}"
            + $"\n  ○ /id {GetString("Command.idlist")}"
            + $"\n  ○ /qq {GetString("Command.qq")}"
            + $"\n  ○ /dump {GetString("Command.dump")}"
            + $"\n  ○ /up {GetString("Command.up")}"
            , ID);
    }
    public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "<Default>", bool removeTags = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (title == "<Default>") title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";
        Main.MessagesToSend.Add((removeTags ? text.RemoveHtmlTags() : text, sendTo, title + '\0'));
    }
    public static void AddChatMessage(string text, string title = "")
    {
        if (!AmongUsClient.Instance.AmHost) return;
        var player = PlayerControl.LocalPlayer;
        if (title == "") title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";
        var name = player.Data.PlayerName;
        player.SetName(title + '\0');
        DestroyableSingleton<HudManager>.Instance?.Chat?.AddChat(player, text);
        player.SetName(name);
    }
    private static Dictionary<byte, PlayerControl> cachedPlayers = new(15);
    public static PlayerControl GetPlayerById(int playerId) => GetPlayerById((byte)playerId);
    public static PlayerControl GetPlayerById(byte playerId)
    {
        if (cachedPlayers.TryGetValue(playerId, out var cachedPlayer) && cachedPlayer != null)
        {
            return cachedPlayer;
        }
        var player = Main.AllPlayerControls.Where(pc => pc.PlayerId == playerId).FirstOrDefault();
        cachedPlayers[playerId] = player;
        return player;
    }
    public static NetworkedPlayerInfo GetPlayerInfoById(int PlayerId) =>
        GameData.Instance.AllPlayers.ToArray().Where(info => info.PlayerId == PlayerId).FirstOrDefault();
    private static StringBuilder SelfMark = new(20);
    private static StringBuilder SelfSuffix = new(20);
    private static StringBuilder TargetMark = new(20);
    private static StringBuilder TargetSuffix = new(20);
    public static void NotifyRoles(bool isForMeeting = false, PlayerControl SpecifySeer = null, bool NoCache = false, bool ForceLoop = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Main.AllPlayerControls == null) return;

        //ミーティング中の呼び出しは不正
        if (GameStates.IsMeeting) return;

        var caller = new StackFrame(1, false);
        var callerMethod = caller.GetMethod();
        string callerMethodName = callerMethod.Name;
        string callerClassName = callerMethod.DeclaringType.FullName;
        var logger = Logger.Handler("NotifyRoles");
        logger.Info("NotifyRolesが" + callerClassName + "." + callerMethodName + "から呼び出されました");
        HudManagerPatch.NowCallNotifyRolesCount++;
        HudManagerPatch.LastSetNameDesyncCount = 0;

        var seerList = PlayerControl.AllPlayerControls;
        if (SpecifySeer != null)
        {
            seerList = new();
            seerList.Add(SpecifySeer);
        }
        var isMushroomMixupActive = IsActive(SystemTypes.MushroomMixupSabotage);
        //seer:ここで行われた変更を見ることができるプレイヤー
        //target:seerが見ることができる変更の対象となるプレイヤー
        foreach (var seer in seerList)
        {
            //seerが落ちているときに何もしない
            if (seer == null || seer.Data.Disconnected) continue;

            if (seer.IsModClient()) continue;
            var seerRole = seer.GetRoleClass();
            string fontSize = isForMeeting ? "1.5" : Main.RoleTextSize.ToString();
            if (isForMeeting && (seer.GetClient().PlatformData.Platform is Platforms.Playstation or Platforms.Switch)) fontSize = "70%";
            logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole() + ":START");

            // 会議じゃなくて，キノコカオス中で，seerが生きていてdesyncインポスターの場合に自身の名前を消す
            if (!isForMeeting && isMushroomMixupActive && seer.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && seer.GetCustomRole().GetRoleInfo()?.IsDesyncImpostor == true)
            {
                seer.RpcSetNamePrivate("<size=0>", true, force: NoCache);
            }
            else
            {
                //名前の後ろに付けるマーカー
                SelfMark.Clear();

                //seer役職が対象のMark
                SelfMark.Append(seerRole?.GetMark(seer, isForMeeting: isForMeeting));
                //seerに関わらず発動するMark
                SelfMark.Append(CustomRoleManager.GetMarkOthers(seer, isForMeeting: isForMeeting));

                //ハートマークを付ける(自分に)
                if (seer.Is(CustomRoles.Lovers) || CustomRoles.Neptune.IsExist(true)) SelfMark.Append(ColorString(GetRoleColor(CustomRoles.Lovers), "♡"));

                //Markとは違い、改行してから追記されます。
                SelfSuffix.Clear();

                //seer役職が対象のLowerText
                SelfSuffix.Append(seerRole?.GetLowerText(seer, isForMeeting: isForMeeting));
                //seerに関わらず発動するLowerText
                SelfSuffix.Append(CustomRoleManager.GetLowerTextOthers(seer, isForMeeting: isForMeeting));

                //seer役職が対象のSuffix
                SelfSuffix.Append(seerRole?.GetSuffix(seer, isForMeeting: isForMeeting));
                //seerに関わらず発動するSuffix
                SelfSuffix.Append(CustomRoleManager.GetSuffixOthers(seer, isForMeeting: isForMeeting));

                //RealNameを取得 なければ現在の名前をRealNamesに書き込む
                string SeerRealName = seer.GetRealName(isForMeeting);

                if (!isForMeeting && MeetingStates.FirstMeeting && Options.ChangeNameToRoleInfo.GetBool())
                    SeerRealName = seer.GetRoleInfo();

                //seerの役職名とSelfTaskTextとseerのプレイヤー名とSelfMarkを合成
                var (enabled, text) = GetRoleNameAndProgressTextData(seer);
                string SelfRoleName = enabled ? $"<size={fontSize}>{text}</size>" : "";
                string SelfDeathReason = seer.KnowDeathReason(seer) ? $"({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(seer.PlayerId))})" : "";
                string SelfName = $"{ColorString(seer.GetRoleColor(), SeerRealName)}{SelfDeathReason}{SelfMark}";

                if (Pelican.IsEaten(seer.PlayerId))
                    SelfName = $"{ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"))}";
                if (NameNotifyManager.GetNameNotify(seer, out var name))
                    SelfName = name;

                SelfName = SelfRoleName + "\r\n" + SelfName;
                SelfName += SelfSuffix.ToString() == "" ? "" : "\r\n " + SelfSuffix.ToString();
                if (!isForMeeting) SelfName += "\r\n";

                //適用
                seer.RpcSetNamePrivate(SelfName, true, force: NoCache);
            }

            //seerが死んでいる場合など、必要なときのみ第二ループを実行する
            foreach (var target in Main.AllPlayerControls)
            {
                //targetがseer自身の場合は何もしない
                if (target.PlayerId == seer.PlayerId) continue;
                logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole() + ":START");

                // 会議じゃなくて，キノコカオス中で，targetが生きていてseerがdesyncインポスターの場合にtargetの名前を消す
                if (!isForMeeting && isMushroomMixupActive && target.IsAlive() && !seer.Is(CustomRoleTypes.Impostor) && seer.GetCustomRole().GetRoleInfo()?.IsDesyncImpostor == true)
                {
                    target.RpcSetNamePrivate("<size=0>", true, seer, force: NoCache);
                }
                else
                {
                    //名前の後ろに付けるマーカー
                    TargetMark.Clear();

                    //seer役職が対象のMark
                    TargetMark.Append(seerRole?.GetMark(seer, target, isForMeeting));
                    //seerに関わらず発動するMark
                    TargetMark.Append(CustomRoleManager.GetMarkOthers(seer, target, isForMeeting));

                    //ハートマークを付ける(相手に)
                    if (seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                    }
                    //霊界からラバーズ視認
                    else if (seer.Data.IsDead && !seer.Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers))
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                    }
                    else if (target.Is(CustomRoles.Neptune) || seer.Is(CustomRoles.Neptune))
                    {
                        TargetMark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♡</color>");
                    }

                    //他人の役職とタスクは幽霊が他人の役職を見れるようになっていてかつ、seerが死んでいる場合のみ表示されます。それ以外の場合は空になります。
                    var targetRoleData = GetRoleNameAndProgressTextData(seer, target);
                    var TargetRoleText = targetRoleData.enabled ? $"<size={fontSize}>{targetRoleData.text}</size>\r\n" : "";

                    TargetSuffix.Clear();
                    //seerに関わらず発動するLowerText
                    TargetSuffix.Append(CustomRoleManager.GetLowerTextOthers(seer, target, isForMeeting: isForMeeting));

                    //seer役職が対象のSuffix
                    TargetSuffix.Append(seerRole?.GetSuffix(seer, target, isForMeeting: isForMeeting));
                    //seerに関わらず発動するSuffix
                    TargetSuffix.Append(CustomRoleManager.GetSuffixOthers(seer, target, isForMeeting: isForMeeting));
                    // 空でなければ先頭に改行を挿入
                    if (TargetSuffix.Length > 0)
                    {
                        TargetSuffix.Insert(0, "\r\n");
                    }

                    //RealNameを取得 なければ現在の名前をRealNamesに書き込む
                    string TargetPlayerName = target.GetRealName(isForMeeting);

                    //调用职业类通过 seer 重写 name
                    seer.GetRoleClass()?.OverrideNameAsSeer(target, ref TargetPlayerName, isForMeeting);
                    //调用职业类通过 seen 重写 name
                    target.GetRoleClass()?.OverrideNameAsSeen(seer, ref TargetPlayerName, isForMeeting);

                    //ターゲットのプレイヤー名の色を書き換えます。
                    TargetPlayerName = TargetPlayerName.ApplyNameColorData(seer, target, isForMeeting);

                    string TargetDeathReason = "";
                    if (seer.KnowDeathReason(target))
                        TargetDeathReason = $"({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})";

                    if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool()) || Concealer.IsHidding) && !isForMeeting)
                        TargetPlayerName = $"<size=0%>{TargetPlayerName}</size>";

                    //全てのテキストを合成します。
                    string TargetName = $"{TargetRoleText}{TargetPlayerName}{TargetDeathReason}{TargetMark}{TargetSuffix}";

                    //適用
                    target.RpcSetNamePrivate(TargetName, true, seer, force: NoCache);
                }

                logger.Info("NotifyRoles-Loop2-" + target.GetNameWithRole() + ":END");
            }
            logger.Info("NotifyRoles-Loop1-" + seer.GetNameWithRole() + ":END");
        }
    }
    public static void MarkEveryoneDirtySettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
    }
    public static void SyncAllSettings()
    {
        PlayerGameOptionsSender.SetDirtyToAll();
        GameOptionsSender.SendAllGameOptions();
    }
    public static void AfterMeetingTasks()
    {
        foreach (var roleClass in CustomRoleManager.AllActiveRoles.Values)
            roleClass.AfterMeetingTasks();
        if (Options.AirShipVariableElectrical.GetBool())
            AirShipElectricalDoors.Initialize();
        DoorsReset.ResetDoors();
        // 空デデンバグ対応 会議後にベントを空にする
        var ventilationSystem = ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out var systemType) ? systemType.TryCast<VentilationSystem>() : null;
        if (ventilationSystem != null)
        {
            ventilationSystem.PlayersInsideVents.Clear();
            ventilationSystem.IsDirty = true;
        }
    }
    public static void ChangeInt(ref int ChangeTo, int input, int max)
    {
        var tmp = ChangeTo * 10;
        tmp += input;
        ChangeTo = Math.Clamp(tmp, 0, max);
    }
    public static void CountAlivePlayers(bool sendLog = false)
    {
        int AliveImpostorCount = Main.AllAlivePlayerControls.Count(pc => pc.Is(CustomRoleTypes.Impostor));
        if (Main.AliveImpostorCount != AliveImpostorCount)
        {
            Logger.Info("存活内鬼人数:" + AliveImpostorCount + "人", "CountAliveImpostors");
            Main.AliveImpostorCount = AliveImpostorCount;
            LastImpostor.SetSubRole();
        }

        if (sendLog)
        {
            var sb = new StringBuilder(100);
            foreach (var countTypes in EnumHelper.GetAllValues<CountTypes>())
            {
                var playersCount = PlayersCount(countTypes);
                if (playersCount == 0) continue;
                sb.Append($"{countTypes}:{AlivePlayersCount(countTypes)}/{playersCount}, ");
            }
            sb.Append($"All:{AllAlivePlayersCount}/{AllPlayersCount}");
            Logger.Info(sb.ToString(), "CountAlivePlayers");
        }
    }
    public static void KickPlayer(int playerId, bool ban, string reason)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        OnPlayerLeftPatch.Add(playerId);
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKickReason, SendOption.Reliable, -1);
        writer.Write(GetString($"DCNotify.{reason}"));
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        _ = new LateTask(() =>
        {
            AmongUsClient.Instance.KickPlayer(playerId, ban);
        }, Math.Max(AmongUsClient.Instance.Ping / 500f, 1f), "Kick Player");
    }
    public static string PadRightV2(this object text, int num)
    {
        int bc = 0;
        var t = text.ToString();
        foreach (char c in t) bc += Encoding.GetEncoding("UTF-8").GetByteCount(c.ToString()) == 1 ? 1 : 2;
        return t?.PadRight(Mathf.Max(num - (bc - t.Length), 0));
    }
    public static DirectoryInfo GetLogFolder(bool auto = false)
    {
        var folder = Directory.CreateDirectory($"{Application.persistentDataPath}/TownOfHost/Logs");
        if (auto)
        {
            folder = Directory.CreateDirectory($"{folder.FullName}/AutoLogs");
        }
        return folder;
    }
    public static void DumpLog(bool popup = false)

    {
        string f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/TONX-logs/";
        string t = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
        string filename = $"{f}TONX-v{Main.PluginVersion}-{t}.log";
        if (!Directory.Exists(f)) Directory.CreateDirectory(f);
        FileInfo file = new(@$"{Environment.CurrentDirectory}/BepInEx/LogOutput.log");
        file.CopyTo(@filename);
        if (PlayerControl.LocalPlayer != null)
        {
            if (popup) PlayerControl.LocalPlayer.ShowPopUp(string.Format(GetString("Message.DumpfileSaved"), $"TONX - v{Main.PluginVersion}-{t}.log"));
            else AddChatMessage(string.Format(GetString("Message.DumpfileSaved"), $"TONX - v{Main.PluginVersion}-{t}.log"));
        }
        ProcessStartInfo psi = new ProcessStartInfo("Explorer.exe")
        { Arguments = "/e,/select," + @filename.Replace("/", "\\") };
        Process.Start(psi);
    }
    public static void OpenDirectory(string path)
    {
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
        };
        Process.Start(startInfo);
    }
    public static string ChatSummary;
    public static string SummaryTexts(byte id, bool isForChat)
    {
        var builder = new StringBuilder();
        // チャットならposタグを使わない(文字数削減)
        if (isForChat)
        {
            return ChatSummary ?? "";
        }
        else
        {
            builder.Append(Main.AllPlayerNames[id]);
            builder.Append(": ").Append(GetProgressText(id).RemoveColorTags());
            builder.Append(' ').Append(GetVitalText(id));
            builder.Append(' ').Append(GetTrueRoleName(id, false).RemoveColorTags());
            builder.Append(' ').Append(GetSubRolesText(id).RemoveColorTags());
            ChatSummary = builder.ToString();
            builder = new StringBuilder();
            // 全プレイヤー中最長の名前の長さからプレイヤー名の後の水平位置を計算する
            // 1em ≒ 半角2文字
            // 空白は0.5emとする
            // SJISではアルファベットは1バイト，日本語は基本的に2バイト
            var longestNameByteCount = Main.AllPlayerNames.Values.Select(name => name.GetByteCount()).OrderByDescending(byteCount => byteCount).FirstOrDefault();
            //最大11.5emとする(★+日本語10文字分+半角空白)
            var pos = Math.Min(((float)longestNameByteCount / 2) + 1.5f /* ★+末尾の半角空白 */ , 11.5f);
            builder.Append(ColorString(Main.PlayerColors[id], Main.AllPlayerNames[id]));
            builder.AppendFormat("<pos={0}em>", pos).Append(GetProgressText(id)).Append("</pos>");
            // "(00/00) " = 4em
            pos += 4f;
            builder.AppendFormat("<pos={0}em>", pos).Append(GetVitalText(id)).Append("</pos>");
            // "Lover's Suicide " = 8em
            // "回線切断 " = 4.5em
            pos += DestroyableSingleton<TranslationController>.Instance.currentLanguage.languageID == SupportedLangs.English ? 8f : 4.5f;
            builder.AppendFormat("<pos={0}em>", pos);
            builder.Append(GetTrueRoleName(id, false));
            builder.Append(GetSubRolesText(id));
            builder.Append("</pos>");
        }
        return builder.ToString();
    }
    public static string RemoveHtmlTags(this string str) => Regex.Replace(str, "<[^>]*?>", string.Empty);
    public static string RemoveHtmlTagsExcept(this string str, string exceptionLabel) => Regex.Replace(str, "<(?!/*" + exceptionLabel + ")[^>]*?>", string.Empty);
    public static string RemoveColorTags(this string str) => Regex.Replace(str, "</?color(=#[0-9a-fA-F]*)?>", "");
    public static void FlashColor(Color color, float duration = 1f)
    {
        var hud = DestroyableSingleton<HudManager>.Instance;
        if (hud.FullScreen == null) return;
        var obj = hud.transform.FindChild("FlashColor_FullScreen")?.gameObject;
        if (obj == null)
        {
            obj = UnityEngine.Object.Instantiate(hud.FullScreen.gameObject, hud.transform);
            obj.name = "FlashColor_FullScreen";
        }
        hud.StartCoroutine(Effects.Lerp(duration, new Action<float>((t) =>
        {
            obj.SetActive(t != 1f);
            obj.GetComponent<SpriteRenderer>().color = new(color.r, color.g, color.b, Mathf.Clamp01((-2f * Mathf.Abs(t - 0.5f) + 1) * color.a / 2)); //アルファ値を0→目標→0に変化させる
        })));
    }

    public static Dictionary<string, Sprite> CachedSprites = new();
    public static Sprite LoadSprite(string path, float pixelsPerUnit = 1f)
    {
        try
        {
            if (CachedSprites.TryGetValue(path + pixelsPerUnit, out var sprite)) return sprite;
            Texture2D texture = LoadTextureFromResources(path);
            sprite = Sprite.Create(texture, new(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
            return CachedSprites[path + pixelsPerUnit] = sprite;
        }
        catch
        {
            Logger.Error($"读入Texture失败：{path}", "LoadImage");
        }
        return null;
    }
    public static Texture2D LoadTextureFromResources(string path)
    {
        try
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            ImageConversion.LoadImage(texture, ms.ToArray(), false);
            return texture;
        }
        catch
        {
            Logger.Error($"读入Texture失败：{path}", "LoadImage");
        }
        return null;
    }
    public static string ColorString(Color32 color, string str) => $"<color=#{color.r:x2}{color.g:x2}{color.b:x2}{color.a:x2}>{str}</color>";
    /// <summary>
    /// Darkness:１の比率で黒色と元の色を混ぜる。マイナスだと白色と混ぜる。
    /// </summary>
    public static Color ShadeColor(this Color color, float Darkness = 0)
    {
        bool IsDarker = Darkness >= 0; //黒と混ぜる
        if (!IsDarker) Darkness = -Darkness;
        float Weight = IsDarker ? 0 : Darkness; //黒/白の比率
        float R = (color.r + Weight) / (Darkness + 1);
        float G = (color.g + Weight) / (Darkness + 1);
        float B = (color.b + Weight) / (Darkness + 1);
        return new Color(R, G, B, color.a);
    }

    /// <summary>
    /// 乱数の簡易的なヒストグラムを取得する関数
    /// <params name="nums">生成した乱数を格納したint配列</params>
    /// <params name="scale">ヒストグラムの倍率 大量の乱数を扱う場合、この値を下げることをお勧めします。</params>
    /// </summary>
    public static string WriteRandomHistgram(int[] nums, float scale = 1.0f)
    {
        int[] countData = new int[nums.Max() + 1];
        foreach (var num in nums)
        {
            if (0 <= num) countData[num]++;
        }
        StringBuilder sb = new();
        for (int i = 0; i < countData.Length; i++)
        {
            // 倍率適用
            countData[i] = (int)(countData[i] * scale);

            // 行タイトル
            sb.AppendFormat("{0:D2}", i).Append(" : ");

            // ヒストグラム部分
            for (int j = 0; j < countData[i]; j++)
                sb.Append('|');

            // 改行
            sb.Append('\n');
        }

        // その他の情報
        sb.Append("最大数 - 最小数: ").Append(countData.Max() - countData.Min());

        return sb.ToString();
    }

    public static bool TryCast<T>(this Il2CppObjectBase obj, out T casted)
    where T : Il2CppObjectBase
    {
        casted = obj.TryCast<T>();
        return casted != null;
    }
    public static int AllPlayersCount => PlayerState.AllPlayerStates.Values.Count(state => state.CountType != CountTypes.OutOfGame);
    public static int AllAlivePlayersCount => Main.AllAlivePlayerControls.Count(pc => !pc.Is(CountTypes.OutOfGame));
    public static bool IsAllAlive => PlayerState.AllPlayerStates.Values.All(state => state.CountType == CountTypes.OutOfGame || !state.IsDead);
    public static int PlayersCount(CountTypes countTypes) => PlayerState.AllPlayerStates.Values.Count(state => state.CountType == countTypes);
    public static int AlivePlayersCount(CountTypes countTypes) => Main.AllAlivePlayerControls.Count(pc => pc.Is(countTypes));

    private const string ActiveSettingsSize = "70%";
    private const string ActiveSettingsLineHeight = "55%";

    public static bool AmDev() => IsDev(EOSManager.Instance.FriendCode);
    public static bool IsDev(this PlayerControl pc) => IsDev(pc.FriendCode);
    public static bool IsDev(string friendCode) => friendCode
        is "actorour#0029" //咔哥
        or "pinklaze#1776" //NCM
        or "sofaagile#3120" //天寸
        or "aerobicgen#3487"; //鲨鲨

    public static Vector2 GetBlackRoomPS()
    {
        return Main.NormalOptions.MapId switch
        {
            0 => new(-27f, 3.3f), // The Skeld
            1 => new(-11.4f, 8.2f), // MIRA HQ
            2 => new(42.6f, -19.9f), // Polus
            4 => new(-16.8f, -6.2f), // Airship
            5 => new(9.4f, 17.9f), // The Fungle
            _ => throw new System.NotImplementedException(),
        };
    }
}