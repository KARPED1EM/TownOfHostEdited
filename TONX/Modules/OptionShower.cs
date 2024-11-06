using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using TONX.Roles.Core;
using UnityEngine;
using static TONX.Translator;

namespace TONX;

[HarmonyPatch(typeof(HudManager))]

public static class OptionShower
{
    public static int currentPage = 0;
    public static List<string> pages = new();
    public static string GetText() => $"{GetString("PressTabToNextPage")}({currentPage + 1}/{pages.Count})\n\n{pages[currentPage]}";
    public static string BuildText()
    {
        //初期化
        StringBuilder sb = new();
        pages = new()
        {
            //1ページに基本ゲーム設定を格納
            GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10) + "\n\n"
        };
        //ゲームモードの表示
        sb.Append($"{Options.GameMode.GetName()}: {Options.GameMode.GetString()}\n\n");
        if (Options.HideGameSettings.GetBool() && !AmongUsClient.Instance.AmHost)
        {
            sb.Append($"<color=#ff0000>{GetString("Message.HideGameSettings")}</color>");
        }
        else
        {
            //Standardの時のみ実行
            if (Options.CurrentGameMode == CustomGameMode.Standard)
            {
                //有効な役職一覧
                sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.GM)}>{Utils.GetRoleName(CustomRoles.GM)}:</color> {Options.EnableGM.GetString()}\n\n");
                sb.Append(GetString("ActiveRolesList")).Append('\n');
                foreach (var kvp in Options.CustomRoleSpawnChances)
                    if (kvp.Value.GameMode is CustomGameMode.Standard or CustomGameMode.All && kvp.Value.GetBool()) //スタンダードか全てのゲームモードで表示する役職
                        sb.Append($"{Utils.ColorString(Utils.GetRoleColor(kvp.Key), Utils.GetRoleName(kvp.Key))}: {kvp.Value.GetString()}×{kvp.Key.GetCount()}\n");
                pages.Add(sb.ToString() + "\n\n");
                sb.Clear();
            }

            Dictionary<string, CustomRoleTypes> pageRoleTypes = new()
            {
                { "TypeImpostor", CustomRoleTypes.Impostor },
                { "TypeCrewmate", CustomRoleTypes.Crewmate },
                { "TypeNeutral", CustomRoleTypes.Neutral },
                { "TypeAddon", CustomRoleTypes.Addon }
            };

            foreach (var type in pageRoleTypes)
            {
                sb.Append($"<size=140%>{Utils.ColorString(Utils.GetCustomRoleTypeColor(type.Value), GetString(type.Key))}</size>\n");
                foreach (var kvp in Options.CustomRoleSpawnChances.Where(o => o.Key.GetCustomRoleTypes() == type.Value && !(o.Key.GetRoleInfo()?.Broken ?? false)))
                {
                    if (!kvp.Key.IsEnable() || kvp.Value.IsHiddenOn(Options.CurrentGameMode)) continue;
                    sb.Append('\n');
                    sb.Append($"{Utils.ColorString(Utils.GetRoleColor(kvp.Key), Utils.GetRoleName(kvp.Key))}: {kvp.Value.GetString()}×{kvp.Key.GetCount()}\n");
                    ShowChildren(kvp.Value, ref sb, Utils.GetRoleColor(kvp.Key).ShadeColor(-0.5f), 1);
                    string rule = Utils.ColorString(Palette.ImpostorRed.ShadeColor(-0.5f), "┣ ");
                    string ruleFooter = Utils.ColorString(Palette.ImpostorRed.ShadeColor(-0.5f), "┗ ");
                }
                pages.Add(sb.ToString());
                sb.Clear();
            }

            sb.Append($"<size=140%><color=#59ef83>{GetString("TabGroup.GameSettings")}</color></size>\n");
            foreach (var opt in OptionItem.AllOptions.Where(x => x.Id is >= 3000000 and < 5000000 && !x.IsHiddenOn(Options.CurrentGameMode) && x.Parent == null))
            {
                if (opt.IsHeader) sb.Append('\n');
                if (opt.IsText) sb.Append($"   {opt.GetName()}\n");
                else sb.Append($"{opt.GetName()}: {opt.GetString()}\n");
                if (opt.GetBool()) ShowChildren(opt, ref sb, Color.white, 1);
            }
            pages.Add(sb.ToString());
            sb.Clear();

            sb.Append($"<size=140%><color={Main.ModColor}>{GetString("TabGroup.SystemSettings")}</color></size>\n");
            foreach (var opt in OptionItem.AllOptions.Where(x => x.Id is >= 2000000 and < 3000000 && !x.IsHiddenOn(Options.CurrentGameMode) && x.Parent == null))
            {
                if (opt.IsHeader) sb.Append('\n');
                if (opt.IsText) sb.Append($"   {opt.GetName()}\n");
                else sb.Append($"{opt.GetName()}: {opt.GetString()}\n");
                if (opt.GetBool()) ShowChildren(opt, ref sb, Color.white, 1);
            }
            pages.Add(sb.ToString());
            sb.Clear();
        }

        if (currentPage >= pages.Count) currentPage = pages.Count - 1; //現在のページが最大ページ数を超えていれば最後のページに修正
        return GetText();
    }
    public static void Next()
    {
        currentPage++;
        if (currentPage >= pages.Count) currentPage = 0; //現在のページが最大ページを超えていれば最初のページに
    }
    public static void Previous()
    {
        currentPage--;
        if (currentPage < 0) currentPage = pages.Count - 1; //超过最小页数时切换到最后一页
    }
    public static void ShowChildren(OptionItem option, ref StringBuilder sb, Color color, int deep = 0)
    {
        foreach (var opt in option.Children.Select((v, i) => new { Value = v, Index = i + 1 }))
        {
            if (opt.Value.Name == "Maximum") continue; //Maximumの項目は飛ばす
            if (deep > 0)
            {
                sb.Append(string.Concat(Enumerable.Repeat(Utils.ColorString(color, "┃"), deep - 1)));
                sb.Append(Utils.ColorString(color, opt.Index == option.Children.Count ? "┗ " : "┣ "));
            }
            sb.Append($"{opt.Value.GetName()}: {opt.Value.GetString()}\n");
            if (opt.Value.GetBool()) ShowChildren(opt.Value, ref sb, color, deep + 1);
        }
    }
}