using app.savedata;
using ImGuiNET;
using REFrameworkNET;
using REFrameworkNET.Attributes;
using REFrameworkNET.Callbacks;
using System;
using System.Collections.Generic;

using EquipGui = app.GUI080001;
using SubmenuGui = app.GUI000007;

namespace MHWildsREFSharpMods;

public static class Extension
{
    public static T? Cast<T>(this _System.Object obj) where T : class
    {
        return (obj as IObject)?.As<T>();
    }

    public static ulong Addr(this _System.Object obj)
    {
        return (obj as IObject)?.GetAddress() ?? 0;
    }
}
public class WeaponLayer
{
    static cEquipWork? mainWeapon = null;
    static cEquipWork? reserveWeapon = null;
    static bool disable = false;

    static int toModelId(byte value)
    {
        return value switch
        {
            0 => -1,
            255 => 0,
            >= 100 => value + 100000 - 100,
            >= 50 => value + 10000 - 50,
            _ => value,
        };
    }

    static byte toByte(int value)
    {
        return value switch
        {
            -1 => 0,
            0 => 255,
            >= 100000 => (byte)(value - 100000 + 100),
            >= 10000 => (byte)(value - 10000 + 50),
            _ => (byte)value,
        };
    }

    static int? GetWeaponIdForModel(int modelId, app.WeaponDef.TYPE wpType)
    {
        var equipData = API.GetManagedSingletonT<app.VariousDataManager>()._Setting.EquipDatas;
        var weaponList = wpType switch
        {
            app.WeaponDef.TYPE.LONG_SWORD => equipData._WeaponLongSword,
            app.WeaponDef.TYPE.SHORT_SWORD => equipData._WeaponShortSword,
            app.WeaponDef.TYPE.TWIN_SWORD => equipData._WeaponTwinSword,
            app.WeaponDef.TYPE.TACHI => equipData._WeaponTachi,
            app.WeaponDef.TYPE.HAMMER => equipData._WeaponHammer,
            app.WeaponDef.TYPE.WHISTLE => equipData._WeaponWhistle,
            app.WeaponDef.TYPE.LANCE => equipData._WeaponLance,
            app.WeaponDef.TYPE.GUN_LANCE => equipData._WeaponGunLance,
            app.WeaponDef.TYPE.SLASH_AXE => equipData._WeaponSlashAxe,
            app.WeaponDef.TYPE.CHARGE_AXE => equipData._WeaponChargeAxe,
            app.WeaponDef.TYPE.ROD => equipData._WeaponRod,
            app.WeaponDef.TYPE.BOW => equipData._WeaponBow,
            app.WeaponDef.TYPE.HEAVY_BOWGUN => equipData._WeaponHeavyBowgun,
            app.WeaponDef.TYPE.LIGHT_BOWGUN => equipData._WeaponLightBowgun,
            _ => null,
        };
        if (weaponList is null) return null;

        for (var i = 0; i < weaponList._Values.Count; ++i)
        {
            var weapon = weaponList._Values[i].Cast<app.user_data.WeaponData.cData>();
            if (weapon is null) continue;
            if (weapon._ModelId == modelId) return weapon._Index;
        }

        if (app.ArtianUtil.getModelId(wpType, 0) == modelId)
            return app.ArtianUtil.getArtianWeaponData(wpType, app.ItemDef.RARE.RARE5)?._Index;
        if (app.ArtianUtil.getModelId(wpType, 2) == modelId)
            return app.ArtianUtil.getArtianWeaponData(wpType, app.ItemDef.RARE.RARE6)?._Index;
        if (app.ArtianUtil.getModelId(wpType, 5) == modelId)
            return app.ArtianUtil.getArtianWeaponData(wpType, app.ItemDef.RARE.RARE7)?._Index;
        return null;
    }

    static Dictionary<int, int?> reverseModelIdCache = new Dictionary<int, int?>();
    static int? GetCachedWeaponIdForModel(int modelId, app.WeaponDef.TYPE wpType)
    {
        if (reverseModelIdCache.ContainsKey(modelId))
            return reverseModelIdCache[modelId];
        var wpId = GetWeaponIdForModel(modelId, wpType);
        reverseModelIdCache[modelId] = wpId;
        return wpId;
    }

    static app.net_packet.cPlCreateInfo? infoPacket = null;
    static app.cHunterCreateInfo? hunterInfo = null;

    [MethodHook(typeof(app.PlayerUtil), nameof(app.PlayerUtil.makeCreateInfoPacketFromCreateInfo), MethodHookType.Pre)]
    static PreHookResult createInfoPacket(Span<ulong> args)
    {
        infoPacket = ManagedObject.FromAddress(args[1]).As<app.net_packet.cPlCreateInfo>();
        hunterInfo = ManagedObject.FromAddress(args[2]).As<app.cHunterCreateInfo>();
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.PlayerUtil), nameof(app.PlayerUtil.makeCreateInfoPacketFromCreateInfo), MethodHookType.Post)]
    static void createInfoPacketPost(ref ulong _)
    {
        API.LogInfo($"mainwptype:{hunterInfo?._WpType}");
        var (_, weaponWork) = currentWeapon(WeaponSlot.Main);
        var mainId = GetCachedWeaponIdForModel(toModelId(weaponWork.FreeVal5), app.WeaponUtil.getCurrentWeaponType(-1));

        var (_, rWeaponWork) = currentWeapon(WeaponSlot.Reserve);
        var reserveId = GetCachedWeaponIdForModel(toModelId(rWeaponWork.FreeVal5), app.WeaponUtil.getCurrentReserveWeaponType());


        if (mainId is not null) infoPacket!.WeaponID = (int)mainId;
        if (reserveId is not null) infoPacket!.ReserveWeaponID = (int)reserveId;

        API.LogToConsole = true;
        API.LogInfo($"forced Ids: {mainId} - {reserveId}");

    }

    enum WeaponSlot
    {
        Main,
        Reserve
    }

    static (int, cEquipWork) currentWeapon(WeaponSlot slot)
    {
        var equipIndex = slot switch
        {
            WeaponSlot.Main => (int)app.EquipDef.EQUIP_INDEX.WEAPON,
            WeaponSlot.Reserve => (int)app.EquipDef.EQUIP_INDEX.RESERVE_WEAPON,
            _ => throw new Exception("Unexpected"),
        };
        var savedata = API.GetManagedSingletonT<app.SaveDataManager>();
        var save = savedata.getCurrentUserSaveData();
        var mainWeaponIdx = save._Equip._EquipIndex.Index[equipIndex];
        return (mainWeaponIdx, save._Equip._EquipBox[mainWeaponIdx]);
    }

    static void refresh(EquipGui gui)
    {
        var (mainWeaponIdx, work) = currentWeapon(WeaponSlot.Main);
        var set = app.EquipDef.EquipSet.REFType.CreateInstance(0);
        set.Invoke(".ctor", [work, mainWeaponIdx]);
        var set_instance = set.As<app.EquipDef.EquipSet>();
        gui._EquipBoxController.updatePlEquip(set_instance, -1);
    }

    [MethodHook(typeof(app.WeaponUtil), "getModelId(app.user_data.WeaponData.cData, app.ArtianUtil.ArtianInfo)", MethodHookType.Post)]
    public static void getModelIdOverride(ref ulong ret)
    {
        var weapon = mainWeapon;
        mainWeapon = null;
        if (weapon is null)
        {
            weapon = reserveWeapon;
            reserveWeapon = null;
        }
        if (weapon is null) return;
        if (weapon.FreeVal5 == 0) return;
        ret = (ulong)toModelId(weapon.FreeVal5);
    }

    [MethodHook(typeof(app.PlayerManager), nameof(app.PlayerManager.generateHunterCreateInfo), MethodHookType.Pre)]
    static PreHookResult generateHunterCreateInfo(Span<ulong> args)
    {
        mainWeapon = ManagedObject.FromAddress(args[2]).As<cEquipWork>();
        reserveWeapon = ManagedObject.FromAddress(args[3]).As<cEquipWork>();
        return PreHookResult.Continue;
    }

    static bool submenuOverride = false;
    static int? weaponModel = null;

    [MethodHook(typeof(EquipGui), nameof(EquipGui.requestSubMenu), MethodHookType.Pre)]
    static PreHookResult requestSubMenuEquip(Span<ulong> args)
    {
        var gui = ManagedObject.FromAddress(args[1])?.As<EquipGui>();
        if (gui is null) return PreHookResult.Continue;
        var selectedWeaponType = gui._CurrentWeaponType;
        var currentWeaponType = app.WeaponUtil.getCurrentWeaponType(-1);
        weaponModel = null;
        submenuOverride = false;

        if (selectedWeaponType != currentWeaponType) return PreHookResult.Continue;

        var (_, current) = currentWeapon(WeaponSlot.Main);
        if (gui._CurrentEquipSet.WorkInfo.Work == current)
        {
            if (current.FreeVal5 == 0) return PreHookResult.Continue;
            submenuOverride = true;
            weaponModel = -1;
            return PreHookResult.Continue;
        }

        var modelId = gui._EquipPreviewParts._CurrentModelId;
        weaponModel = modelId;
        submenuOverride = true;
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.GUIManager), nameof(app.GUIManager.requestSubMenu), MethodHookType.Pre)]
    static PreHookResult requestSubMenuPreHook(Span<ulong> args)
    {
        if (!submenuOverride) return PreHookResult.Continue;
        var guid = _System.Guid.NewGuid();
        var menu_info = ManagedObject.FromAddress(args[3]).As<app.cGUISubMenuInfo>();
        var menu_name = weaponModel >= 0
            ? "Layer on equipped weapon"
            : "Reset Layer";
        menu_info.addItem(menu_name, guid, guid, true, false, null);
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(EquipGui), nameof(EquipGui.callbackSubMenuDecide), MethodHookType.Pre)]
    static PreHookResult decideHook(Span<ulong> args)
    {
        var gui = ManagedObject.FromAddress(args[1]).As<EquipGui>();
        var index = args[4];
        var check = submenuOverride;
        submenuOverride = false;
        if (!check) return PreHookResult.Continue;
        if (index != 4) return PreHookResult.Continue;
        var (_, main_wp) = currentWeapon(WeaponSlot.Main);
        if (weaponModel is int w)
        {
            main_wp.FreeVal5 = toByte(w);
            refresh(gui);
        }
        return PreHookResult.Skip;
    }

    [MethodHook(typeof(SubmenuGui), nameof(SubmenuGui.callbackCancel), MethodHookType.Pre)]
    static PreHookResult cancelHook(Span<ulong> args)
    {
        submenuOverride = false;
        return PreHookResult.Continue;
    }


    [Callback(typeof(ImGuiDrawUI), CallbackType.Pre)]
    static void ImguiDraw()
    {
        if (!ImGui.TreeNode("Weapon Layering")) return;
        var (_, weapon) = currentWeapon(WeaponSlot.Main);
        var weaponData = app.WeaponUtil.getWeaponData(weapon);
        ImGui.Text($"current weapon:");
        ImGui.Text($"modelId:{weaponData._ModelId}:{weaponData._CustomModelId}");
        ImGui.Text($"freeVal5:{weapon.FreeVal5} (as model: {toModelId(weapon.FreeVal5)})");
        var reverseWeaponId = GetWeaponIdForModel(toModelId(weapon.FreeVal5), app.WeaponUtil.getCurrentWeaponType(-1));
        ImGui.Text($"reversed weapon Id: {reverseWeaponId}");

        if (ImGui.TreeNode("Debug"))
        {
            var wpType = app.WeaponUtil.getCurrentWeaponType(-1);
            var t6 = app.ArtianUtil.getArtianWeaponData(wpType, app.ItemDef.RARE.RARE5)?._Index;
            var t7 = app.ArtianUtil.getArtianWeaponData(wpType, app.ItemDef.RARE.RARE6)?._Index;
            var t8 = app.ArtianUtil.getArtianWeaponData(wpType, app.ItemDef.RARE.RARE7)?._Index;
            ImGui.Text($"artian index {t6},{t7},{t8}");

            ImGui.TreePop();
        }
        ImGui.TreePop();
    }

}


