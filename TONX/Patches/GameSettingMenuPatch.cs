using System;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using static UnityEngine.RemoteConfigSettingsHelper;

namespace TONX;

[HarmonyPatch(typeof(GameSettingMenu))]
public class GameSettingMenuPatch
{
    // ゲーム設定メニュータブ
    public enum GameSettingMenuTab
    {
        GamePresets = 0,
        GameSettings,
        RoleSettings,
        Mod_SystemSettings,
        Mod_GameSettings,
        Mod_ImpostorRoles,
        Mod_CrewmateRoles,
        Mod_NeutralRoles,
        Mod_AddOns,
        Mod_OtherRoles,
        MaxCount,
    }

    // ボタンに表示する名前
    public static string[] buttonName = new string[]{
        "Vanilla Settings",
        "System Settings",
        "Game Settings",
        "Impostor Roles",
        "Crewmate Roles",
        "Neutral Roles",
        "Add-Ons",
        "Other Roles",
};

    // 左側配置ボタン座標
    private static Vector3 buttonPosition_Left = new(-3.9f, -0.4f, 0f);
    // 右側配置ボタン座標
    private static Vector3 buttonPosition_Right = new(-2.4f, -0.4f, 0f);
    // ボタンサイズ
    private static Vector3 buttonSize = new(0.45f, 0.6f, 1f);

    private static GameOptionsMenu templateGameOptionsMenu;
    private static PassiveButton templateGameSettingsButton;

    // MOD設定用ボタン格納変数
    static Dictionary<TabGroup, PassiveButton> ModSettingsButtons = new();
    // MOD設定メニュー用タブ格納変数
    static Dictionary<TabGroup, GameOptionsMenu> ModSettingsTabs = new();

    // ゲーム設定メニュー 初期関数
    [HarmonyPatch(nameof(GameSettingMenu.Start)), HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void StartPostfix(GameSettingMenu __instance)
    {
        /******** ボタン作成 ********/

        // 各グループ毎にボタンを作成する
        ModSettingsButtons = new();
        foreach (var tab in EnumHelper.GetAllValues<TabGroup>())
        {
            // ゲーム設定ボタンを元にコピー
            var button = Object.Instantiate(templateGameSettingsButton, __instance.GameSettingsButton.transform.parent);
            button.gameObject.SetActive(true);
            // 名前は「button_ + ボタン名」
            button.name = "Button_" + buttonName[(int)tab + 1]; // buttonName[0]はバニラ設定用の名前なので+1
            // ボタンテキスト
            var label = button.GetComponentInChildren<TextMeshPro>();
            // ボタンテキストの翻訳破棄
            label.DestroyTranslator();
            // ボタンテキストの名前変更
            label.text = Translator.GetString($"TabGroup.{tab}");
            // ボタンテキストの色変更
            Color32 tabcolor = tab switch
            {
                TabGroup.SystemSettings => Main.ModColor32,
                TabGroup.GameSettings => new(89, 239, 131, 255),
                TabGroup.ImpostorRoles => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Impostor),
                TabGroup.CrewmateRoles => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Crewmate),
                TabGroup.NeutralRoles => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Neutral),
                TabGroup.Addons => Utils.GetCustomRoleTypeColor(Roles.Core.CustomRoleTypes.Addon),
                TabGroup.OtherRoles => new(118, 184, 224, 255),
                _ => Color.white,
            };
            button.activeTextColor = button.inactiveTextColor = tabcolor;
            // ボタンテキストの選択中の色変更
            button.selectedTextColor = tabcolor;


            // 各種スプライトをオリジナルのものに変更
            button.inactiveSprites.GetComponent<SpriteRenderer>().color = Utils.ShadeColor(tabcolor, 0.2f);
            button.activeSprites.GetComponent<SpriteRenderer>().color = tabcolor;
            button.selectedSprites.GetComponent<SpriteRenderer>().color = tabcolor;

            // Y座標オフセット
            Vector3 offset = new (0.0f, 0.5f * (((int)tab + 1) / 2), 0.0f);
            // ボタンの座標設定
            button.transform.localPosition = ((((int)tab + 1) % 2 == 0) ? buttonPosition_Left : buttonPosition_Right) - offset;
            // ボタンのサイズ設定
            button.transform.localScale = buttonSize;

            // ボタンがクリックされた時の設定
            var buttonComponent = button.GetComponent<PassiveButton>();
            buttonComponent.OnClick = new();
            // ボタンがクリックされるとタブをそのものに変更する
            buttonComponent.OnClick.AddListener(
                (Action)(() => __instance.ChangeTab((int)tab + 3, false)));

            // ボタン登録
            ModSettingsButtons.Add(tab, button);
        }/******** ボタン作成 ここまで ********/

        /******** タブ作成 ********/
        //// ストリングオプションのテンプレート作成
        //var templateStringOption = GameObject.Find("Main Camera/PlayerOptionsMenu(Clone)/MainArea/GAME SETTINGS TAB/Scroller/SliderInner/GameOption_String(Clone)").GetComponent<StringOption>();
        //if (templateStringOption == null) return;

        ModGameOptionsMenu.OptionList = new();
        ModGameOptionsMenu.BehaviourList = new();
        ModGameOptionsMenu.CategoryHeaderList = new();

        // 各グループ毎にタブを作成する/基盤作成
        ModSettingsTabs = new();
        foreach (var tab in EnumHelper.GetAllValues<TabGroup>())
        {
            // ゲーム設定タブからコピー
            var setTab = Object.Instantiate(templateGameOptionsMenu, __instance.GameSettingsTab.transform.parent);
            // 名前はゲーム設定タブEnumから取得
            setTab.name = ((GameSettingMenuTab)tab + 3).ToString();
            //// 中身を削除
            //setTab.GetComponentsInChildren<OptionBehaviour>().Do(x => Object.Destroy(x.gameObject));
            //setTab.GetComponentsInChildren<CategoryHeaderMasked>().Do(x => Object.Destroy(x.gameObject));
            setTab.gameObject.SetActive(false);

            // 設定タブを追加
            ModSettingsTabs.Add(tab, setTab);
        }

        foreach (var tab in EnumHelper.GetAllValues<TabGroup>())
        {
            if (ModSettingsButtons.TryGetValue(tab, out var button))
            {
                __instance.ControllerSelectable.Add(button);
            }
        }

        //⇒GamOptionsMenuPatchで処理



    }
    private static void SetDefaultButton(GameSettingMenu __instance)
    {
        /******** デフォルトボタン設定 ********/
        // プリセット設定 非表示
        __instance.GamePresetsButton.gameObject.SetActive(false);

        /**** ゲーム設定ボタンを変更 ****/
        var gameSettingButton = __instance.GameSettingsButton;
        // 座標指定
        gameSettingButton.transform.localPosition = new(-3f, -0.5f, 0f);
        // ボタンテキスト
        var textLabel = gameSettingButton.GetComponentInChildren<TextMeshPro>();
        // 翻訳破棄
        textLabel.DestroyTranslator();
        // バニラ設定ボタンの名前を設定
        textLabel.text = "";
        // ボタンテキストの色変更
        gameSettingButton.activeTextColor = gameSettingButton.inactiveTextColor = Color.black;
        // ボタンテキストの選択中の色変更
        gameSettingButton.selectedTextColor = Color.blue;

        var vanillaActiveButton = Utils.LoadSprite($"TownOfHost_Y.Resources.Tab_Active_VanillaGameSettings.png", 100f);
        // 各種スプライトをオリジナルのものに変更
        gameSettingButton.inactiveSprites.GetComponent<SpriteRenderer>().sprite = Utils.LoadSprite($"TownOfHost_Y.Resources.Tab_Small_VanillaGameSettings.png", 100f);
        gameSettingButton.activeSprites.GetComponent<SpriteRenderer>().sprite = vanillaActiveButton;
        gameSettingButton.selectedSprites.GetComponent<SpriteRenderer>().sprite = vanillaActiveButton;
        // ボタンの座標設定
        gameSettingButton.transform.localPosition = buttonPosition_Left;
        // ボタンのサイズ設定
        gameSettingButton.transform.localScale = buttonSize;
        /**** ゲーム設定ボタンを変更 ここまで ****/

        // バニラ役職設定 非表示
        __instance.RoleSettingsButton.gameObject.SetActive(false);
        /******** デフォルトボタン設定 ここまで ********/

        __instance.DefaultButtonSelected = gameSettingButton;
        __instance.ControllerSelectable = new();
        __instance.ControllerSelectable.Add(gameSettingButton);
    }

    [HarmonyPatch(nameof(GameSettingMenu.ChangeTab)), HarmonyPrefix]
    public static bool ChangeTabPrefix(GameSettingMenu __instance, ref int tabNum, [HarmonyArgument(1)] bool previewOnly)
    {
        //// プリセットタブは表示させないため、ゲーム設定タブを設定する
        //if (tabNum == (int)GameSettingMenuTab.GamePresets) {
        //    tabNum = (int)GameSettingMenuTab.GameSettings;

        //    // What Is this?のテキスト文を変更
        //    // __instance.MenuDescriptionText.text = "test";
        //}

        ModGameOptionsMenu.TabIndex = tabNum;

        GameOptionsMenu settingsTab;
        PassiveButton button;

        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            foreach (var tab in EnumHelper.GetAllValues<TabGroup>())
            {
                if (ModSettingsTabs.TryGetValue(tab, out settingsTab) &&
                    settingsTab != null)
                {
                    settingsTab.gameObject.SetActive(false);
                }
            }
            foreach (var tab in EnumHelper.GetAllValues<TabGroup>())
            {
                if (ModSettingsButtons.TryGetValue(tab, out button) &&
                    button != null)
                {
                    button.SelectButton(false);
                }
            }
        }

        if (tabNum < 3) return true;

        if ((previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick) || !previewOnly)
        {
            __instance.PresetsTab.gameObject.SetActive(false);
            __instance.GameSettingsTab.gameObject.SetActive(false);
            __instance.RoleSettingsTab.gameObject.SetActive(false);
            __instance.GamePresetsButton.SelectButton(false);
            __instance.GameSettingsButton.SelectButton(false);
            __instance.RoleSettingsButton.SelectButton(false);

            if (ModSettingsTabs.TryGetValue((TabGroup)(tabNum - 3), out settingsTab) &&
                settingsTab != null)
            {
                settingsTab.gameObject.SetActive(true);
                __instance.MenuDescriptionText.DestroyTranslator();
                __instance.MenuDescriptionText.text = Translator.GetString($"MenuDescriptionText.{(TabGroup)(tabNum - 3)}");
            }
        }
        if (previewOnly)
        {
            __instance.ToggleLeftSideDarkener(false);
            __instance.ToggleRightSideDarkener(true);
            return false;
        }
        __instance.ToggleLeftSideDarkener(true);
        __instance.ToggleRightSideDarkener(false);
        if (ModSettingsButtons.TryGetValue((TabGroup)(tabNum - 3), out button) &&
            button != null)
        {
            button.SelectButton(true);
        }

        return false;
    }

    [HarmonyPatch(nameof(GameSettingMenu.OnEnable)), HarmonyPrefix]
    private static bool OnEnablePrefix(GameSettingMenu __instance)
    {
        if (templateGameOptionsMenu == null)
        {
            templateGameOptionsMenu = Object.Instantiate(__instance.GameSettingsTab, __instance.GameSettingsTab.transform.parent);
            templateGameOptionsMenu.gameObject.SetActive(false);
        }
        if (templateGameSettingsButton == null)
        {
            templateGameSettingsButton = Object.Instantiate(__instance.GameSettingsButton, __instance.GameSettingsButton.transform.parent);
            templateGameSettingsButton.gameObject.SetActive(false);
        }

        SetDefaultButton(__instance);

        ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, __instance.ControllerSelectable, false);
        DestroyableSingleton<HudManager>.Instance.menuNavigationPrompts.SetActive(false);
        if (Controller.currentTouchType != Controller.TouchType.Joystick)
        {
            __instance.ChangeTab(1, Controller.currentTouchType == Controller.TouchType.Joystick);
        }
        __instance.StartCoroutine(__instance.CoSelectDefault());

        return false;
    }
    [HarmonyPatch(nameof(GameSettingMenu.Close)), HarmonyPostfix]
    private static void ClosePostfix(GameSettingMenu __instance)
    {
        foreach (var button in ModSettingsButtons.Values)
            UnityEngine.Object.Destroy(button);
        foreach (var tab in ModSettingsTabs.Values)
            UnityEngine.Object.Destroy(tab);
        ModSettingsButtons = new();
        ModSettingsTabs = new();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSyncSettings))]
public class RpcSyncSettingsPatch
{
    public static void Postfix()
    {
        OptionItem.SyncAllOptions();
    }
}

    //[HarmonyPatch(typeof(NormalGameOptionsV08), nameof(NormalGameOptionsV08.SetRecommendations))]
    //public static class SetRecommendationsPatch
    //{
    //    public static bool Prefix(NormalGameOptionsV08 __instance, int numPlayers, bool isOnline)
    //    {
    //        numPlayers = Mathf.Clamp(numPlayers, 4, 15);
    //        __instance.PlayerSpeedMod = __instance.MapId == 4 ? 1.25f : 1f; //AirShipなら1.25、それ以外は1
    //        __instance.CrewLightMod = 0.5f;
    //        __instance.ImpostorLightMod = 1.75f;
    //        __instance.KillCooldown = 25f;
    //        __instance.NumCommonTasks = 2;
    //        __instance.NumLongTasks = 4;
    //        __instance.NumShortTasks = 6;
    //        __instance.NumEmergencyMeetings = 1;
    //        if (!isOnline)
    //            __instance.NumImpostors = NormalGameOptionsV08.RecommendedImpostors[numPlayers];
    //        __instance.KillDistance = 0;
    //        __instance.DiscussionTime = 0;
    //        __instance.VotingTime = 150;
    //        __instance.IsDefaults = true;
    //        __instance.ConfirmImpostor = false;
    //        __instance.VisualTasks = false;

    //        __instance.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
    //        __instance.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
    //        __instance.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
    //        __instance.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
    //        __instance.roleOptions.SetRoleRecommended(RoleTypes.Shapeshifter);
    //        __instance.roleOptions.SetRoleRecommended(RoleTypes.Scientist);
    //        __instance.roleOptions.SetRoleRecommended(RoleTypes.GuardianAngel);
    //        __instance.roleOptions.SetRoleRecommended(RoleTypes.Engineer);

    //        if (Options.CurrentGameMode == CustomGameMode.HideAndSeek) //HideAndSeek
    //        {
    //            __instance.PlayerSpeedMod = 1.75f;
    //            __instance.CrewLightMod = 5f;
    //            __instance.ImpostorLightMod = 0.25f;
    //            __instance.NumImpostors = 1;
    //            __instance.NumCommonTasks = 0;
    //            __instance.NumLongTasks = 0;
    //            __instance.NumShortTasks = 10;
    //            __instance.KillCooldown = 10f;
    //        }
    //        if (Options.IsStandardHAS) //StandardHAS
    //        {
    //            __instance.PlayerSpeedMod = 1.75f;
    //            __instance.CrewLightMod = 5f;
    //            __instance.ImpostorLightMod = 0.25f;
    //            __instance.NumImpostors = 1;
    //            __instance.NumCommonTasks = 0;
    //            __instance.NumLongTasks = 0;
    //            __instance.NumShortTasks = 10;
    //            __instance.KillCooldown = 10f;
    //        }
    //        if (Options.IsCCMode)
    //        {
    //            __instance.PlayerSpeedMod = 1.5f;
    //            __instance.CrewLightMod = 0.5f;
    //            __instance.ImpostorLightMod = 0.75f;
    //            __instance.NumImpostors = 1;
    //            __instance.NumCommonTasks = 0;
    //            __instance.NumLongTasks = 0;
    //            __instance.NumShortTasks = 1;
    //            __instance.KillCooldown = 20f;
    //            __instance.NumEmergencyMeetings = 1;
    //            __instance.EmergencyCooldown = 30;
    //            __instance.KillDistance = 0;
    //            __instance.DiscussionTime = 0;
    //            __instance.VotingTime = 60;
    //        }
    //        //if (Options.IsONMode)
    //        //{
    //        //    __instance.NumCommonTasks = 1;
    //        //    __instance.NumLongTasks = 0;
    //        //    __instance.NumShortTasks = 1;
    //        //    __instance.KillCooldown = 20f;
    //        //    __instance.NumEmergencyMeetings = 0;
    //        //    __instance.KillDistance = 0;
    //        //    __instance.DiscussionTime = 0;
    //        //    __instance.VotingTime = 300;
    //        //}

    //        return false;
    //    }
    //}
//}