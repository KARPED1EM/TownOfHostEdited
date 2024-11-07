using TMPro;
using TONX.Roles.Core;
using TONX.Roles.Core.Descriptions;
using UnityEngine;

namespace TONX.Modules;

public static class InGameRoleInfoMenu
{
    public static bool Showing => Fill != null && Fill.active && Menu != null && Menu.active;

    public static GameObject Fill;
    public static SpriteRenderer FillSP => Fill.GetComponent<SpriteRenderer>();

    public static GameObject Menu;

    public static GameObject MainInfo;
    public static GameObject AddonsInfo;
    public static TextMeshPro MainInfoTMP => MainInfo.GetComponent<TextMeshPro>();
    public static TextMeshPro AddonsInfoTMP => AddonsInfo.GetComponent<TextMeshPro>();

    public static void Init()
    {
        var DOBScreen = AccountManager.Instance.transform.FindChild("DOBEnterScreen");

        Fill = new("TONX Role Info Menu Fill") { layer = 5 };
        Fill.transform.SetParent(HudManager.Instance.transform.parent, true);
        Fill.transform.localPosition = new(0f, 0f, -980f);
        Fill.transform.localScale = new(20f, 10f, 1f);
        Fill.AddComponent<SpriteRenderer>().sprite = DOBScreen.FindChild("Fill").GetComponent<SpriteRenderer>().sprite;
        FillSP.color = new(0f, 0f, 0f, 0.75f);

        Menu = Object.Instantiate(DOBScreen.FindChild("InfoPage").gameObject, HudManager.Instance.transform.parent);
        Menu.name = "TONX Role Info Menu Page";
        Menu.transform.SetLocalZ(-990f);

        Object.Destroy(Menu.transform.FindChild("Title Text").gameObject);
        Object.Destroy(Menu.transform.FindChild("BackButton").gameObject);
        Object.Destroy(Menu.transform.FindChild("EvenMoreInfo").gameObject);

        MainInfo = Menu.transform.FindChild("InfoText_TMP").gameObject;
        MainInfo.name = "Main Role Info";
        MainInfo.DestroyTranslator();
        MainInfo.transform.localPosition = new(-2.3f, 0.8f, 4f);
        MainInfo.GetComponent<RectTransform>().sizeDelta = new(4.5f, 10f);
        MainInfoTMP.alignment = TextAlignmentOptions.Left;
        MainInfoTMP.fontSize = 2f;

        AddonsInfo = Object.Instantiate(MainInfo, MainInfo.transform.parent);
        AddonsInfo.name = "Addons Info";
        AddonsInfo.DestroyTranslator();
        AddonsInfo.transform.SetLocalX(2.3f);
        AddonsInfo.transform.localScale = new(0.7f, 0.7f, 0.7f);
    }

    public static void SetRoleInfoRef(PlayerControl player)
    {
        if (player == null) return;
        if (!Fill || !Menu) Init();

        MainInfoTMP.text = player?.GetCustomRole().GetRoleInfo()?.Description?.FullFormatHelp ?? "None";
        AddonsInfoTMP.text = AddonDescription.FullFormatHelpByPlayer(player);
    }

    public static void Show()
    {
        if (!Fill || !Menu) Init();
        if (!Showing)
        {
            Fill?.SetActive(true);
            Menu?.SetActive(true);
        }
        //HudManager.Instance?.gameObject.SetActive(false);
    }
    public static void Hide()
    {
        if (Showing)
        {
            Fill?.SetActive(false);
            Menu?.SetActive(false);
        }
        //HudManager.Instance?.gameObject?.SetActive(true);
    }
}
