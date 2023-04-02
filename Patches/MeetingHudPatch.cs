using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
class CheckForEndVotingPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        var voteLog = Logger.Handler("Vote");
        try
        {
            List<MeetingHud.VoterState> statesList = new();
            MeetingHud.VoterState[] states;
            foreach (var pva in __instance.playerStates)
            {
                if (pva == null) continue;
                PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
                if (pc == null) continue;
                //死んでいないディクテーターが投票済み

                //主动叛变
                if (pva.DidVote && pc.PlayerId == pva.VotedFor && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    if (Options.MadmateSpawnMode.GetInt() == 2 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && Utils.CanBeMadmate(pc))
                    {
                        Main.MadmateNum++;
                        Main.PlayerStates[pc.PlayerId].SetSubRole(CustomRoles.Madmate);
                        Utils.NotifyRoles(true, pc, true);
                        Logger.Info("役職設定:" + pc?.Data?.PlayerName + " = " + pc.GetCustomRole().ToString() + " + " + CustomRoles.Madmate.ToString(), "Assign " + CustomRoles.Madmate.ToString());
                    }
                }

                if (pc.Is(CustomRoles.Dictator) && pva.DidVote && pc.PlayerId != pva.VotedFor && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    var voteTarget = Utils.GetPlayerById(pva.VotedFor);
                    TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, pc.PlayerId);
                    statesList.Add(new()
                    {
                        VoterId = pva.TargetPlayerId,
                        VotedForId = pva.VotedFor
                    });
                    states = statesList.ToArray();
                    if (AntiBlackout.OverrideExiledPlayer)
                    {
                        __instance.RpcVotingComplete(states, null, true);
                        ExileControllerWrapUpPatch.AntiBlackout_LastExiled = voteTarget.Data;
                    }
                    else __instance.RpcVotingComplete(states, voteTarget.Data, false); //通常処理

                    Logger.Info($"{voteTarget.GetNameWithRole()} 被独裁者驱逐", "Dictator");
                    CheckForDeathOnExile(PlayerState.DeathReason.Vote, pva.VotedFor);
                    Logger.Info("独裁投票，会议强制结束", "Special Phase");
                    voteTarget.SetRealKiller(pc);
                    Main.LastVotedPlayerInfo = voteTarget.Data;
                    if (Main.LastVotedPlayerInfo != null)
                        ConfirmEjections(Main.LastVotedPlayerInfo);
                    return true;
                }

                if (pva.DidVote && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    var voteTarget = Utils.GetPlayerById(pva.VotedFor);
                    if (voteTarget != null)
                    {
                        switch (pc.GetCustomRole())
                        {
                            case CustomRoles.Divinator:
                                Divinator.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Eraser:
                                Eraser.OnVote(pc, voteTarget);
                                break;
                        }
                    }
                }

            }
            foreach (var ps in __instance.playerStates)
            {
                //死んでいないプレイヤーが投票していない
                if (!(Main.PlayerStates[ps.TargetPlayerId].IsDead || ps.DidVote)) return false;
            }

            GameData.PlayerInfo exiledPlayer = PlayerControl.LocalPlayer.Data;
            bool tie = false;

            for (var i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea ps = __instance.playerStates[i];
                if (ps == null) continue;
                voteLog.Info(string.Format("{0,-2}{1}:{2,-3}{3}", ps.TargetPlayerId, Utils.PadRightV2($"({Utils.GetVoteName(ps.TargetPlayerId)})", 40), ps.VotedFor, $"({Utils.GetVoteName(ps.VotedFor)})"));
                var voter = Utils.GetPlayerById(ps.TargetPlayerId);
                if (voter == null || voter.Data == null || voter.Data.Disconnected) continue;
                if (Options.VoteMode.GetBool())
                {
                    if (ps.VotedFor == 253 && !voter.Data.IsDead && //スキップ
                        !(Options.WhenSkipVoteIgnoreFirstMeeting.GetBool() && MeetingStates.FirstMeeting) && //初手会議を除く
                        !(Options.WhenSkipVoteIgnoreNoDeadBody.GetBool() && !MeetingStates.IsExistDeadBody) && //死体がない時を除く
                        !(Options.WhenSkipVoteIgnoreEmergency.GetBool() && MeetingStates.IsEmergencyMeeting) //緊急ボタンを除く
                        )
                    {
                        switch (Options.GetWhenSkipVote())
                        {
                            case VoteMode.Suicide:
                                TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, ps.TargetPlayerId);
                                voteLog.Info($"スキップしたため{voter.GetNameWithRole()}を自殺させました");
                                break;
                            case VoteMode.SelfVote:
                                ps.VotedFor = ps.TargetPlayerId;
                                voteLog.Info($"スキップしたため{voter.GetNameWithRole()}に自投票させました");
                                break;
                            default:
                                break;
                        }
                    }
                    if (ps.VotedFor == 254 && !voter.Data.IsDead)//無投票
                    {
                        switch (Options.GetWhenNonVote())
                        {
                            case VoteMode.Suicide:
                                TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, ps.TargetPlayerId);
                                voteLog.Info($"無投票のため{voter.GetNameWithRole()}を自殺させました");
                                break;
                            case VoteMode.SelfVote:
                                ps.VotedFor = ps.TargetPlayerId;
                                voteLog.Info($"無投票のため{voter.GetNameWithRole()}に自投票させました");
                                break;
                            case VoteMode.Skip:
                                ps.VotedFor = 253;
                                voteLog.Info($"無投票のため{voter.GetNameWithRole()}にスキップさせました");
                                break;
                            default:
                                break;
                        }
                    }
                }
                if (ps.TargetPlayerId != ps.VotedFor || Options.MadmateSpawnMode.GetInt() != 2) //主动叛变模式下自票无效
                {
                    statesList.Add(new MeetingHud.VoterState()
                    {
                        VoterId = ps.TargetPlayerId,
                        VotedForId = ps.VotedFor
                    });
                }
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Mayor) && !Options.MayorHideVote.GetBool()) //Mayorの投票数
                {
                    for (var i2 = 0; i2 < Options.MayorAdditionalVote.GetFloat(); i2++)
                    {
                        statesList.Add(new MeetingHud.VoterState()
                        {
                            VoterId = ps.TargetPlayerId,
                            VotedForId = ps.VotedFor
                        });
                    }
                }
            }
            states = statesList.ToArray();

            var VotingData = __instance.CustomCalculateVotes();
            byte exileId = byte.MaxValue;
            int max = 0;
            voteLog.Info("===追放者確認処理開始===");
            foreach (var data in VotingData)
            {
                voteLog.Info($"{data.Key}({Utils.GetVoteName(data.Key)}):{data.Value}票");
                if (data.Value > max)
                {
                    voteLog.Info(data.Key + "番が最高値を更新(" + data.Value + ")");
                    exileId = data.Key;
                    max = data.Value;
                    tie = false;
                }
                else if (data.Value == max)
                {
                    voteLog.Info(data.Key + "番が" + exileId + "番と同数(" + data.Value + ")");
                    exileId = byte.MaxValue;
                    tie = true;
                }
                voteLog.Info($"exileId: {exileId}, max: {max}票");
            }

            voteLog.Info($"追放者決定: {exileId}({Utils.GetVoteName(exileId)})");

            bool braked = false;
            if (tie) //破平者判断
            {
                byte target = byte.MaxValue;
                foreach (var data in VotingData)
                {
                    if (Main.BrakarVoteFor.Contains(data.Key))
                    {
                        if (target != byte.MaxValue)
                        {
                            target = byte.MaxValue;
                            break;
                        }
                        target = data.Key;
                    }
                }
                if (target != byte.MaxValue)
                {
                    Logger.Info("破平者覆盖驱逐玩家", "Brakar Vote");
                    exiledPlayer = Utils.GetPlayerInfoById(target);
                    tie = false;
                    braked = true;
                }
            }

            Collector.CollectAmount(VotingData, __instance);

            if (Options.VoteMode.GetBool() && Options.WhenTie.GetBool() && tie)
            {
                switch ((TieMode)Options.WhenTie.GetValue())
                {
                    case TieMode.Default:
                        exiledPlayer = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == exileId);
                        break;
                    case TieMode.All:
                        var exileIds = VotingData.Where(x => x.Key < 15 && x.Value == max).Select(kvp => kvp.Key).ToArray();
                        foreach (var playerId in exileIds)
                            Utils.GetPlayerById(playerId).SetRealKiller(null);
                        TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Vote, exileIds);
                        exiledPlayer = null;
                        break;
                    case TieMode.Random:
                        exiledPlayer = GameData.Instance.AllPlayers.ToArray().OrderBy(_ => Guid.NewGuid()).FirstOrDefault(x => VotingData.TryGetValue(x.PlayerId, out int vote) && vote == max);
                        tie = false;
                        break;
                }
            }
            else if (!braked)
                exiledPlayer = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => !tie && info.PlayerId == exileId);
            exiledPlayer?.Object.SetRealKiller(null);

            //RPC
            if (AntiBlackout.OverrideExiledPlayer)
            {
                __instance.RpcVotingComplete(states, null, true);
                ExileControllerWrapUpPatch.AntiBlackout_LastExiled = exiledPlayer;
            }
            else __instance.RpcVotingComplete(states, exiledPlayer, tie); //通常処理

            CheckForDeathOnExile(PlayerState.DeathReason.Vote, exileId);

            Main.LastVotedPlayerInfo = exiledPlayer;
            if (Main.LastVotedPlayerInfo != null)
                ConfirmEjections(Main.LastVotedPlayerInfo);

            return false;
        }
        catch (Exception ex)
        {
            Logger.SendInGame(string.Format(GetString("Error.MeetingException"), ex.Message), true);
            throw;
        }
    }

    // 参考：https://github.com/music-discussion/TownOfHost-TheOtherRoles
    private static void ConfirmEjections(GameData.PlayerInfo exiledPlayer)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (exiledPlayer == null) return;
        var exileId = exiledPlayer.PlayerId;
        if (exileId is < 0 or > 254) return;
        var realName = exiledPlayer.Object.GetRealName(isMeeting: true);
        Main.LastVotedPlayer = realName;

        var player = Utils.GetPlayerById(exiledPlayer.PlayerId);
        var role = GetString(exiledPlayer.GetCustomRole().ToString());
        var crole = exiledPlayer.GetCustomRole();
        var coloredRole = Utils.ColorString(Utils.GetRoleColor(exiledPlayer.GetCustomRole()), $"{role}");
        var name = "";
        int impnum = 0;
        int neutralnum = 0;
        foreach (var pc in Main.AllAlivePlayerControls)
        {
            var pc_role = pc.GetCustomRole();
            if (pc_role.IsImpostor() && pc != exiledPlayer.Object)
                impnum++;
            else if (pc_role.IsNeutralKilling() && pc != exiledPlayer.Object)
                neutralnum++;
        }
        switch (Options.CEMode.GetInt())
        {
            case 0:
                name = string.Format(GetString("PlayerExiled"), realName);
                break;
            case 1:
                if (player.GetCustomRole().IsImpostor())
                    name = string.Format(GetString("BelongTo"), realName, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("TeamImpostor")));
                else if (player.GetCustomRole().IsCrewmate())
                    name = string.Format(GetString("IsGood"), realName);
                else if (player.GetCustomRole().IsNeutral())
                    name = string.Format(GetString("BelongTo"), realName, Utils.ColorString(new Color32(255, 171, 27, byte.MaxValue), GetString("TeamNeutral")));
                break;
            case 2:
                name = string.Format(GetString("PlayerIsRole"), realName, coloredRole);
                break;
        }
        var DecidedWinner = false;
        //小丑胜利
        if (crole == CustomRoles.Jester)
        {
            //EAC封禁名单玩家成为小丑达成胜利条件后，先嘲笑一波
            if (exiledPlayer.PlayerId == PlayerControl.LocalPlayer.PlayerId && BanManager.CheckEACList(exiledPlayer.FriendCode))
                name = string.Format("哈哈哈哈哈哈哈哈哈哈哈哈哈哈\n{0} 居然是 {1} 啊哈哈哈哈笑死我了\n没想到吧，哈哈哈这不是小丑吗哈哈哈哈哈", realName, coloredRole);
            else
                name = string.Format(GetString("ExiledJester"), realName, coloredRole);
            DecidedWinner = true;
        }
        //处刑人胜利
        if (Executioner.Target.ContainsValue(exileId))
        {
            name = string.Format(GetString("ExiledExeTarget"), realName, coloredRole);
            DecidedWinner = true;
        }
        //冤罪师胜利
        var playerList = Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller().PlayerId == exileId);
        if (playerList.Count() > 0)
        {
            if (!(!Options.InnocentCanWinByImp.GetBool() && crole.IsImpostor()))
            {
                if (DecidedWinner) name += string.Format(GetString("ExiledInnocentTargetAddBelow"));
                else name = string.Format(GetString("ExiledInnocentTargetInOneLine"), realName, coloredRole);
                DecidedWinner = true;
            }
        }

        if (DecidedWinner) name += "<size=0>";
        if (Options.ShowImpRemainOnEject.GetBool() && !DecidedWinner)
        {
            name += "\n";
            string comma = neutralnum > 0 ? "，" : "";
            if (impnum == 0) name += GetString("NoImpRemain") + comma;
            else name += string.Format(GetString("ImpRemain"), impnum) + comma;
            if (Options.ShowNKRemainOnEject.GetBool() && neutralnum > 0)
                name += string.Format(GetString("NeutralRemain"), neutralnum);
        }
        name += "<size=0>";
        new LateTask(() =>
        {
            Main.DoBlockNameChange = true;
            if (GameStates.IsInGame) player.RpcSetName(name);
        }, 3.0f, "Change Exiled Player Name");
        new LateTask(() =>
        {
            if (GameStates.IsInGame)
            {
                player.RpcSetName(realName);
                Main.DoBlockNameChange = false;
            }
        }, 11.5f, "Change Exiled Player Name Back");
    }
    public static bool CheckRole(byte id, CustomRoles role)
    {
        var player = Main.AllPlayerControls.Where(pc => pc.PlayerId == id).FirstOrDefault();
        return player != null && player.Is(role);
    }
    public static void TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason deathReason, params byte[] playerIds)
    {
        var AddedIdList = new List<byte>();
        foreach (var playerId in playerIds)
            if (Main.AfterMeetingDeathPlayers.TryAdd(playerId, deathReason))
                AddedIdList.Add(playerId);
        CheckForDeathOnExile(deathReason, AddedIdList.ToArray());
    }
    public static void CheckForDeathOnExile(PlayerState.DeathReason deathReason, params byte[] playerIds)
    {
        Witch.OnCheckForEndVoting(deathReason, playerIds);
        foreach (var playerId in playerIds)
        {
            //Loversの後追い
            if (CustomRoles.Lovers.IsEnable() && !Main.isLoversDead && Main.LoversPlayers.Find(lp => lp.PlayerId == playerId) != null)
                FixedUpdatePatch.LoversSuicide(playerId, true);
            //道連れチェック
            RevengeOnExile(playerId, deathReason);
        }
    }
    private static void RevengeOnExile(byte playerId, PlayerState.DeathReason deathReason)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return;
        var target = PickRevengeTarget(player, deathReason);
        if (target == null) return;
        TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Revenge, target.PlayerId);
        target.SetRealKiller(player);
        Logger.Info($"{player.GetNameWithRole()}の道連れ先:{target.GetNameWithRole()}", "RevengeOnExile");
    }
    private static PlayerControl PickRevengeTarget(PlayerControl exiledplayer, PlayerState.DeathReason deathReason)//道連れ先選定
    {
        List<PlayerControl> TargetList = new();
        foreach (var candidate in Main.AllAlivePlayerControls)
        {
            if (candidate == exiledplayer || Main.AfterMeetingDeathPlayers.ContainsKey(candidate.PlayerId)) continue;
        }
        if (TargetList == null || TargetList.Count == 0) return null;
        var rand = IRandom.Instance;
        var target = TargetList[rand.Next(TargetList.Count)];
        return target;
    }
}

static class ExtendedMeetingHud
{
    public static Dictionary<byte, int> CustomCalculateVotes(this MeetingHud __instance)
    {
        Logger.Info("===计算票数处理开始===", "Vote");
        Dictionary<byte, int> dic = new();
        Main.BrakarVoteFor = new();
        Collector.CollectorVoteFor = new();
        //| 投票された人 | 投票された回数 |
        for (int i = 0; i < __instance.playerStates.Length; i++)
        {
            PlayerVoteArea ps = __instance.playerStates[i];//该玩家面板里的所有会议中的玩家
            if (ps == null) continue;
            if (ps.VotedFor is not 252 and not byte.MaxValue and not 254)//该玩家面板里是否投了该玩家
            {
                // 默认票数1票
                int VoteNum = 1;

                // 投票给有效玩家时才进行的判断
                var target = Utils.GetPlayerById(ps.VotedFor);
                if (target != null)
                {
                    // 僵尸、活死人无法被票
                    if (target.Is(CustomRoles.Zombie) || target.Is(CustomRoles.Glitch)) VoteNum = 0;
                    // 投给贱人的票变为999
                    if (target.Is(CustomRoles.Bitch)) VoteNum = 999;
                    // 记录破平者投票
                    if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Brakar))
                        if (!Main.BrakarVoteFor.Contains(target.PlayerId))
                            Main.BrakarVoteFor.Add(target.PlayerId);
                    // 集票者记录数据
                    Collector.CollectorVotes(target, ps);
                }

                //市长附加票数
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Mayor)
                    && ps.TargetPlayerId != ps.VotedFor
                    ) VoteNum += Options.MayorAdditionalVote.GetInt();
                //窃票者附加票数
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.TicketsStealer))
                    VoteNum += (int)(Main.AllPlayerControls.Where(x => (x.GetRealKiller() == null ? -1 : x.GetRealKiller().PlayerId) == ps.TargetPlayerId).Count() * Options.TicketsPerKill.GetFloat());

                // 主动叛变模式下自票无效
                if (ps.TargetPlayerId == ps.VotedFor && Options.MadmateSpawnMode.GetInt() == 2) VoteNum = 0;

                //投票を1追加 キーが定義されていない場合は1で上書きして定義
                dic[ps.VotedFor] = !dic.TryGetValue(ps.VotedFor, out int num) ? VoteNum : num + VoteNum;//统计该玩家被投的数量
            }
        }
        return dic;
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
class MeetingHudStartPatch
{
    public static void NotifyRoleSkillOnMeetingStart()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        string MimicMsg = "";
        foreach (var pc in Main.AllPlayerControls)
        {
            //主动叛变模式提示
            if (Options.MadmateSpawnMode.GetInt() == 2 && CustomRoles.Madmate.GetCount() > 0 && pc.IsAlive())
            {
                new LateTask(() =>
                {
                    Utils.SendMessage(string.Format(GetString("Message.MadmateSelfVoteModeNotify"), GetString("MadmateSpawnMode.SelfVote")), pc.PlayerId);
                }, 5.0f, "Notice MadmateVoteself Mode");
            }
            //黑手党死后技能提示
            if (pc.Is(CustomRoles.Mafia) && !pc.IsAlive())
            {
                new LateTask(() =>
                {
                    Utils.SendMessage(GetString("MafiaDeadMsg"), pc.PlayerId);
                }, 5.0f, "Notice Mafia Skill");
            }
            //网红死亡消息提示
            foreach (var csId in Main.CyberStarDead)
            {
                if (!Options.ImpKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsImpostor()) continue;
                if (!Options.NeutralKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsNeutral()) continue;

                var cs = Utils.GetPlayerById(csId);
                if (cs == null) continue;
                new LateTask(() =>
                {
                    Utils.SendMessage(string.Format(GetString("CyberStarDead"), cs.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.CyberStar), GetString("CyberStarNewsTitle")));
                }, 5.0f, "Notice CyberStar Skill");
            }
            //侦探报告线索
            if (Main.DetectiveNotify.ContainsKey(pc.PlayerId))
            {
                new LateTask(() =>
                {
                    Utils.SendMessage(Main.DetectiveNotify[pc.PlayerId], pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Detective), GetString("DetectiveNoticeTitle")));
                }, 5.0f, "Notice Detective Skill");
            }
            //提示神存活
            if (pc.Is(CustomRoles.God) && pc.IsAlive())
            {
                new LateTask(() =>
                {
                    Utils.SendMessage(GetString("GodNoticeAlive"), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.God), GetString("GodAliveTitle")));
                }, 5.0f, "Notice God Alive");
            }
            //宝箱怪的消息
            if (pc.Is(CustomRoles.Mimic) && !pc.IsAlive())
            {
                foreach (var dpc in Main.AllPlayerControls.Where(x => (x.GetRealKiller() == null ? -1 : x.GetRealKiller().PlayerId) == pc.PlayerId))
                    MimicMsg += $"\n{dpc.GetNameWithRole()}";
            }
        }
        if (MimicMsg != "")
        {
            MimicMsg = GetString("MimicDeadMsg") + "\n" + MimicMsg;
            new LateTask(() =>
            {
                foreach (var ipc in Main.AllPlayerControls.Where(x => x.GetCustomRole().IsImpostorTeam()))
                    Utils.SendMessage(MimicMsg, ipc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mimic), GetString("MimicMsgTitle")));
            }, 5.0f, "Notice Mimic Dead Msg");
        }
    }
    public static void Prefix(MeetingHud __instance)
    {
        Logger.Info("------------会议开始------------", "Phase");
        ChatUpdatePatch.DoBlockChat = true;
        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        Main.AllPlayerControls.Do(x => ReportDeadBodyPatch.WaitReport[x.PlayerId].Clear());
        MeetingStates.MeetingCalled = true;

        if (!AmongUsClient.Instance.AmHost) return;

        Main.LastVotedPlayerInfo = null;
        Main.GuesserGuessed.Clear();
        Main.VeteranInProtect.Clear();
        Main.GrenadierBlinding.Clear();
        Main.MadGrenadierBlinding.Clear();
        Divinator.didVote.Clear();

        Counterfeiter.OnMeetingStart();
        BallLightning.OnMeetingStart();
        QuickShooter.OnMeetingStart();
        Eraser.OnMeetingStart();
        Hacker.OnMeetingStart();
        Psychic.OnMeetingStart();

        NotifyRoleSkillOnMeetingStart();
    }
    public static void Postfix(MeetingHud __instance)
    {
        SoundManager.Instance.ChangeAmbienceVolume(0f);
        if (!GameStates.IsModHost) return;
        foreach (var pva in __instance.playerStates)
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null) continue;
            var RoleTextData = Utils.GetRoleText(pc.PlayerId, !pc.AmOwner && !PlayerControl.LocalPlayer.Data.IsDead);
            if (pc.GetCustomRole().IsImpostor() && PlayerControl.LocalPlayer.GetCustomRole().IsImpostor() && !PlayerControl.LocalPlayer.Data.IsDead) RoleTextData = Utils.GetRoleText(pc.PlayerId, true);
            var roleTextMeeting = UnityEngine.Object.Instantiate(pva.NameText);
            roleTextMeeting.transform.SetParent(pva.NameText.transform);
            roleTextMeeting.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            roleTextMeeting.fontSize = 1.5f;
            roleTextMeeting.text = RoleTextData.Item1;
            if (Main.VisibleTasksCount) roleTextMeeting.text += Utils.GetProgressText(pc);
            roleTextMeeting.color = RoleTextData.Item2;
            roleTextMeeting.gameObject.name = "RoleTextMeeting";
            roleTextMeeting.enableWordWrapping = false;
            roleTextMeeting.enabled =
                pc.AmOwner || //対象がLocalPlayer
                (Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) || //LocalPlayerが死亡していて幽霊が他人の役職を見れるとき
                (AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer.Is(CustomRoles.GM)) ||
                (pc.GetCustomRole().IsImpostor() && PlayerControl.LocalPlayer.GetCustomRole().IsImpostor() && !PlayerControl.LocalPlayer.Data.IsDead && Options.ImpKnowAlliesRole.GetBool()) ||
                (pc.GetCustomRole().IsImpostor() && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && !PlayerControl.LocalPlayer.Data.IsDead) ||
                (pc.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool()) ||
                PlayerControl.LocalPlayer.Is(CustomRoles.God);
            if (EvilTracker.IsTrackTarget(PlayerControl.LocalPlayer, pc) && EvilTracker.CanSeeLastRoomInMeeting)
            {
                roleTextMeeting.text = EvilTracker.GetArrowAndLastRoom(PlayerControl.LocalPlayer, pc);
                roleTextMeeting.enabled = true;
            }
        }

        if (Options.SyncButtonMode.GetBool())
        {
            Utils.SendMessage(string.Format(GetString("Message.SyncButtonLeft"), Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount));
            Logger.Info("緊急会議ボタンはあと" + (Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount) + "回使用可能です。", "SyncButtonMode");
        }
        if (AntiBlackout.OverrideExiledPlayer)
        {
            Utils.SendMessage(Translator.GetString("Warning.OverrideExiledPlayer"));
        }
        if (MeetingStates.FirstMeeting) TemplateManager.SendTemplate("OnFirstMeeting", noErr: true);
        TemplateManager.SendTemplate("OnMeeting", noErr: true);

        if (AmongUsClient.Instance.AmHost)
        {
            _ = new LateTask(() =>
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    pc.RpcSetNameEx(pc.GetRealName(isMeeting: true));
                }
                ChatUpdatePatch.DoBlockChat = false;
            }, 3f, "SetName To Chat");
        }

        foreach (var pva in __instance.playerStates)
        {
            if (pva == null) continue;
            PlayerControl seer = PlayerControl.LocalPlayer;
            PlayerControl target = Utils.GetPlayerById(pva.TargetPlayerId);
            if (target == null) continue;

            var sb = new StringBuilder();

            //会議画面での名前変更
            //自分自身の名前の色を変更
            //NameColorManager準拠の処理
            pva.NameText.text = pva.NameText.text.ApplyNameColorData(seer, target, true);

            //とりあえずSnitchは会議中にもインポスターを確認することができる仕様にしていますが、変更する可能性があります。

            if (seer.KnowDeathReason(target))
                sb.Append($"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doctor), Utils.GetVitalText(target.PlayerId))})");

            //インポスター表示
            switch (seer.GetCustomRole().GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    if (target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetPlayerTaskState().IsTaskFinished)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), "★")); //変更対象にSnitchマークをつける
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
            }
            switch (seer.GetCustomRole())
            {
                case CustomRoles.Arsonist:
                    if (seer.IsDousedPlayer(target)) //seerがtargetに既にオイルを塗っている(完了)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist), "▲"));
                    break;
                case CustomRoles.Executioner:
                    sb.Append(Executioner.TargetMark(seer, target));
                    break;
                case CustomRoles.Jackal:
                case CustomRoles.Pelican:
                case CustomRoles.DarkHide:
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
                case CustomRoles.EvilTracker:
                    sb.Append(EvilTracker.GetTargetMark(seer, target));
                    break;
                case CustomRoles.Revolutionist:
                    if (seer.IsDrawPlayer(target)) //seerがtargetに既にオイルを塗っている(完了)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Revolutionist), "●"));
                    break;
                case CustomRoles.Psychic:
                    if (target.IsRedForPsy())
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), pva.NameText.text);
                    break;
                case CustomRoles.Mafia:
                    if (seer.Data.IsDead && !target.Data.IsDead)
                    {
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mafia), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    }
                    break;
                case CustomRoles.NiceGuesser:
                case CustomRoles.EvilGuesser:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                    {
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(seer.Is(CustomRoles.NiceGuesser) ? CustomRoles.NiceGuesser : CustomRoles.EvilGuesser), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    }
                    break;
                case CustomRoles.Medicaler:
                    sb.Append(Medicaler.TargetMark(seer, target));
                    break;
                case CustomRoles.Gamer:
                    sb.Append(Gamer.TargetMark(seer, target));
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
            }

            bool isLover = false;
            foreach (var subRole in target.GetCustomSubRoles())
            {
                switch (subRole)
                {
                    case CustomRoles.Lovers:
                        if (seer.Is(CustomRoles.Lovers) || seer.Data.IsDead)
                        {
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), "♡"));
                            isLover = true;
                        }
                        break;
                }
            }

            //海王相关显示
            if ((seer.Is(CustomRoles.Ntr) || target.Is(CustomRoles.Ntr)) && !seer.Data.IsDead && !isLover)
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), "♡"));
            else if (seer == target && CustomRolesHelper.RoleExist(CustomRoles.Ntr) && !isLover)
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), "♡"));

            //呪われている場合
            sb.Append(Witch.GetSpelledMark(target.PlayerId, true));

            //如果是大明星
            if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SuperStar), "★"));

            //球状闪电提示
            if (BallLightning.IsGhost(target))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.BallLightning), "■"));

            //医生护盾提示
            if (seer.PlayerId == target.PlayerId)
                sb.Append(Medicaler.GetSheildMark(seer));

            //会議画面ではインポスター自身の名前にSnitchマークはつけません。

            pva.NameText.text += sb.ToString();
        }
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
class MeetingHudUpdatePatch
{
    public static void Postfix(MeetingHud __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Input.GetMouseButtonUp(1) && Input.GetKey(KeyCode.LeftControl))
        {
            __instance.playerStates.DoIf(x => x.HighlightedFX.enabled, x =>
            {
                var player = Utils.GetPlayerById(x.TargetPlayerId);
                if (player != null && !player.Data.IsDead)
                {
                    player.RpcExileV2();
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Execution;
                    Main.PlayerStates[player.PlayerId].SetDead();
                    Utils.SendMessage(string.Format(GetString("Message.Executed"), player.Data.PlayerName));
                    Logger.Info($"{player.GetNameWithRole()}を処刑しました", "Execution");
                    __instance.CheckForEndVoting();
                }
            });
        }
    }
}
[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetHighlighted))]
class SetHighlightedPatch
{
    public static bool Prefix(PlayerVoteArea __instance, bool value)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!__instance.HighlightedFX) return false;
        __instance.HighlightedFX.enabled = value;
        return false;
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
class MeetingHudOnDestroyPatch
{
    public static void Postfix()
    {
        MeetingStates.FirstMeeting = false;
        Logger.Info("------------会议结束------------", "Phase");
        if (AmongUsClient.Instance.AmHost)
        {
            AntiBlackout.SetIsDead();
            Main.AllPlayerControls.Do(pc => RandomSpawn.CustomNetworkTransformPatch.NumOfTP[pc.PlayerId] = 0);

            Main.CyberStarDead.Clear();
            Main.DetectiveNotify.Clear();
            Main.LastVotedPlayerInfo = null;
            EAC.MeetingTimes = 0;

            Eraser.OnMeetingDestroy();
        }
    }
}