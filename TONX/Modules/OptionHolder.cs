using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TONX.Modules;
using TONX.Roles.AddOns.Common;
using TONX.Roles.AddOns.Crewmate;
using TONX.Roles.AddOns.Impostor;
using TONX.Roles.Core;
using UnityEngine;

namespace TONX;

[Flags]
public enum CustomGameMode
{
    Standard = 0x01,
    All = int.MaxValue
}

[HarmonyPatch]
public static class Options
{
    static Task taskOptionsLoad;
    [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.Initialize)), HarmonyPostfix]
    public static void OptionsLoadStart()
    {
        Logger.Info("Options.Load Start", "Options");
        taskOptionsLoad = Task.Run(Load);
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix]
    public static void WaitOptionsLoad()
    {
        taskOptionsLoad.Wait();
        Logger.Info("Options.Load End", "Options");
    }

    // 预设
    private static readonly string[] presets =
    {
        Main.Preset1.Value, Main.Preset2.Value, Main.Preset3.Value,
        Main.Preset4.Value, Main.Preset5.Value
    };

    // 游戏模式
    public static OptionItem GameMode;
    public static CustomGameMode CurrentGameMode
        => GameMode.GetInt() switch
        {
            _ => CustomGameMode.Standard
        };

    public static readonly string[] gameModes =
    {
        "Standard"
    };

    // 地图启用
    public static bool IsActiveSkeld => AddedTheSkeld.GetBool() || Main.NormalOptions.MapId == 0;
    public static bool IsActiveMiraHQ => AddedMiraHQ.GetBool() || Main.NormalOptions.MapId == 1;
    public static bool IsActivePolus => AddedPolus.GetBool() || Main.NormalOptions.MapId == 2;
    public static bool IsActiveAirship => AddedTheAirShip.GetBool() || Main.NormalOptions.MapId == 4;
    public static bool IsActiveFungle => AddedTheFungle.GetBool() || Main.NormalOptions.MapId == 5;

    // 职业数量・生成模式&概率
    public static Dictionary<CustomRoles, OptionItem> CustomRoleCounts;
    public static Dictionary<CustomRoles, StringOptionItem> CustomRoleSpawnChances;
    public static readonly string[] Rates =
    {
        "Rate0",  "Rate5",  "Rate10", "Rate20", "Rate30", "Rate40",
        "Rate50", "Rate60", "Rate70", "Rate80", "Rate90", "Rate100",
    };
    public static readonly string[] RoleSpwanModes =
    {
        "RoleOff", "RoleRate", "RoleOn"
    };
    public static readonly string[] RoleSpwanToggle =
    {
        "RoleOff", "AddonEnabled"
    };
    public static readonly string[] CheatResponsesNames =
    {
        "Ban", "Kick", "NoticeMe","NoticeEveryone"
    };
    public static readonly string[] ConfirmEjectionsModes =
    {
        "ConfirmEjections.None",
        "ConfirmEjections.Team",
        "ConfirmEjections.Role"
    };

    #region 创建选项

    //// 阵营 ////

    public static float DefaultKillCooldown = Main.NormalOptions?.KillCooldown ?? 20;
    public static OptionItem DefaultShapeshiftCooldown;

    public static OptionItem DeadImpCantSabotage;
    public static OptionItem ImpKnowAlliesRole;
    public static OptionItem ImpKnowWhosMadmate;
    public static OptionItem MadmateKnowWhosImp;
    public static OptionItem MadmateKnowWhosMadmate;
    public static OptionItem ImpCanKillMadmate;
    public static OptionItem MadmateCanKillImp;

    public static OptionItem NeutralRolesMinPlayer;
    public static OptionItem NeutralRolesMaxPlayer;
    public static OptionItem NeutralRoleWinTogether;
    public static OptionItem NeutralWinTogether;

    public static OptionItem AddonsNumLimit;

    public static OptionItem LoverKnowRoles;
    public static OptionItem LoverSuicide;

    public static OptionItem MadmateSpawnMode;
    public static OptionItem MadmateCountMode;
    public static OptionItem SheriffCanBeMadmate;
    public static OptionItem MayorCanBeMadmate;
    public static OptionItem NGuesserCanBeMadmate;
    public static OptionItem SnitchCanBeMadmate;
    public static OptionItem JudgeCanBeMadmate;
    public static OptionItem MadSnitchTasks;

    //// 游戏设置 ////

    public static OptionItem EnableGM;

    // 驱逐相关设定
    public static OptionItem CEMode;
    public static OptionItem ConfirmEjectionsNK;
    public static OptionItem ConfirmEjectionsNonNK;
    public static OptionItem ConfirmEjectionsNeutralAsImp;
    public static OptionItem ShowImpRemainOnEject;
    public static OptionItem ShowNKRemainOnEject;
    public static OptionItem ShowTeamNextToRoleNameOnEject;

    // 禁用相关设定
    public static OptionItem DisableVanillaRoles;
    public static OptionItem DisableHiddenRoles;
    public static OptionItem DisableTaskWin;

    public static OptionItem DisableTasks;
    public static OptionItem DisableSwipeCard;
    public static OptionItem DisableSubmitScan;
    public static OptionItem DisableUnlockSafe;
    public static OptionItem DisableUploadData;
    public static OptionItem DisableStartReactor;
    public static OptionItem DisableResetBreaker;

    public static OptionItem DisableMeeting;
    public static OptionItem DisableCloseDoor;
    public static OptionItem DisableSabotage;

    public static OptionItem DisableDevices;
    public static OptionItem DisableSkeldDevices;
    public static OptionItem DisableSkeldAdmin;
    public static OptionItem DisableSkeldCamera;
    public static OptionItem DisableMiraHQDevices;
    public static OptionItem DisableMiraHQAdmin;
    public static OptionItem DisableMiraHQDoorLog;
    public static OptionItem DisablePolusDevices;
    public static OptionItem DisablePolusAdmin;
    public static OptionItem DisablePolusCamera;
    public static OptionItem DisablePolusVital;
    public static OptionItem DisableAirshipDevices;
    public static OptionItem DisableAirshipCockpitAdmin;
    public static OptionItem DisableAirshipRecordsAdmin;
    public static OptionItem DisableAirshipCamera;
    public static OptionItem DisableAirshipVital;
    public static OptionItem DisableFungleDevices;
    public static OptionItem DisableFungleVital;
    public static OptionItem DisableDevicesIgnoreConditions;
    public static OptionItem DisableDevicesIgnoreImpostors;
    public static OptionItem DisableDevicesIgnoreNeutrals;
    public static OptionItem DisableDevicesIgnoreCrewmates;
    public static OptionItem DisableDevicesIgnoreAfterAnyoneDied;

    // 会议相关设定
    public static OptionItem SyncButtonMode;
    public static OptionItem SyncedButtonCount;

    public static OptionItem AllAliveMeeting;
    public static OptionItem AllAliveMeetingTime;

    public static OptionItem AdditionalEmergencyCooldown;
    public static OptionItem AdditionalEmergencyCooldownThreshold;
    public static OptionItem AdditionalEmergencyCooldownTime;

    public static OptionItem VoteMode;
    public static OptionItem WhenSkipVote;
    public static OptionItem WhenSkipVoteIgnoreFirstMeeting;
    public static OptionItem WhenSkipVoteIgnoreNoDeadBody;
    public static OptionItem WhenSkipVoteIgnoreEmergency;
    public static OptionItem WhenNonVote;
    public static OptionItem WhenTie;

    // 破坏相关设定
    public static OptionItem CommsCamouflage;
    public static OptionItem DisableReportWhenCC;

    public static OptionItem SabotageTimeControl;
    public static OptionItem PolusReactorTimeLimit;
    public static OptionItem AirshipReactorTimeLimit;
    public static OptionItem FungleReactorTimeLimit;
    public static OptionItem FungleMushroomMixupDuration;

    public static OptionItem LightsOutSpecialSettings;
    public static OptionItem DisableAirshipViewingDeckLightsPanel;
    public static OptionItem DisableAirshipGapRoomLightsPanel;
    public static OptionItem DisableAirshipCargoLightsPanel;
    public static OptionItem BlockDisturbancesToSwitches;

    public static OptionItem ModifySabotageCooldown;
    public static OptionItem SabotageCooldown;

    // 地图相关设定
    public static OptionItem AirShipVariableElectrical;
    public static OptionItem DisableAirshipMovingPlatform;
    public static OptionItem ResetDoorsEveryTurns;
    public static OptionItem DoorsResetMode;
    public static OptionItem DisableFungleSporeTrigger;

    // 随机出生相关设定
    public static OptionItem EnableRandomSpawn;
    //Skeld
    public static OptionItem RandomSpawnSkeld;
    public static OptionItem RandomSpawnSkeldCafeteria;
    public static OptionItem RandomSpawnSkeldWeapons;
    public static OptionItem RandomSpawnSkeldLifeSupp;
    public static OptionItem RandomSpawnSkeldNav;
    public static OptionItem RandomSpawnSkeldShields;
    public static OptionItem RandomSpawnSkeldComms;
    public static OptionItem RandomSpawnSkeldStorage;
    public static OptionItem RandomSpawnSkeldAdmin;
    public static OptionItem RandomSpawnSkeldElectrical;
    public static OptionItem RandomSpawnSkeldLowerEngine;
    public static OptionItem RandomSpawnSkeldUpperEngine;
    public static OptionItem RandomSpawnSkeldSecurity;
    public static OptionItem RandomSpawnSkeldReactor;
    public static OptionItem RandomSpawnSkeldMedBay;
    //Mira
    public static OptionItem RandomSpawnMira;
    public static OptionItem RandomSpawnMiraCafeteria;
    public static OptionItem RandomSpawnMiraBalcony;
    public static OptionItem RandomSpawnMiraStorage;
    public static OptionItem RandomSpawnMiraJunction;
    public static OptionItem RandomSpawnMiraComms;
    public static OptionItem RandomSpawnMiraMedBay;
    public static OptionItem RandomSpawnMiraLockerRoom;
    public static OptionItem RandomSpawnMiraDecontamination;
    public static OptionItem RandomSpawnMiraLaboratory;
    public static OptionItem RandomSpawnMiraReactor;
    public static OptionItem RandomSpawnMiraLaunchpad;
    public static OptionItem RandomSpawnMiraAdmin;
    public static OptionItem RandomSpawnMiraOffice;
    public static OptionItem RandomSpawnMiraGreenhouse;
    //Polus
    public static OptionItem RandomSpawnPolus;
    public static OptionItem RandomSpawnPolusOfficeLeft;
    public static OptionItem RandomSpawnPolusOfficeRight;
    public static OptionItem RandomSpawnPolusAdmin;
    public static OptionItem RandomSpawnPolusComms;
    public static OptionItem RandomSpawnPolusWeapons;
    public static OptionItem RandomSpawnPolusBoilerRoom;
    public static OptionItem RandomSpawnPolusLifeSupp;
    public static OptionItem RandomSpawnPolusElectrical;
    public static OptionItem RandomSpawnPolusSecurity;
    public static OptionItem RandomSpawnPolusDropship;
    public static OptionItem RandomSpawnPolusStorage;
    public static OptionItem RandomSpawnPolusRocket;
    public static OptionItem RandomSpawnPolusLaboratory;
    public static OptionItem RandomSpawnPolusToilet;
    public static OptionItem RandomSpawnPolusSpecimens;
    //AIrShip
    public static OptionItem RandomSpawnAirship;
    public static OptionItem RandomSpawnAirshipBrig;
    public static OptionItem RandomSpawnAirshipEngine;
    public static OptionItem RandomSpawnAirshipKitchen;
    public static OptionItem RandomSpawnAirshipCargoBay;
    public static OptionItem RandomSpawnAirshipRecords;
    public static OptionItem RandomSpawnAirshipMainHall;
    public static OptionItem RandomSpawnAirshipNapRoom;
    public static OptionItem RandomSpawnAirshipMeetingRoom;
    public static OptionItem RandomSpawnAirshipGapRoom;
    public static OptionItem RandomSpawnAirshipVaultRoom;
    public static OptionItem RandomSpawnAirshipComms;
    public static OptionItem RandomSpawnAirshipCockpit;
    public static OptionItem RandomSpawnAirshipArmory;
    public static OptionItem RandomSpawnAirshipViewingDeck;
    public static OptionItem RandomSpawnAirshipSecurity;
    public static OptionItem RandomSpawnAirshipElectrical;
    public static OptionItem RandomSpawnAirshipMedical;
    public static OptionItem RandomSpawnAirshipToilet;
    public static OptionItem RandomSpawnAirshipShowers;
    //Fungle
    public static OptionItem RandomSpawnFungle;
    public static OptionItem RandomSpawnFungleKitchen;
    public static OptionItem RandomSpawnFungleBeach;
    public static OptionItem RandomSpawnFungleCafeteria;
    public static OptionItem RandomSpawnFungleRecRoom;
    public static OptionItem RandomSpawnFungleBonfire;
    public static OptionItem RandomSpawnFungleDropship;
    public static OptionItem RandomSpawnFungleStorage;
    public static OptionItem RandomSpawnFungleMeetingRoom;
    public static OptionItem RandomSpawnFungleSleepingQuarters;
    public static OptionItem RandomSpawnFungleLaboratory;
    public static OptionItem RandomSpawnFungleGreenhouse;
    public static OptionItem RandomSpawnFungleReactor;
    public static OptionItem RandomSpawnFungleJungleTop;
    public static OptionItem RandomSpawnFungleJungleBottom;
    public static OptionItem RandomSpawnFungleLookout;
    public static OptionItem RandomSpawnFungleMiningPit;
    public static OptionItem RandomSpawnFungleHighlands;
    public static OptionItem RandomSpawnFungleUpperEngine;
    public static OptionItem RandomSpawnFunglePrecipice;
    public static OptionItem RandomSpawnFungleComms;

    // 其它设定
    public static OptionItem RandomMapsMode;
    public static OptionItem AddedTheSkeld;
    public static OptionItem AddedMiraHQ;
    public static OptionItem AddedPolus;
    public static OptionItem AddedTheAirShip;
    public static OptionItem AddedTheFungle;
    // public static OptionItem AddedDleks;

    public static OptionItem LadderDeath;
    public static OptionItem LadderDeathChance;

    public static OptionItem FixFirstKillCooldown;
    public static OptionItem ShieldPersonDiedFirst;
    public static OptionItem KillFlashDuration;

    // 幽灵相关设定
    public static OptionItem GhostIgnoreTasks;
    public static OptionItem GhostCanSeeOtherRoles;
    public static OptionItem GhostCanSeeOtherTasks;
    public static OptionItem GhostCanSeeOtherVotes;
    public static OptionItem GhostCanSeeDeathReason;

    //// 系统设定 ////

    // 自动踢出相关设定
    public static OptionItem KickLowLevelPlayer;
    public static OptionItem KickAndroidPlayer;
    public static OptionItem KickPlayerFriendCodeNotExist;
    public static OptionItem ApplyDenyNameList;
    public static OptionItem ApplyBanList;
    public static OptionItem FixSpawnPacketSize;

    public static OptionItem AutoKickStart;
    public static OptionItem AutoKickStartAsBan;
    public static OptionItem AutoKickStartTimes;
    public static OptionItem AutoKickStopWords;
    public static OptionItem AutoKickStopWordsAsBan;
    public static OptionItem AutoKickStopWordsTimes;
    public static OptionItem AutoWarnStopWords;

    // 云服务相关设定
    public static OptionItem ShareLobby;
    public static OptionItem ShareLobbyMinPlayer;

    // 游戏信息相关设定
    public static OptionItem AutoDisplayKillLog;
    public static OptionItem AutoDisplayLastResult;
    public static OptionItem ChangeNameToRoleInfo;
    public static OptionItem SendRoleDescriptionFirstMeeting;
    public static OptionItem HideGameSettings;
    public static OptionItem DIYGameSettings;

    // 个性化相关设定
    public static OptionItem SuffixMode;
    public static OptionItem FormatNameMode;
    public static OptionItem DisableEmojiName;
    public static OptionItem PlayerCanSetColor;
    public static OptionItem KPDCamouflageMode;
    public static OptionItem AllowPlayerPlayWithColoredNameByCustomTags;
    public static OptionItem NonModPleyerCanShowUpperCustomTag;

    // 高级设定
    public static OptionItem NoGameEnd;
    public static OptionItem AllowConsole;
    public static OptionItem EnableDirectorMode;
    public static OptionItem LowLoadMode;
    public static OptionItem EndWhenPlayerBug;
    public static OptionItem CheatResponses;
    public static OptionItem RoleAssigningAlgorithm;

    #endregion

    /* 选项 */

    public static readonly string[] madmateSpawnMode =
    {
        "MadmateSpawnMode.Assign",
        "MadmateSpawnMode.FirstKill",
        "MadmateSpawnMode.SelfVote",
    };
    public static readonly string[] madmateCountMode =
    {
        "MadmateCountMode.None",
        "MadmateCountMode.Imp",
        "MadmateCountMode.Crew",
    };
    public static readonly string[] suffixModes =
    {
        "SuffixMode.None",
        "SuffixMode.Version",
        "SuffixMode.Streaming",
        "SuffixMode.Recording",
        "SuffixMode.RoomHost",
        "SuffixMode.OriginalName",
        "SuffixMode.DoNotKillMe",
        "SuffixMode.NoAndroidPlz"
    };
    public static readonly string[] roleAssigningAlgorithms =
    {
        "RoleAssigningAlgorithm.Default",
        "RoleAssigningAlgorithm.NetRandom",
        "RoleAssigningAlgorithm.HashRandom",
        "RoleAssigningAlgorithm.Xorshift",
        "RoleAssigningAlgorithm.MersenneTwister",
    };
    public static readonly string[] formatNameModes =
    {
        "FormatNameModes.None",
        "FormatNameModes.Color",
        "FormatNameModes.Snacks",
    };
    public static readonly string[] voteModes =
    {
        "Default", "Suicide", "SelfVote", "Skip"
    };
    public static readonly string[] tieModes =
    {
        "TieMode.Default", "TieMode.All", "TieMode.Random"
    };
    public static VoteMode GetWhenSkipVote() => (VoteMode)WhenSkipVote.GetValue();
    public static VoteMode GetWhenNonVote() => (VoteMode)WhenNonVote.GetValue();
    public static SuffixModes GetSuffixMode() => (SuffixModes)SuffixMode.GetValue();

    public static int UsedButtonCount = 0;
    public static int SnitchExposeTaskLeft = 1;

    public static bool IsLoaded = false;
    public static int GetRoleCount(CustomRoles role)
    {
        return GetRoleChance(role) == 0 ? 0 : CustomRoleCounts.TryGetValue(role, out var option) ? option.GetInt() : 0;
    }
    public static int GetRoleChance(CustomRoles role)
    {
        if (!CustomRoleSpawnChances.TryGetValue(role, out var option)) return 0;
        if (option.Selections.Length == 2) return option.GetInt() == 0 ? 0 : 2;
        if (option.Selections.Length == 3) return option.GetInt();
        else return option.GetInt() switch
        {
            0 => 0,
            1 => 5,
            _ => option.GetInt() * 10 - 10
        };
    }
    public static void Load()
    {
        if (IsLoaded) return;
        OptionSaver.Initialize();

        // 预设//9人以上部屋で落ちる現象の対策
        FixSpawnPacketSize = BooleanOptionItem.Create(1_000_200, "FixSpawnPacketSize", false, TabGroup.SystemSettings, true)
            .SetColor(new Color32(255, 255, 0, 255))
            .SetGameMode(CustomGameMode.All);

        _ = PresetOptionItem.Create(0, TabGroup.SystemSettings)
            .SetColor(new Color32(255, 235, 4, byte.MaxValue))
            .SetHeader(true);

        // 游戏模式
        GameMode = StringOptionItem.Create(1, "GameMode", gameModes, 0, TabGroup.GameSettings, false)
            .SetHeader(true);

        Logger.Msg("Loading Role Options...", "Load Options");

        #region 职业详细设置
        CustomRoleCounts = new();
        CustomRoleSpawnChances = new();

        var sortedRoleInfo = CustomRoleManager.AllRolesInfo.Values.Where(role => !(role.RoleName.GetRoleInfo()?.Hidden ?? false)).OrderBy(role => role.ConfigId);

        // 各职业的总体设定
        ImpKnowAlliesRole = BooleanOptionItem.Create(1_000_001, "ImpKnowAlliesRole", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetHeader(true);
        ImpKnowWhosMadmate = BooleanOptionItem.Create(1_000_002, "ImpKnowWhosMadmate", false, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);
        ImpCanKillMadmate = BooleanOptionItem.Create(1_000_003, "ImpCanKillMadmate", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);

        MadmateKnowWhosMadmate = BooleanOptionItem.Create(1_001_001, "MadmateKnowWhosMadmate", false, TabGroup.ImpostorRoles, false)
            .SetHeader(true)
            .SetGameMode(CustomGameMode.Standard);
        MadmateKnowWhosImp = BooleanOptionItem.Create(1_001_002, "MadmateKnowWhosImp", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);
        MadmateCanKillImp = BooleanOptionItem.Create(1_001_003, "MadmateCanKillImp", true, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);

        DefaultShapeshiftCooldown = FloatOptionItem.Create(1_002_001, "DefaultShapeshiftCooldown", new(5f, 999f, 5f), 15f, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Seconds);
        DeadImpCantSabotage = BooleanOptionItem.Create(1_002_002, "DeadImpCantSabotage", false, TabGroup.ImpostorRoles, false)
            .SetGameMode(CustomGameMode.Standard);

        NeutralRolesMinPlayer = IntegerOptionItem.Create(1_003_001, "NeutralRolesMinPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetValueFormat(OptionFormat.Players);
        NeutralRolesMaxPlayer = IntegerOptionItem.Create(1_003_002, "NeutralRolesMaxPlayer", new(0, 15, 1), 0, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);
        NeutralRoleWinTogether = BooleanOptionItem.Create(1_003_003, "NeutralRoleWinTogether", false, TabGroup.NeutralRoles, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetHeader(true);
        NeutralWinTogether = BooleanOptionItem.Create(1_003_004, "NeutralWinTogether", false, TabGroup.NeutralRoles, false).SetParent(NeutralRoleWinTogether)
            .SetGameMode(CustomGameMode.Standard);

        AddonsNumLimit = IntegerOptionItem.Create(1_003_005, "AddonsNumLimit", new(0, 99, 1), 1, TabGroup.Addons, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true);

        // GM
        EnableGM = BooleanOptionItem.Create(100, "GM", false, TabGroup.GameSettings, false)
            .SetColor(Utils.GetRoleColor(CustomRoles.GM))
            .SetHeader(true);

        bool setupExpNow = false;

    StartSetupRoleOptions:

        if (setupExpNow)
        {
            TextOptionItem.Create(1_100_001, "OtherRoles.ImpostorRoles", TabGroup.OtherRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(247, 70, 49, byte.MaxValue));
        }

        // Impostor
        sortedRoleInfo.Where(role => role.CustomRoleType == CustomRoleTypes.Impostor && role.Experimental == setupExpNow).Do(info =>
        {
            SetupRoleOptions(info);
            info.OptionCreator?.Invoke();
        });

        if (setupExpNow)
        {
            TextOptionItem.Create(1_100_002, "OtherRoles.CrewmateRoles", TabGroup.OtherRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(140, 255, 255, byte.MaxValue));
        }

        // Crewmate
        sortedRoleInfo.Where(role => role.CustomRoleType == CustomRoleTypes.Crewmate && role.Experimental == setupExpNow).Do(info =>
        {
            SetupRoleOptions(info);
            info.OptionCreator?.Invoke();
        });

        if (setupExpNow)
        {
            TextOptionItem.Create(1_100_003, "OtherRoles.NeutralRoles", TabGroup.OtherRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 171, 27, byte.MaxValue));
        }

        // Neutral
        sortedRoleInfo.Where(role => role.CustomRoleType == CustomRoleTypes.Neutral && role.Experimental == setupExpNow).Do(info =>
        {
            SetupRoleOptions(info);
            info.OptionCreator?.Invoke();
        });

        if (setupExpNow)
        {
            TextOptionItem.Create(1_100_004, "OtherRoles.Addons", TabGroup.OtherRoles)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 154, 206, byte.MaxValue));
        }

        // Experimental Roles
        if (!setupExpNow)
        {
            setupExpNow = true;
            goto StartSetupRoleOptions;
        }

        // Add-Ons

        // 通用附加
        TextOptionItem.Create(5_100_001, "MenuTitle.Addon.Common", TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Utils.GetCustomRoleTypeColor(CustomRoleTypes.Addon));

        #region Options of Lover
        SetupRoleOptions(80100, TabGroup.Addons, CustomRoles.Lovers, assignCountRule: new(2, 2, 2));
        LoverKnowRoles = BooleanOptionItem.Create(80100 + 4, "LoverKnowRoles", true, TabGroup.Addons, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Lovers])
            .SetGameMode(CustomGameMode.Standard);
        LoverSuicide = BooleanOptionItem.Create(80100 + 3, "LoverSuicide", true, TabGroup.Addons, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Lovers])
            .SetGameMode(CustomGameMode.Standard);
        #endregion

        Neptune.SetupCustomOption();
        Watcher.SetupCustomOption();
        Lighter.SetupCustomOption();
        Seer.SetupCustomOption();
        Flashman.SetupCustomOption();
        Tiebreaker.SetupCustomOption();
        Oblivious.SetupCustomOption();
        Bewilder.SetupCustomOption();
        Fool.SetupCustomOption();
        Avenger.SetupCustomOption();
        Egoist.SetupCustomOption();
        Schizophrenic.SetupCustomOption();
        Reach.SetupCustomOption();
        Bait.SetupCustomOption();
        Beartrap.SetupCustomOption();

        // 船员专属附加
        TextOptionItem.Create(5_100_002, "MenuTitle.Addon.Crew", TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Utils.GetCustomRoleTypeColor(CustomRoleTypes.Crewmate));

        YouTuber.SetupCustomOption();
        Workhorse.SetupCustomOption();
        SetupMadmateRoleOptionsToggle(80200);

        // 内鬼专属附加
        TextOptionItem.Create(5_100_003, "MenuTitle.Addon.Imp", TabGroup.Addons)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(Utils.GetCustomRoleTypeColor(CustomRoleTypes.Impostor));

        LastImpostor.SetupCustomOption();
        TicketsStealer.SetupCustomOption();
        Mimic.SetupCustomOption();

        #endregion

        Logger.Msg("Loading System Options...", "Load Options");

        #region 系统设置

        // 自动踢出相关设定
        TextOptionItem.Create(2_100_001, "MenuTitle.AutoKick", TabGroup.SystemSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));

        KickLowLevelPlayer = IntegerOptionItem.Create(2_000_001, "KickLowLevelPlayer", new(0, 100, 1), 0, TabGroup.SystemSettings, false)
            .SetValueFormat(OptionFormat.Level)
            .SetHeader(true)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        KickAndroidPlayer = BooleanOptionItem.Create(2_000_002, "KickAndroidPlayer", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        KickPlayerFriendCodeNotExist = BooleanOptionItem.Create(2_000_003, "KickPlayerFriendCodeNotExist", false, TabGroup.SystemSettings, true)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        ApplyDenyNameList = BooleanOptionItem.Create(2_000_004, "ApplyDenyNameList", true, TabGroup.SystemSettings, true)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        ApplyBanList = BooleanOptionItem.Create(2_000_005, "ApplyBanList", true, TabGroup.SystemSettings, true)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        AutoKickStart = BooleanOptionItem.Create(2_000_006, "AutoKickStart", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        AutoKickStartTimes = IntegerOptionItem.Create(2_000_007, "AutoKickStartTimes", new(0, 99, 1), 1, TabGroup.SystemSettings, false).SetParent(AutoKickStart)
            .SetValueFormat(OptionFormat.Times)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        AutoKickStartAsBan = BooleanOptionItem.Create(2_000_008, "AutoKickStartAsBan", false, TabGroup.SystemSettings, false).SetParent(AutoKickStart)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        AutoKickStopWords = BooleanOptionItem.Create(2_000_009, "AutoKickStopWords", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        AutoKickStopWordsTimes = IntegerOptionItem.Create(2_000_010, "AutoKickStopWordsTimes", new(0, 99, 1), 3, TabGroup.SystemSettings, false).SetParent(AutoKickStopWords)
            .SetValueFormat(OptionFormat.Times)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        AutoKickStopWordsAsBan = BooleanOptionItem.Create(2_000_011, "AutoKickStopWordsAsBan", false, TabGroup.SystemSettings, false).SetParent(AutoKickStopWords)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));
        AutoWarnStopWords = BooleanOptionItem.Create(2_000_012, "AutoWarnStopWords", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(0, 121, 255, byte.MaxValue));

        // 云服务相关设定
        TextOptionItem.Create(2_100_002, "MenuTitle.CloudServer", TabGroup.SystemSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(0, 223, 162, byte.MaxValue));

        ShareLobby = BooleanOptionItem.Create(2_001_001, "ShareLobby", true, TabGroup.SystemSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(0, 223, 162, byte.MaxValue));
        ShareLobbyMinPlayer = IntegerOptionItem.Create(2_001_002, "ShareLobbyMinPlayer", new(3, 12, 1), 5, TabGroup.SystemSettings, false).SetParent(ShareLobby)
            .SetValueFormat(OptionFormat.Players)
            .SetColor(new Color32(0, 223, 162, byte.MaxValue));

        // 游戏信息相关设定
        TextOptionItem.Create(2_100_003, "MenuTitle.GameInfo", TabGroup.SystemSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(246, 250, 112, byte.MaxValue));

        AutoDisplayKillLog = BooleanOptionItem.Create(2_002_001, "AutoDisplayKillLog", true, TabGroup.SystemSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(246, 250, 112, byte.MaxValue));
        AutoDisplayLastResult = BooleanOptionItem.Create(2_002_002, "AutoDisplayLastResult", true, TabGroup.SystemSettings, false)
            .SetColor(new Color32(246, 250, 112, byte.MaxValue));
        ChangeNameToRoleInfo = BooleanOptionItem.Create(2_002_003, "ChangeNameToRoleInfo", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(246, 250, 112, byte.MaxValue));
        SendRoleDescriptionFirstMeeting = BooleanOptionItem.Create(2_002_004, "SendRoleDescriptionFirstMeeting", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(246, 250, 112, byte.MaxValue));
        HideGameSettings = BooleanOptionItem.Create(2_002_005, "HideGameSettings", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(246, 250, 112, byte.MaxValue));
        DIYGameSettings = BooleanOptionItem.Create(2_002_006, "DIYGameSettings", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(246, 250, 112, byte.MaxValue));

        // 个性化相关设定
        TextOptionItem.Create(2_100_004, "MenuTitle.Personality", TabGroup.SystemSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));

        SuffixMode = StringOptionItem.Create(2_003_001, "SuffixMode", suffixModes, 0, TabGroup.SystemSettings, true)
            .SetHeader(true)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));
        FormatNameMode = StringOptionItem.Create(2_003_002, "FormatNameMode", formatNameModes, 0, TabGroup.SystemSettings, false)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));
        DisableEmojiName = BooleanOptionItem.Create(2_003_003, "DisableEmojiName", true, TabGroup.SystemSettings, false)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));
        PlayerCanSetColor = BooleanOptionItem.Create(2_003_004, "PlayerCanSetColor", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));
        KPDCamouflageMode = BooleanOptionItem.Create(2_003_005, "KPDCamouflageMode", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));
        AllowPlayerPlayWithColoredNameByCustomTags = BooleanOptionItem.Create(2_003_006, "AllowPlayerPlayWithColoredNameByCustomTags", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));
        NonModPleyerCanShowUpperCustomTag = BooleanOptionItem.Create(2_003_007, "NonModPleyerCanShowUpperCustomTag", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(255, 0, 96, byte.MaxValue));

        // 高级设定
        TextOptionItem.Create(2_100_005, "MenuTitle.Advanced", TabGroup.SystemSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 118, 224, byte.MaxValue));

        NoGameEnd = BooleanOptionItem.Create(2_004_001, "NoGameEnd", false, TabGroup.SystemSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(147, 118, 224, byte.MaxValue));
        AllowConsole = BooleanOptionItem.Create(2_004_002, "AllowConsole", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(147, 118, 224, byte.MaxValue));
        EnableDirectorMode = BooleanOptionItem.Create(2_004_003, "EnableDirectorMode", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(147, 118, 224, byte.MaxValue));
        LowLoadMode = BooleanOptionItem.Create(2_004_004, "LowLoadMode", false, TabGroup.SystemSettings, false)
            .SetColor(new Color32(147, 118, 224, byte.MaxValue));
        EndWhenPlayerBug = BooleanOptionItem.Create(2_004_005, "EndWhenPlayerBug", true, TabGroup.SystemSettings, false)
            .SetColor(new Color32(147, 118, 224, byte.MaxValue));
        CheatResponses = StringOptionItem.Create(2_004_006, "CheatResponses", CheatResponsesNames, 0, TabGroup.SystemSettings, false)
            .SetColor(new Color32(147, 118, 224, byte.MaxValue));
        RoleAssigningAlgorithm = StringOptionItem.Create(2_004_007, "RoleAssigningAlgorithm", roleAssigningAlgorithms, 4, TabGroup.SystemSettings, true)
           .RegisterUpdateValueEvent((object obj, OptionItem.UpdateValueEventArgs args) => IRandom.SetInstanceById(args.CurrentValue))
           .SetColor(new Color32(147, 118, 224, byte.MaxValue));

        DebugModeManager.SetupCustomOption();

        #endregion 

        Logger.Msg("Loading Game Options...", "Load Options");

        #region 游戏设置

        // 驱逐相关设定
        TextOptionItem.Create(3_100_001, "MenuTitle.Ejections", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        CEMode = StringOptionItem.Create(3_000_001, "ConfirmEjectionsMode", ConfirmEjectionsModes, 2, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowImpRemainOnEject = BooleanOptionItem.Create(3_000_002, "ShowImpRemainOnEject", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowNKRemainOnEject = BooleanOptionItem.Create(3_000_003, "ShowNKRemainOnEject", true, TabGroup.GameSettings, false).SetParent(ShowImpRemainOnEject)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));
        ShowTeamNextToRoleNameOnEject = BooleanOptionItem.Create(3_000_004, "ShowTeamNextToRoleNameOnEject", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 238, 232, byte.MaxValue));

        // 禁用相关设定
        TextOptionItem.Create(3_100_002, "MenuTitle.Disable", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        DisableVanillaRoles = BooleanOptionItem.Create(3_001_001, "DisableVanillaRoles", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableHiddenRoles = BooleanOptionItem.Create(3_001_002, "DisableHiddenRoles", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableTaskWin = BooleanOptionItem.Create(3_001_003, "DisableTaskWin", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        // 禁用任务
        DisableTasks = BooleanOptionItem.Create(3_002_001, "DisableTasks", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSwipeCard = BooleanOptionItem.Create(3_002_002, "DisableSwipeCardTask", false, TabGroup.GameSettings, false).SetParent(DisableTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableSubmitScan = BooleanOptionItem.Create(3_002_003, "DisableSubmitScanTask", false, TabGroup.GameSettings, false).SetParent(DisableTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUnlockSafe = BooleanOptionItem.Create(3_002_004, "DisableUnlockSafeTask", false, TabGroup.GameSettings, false).SetParent(DisableTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableUploadData = BooleanOptionItem.Create(3_002_005, "DisableUploadDataTask", false, TabGroup.GameSettings, false).SetParent(DisableTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableStartReactor = BooleanOptionItem.Create(3_002_006, "DisableStartReactorTask", false, TabGroup.GameSettings, false).SetParent(DisableTasks)
            .SetGameMode(CustomGameMode.Standard);
        DisableResetBreaker = BooleanOptionItem.Create(3_002_007, "DisableResetBreakerTask", false, TabGroup.GameSettings, false).SetParent(DisableTasks)
            .SetGameMode(CustomGameMode.Standard);

        DisableMeeting = BooleanOptionItem.Create(3_003_001, "DisableMeeting", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableCloseDoor = BooleanOptionItem.Create(3_003_002, "DisableCloseDoor", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));
        DisableSabotage = BooleanOptionItem.Create(3_003_003, "DisableSabotage", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue));

        // 禁用设备
        DisableDevices = BooleanOptionItem.Create(3_004_001, "DisableDevices", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(255, 153, 153, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        DisableSkeldDevices = BooleanOptionItem.Create(3_004_002, "DisableSkeldDevices", false, TabGroup.GameSettings, false).SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableSkeldAdmin = BooleanOptionItem.Create(3_004_003, "DisableSkeldAdmin", false, TabGroup.GameSettings, false).SetParent(DisableSkeldDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableSkeldCamera = BooleanOptionItem.Create(3_004_004, "DisableSkeldCamera", false, TabGroup.GameSettings, false).SetParent(DisableSkeldDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableMiraHQDevices = BooleanOptionItem.Create(3_004_005, "DisableMiraHQDevices", false, TabGroup.GameSettings, false).SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableMiraHQAdmin = BooleanOptionItem.Create(3_004_006, "DisableMiraHQAdmin", false, TabGroup.GameSettings, false).SetParent(DisableMiraHQDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableMiraHQDoorLog = BooleanOptionItem.Create(3_004_007, "DisableMiraHQDoorLog", false, TabGroup.GameSettings, false).SetParent(DisableMiraHQDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolusDevices = BooleanOptionItem.Create(3_004_008, "DisablePolusDevices", false, TabGroup.GameSettings, false).SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolusAdmin = BooleanOptionItem.Create(3_004_009, "DisablePolusAdmin", false, TabGroup.GameSettings, false).SetParent(DisablePolusDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolusCamera = BooleanOptionItem.Create(3_004_010, "DisablePolusCamera", false, TabGroup.GameSettings, false).SetParent(DisablePolusDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisablePolusVital = BooleanOptionItem.Create(3_004_011, "DisablePolusVital", false, TabGroup.GameSettings, false).SetParent(DisablePolusDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipDevices = BooleanOptionItem.Create(3_004_012, "DisableAirshipDevices", false, TabGroup.GameSettings, false).SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipCockpitAdmin = BooleanOptionItem.Create(3_004_013, "DisableAirshipCockpitAdmin", false, TabGroup.GameSettings, false).SetParent(DisableAirshipDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipRecordsAdmin = BooleanOptionItem.Create(3_004_014, "DisableAirshipRecordsAdmin", false, TabGroup.GameSettings, false).SetParent(DisableAirshipDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipCamera = BooleanOptionItem.Create(3_004_015, "DisableAirshipCamera", false, TabGroup.GameSettings, false).SetParent(DisableAirshipDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipVital = BooleanOptionItem.Create(3_004_016, "DisableAirshipVital", false, TabGroup.GameSettings, false).SetParent(DisableAirshipDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableFungleDevices = BooleanOptionItem.Create(3_004_017, "DisableFungleDevices", false, TabGroup.GameSettings, false).SetParent(DisableDevices)
                .SetGameMode(CustomGameMode.Standard);
        DisableFungleVital = BooleanOptionItem.Create(3_004_018, "DisableFungleVital", false, TabGroup.GameSettings, false).SetParent(DisableFungleDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreConditions = BooleanOptionItem.Create(3_005_001, "IgnoreConditions", false, TabGroup.GameSettings, false).SetParent(DisableDevices)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreImpostors = BooleanOptionItem.Create(3_005_002, "IgnoreImpostors", false, TabGroup.GameSettings, false).SetParent(DisableDevicesIgnoreConditions)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreNeutrals = BooleanOptionItem.Create(3_005_003, "IgnoreNeutrals", false, TabGroup.GameSettings, false).SetParent(DisableDevicesIgnoreConditions)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreCrewmates = BooleanOptionItem.Create(3_005_004, "IgnoreCrewmates", false, TabGroup.GameSettings, false).SetParent(DisableDevicesIgnoreConditions)
            .SetGameMode(CustomGameMode.Standard);
        DisableDevicesIgnoreAfterAnyoneDied = BooleanOptionItem.Create(3_005_005, "IgnoreAfterAnyoneDied", false, TabGroup.GameSettings, false).SetParent(DisableDevicesIgnoreConditions)
            .SetGameMode(CustomGameMode.Standard);

        //会议相关设定
        TextOptionItem.Create(3_100_003, "MenuTitle.Meeting", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));

        // 会议限制次数
        SyncButtonMode = BooleanOptionItem.Create(3_010_001, "SyncButtonMode", false, TabGroup.GameSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        SyncedButtonCount = IntegerOptionItem.Create(3_010_002, "SyncedButtonCount", new(0, 100, 1), 10, TabGroup.GameSettings, false).SetParent(SyncButtonMode)
            .SetValueFormat(OptionFormat.Times)
            .SetGameMode(CustomGameMode.Standard);

        // 全员存活时的会议时间
        AllAliveMeeting = BooleanOptionItem.Create(3_011_001, "AllAliveMeeting", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AllAliveMeetingTime = FloatOptionItem.Create(3_011_002, "AllAliveMeetingTime", new(1f, 300f, 1f), 10f, TabGroup.GameSettings, false).SetParent(AllAliveMeeting)
            .SetValueFormat(OptionFormat.Seconds);

        // 附加紧急会议
        AdditionalEmergencyCooldown = BooleanOptionItem.Create(3_012_001, "AdditionalEmergencyCooldown", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue));
        AdditionalEmergencyCooldownThreshold = IntegerOptionItem.Create(3_012_002, "AdditionalEmergencyCooldownThreshold", new(1, 15, 1), 1, TabGroup.GameSettings, false).SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Players);
        AdditionalEmergencyCooldownTime = FloatOptionItem.Create(3_012_003, "AdditionalEmergencyCooldownTime", new(1f, 60f, 1f), 1f, TabGroup.GameSettings, false).SetParent(AdditionalEmergencyCooldown)
            .SetGameMode(CustomGameMode.Standard)
            .SetValueFormat(OptionFormat.Seconds);

        // 投票相关设定
        VoteMode = BooleanOptionItem.Create(3_013_001, "VoteMode", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(147, 241, 240, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVote = StringOptionItem.Create(3_013_002, "WhenSkipVote", voteModes[0..3], 0, TabGroup.GameSettings, false).SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreFirstMeeting = BooleanOptionItem.Create(3_013_003, "WhenSkipVoteIgnoreFirstMeeting", false, TabGroup.GameSettings, false).SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreNoDeadBody = BooleanOptionItem.Create(3_013_004, "WhenSkipVoteIgnoreNoDeadBody", false, TabGroup.GameSettings, false).SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenSkipVoteIgnoreEmergency = BooleanOptionItem.Create(3_013_005, "WhenSkipVoteIgnoreEmergency", false, TabGroup.GameSettings, false).SetParent(WhenSkipVote)
            .SetGameMode(CustomGameMode.Standard);
        WhenNonVote = StringOptionItem.Create(3_013_006, "WhenNonVote", voteModes, 0, TabGroup.GameSettings, false).SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);
        WhenTie = StringOptionItem.Create(3_013_007, "WhenTie", tieModes, 0, TabGroup.GameSettings, false).SetParent(VoteMode)
            .SetGameMode(CustomGameMode.Standard);

        // 破坏相关设定
        TextOptionItem.Create(3_100_004, "MenuTitle.Sabotage", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(241, 212, 227, byte.MaxValue));

        // 通讯破坏小黑人
        CommsCamouflage = BooleanOptionItem.Create(3_020_001, "CommsCamouflage", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(241, 212, 227, byte.MaxValue));
        DisableReportWhenCC = BooleanOptionItem.Create(3_020_002, "DisableReportWhenCC", false, TabGroup.GameSettings, false).SetParent(CommsCamouflage)
            .SetGameMode(CustomGameMode.Standard);

        // 破坏时间设定
        SabotageTimeControl = BooleanOptionItem.Create(3_021_001, "SabotageTimeControl", false, TabGroup.GameSettings, false)
           .SetColor(new Color32(241, 212, 227, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        PolusReactorTimeLimit = FloatOptionItem.Create(3_021_002, "PolusReactorTimeLimit", new(1f, 60f, 1f), 30f, TabGroup.GameSettings, false).SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        AirshipReactorTimeLimit = FloatOptionItem.Create(3_021_003, "AirshipReactorTimeLimit", new(1f, 90f, 1f), 60f, TabGroup.GameSettings, false).SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);
        FungleReactorTimeLimit = FloatOptionItem.Create(3_021_004, "FungleReactorTimeLimit", new(1f, 90f, 1f), 60f, TabGroup.GameSettings, false).SetParent(SabotageTimeControl)
               .SetValueFormat(OptionFormat.Seconds)
               .SetGameMode(CustomGameMode.Standard);
        FungleMushroomMixupDuration = FloatOptionItem.Create(3_021_005, "FungleMushroomMixupDuration", new(1f, 20f, 1f), 10f, TabGroup.GameSettings, false).SetParent(SabotageTimeControl)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        // 停电特殊设定（飞艇）
        LightsOutSpecialSettings = BooleanOptionItem.Create(3_022_001, "LightsOutSpecialSettings", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(241, 212, 227, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipViewingDeckLightsPanel = BooleanOptionItem.Create(3_022_002, "DisableAirshipViewingDeckLightsPanel", false, TabGroup.GameSettings, false).SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipGapRoomLightsPanel = BooleanOptionItem.Create(3_022_003, "DisableAirshipGapRoomLightsPanel", false, TabGroup.GameSettings, false).SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        DisableAirshipCargoLightsPanel = BooleanOptionItem.Create(3_022_004, "DisableAirshipCargoLightsPanel", false, TabGroup.GameSettings, false).SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);
        BlockDisturbancesToSwitches = BooleanOptionItem.Create(3_022_005, "BlockDisturbancesToSwitches", false, TabGroup.GameSettings, false).SetParent(LightsOutSpecialSettings)
            .SetGameMode(CustomGameMode.Standard);

        // 修改破坏冷却时间
        ModifySabotageCooldown = BooleanOptionItem.Create(3_023_001, "ModifySabotageCooldown", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(241, 212, 227, byte.MaxValue))
            .SetGameMode(CustomGameMode.Standard);
        SabotageCooldown = FloatOptionItem.Create(3_023_002, "SabotageCooldown", new(1f, 60f, 1f), 30f, TabGroup.GameSettings, false).SetParent(ModifySabotageCooldown)
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        // 地图相关设定
        TextOptionItem.Create(3_100_005, "MenuTitle.Map", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(85, 170, 255, byte.MaxValue));
        AirShipVariableElectrical = BooleanOptionItem.Create(3_030_001, "AirShipVariableElectrical", false, TabGroup.GameSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(85, 170, 255, byte.MaxValue));
        DisableAirshipMovingPlatform = BooleanOptionItem.Create(3_030_002, "DisableAirshipMovingPlatform", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(85, 170, 255, byte.MaxValue));
        ResetDoorsEveryTurns = BooleanOptionItem.Create(3_030_003, "ResetDoorsEveryTurns", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(85, 170, 255, byte.MaxValue));
        DoorsResetMode = StringOptionItem.Create(3_030_004, "DoorsResetMode", EnumHelper.GetAllNames<DoorsReset.ResetMode>(), 0, TabGroup.GameSettings, false).SetParent(ResetDoorsEveryTurns)
            .SetColor(new Color32(85, 170, 255, byte.MaxValue));
        DisableFungleSporeTrigger = BooleanOptionItem.Create(3_030_005, "DisableFungleSporeTrigger", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(85, 170, 255, byte.MaxValue));
        EnableRandomSpawn = BooleanOptionItem.Create(3_030_006, "RandomSpawn", false, TabGroup.GameSettings, false)
            .SetColor(new Color32(85, 170, 255, byte.MaxValue))
            .SetGameMode(CustomGameMode.All);
        RandomSpawn.SetupCustomOption(3_031_000);

        // 其它设定
        TextOptionItem.Create(3_100_007, "MenuTitle.Other", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        // 随机地图模式
        RandomMapsMode = BooleanOptionItem.Create(3_040_001, "RandomMapsMode", false, TabGroup.GameSettings, false)
            .SetHeader(true)
            .SetColor(new Color32(193, 255, 209, byte.MaxValue));
        AddedTheSkeld = BooleanOptionItem.Create(3_040_002, "AddedTheSkeld", false, TabGroup.GameSettings, false).SetParent(RandomMapsMode);
        AddedMiraHQ = BooleanOptionItem.Create(3_040_003, "AddedMIRAHQ", false, TabGroup.GameSettings, false).SetParent(RandomMapsMode);
        AddedPolus = BooleanOptionItem.Create(3_040_004, "AddedPolus", false, TabGroup.GameSettings, false).SetParent(RandomMapsMode);
        AddedTheAirShip = BooleanOptionItem.Create(3_040_005, "AddedTheAirShip", false, TabGroup.GameSettings, false).SetParent(RandomMapsMode);
        // MapDleks = CustomOption.Create(3_040_006, Color.white, "AddedDleks", false, RandomMapMode);
        AddedTheFungle = BooleanOptionItem.Create(3_040_007, "AddedTheFungle", false, TabGroup.GameSettings, false).SetParent(RandomMapsMode);

        // 梯子摔死
        LadderDeath = BooleanOptionItem.Create(3_042_001, "LadderDeath", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue));
        LadderDeathChance = StringOptionItem.Create(3_042_002, "LadderDeathChance", Rates[1..], 0, TabGroup.GameSettings, false).SetParent(LadderDeath)
            .SetGameMode(CustomGameMode.Standard);

        // 修正首刀时间
        FixFirstKillCooldown = BooleanOptionItem.Create(3_043_001, "FixFirstKillCooldown", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        // 首刀保护
        ShieldPersonDiedFirst = BooleanOptionItem.Create(3_044_001, "ShieldPersonDiedFirst", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue));

        // 杀戮闪烁持续
        KillFlashDuration = FloatOptionItem.Create(3_045_001, "KillFlashDuration", new(0.1f, 0.45f, 0.05f), 0.3f, TabGroup.GameSettings, false)
           .SetColor(new Color32(193, 255, 209, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetGameMode(CustomGameMode.Standard);

        // 幽灵相关设定
        TextOptionItem.Create(3_100_008, "MenuTitle.Ghost", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        // 幽灵设置
        GhostIgnoreTasks = BooleanOptionItem.Create(3_050_001, "GhostIgnoreTasks", false, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetHeader(true)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeOtherRoles = BooleanOptionItem.Create(3_050_002, "GhostCanSeeOtherRoles", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeOtherTasks = BooleanOptionItem.Create(3_050_003, "GhostCanSeeOtherTasks", true, TabGroup.GameSettings, false)
                .SetGameMode(CustomGameMode.Standard)
            .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeOtherVotes = BooleanOptionItem.Create(3_050_004, "GhostCanSeeOtherVotes", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
             .SetColor(new Color32(217, 218, 255, byte.MaxValue));
        GhostCanSeeDeathReason = BooleanOptionItem.Create(3_050_005, "GhostCanSeeDeathReason", true, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.Standard)
           .SetColor(new Color32(217, 218, 255, byte.MaxValue));

        #endregion 

        OptionSaver.Load();

        IsLoaded = true;
        Logger.Msg("All Mod Options Loaded!", "Load Options");
    }

    public static void SetupAddonOptions(int id, TabGroup tab, CustomRoles role, CustomGameMode customGameMode = CustomGameMode.Standard)
        => SetupAddonOptions(id, tab, role, Rates, true, customGameMode);
    public static void SetupAddonOptions(int id, TabGroup tab, CustomRoles role, string[] selections, bool canSetNum, CustomGameMode customGameMode = CustomGameMode.Standard)
    {
        var spawnOption = StringOptionItem.Create(id, role.ToString(), selections, 0, tab, false).SetColor(Utils.GetRoleColor(role))
                .SetHeader(true)
                .SetGameMode(customGameMode) as StringOptionItem;
        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(1, canSetNum ? 15 : 1, 1), 1, tab, false).SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetHidden(!canSetNum)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }
    public static void SetupRoleOptions(SimpleRoleInfo info) =>
        SetupRoleOptions(info.ConfigId, info.Tab, info.RoleName);
    public static void SetupRoleOptions(int id, TabGroup tab, CustomRoles role, IntegerValueRule assignCountRule = null, CustomGameMode customGameMode = CustomGameMode.Standard)
    {
        if (role.IsVanilla()) return;
        assignCountRule ??= role.GetRoleInfo().AssignCountRule ?? new(1, 15, 1);

        bool broken = role.GetRoleInfo()?.Broken ?? false;

        var spawnOption = StringOptionItem.Create(id, role.ToString(), RoleSpwanModes, 0, tab, false)
            .SetColor(broken ? Palette.DisabledGrey : Utils.GetRoleColor(role))
            .SetHeader(true)
            .SetAddDesc(broken ? Utils.ColorString(Palette.DisabledGrey, Translator.GetString("RoleBroken")) : "")
            .SetGameMode(customGameMode) as StringOptionItem;

        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", assignCountRule, assignCountRule.Step, tab, false)
            .SetParent(spawnOption)
            .SetValueFormat(OptionFormat.Players)
            .SetGameMode(customGameMode);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }
    private static void SetupMadmateRoleOptionsToggle(int id, CustomGameMode customGameMode = CustomGameMode.Standard)
    {
        var role = CustomRoles.Madmate;
        var spawnOption = StringOptionItem.Create(id, role.ToString(), RoleSpwanToggle, 0, TabGroup.Addons, false).SetColor(Utils.GetRoleColor(role))
                .SetHeader(true)
                .SetGameMode(customGameMode) as StringOptionItem;

        var countOption = IntegerOptionItem.Create(id + 1, "Maximum", new(1, 15, 1), 1, TabGroup.Addons, false).SetParent(spawnOption)
            .SetGameMode(customGameMode);

        MadmateSpawnMode = StringOptionItem.Create(id + 10, "MadmateSpawnMode", madmateSpawnMode, 0, TabGroup.Addons, false).SetParent(spawnOption);
        MadmateCountMode = StringOptionItem.Create(id + 11, "MadmateCountMode", madmateCountMode, 0, TabGroup.Addons, false).SetParent(spawnOption);
        SheriffCanBeMadmate = BooleanOptionItem.Create(id + 12, "SheriffCanBeMadmate", false, TabGroup.Addons, false).SetParent(spawnOption);
        MayorCanBeMadmate = BooleanOptionItem.Create(id + 13, "MayorCanBeMadmate", false, TabGroup.Addons, false).SetParent(spawnOption);
        NGuesserCanBeMadmate = BooleanOptionItem.Create(id + 14, "NGuesserCanBeMadmate", false, TabGroup.Addons, false).SetParent(spawnOption);
        SnitchCanBeMadmate = BooleanOptionItem.Create(id + 15, "SnitchCanBeMadmate", false, TabGroup.Addons, false).SetParent(spawnOption);
        MadSnitchTasks = IntegerOptionItem.Create(id + 16, "MadSnitchTasks", new(1, 99, 1), 3, TabGroup.Addons, false).SetParent(SnitchCanBeMadmate)
            .SetValueFormat(OptionFormat.Pieces);
        JudgeCanBeMadmate = BooleanOptionItem.Create(id + 17, "JudgeCanBeMadmate", false, TabGroup.Addons, false).SetParent(spawnOption);

        CustomRoleSpawnChances.Add(role, spawnOption);
        CustomRoleCounts.Add(role, countOption);
    }
    public class OverrideTasksData
    {
        public static Dictionary<CustomRoles, OverrideTasksData> AllData = new();
        public CustomRoles Role { get; private set; }
        public int IdStart { get; private set; }
        public OptionItem doOverride;
        public OptionItem assignCommonTasks;
        public OptionItem numLongTasks;
        public OptionItem numShortTasks;

        public OverrideTasksData(int idStart, TabGroup tab, CustomRoles role)
        {
            IdStart = idStart;
            Role = role;
            Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(Utils.GetRoleColor(role), Utils.GetRoleName(role)) } };
            doOverride = BooleanOptionItem.Create(idStart++, "doOverride", false, tab, false).SetParent(CustomRoleSpawnChances[role])
                .SetValueFormat(OptionFormat.None);
            doOverride.ReplacementDictionary = replacementDic;
            assignCommonTasks = BooleanOptionItem.Create(idStart++, "assignCommonTasks", true, tab, false).SetParent(doOverride)
                .SetValueFormat(OptionFormat.None);
            assignCommonTasks.ReplacementDictionary = replacementDic;
            numLongTasks = IntegerOptionItem.Create(idStart++, "roleLongTasksNum", new(0, 99, 1), 3, tab, false).SetParent(doOverride)
                .SetValueFormat(OptionFormat.Pieces);
            numLongTasks.ReplacementDictionary = replacementDic;
            numShortTasks = IntegerOptionItem.Create(idStart++, "roleShortTasksNum", new(0, 99, 1), 3, tab, false).SetParent(doOverride)
                .SetValueFormat(OptionFormat.Pieces);
            numShortTasks.ReplacementDictionary = replacementDic;

            if (!AllData.ContainsKey(role)) AllData.Add(role, this);
            else Logger.Warn("重複したCustomRolesを対象とするOverrideTasksDataが作成されました", "OverrideTasksData");
        }
        public static OverrideTasksData Create(int idStart, TabGroup tab, CustomRoles role)
        {
            return new OverrideTasksData(idStart, tab, role);
        }
        public static OverrideTasksData Create(SimpleRoleInfo roleInfo, int idOffset)
        {
            return new OverrideTasksData(roleInfo.ConfigId + idOffset, roleInfo.Tab, roleInfo.RoleName);
        }
    }
}