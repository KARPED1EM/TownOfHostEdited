using AmongUs.GameOptions;
using System;
using System.Linq;
using TONX.Roles.Core.Descriptions;
using UnityEngine;
using static TONX.Options;

namespace TONX.Roles.Core;

public class SimpleRoleInfo
{
    public Type ClassType;
    public Func<PlayerControl, RoleBase> CreateInstance;
    public CustomRoles RoleName;
    public Func<RoleTypes> BaseRoleType;
    public CustomRoleTypes CustomRoleType;
    public CountTypes CountType;
    public Color RoleColor;
    public string RoleColorCode;
    public int ConfigId;
    public TabGroup Tab;
    public OptionItem RoleOption => CustomRoleSpawnChances[RoleName];
    public bool IsEnable = false;
    public OptionCreatorDelegate OptionCreator;
    public string ChatCommand;
    /// <summary>本人視点のみインポスターに見える役職</summary>
    public bool IsDesyncImpostor;
    private Func<AudioClip> introSound;
    public AudioClip IntroSound => introSound?.Invoke();
    public bool Experimental;
    public bool Broken;
    /// <summary>
    /// 人数设定上的最小人数/最大人数/一单位数
    /// </summary>
    public IntegerValueRule AssignCountRule;
    /// <summary>
    /// 人数应该分配多少
    /// 役職の抽選回数 = 設定人数 / AssignUnitCount
    /// </summary>
    public int AssignUnitCount => AssignCountRule?.Step ?? 1;
    /// <summary>
    /// 実際にアサインされる役職の内訳
    /// </summary>
    public CustomRoles[] AssignUnitRoles;
    /// <summary>役職の説明関係</summary>
    public RoleDescription Description { get; private set; }

    private SimpleRoleInfo(
        Type classType,
        Func<PlayerControl, RoleBase> createInstance,
        CustomRoles roleName,
        Func<RoleTypes> baseRoleType,
        CustomRoleTypes customRoleType,
        CountTypes countType,
        int configId,
        OptionCreatorDelegate optionCreator,
        string chatCommand,
        string colorCode,
        bool isDesyncImpostor,
        TabGroup tab,
        Func<AudioClip> introSound,
        bool experimental,
        bool broken,
        IntegerValueRule assignCountRule,
        CustomRoles[] assignUnitRoles
    )
    {
        ClassType = classType;
        CreateInstance = createInstance;
        RoleName = roleName;
        BaseRoleType = baseRoleType;
        CustomRoleType = customRoleType;
        CountType = countType;
        ConfigId = configId;
        OptionCreator = optionCreator;
        IsDesyncImpostor = isDesyncImpostor;
        this.introSound = introSound;
        ChatCommand = chatCommand;
        Experimental = experimental;
        Broken = broken;
        AssignCountRule = assignCountRule;
        AssignUnitRoles = assignUnitRoles;

        if (colorCode == "")
            colorCode = customRoleType switch
            {
                CustomRoleTypes.Impostor => "#ff1919",
                CustomRoleTypes.Crewmate => "#8cffff",
                _ => "#ffffff"
            };
        RoleColorCode = colorCode;

        _ = ColorUtility.TryParseHtmlString(colorCode, out RoleColor);

        if (Experimental) tab = TabGroup.OtherRoles;
        else if (tab == TabGroup.GameSettings)
            tab = CustomRoleType switch
            {
                CustomRoleTypes.Impostor => TabGroup.ImpostorRoles,
                CustomRoleTypes.Crewmate => TabGroup.CrewmateRoles,
                CustomRoleTypes.Neutral => TabGroup.NeutralRoles,
                CustomRoleTypes.Addon => TabGroup.Addons,
                _ => tab
            };
        Tab = tab;

        CustomRoleManager.AllRolesInfo.Add(roleName, this);
    }
    public static SimpleRoleInfo Create(
        Type classType,
        Func<PlayerControl, RoleBase> createInstance,
        CustomRoles roleName,
        Func<RoleTypes> baseRoleType,
        CustomRoleTypes customRoleType,
        int configId,
        OptionCreatorDelegate optionCreator,
        string chatCommand,
        string colorCode = "",
        bool isDesyncImpostor = false,
        TabGroup tab = TabGroup.GameSettings,
        Func<AudioClip> introSound = null,
        CountTypes? countType = null,
        bool experimental = false,
        bool broken = false,
        IntegerValueRule assignCountRule = null,
        CustomRoles[] assignUnitRoles = null
    )
    {
        countType ??= customRoleType == CustomRoleTypes.Impostor ?
            CountTypes.Impostor :
            CountTypes.Crew;
        assignCountRule ??= customRoleType == CustomRoleTypes.Impostor ?
            new(1, 3, 1) :
            new(1, 15, 1);
        assignUnitRoles ??= Enumerable.Repeat(roleName, assignCountRule.Step).ToArray();
        var roleInfo = new SimpleRoleInfo(
                classType,
                createInstance,
                roleName,
                baseRoleType,
                customRoleType,
                countType.Value,
                configId,
                optionCreator,
                chatCommand,
                colorCode,
                isDesyncImpostor,
                tab,
                introSound,
                experimental,
                broken,
                assignCountRule,
                assignUnitRoles
            );
        roleInfo.Description = new SingleRoleDescription(roleInfo);
        return roleInfo;
    }
    public static SimpleRoleInfo CreateForVanilla(
        Type classType,
        Func<PlayerControl, RoleBase> createInstance,
        RoleTypes baseRoleType,
        string colorCode = ""
    )
    {
        CustomRoles roleName;
        CustomRoleTypes customRoleType;
        CountTypes countType = CountTypes.Crew;

        switch (baseRoleType)
        {
            case RoleTypes.Engineer:
                roleName = CustomRoles.Engineer;
                customRoleType = CustomRoleTypes.Crewmate;
                break;
            case RoleTypes.Noisemaker:
                roleName = CustomRoles.Noisemaker;
                customRoleType = CustomRoleTypes.Crewmate;
                break;
            case RoleTypes.Tracker:
                roleName = CustomRoles.Tracker;
                customRoleType = CustomRoleTypes.Crewmate;
                break;
            case RoleTypes.Scientist:
                roleName = CustomRoles.Scientist;
                customRoleType = CustomRoleTypes.Crewmate;
                break;
            case RoleTypes.GuardianAngel:
                roleName = CustomRoles.GuardianAngel;
                customRoleType = CustomRoleTypes.Crewmate;
                break;
            case RoleTypes.Impostor:
                roleName = CustomRoles.Impostor;
                customRoleType = CustomRoleTypes.Impostor;
                countType = CountTypes.Impostor;
                break;
            case RoleTypes.Phantom:
                roleName = CustomRoles.Phantom;
                customRoleType = CustomRoleTypes.Impostor;
                countType = CountTypes.Impostor;
                break;
            case RoleTypes.Shapeshifter:
                roleName = CustomRoles.Shapeshifter;
                customRoleType = CustomRoleTypes.Impostor;
                countType = CountTypes.Impostor;
                break;
            default:
                roleName = CustomRoles.Crewmate;
                customRoleType = CustomRoleTypes.Crewmate;
                break;
        }
        var roleInfo = new SimpleRoleInfo(
                classType,
                createInstance,
                roleName,
                () => baseRoleType,
                customRoleType,
                countType,
                -1,
                null,
                null,
                colorCode,
                false,
                TabGroup.GameSettings,
                null,
                false,
                false,
                new(1, 15, 1),
                new CustomRoles[1] { roleName }
            );
        roleInfo.Description = new VanillaRoleDescription(roleInfo, baseRoleType);
        return roleInfo;
    }
    public delegate void OptionCreatorDelegate();
}