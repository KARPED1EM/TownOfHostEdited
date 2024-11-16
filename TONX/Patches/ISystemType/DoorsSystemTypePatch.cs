using HarmonyLib;
using Hazel;
using TONX.Roles.AddOns.Common;
using TONX.Roles.Core;
using TONX.Roles.Core.Interfaces;

namespace TONX.Patches.ISystemType;

[HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.UpdateSystem))]
public static class DoorsSystemTypeUpdateSystemPatch
{
    public static bool Prefix(DoorsSystemType __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
    {
        byte amount;
        {
            var newReader = MessageReader.Get(msgReader);
            amount = newReader.ReadByte();
            newReader.Recycle();
        }
        if (player.Is(CustomRoles.Fool) && Fool.OptionImpFoolCanNotOpenDoor.GetBool()) return false;

        if (player.GetRoleClass() is ISystemTypeUpdateHook systemTypeUpdateHook && !systemTypeUpdateHook.UpdateDoorsSystem(__instance, amount))
        {
            return false;
        }
        return true;
    }
}