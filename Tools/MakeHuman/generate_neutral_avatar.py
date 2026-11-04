"""
Generate the gender-neutral human avatar for Avian_Lab_Embodiment with MakeHuman (MPFB2 in Blender).

Why MakeHuman: it is the free, CC0 human generator used in many CHI / IEEE VR / UIST embodiment studies, and
its macro "gender" slider lets us build an androgynous body (gender = 0.5) instead of picking a male or a
female model. The Rocketbox library (used for the male / female bodies) has no androgynous avatars; the
VALID library does not have them yet either.

Design choices follow the androgynous-agent guidelines of Nag & Yalcin (2020, "Gender stereotypes in virtual
agents", IVA): neutral body proportions, no breasts, short hair, no makeup skin, loose unisex clothes
(plain T-shirt and jeans), neutral face.

Requirements
  * Blender >= 4.2 with the MPFB2 extension installed (https://static.makehumancommunity.org/mpfb.html,
    GPL, repo https://github.com/makehumancommunity/mpfb2). Install it from Blender:
    Edit > Preferences > Get Extensions > search "MPFB" > Install, or drop the mpfb folder into
    %APPDATA%/Blender Foundation/Blender/<version>/extensions/user_default/mpfb.
  * The "makehuman_system_assets" CC0 asset pack:
    https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip
    Pass it with --asset-zip (it is unpacked into MPFB's data folder on first run) or unpack it there yourself.

Run (from the repository root, Windows example):

  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --python Tools/MakeHuman/generate_neutral_avatar.py -- ^
      --out Assets/AvianVoice/Resources/Avatars/MakeHuman/Neutral_Adult_01 ^
      --asset-zip path/to/makehuman_system_assets_cc0.zip

The script writes Neutral_Adult_01.fbx (+ Neutral_Adult_01.fbm/ textures). Unity imports it as Humanoid through
Assets/AvianVoice/Editor/MakeHumanAvatarImport.cs. The body gets a second material slot "neutral_head" for the
head faces so FirstPersonHeadHider can hide the head for the headset camera only.
"""

import argparse
import importlib
import os
import sys
import traceback
import zipfile

import bpy

EXTENSION_MODULE = "bl_ext.user_default.mpfb"


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description="Generate the neutral MakeHuman avatar")
    parser.add_argument("--out", required=True, help="Output folder (created). The FBX is named after the folder.")
    parser.add_argument("--asset-zip", default="", help="makehuman_system_assets_cc0.zip, unpacked into MPFB data if needed")
    parser.add_argument("--name", default="", help="Avatar / FBX name (default: output folder name)")
    parser.add_argument("--gender", type=float, default=0.5, help="0 = female, 1 = male, 0.5 = androgynous")
    parser.add_argument("--age", type=float, default=0.5, help="0.5 = about 25 years")
    parser.add_argument("--muscle", type=float, default=0.5)
    parser.add_argument("--weight", type=float, default=0.5)
    parser.add_argument("--height", type=float, default=0.5)
    parser.add_argument("--proportions", type=float, default=0.5)
    parser.add_argument("--cupsize", type=float, default=0.0, help="0 = flat chest (androgynous)")
    parser.add_argument("--firmness", type=float, default=0.5)
    parser.add_argument("--asian", type=float, default=0.5)
    parser.add_argument("--caucasian", type=float, default=0.25)
    parser.add_argument("--african", type=float, default=0.25)
    parser.add_argument("--skin", default="young_asian_male.mhmat", help="Skin .mhmat from the system assets (no makeup)")
    parser.add_argument("--hair", default="short04.mhclo")
    parser.add_argument("--eyebrows", default="eyebrow001.mhclo")
    parser.add_argument("--eyelashes", default="eyelashes01.mhclo")
    parser.add_argument("--eyes", default="low-poly.mhclo")
    parser.add_argument("--clothes", default="male_casualsuit06.mhclo", help="Plain white T-shirt and jeans")
    parser.add_argument("--shoes", default="shoes01.mhclo")
    parser.add_argument("--rig", default="mixamo_unity", help="MPFB built-in rig; mixamo_unity maps to Unity Humanoid")
    parser.add_argument("--no-teeth", action="store_true")
    return parser.parse_args(argv)


def dynamic_import(absolute_package_str, key):
    """MPFB lives at an unknown module path inside Blender's extension system; find it by suffix."""
    for amod in list(sys.modules):
        if amod.endswith(absolute_package_str):
            mod = importlib.import_module(amod)
            if hasattr(mod, key):
                return getattr(mod, key)
    raise ImportError("MPFB module not found: " + absolute_package_str + " (is the MPFB extension installed and enabled?)")


def enable_mpfb():
    try:
        bpy.ops.extensions.repo_refresh_all()
    except Exception as e:  # noqa: BLE001
        print("[neutral-avatar] extension refresh skipped:", e)
    try:
        bpy.ops.preferences.addon_enable(module=EXTENSION_MODULE)
    except Exception as e:  # noqa: BLE001
        print("[neutral-avatar] addon_enable failed:", e)
        raise


def ensure_assets(asset_zip, location_service):
    data_dir = location_service.get_user_data()
    os.makedirs(data_dir, exist_ok=True)
    if os.path.isdir(os.path.join(data_dir, "skins")) and os.path.isdir(os.path.join(data_dir, "clothes")):
        print("[neutral-avatar] assets present in", data_dir)
        return data_dir
    if not asset_zip or not os.path.isfile(asset_zip):
        raise RuntimeError("MakeHuman system assets not found in " + data_dir + "; pass --asset-zip <makehuman_system_assets_cc0.zip>")
    print("[neutral-avatar] unpacking", asset_zip, "into", data_dir)
    with zipfile.ZipFile(asset_zip) as z:
        z.extractall(data_dir)
    return data_dir


def find_asset(asset_service, fname, subdir):
    path = asset_service.find_asset_absolute_path(fname, asset_subdir=subdir)
    if path is None:
        raise RuntimeError("asset not found: " + subdir + "/" + fname)
    return path


def rename_materials(obj, new_name):
    for slot in obj.material_slots:
        if slot.material is not None:
            slot.material.name = new_name


def split_head_material(basemesh, head_bone="mixamorig:Head", neck_bone="mixamorig:Neck", min_weight=0.5):
    """Give the head faces their own material slot ("neutral_head") so Unity can hide only the head in first person."""
    mesh = basemesh.data
    groups = {g.name: g.index for g in basemesh.vertex_groups}
    head_index = groups.get(head_bone)
    neck_index = groups.get(neck_bone)
    if head_index is None:
        print("[neutral-avatar] no head vertex group; head material not split")
        return

    if len(basemesh.material_slots) == 0 or basemesh.material_slots[0].material is None:
        print("[neutral-avatar] basemesh has no material; head material not split")
        return

    body_material = basemesh.material_slots[0].material
    head_material = body_material.copy()
    head_material.name = "neutral_head"
    basemesh.data.materials.append(head_material)
    head_slot = len(basemesh.material_slots) - 1

    def head_weight(vertex):
        w = 0.0
        for g in vertex.groups:
            if g.group == head_index:
                w += g.weight
            elif neck_index is not None and g.group == neck_index:
                w += g.weight * 0.5
        return w

    heads = [head_weight(v) >= min_weight for v in mesh.vertices]
    count = 0
    for poly in mesh.polygons:
        if all(heads[i] for i in poly.vertices):
            poly.material_index = head_slot
            count += 1
    print("[neutral-avatar] head faces:", count, "of", len(mesh.polygons))


def main():
    args = parse_args()
    out_dir = os.path.abspath(args.out)
    name = args.name or os.path.basename(out_dir.rstrip("/\\"))
    os.makedirs(out_dir, exist_ok=True)

    enable_mpfb()
    HumanService = dynamic_import("mpfb.services.humanservice", "HumanService")
    AssetService = dynamic_import("mpfb.services.assetservice", "AssetService")
    ExportService = dynamic_import("mpfb.services.exportservice", "ExportService")
    ObjectService = dynamic_import("mpfb.services.objectservice", "ObjectService")
    LocationService = dynamic_import("mpfb.services.locationservice", "LocationService")

    ensure_assets(args.asset_zip, LocationService)

    # Fresh scene.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    enable_mpfb()

    macro = {
        "gender": args.gender,
        "age": args.age,
        "muscle": args.muscle,
        "weight": args.weight,
        "proportions": args.proportions,
        "height": args.height,
        "cupsize": args.cupsize,
        "firmness": args.firmness,
        "race": {"asian": args.asian, "caucasian": args.caucasian, "african": args.african},
    }
    print("[neutral-avatar] macro", macro)
    basemesh = HumanService.create_human(macro_detail_dict=macro)
    basemesh.name = name

    HumanService.set_character_skin(find_asset(AssetService, args.skin, "skins"), basemesh, skin_type="GAMEENGINE")
    rename_materials(basemesh, "neutral_body")

    HumanService.add_builtin_rig(basemesh, args.rig)

    assets = [
        ("eyes", args.eyes, "Eyes", "neutral_head_eyes"),
        ("eyebrows", args.eyebrows, "Eyebrows", "neutral_head_eyebrows"),
        ("eyelashes", args.eyelashes, "Eyelashes", "neutral_head_eyelashes"),
        ("hair", args.hair, "Hair", "neutral_hair"),
        ("clothes", args.clothes, "Clothes", "neutral_clothes"),
        ("clothes", args.shoes, "Clothes", "neutral_shoes"),
    ]
    if not args.no_teeth:
        assets += [("teeth", "teeth_base.mhclo", "Teeth", "neutral_head_teeth"), ("tongue", "tongue01.mhclo", "Tongue", "neutral_head_tongue")]

    for subdir, fname, atype, material_name in assets:
        if not fname:
            continue
        path = find_asset(AssetService, fname, subdir)
        before = set(bpy.data.objects.keys())
        HumanService.add_mhclo_asset(path, basemesh, asset_type=atype, material_type="GAMEENGINE")
        for obj_name in set(bpy.data.objects.keys()) - before:
            obj = bpy.data.objects[obj_name]
            if obj.type == "MESH":
                obj.name = name + "_" + material_name.replace("neutral_", "")
                rename_materials(obj, material_name)

    # Export copy with helpers removed and modifiers baked.
    export_root = ExportService.create_character_copy(basemesh, name_suffix="_export")
    export_basemesh = ObjectService.find_object_of_type_amongst_nearest_relatives(export_root, "Basemesh")
    ExportService.bake_modifiers_remove_helpers(export_basemesh, bake_masks=True, bake_subdiv=True, remove_helpers=True, also_proxy=True)
    split_head_material(export_basemesh, head_bone="mixamorig:Head" if args.rig.startswith("mixamo") else "head",
                        neck_bone="mixamorig:Neck" if args.rig.startswith("mixamo") else "neck")
    export_root.name = name

    bpy.ops.object.select_all(action="DESELECT")
    export_root.select_set(True)
    for child in ObjectService.get_list_of_children(export_root):
        child.select_set(True)
    bpy.context.view_layer.objects.active = export_root

    fbx_path = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="COPY",
        embed_textures=False,
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        axis_forward="-Z",
        axis_up="Y",
    )
    print("[neutral-avatar] wrote", fbx_path)

    with open(os.path.join(out_dir, "GENERATED.md"), "w", encoding="utf-8") as f:
        f.write("# " + name + "\n\nGenerated by Tools/MakeHuman/generate_neutral_avatar.py with MakeHuman (MPFB2) in Blender "
                + bpy.app.version_string + ".\n\nMacro settings: " + repr(macro) + "\n\nSkin: " + args.skin + ", hair: " + args.hair
                + ", clothes: " + args.clothes + " + " + args.shoes + ", rig: " + args.rig + "\n\nMakeHuman base mesh and system assets are CC0.\n")


if __name__ == "__main__":
    try:
        main()
    except Exception:  # noqa: BLE001
        traceback.print_exc()
        sys.exit(1)
