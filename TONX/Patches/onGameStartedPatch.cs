using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using TONX.Attributes;
using TONX.Modules;
using TONX.Roles.AddOns;
using TONX.Roles.Core;
using static TONX.Modules.CustomRoleSelector;
using static TONX.Translator;

namespace TONX;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal class ChangeRoleSettings
{
    public static void Postfix(AmongUsClient __instance)
    {
        try
        {
            //注:この時点では役職は設定されていません。
            Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            if (Options.DisableVanillaRoles.GetBool())
            {
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Tracker, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Noisemaker, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Phantom, 0, 0);
            }

            Main.OverrideWelcomeMsg = "";
            Main.AllPlayerKillCooldown = new();
            Main.AllPlayerSpeed = new();

            Main.LastEnteredVent = new();
            Main.LastEnteredVentLocation = new();

            Main.AfterMeetingDeathPlayers = new();
            Main.clientIdList = new();

            Main.CheckShapeshift = new();
            Main.ShapeshiftTarget = new();

            Main.ShieldPlayer = Options.ShieldPersonDiedFirst.GetBool() ? Main.FirstDied : byte.MaxValue;
            Main.FirstDied = byte.MaxValue;

            ReportDeadBodyPatch.CanReport = new();

            Options.UsedButtonCount = 0;

            Main.RealOptionsData = new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);
            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;

            Main.introDestroyed = false;

            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = new();

            Main.PlayerColors = new();
            //名前の記録
            RPC.SyncAllPlayerNames();

            //var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId);
            //if (invalidColor.Any())
            //{
            //    var msg = Translator.GetString("Error.InvalidColor");
            //    Logger.SendInGame(msg);
            //    msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.name}({p.Data.DefaultOutfit.ColorId})"));
            //    Utils.SendMessage(msg);
            //    Logger.Error(msg, "CoStartGame");
            //}

            GameModuleInitializerAttribute.InitializeAll();

            foreach (var target in Main.AllPlayerControls)
            {
                foreach (var seer in Main.AllPlayerControls)
                {
                    var pair = (target.PlayerId, seer.PlayerId);
                    Main.LastNotifyNames[pair] = target.name;
                }
            }
            foreach (var pc in Main.AllPlayerControls)
            {
                var colorId = pc.Data.DefaultOutfit.ColorId;
                if (AmongUsClient.Instance.AmHost && Options.FormatNameMode.GetInt() == 1) pc.RpcSetName(Palette.GetColorName(colorId));
                PlayerState.Create(pc.PlayerId);
                //Main.AllPlayerNames[pc.PlayerId] = pc?.Data?.PlayerName;
                Main.PlayerColors[pc.PlayerId] = Palette.PlayerColors[colorId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod); //移動速度をデフォルトの移動速度に変更
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                ReportDeadBodyPatch.WaitReport[pc.PlayerId] = new();
                pc.cosmetics.nameText.text = pc.name;

                var outfit = pc.Data.DefaultOutfit;
                Camouflage.PlayerSkins[pc.PlayerId] = new NetworkedPlayerInfo.PlayerOutfit().Set(outfit.PlayerName, outfit.ColorId, outfit.HatId, outfit.SkinId, outfit.VisorId, outfit.PetId);
                Main.clientIdList.Add(pc.GetClientId());
            }
            Main.VisibleTasksCount = true;
            if (__instance.AmHost)
            {
                RPC.SyncCustomSettingsRPC();
            }

            IRandom.SetInstanceById(Options.RoleAssigningAlgorithm.GetValue());

            MeetingStates.MeetingCalled = false;
            MeetingStates.FirstMeeting = true;
            GameStates.AlreadyDied = false;
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Change Role Setting Postfix");
            Logger.Fatal(ex.ToString(), "Change Role Setting Postfix");
        }
    }
}
[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
internal class SelectRolesPatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            //CustomRpcSenderとRpcSetRoleReplacerの初期化
            Dictionary<byte, CustomRpcSender> senders = new();
            foreach (var pc in Main.AllPlayerControls)
            {
                senders[pc.PlayerId] = new CustomRpcSender($"{pc.name}'s SetRole Sender", SendOption.Reliable, false)
                        .StartMessage(pc.GetClientId());
            }
            RpcSetRoleReplacer.StartReplace(senders);

            if (Options.EnableGM.GetBool())
            {
                PlayerControl.LocalPlayer.RpcSetCustomRole(CustomRoles.GM);
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Crewmate);
                PlayerControl.LocalPlayer.Data.IsDead = true;
                PlayerState.AllPlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            }

            SelectCustomRoles();
            SelectAddonRoles();
            CalculateVanillaRoleCount();

            //指定原版特殊职业数量
            RoleTypes[] RoleTypesList = [RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.Noisemaker, RoleTypes.Tracker, RoleTypes.Shapeshifter, RoleTypes.Phantom]; foreach (var roleTypes in RoleTypesList)
            {
                var roleOpt = Main.NormalOptions.roleOptions;
                int numRoleTypes = GetRoleTypesCount(roleTypes);
                roleOpt.SetRoleRate(roleTypes, numRoleTypes, numRoleTypes > 0 ? 100 : 0);
            }


            // 注册反职业
            foreach (var kv in RoleResult.Where(x => x.Value.GetRoleInfo().IsDesyncImpostor))
                AssignDesyncRole(kv.Value, kv.Key, senders, BaseRole: kv.Value.GetRoleInfo().BaseRoleType.Invoke());

            foreach (var cp in RoleResult.Where(x => x.Value == CustomRoles.CrewPostor))
                AssignDesyncRole(cp.Value, cp.Key, senders, BaseRole: RoleTypes.Crewmate, hostBaseRole: RoleTypes.Impostor);

        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Prefix");
            ex.Message.Split(@"\r\n").Do(line => Logger.Fatal(line, "Select Role Prefix"));
        }
        //以下、バニラ側の役職割り当てが入る
    }

    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            List<(PlayerControl, RoleTypes)> newList = new();
            foreach (var sd in RpcSetRoleReplacer.StoragedData)
            {
                var kp = RoleResult.Where(x => x.Key.PlayerId == sd.Item1.PlayerId).FirstOrDefault();
                if (kp.Value.GetRoleInfo().IsDesyncImpostor || kp.Value == CustomRoles.CrewPostor)
                {
                    Logger.Warn($"反向原版职业 => {sd.Item1.GetRealName()}: {sd.Item2}", "Override Role Select");
                    continue;
                }
                newList.Add((sd.Item1, kp.Value.GetRoleTypes()));
                if (sd.Item2 == kp.Value.GetRoleTypes())
                    Logger.Warn($"注册原版职业 => {sd.Item1.GetRealName()}: {sd.Item2}", "Override Role Select");
                else
                    Logger.Warn($"覆盖原版职业 => {sd.Item1.GetRealName()}: {sd.Item2} => {kp.Value.GetRoleTypes()}", "Override Role Select");
            }
            if (Options.EnableGM.GetBool()) newList.Add((PlayerControl.LocalPlayer, RoleTypes.Crewmate));
            RpcSetRoleReplacer.StoragedData = newList;

            RpcSetRoleReplacer.Release(); //保存していたSetRoleRpcを一気に書く
            RpcSetRoleReplacer.senders.Do(kvp => kvp.Value.SendMessage());

            // 不要なオブジェクトの削除
            RpcSetRoleReplacer.senders = null;
            RpcSetRoleReplacer.DesyncImpostorList = null;
            RpcSetRoleReplacer.StoragedData = null;

            //Utils.ApplySuffix();

            var rd = IRandom.Instance;

            foreach (var pc in Main.AllAlivePlayerControls)
            {
                pc.Data.IsDead = false; //プレイヤーの死を解除する
                var state = PlayerState.GetByPlayerId(pc.PlayerId);
                if (state.MainRole != CustomRoles.NotAssigned) continue; //既にカスタム役職が割り当てられていればスキップ
                var role = pc.Data.Role.Role.GetCustomRoleTypes();
                if (role == CustomRoles.NotAssigned)
                    Logger.SendInGame(string.Format(GetString("Error.InvalidRoleAssignment"), pc?.Data?.PlayerName));
                state.SetMainRole(role);
            }

            foreach (var (player, role) in RoleResult.Where(kvp => !(kvp.Value.GetRoleInfo()?.IsDesyncImpostor ?? false)))
            {
                SetColorPatch.IsAntiGlitchDisabled = true;

                PlayerState.GetByPlayerId(player.PlayerId).SetMainRole(role);
                Logger.Info($"注册模组职业：{player?.Data?.PlayerName} => {role}", "AssignCustomRoles");

                SetColorPatch.IsAntiGlitchDisabled = false;
            }

            if (CustomRoles.Lovers.IsEnable() && CustomRoles.Hater.IsEnable()) AssignLoversRoles();
            else if (CustomRoles.Lovers.IsEnable() && rd.Next(0, 100) < Options.GetRoleChance(CustomRoles.Lovers)) AssignLoversRoles();
            if (CustomRoles.Madmate.IsEnable() && Options.MadmateSpawnMode.GetInt() == 0) AssignMadmateRoles();
            AddOnsAssignData.AssignAddOnsFromList();

            foreach (var pair in PlayerState.AllPlayerStates)
            {
                ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                foreach (var subRole in pair.Value.SubRoles)
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, subRole);
            }

            CustomRoleManager.CreateInstance();
            foreach (var pc in Main.AllPlayerControls)
            {
                HudManager.Instance.SetHudActive(true);
                pc.ResetKillCooldown();
            }

            RoleTypes[] RoleTypesList = [RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.Noisemaker, RoleTypes.Tracker, RoleTypes.Shapeshifter, RoleTypes.Phantom]; foreach (var roleTypes in RoleTypesList)
            {
                var roleOpt = Main.NormalOptions.roleOptions;
                roleOpt.SetRoleRate(roleTypes, 0, 0);
            }

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:
                    GameEndChecker.SetPredicateToNormal();
                    break;
            }

            GameOptionsSender.AllSenders.Clear();
            foreach (var pc in Main.AllPlayerControls)
            {
                GameOptionsSender.AllSenders.Add(
                    new PlayerGameOptionsSender(pc)
                );
            }

            /*
            //インポスターのゴーストロールがクルーになるバグ対策
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc.Data.Role.IsImpostor || Main.ResetCamPlayerList.Contains(pc.PlayerId))
                {
                    pc.Data.Role.DefaultGhostRole = RoleTypes.ImpostorGhost;
                }
            }
            */
            Utils.CountAlivePlayers(true);
            Utils.SyncAllSettings();
            SetColorPatch.IsAntiGlitchDisabled = false;
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Postfix");
            ex.Message.Split(@"\r\n").Do(line => Logger.Fatal(line, "Select Role Postfix"));
        }
    }
    private static void AssignDesyncRole(CustomRoles role, PlayerControl player, Dictionary<byte, CustomRpcSender> senders, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
    {
        if (!role.IsEnable())
return;

        var hostId = PlayerControl.LocalPlayer.PlayerId;

        PlayerState.GetByPlayerId(player.PlayerId).SetMainRole(role);
        RpcSetRoleReplacer.DesyncImpostorList.Add(player.PlayerId);

        var hostRole = player.PlayerId == hostId ? hostBaseRole : RoleTypes.Crewmate;
        foreach (var seer in Main.AllPlayerControls)
        {
            if (seer.PlayerId == hostId)
            {
                //ホスト視点は即確定
                player.StartCoroutine(player.CoSetRole(hostRole, false));
            }
            else
            {
                var assignRole = seer.PlayerId == player.PlayerId ? BaseRole : RoleTypes.Scientist;
                senders[player.PlayerId].RpcSetRole(player, assignRole, seer.GetClientId());
            }
        }
        player.Data.IsDead = true;
        Logger.Info($"注册模组职业：{player?.Data?.PlayerName} => {role}", "AssignCustomSubRoles");
    }
    private static void AssignLoversRoles(int RawCount = -1)
    {
        //Loversを初期化
        Main.LoversPlayers.Clear();
        Main.isLoversDead = false;
        var allPlayers = new List<PlayerControl>();
        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc.Is(CustomRoles.GM) || (PlayerState.GetByPlayerId(pc.PlayerId).SubRoles.Count >= Options.AddonsNumLimit.GetInt())
                || pc.Is(CustomRoles.LazyGuy) || pc.Is(CustomRoles.Neptune) || pc.Is(CustomRoles.God) || pc.Is(CustomRoles.Hater)) continue;
            allPlayers.Add(pc);
        }
        var loversRole = CustomRoles.Lovers;
        var rd = IRandom.Instance;
        var count = Math.Clamp(RawCount, 0, allPlayers.Count);
        if (RawCount == -1) count = Math.Clamp(loversRole.GetCount(), 0, allPlayers.Count);
        if (count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var player = allPlayers[rd.Next(0, allPlayers.Count)];
            Main.LoversPlayers.Add(player);
            allPlayers.Remove(player);
            PlayerState.GetByPlayerId(player.PlayerId).SetSubRole(loversRole);
            Logger.Info($"注册附加职业：{player?.Data?.PlayerName}（{player.GetCustomRole()}）=> {loversRole}", "AssignCustomSubRoles");
        }
        RPC.SyncLoversPlayers();
    }
    private static void AssignMadmateRoles()
    {
        var allPlayers = Main.AllPlayerControls.Where(x => x.CanBeMadmate()).ToList();
        var count = Math.Clamp(CustomRoles.Madmate.GetCount(), 0, allPlayers.Count);
        if (count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var player = allPlayers[IRandom.Instance.Next(0, allPlayers.Count)];
            allPlayers.Remove(player);
            PlayerState.GetByPlayerId(player.PlayerId).SetSubRole(CustomRoles.Madmate);
            Logger.Info($"注册附加职业：{player?.Data?.PlayerName}（{player.GetCustomRole()}）=> {CustomRoles.Madmate}", "AssignCustomSubRoles");
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    private class RpcSetRoleReplacer
    {
        public static bool doReplace = false;
        public static Dictionary<byte, CustomRpcSender> senders;
        public static List<(PlayerControl, RoleTypes)> StoragedData = new();
        // 役職Desyncなど別の処理でSetRoleRpcを書き込み済みなため、追加の書き込みが不要なSenderのリスト
        public static List<byte> DesyncImpostorList;
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes roleType)
        {
            if (doReplace && senders != null)
            {
                StoragedData.Add((__instance, roleType));
                return false;
            }
            else return true;
        }
        public static void Release()
        {
            foreach (var (player, role) in StoragedData)
            {
                player.SetRole(role);
                var impostorRole = role is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom;
                if (impostorRole && DesyncImpostorList.Count != 0)
                {
                    foreach (var seer in Main.AllPlayerControls)
                    {
                        var assignRole = DesyncImpostorList.Contains(seer.PlayerId) ? RoleTypes.Scientist : role;
                        senders[player.PlayerId].RpcSetRole(player, assignRole, seer.GetClientId());
                    }
                }
                else
                {
                    senders[player.PlayerId].RpcSetRole(player, role);
                }
            }
            doReplace = false;
        }
        public static void StartReplace(Dictionary<byte, CustomRpcSender> senders)
        {
            RpcSetRoleReplacer.senders = senders;
            StoragedData = new();
            DesyncImpostorList = new();
            doReplace = true;
        }
    }
}