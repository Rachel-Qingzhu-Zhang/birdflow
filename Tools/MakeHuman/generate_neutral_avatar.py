"""
Generate the gender-neutral human avatar for Avian_Lab_Embodiment with MakeHuman (MPFB2 in Blender).

Why MakeHuman: it is the free, CC0 human generator used in many CHI / IEEE VR / UIST embodiment studies, and
its macro "gender" slider lets us build an androgynous body (gender = 0.5) instead of picking a male or a
female model. The Rocketbox library (used for the male / female bodies) has no androgynous avatars; the
VALID library does not have them yet either.

Default style is a **mannequin**: the androgynous MakeHuman body with no face texture, no hair, no clothes and
one plain matte colour, like the faceless mannequin avatars of VR embodiment studies (e.g. Lugrin et al. 2015,
IEEE VR, "Anthropomorphism and illusion of virtual body ownership"). The eyeballs stay as plain geometry so the
sockets are not hollow. `--style dressed` builds the earlier variant instead (skin texture, short hair, plain
T-shirt and jeans, following the androgynous-agent guidelines of Nag & Yalcin 2020).

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
    parser.add_argument("--flat-chest", type=float, default=1.0,
                        help="1 = refill the chest flat (no breast mounds), 0 = MakeHuman's chest as is")
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
    parser.add_argument("--style", choices=["mannequin", "dressed"], default="mannequin",
                        help="mannequin = faceless plain body (default); dressed = skin texture, hair, T-shirt and jeans")
    parser.add_argument("--mannequin-color", default="0.74,0.72,0.70", help="Mannequin base colour, linear RGB 0-1")
    parser.add_argument("--mannequin-roughness", type=float, default=0.62)
    parser.add_argument("--no-teeth", action="store_true")
    parser.add_argument("--keep-logo", action="store_true", help="Keep the MakeHuman print on the T-shirt texture")
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


def plain_material(name, rgb, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = 0.0
    mat.diffuse_color = (rgb[0], rgb[1], rgb[2], 1.0)
    mat.roughness = roughness
    return mat


def assign_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


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


def flatten_chest(basemesh, strength=1.0, arm_groups=("mixamorig:LeftArm", "mixamorig:RightArm", "upperarm01.L", "upperarm01.R")):
    """Remove the breast mounds: refill the chest with a smooth surface that continues the body around it.

    Even at gender 0.5 with the smallest cup size, MakeHuman's female half leaves small breasts (the macro breast target
    only reaches the "mincup" shape, and pushing it further distorts the chest). Here the chest is seen as a radius
    around the torso's vertical axis, r(angle, height), from the front round to the sides below the armpits. The radius
    of the chest vertices is replaced by a thin-plate spline fitted to three rings of untouched vertices around that
    region (belly below, upper chest above, torso sides) and to the breastbone line, so the fill continues the surrounding surface and its slope:
    the ribcage, the breastbone line, the upper chest, the belly and the overall body shape stay, only the mounds
    (and nipples) go.
    Works on the baked mesh (no shape keys) in the rest pose; MakeHuman faces -Y in Blender.
    Returns (vertex count changed, max change in m).
    """
    import math

    import numpy as np

    mesh = basemesh.data
    n = len(mesh.vertices)
    co = np.empty(n * 3, np.float64)
    mesh.vertices.foreach_get("co", co)
    co = co.reshape(n, 3)
    nor = np.empty(n * 3, np.float32)
    mesh.vertices.foreach_get("normal", nor)
    nor = nor.reshape(n, 3)
    edges = np.empty(len(mesh.edges) * 2, np.int64)
    mesh.edges.foreach_get("vertices", edges)
    edges = edges.reshape(-1, 2)

    floor = co[:, 2].min()
    height = co[:, 2].max() - floor
    scale = height / 1.635  # the region sizes below were set on the default 1.635 m body

    arm = np.zeros(n, bool)
    group_index = {g.name: g.index for g in basemesh.vertex_groups}
    arm_indices = {group_index[name] for name in arm_groups if name in group_index}
    if arm_indices:
        for v in mesh.vertices:
            for g in v.groups:
                if g.group in arm_indices and g.weight > 0.5:
                    arm[v.index] = True
                    break

    x, y, z = co[:, 0], co[:, 1], co[:, 2] - floor

    # Breast apex: the most forward front-facing vertex beside the midline in the chest band (above the belly).
    band = (nor[:, 1] < -0.2) & ~arm & (np.abs(x) > 0.025 * scale) & (np.abs(x) < 0.15 * scale)         & (z > 0.66 * height) & (z < 0.80 * height)
    if not band.any():
        print("[neutral-avatar] chest not found; chest left as is")
        return 0, 0.0
    apex = np.flatnonzero(band)[np.argmin(y[band])]
    ax, az = abs(x[apex]), z[apex]
    z_lo, z_hi = az - 0.12 * scale, az + 0.13 * scale

    # Torso axis: halfway between the front and the back of the trunk near the midline in that band.
    trunk = ~arm & (np.abs(x) < 0.05 * scale) & (z > z_lo) & (z < z_hi)
    yc = 0.5 * (y[trunk].min() + y[trunk].max())
    theta = np.arctan2(x, yc - y)  # 0 = straight ahead, +-90 deg = the sides
    radius = np.hypot(x, y - yc)

    # The chest from the inframammary fold to below the collarbones, round to the sides below the armpits. The
    # breastbone line stays as it is and anchors the fill too: without it the spline domes the middle of the chest
    # forward (it continues the belly's forward slope below and the upper chest's backward slope above).
    sternum = np.abs(x) < 0.012 * scale
    region = ~arm & ~sternum & (np.abs(theta) < math.radians(72)) & (z > z_lo) & (z < z_hi)
    if not region.any():
        return 0, 0.0

    neighbours = [[] for _ in range(n)]
    for a, b in edges:
        neighbours[a].append(b)
        neighbours[b].append(a)
    inner = set(np.flatnonzero(region).tolist())
    anchors, frontier, seen = [], inner, set(inner)
    for _ in range(3):
        frontier = {j for i in frontier for j in neighbours[i] if j not in seen}
        seen |= frontier
        anchors += [j for j in frontier if not arm[j] and abs(theta[j]) < math.radians(110)]
    anchors += np.flatnonzero(sternum & ~arm & (z > z_lo) & (z < z_hi) & (theta.__abs__() < 0.5)).tolist()
    unknown = np.array(sorted(inner))
    anchors = np.array(sorted(set(anchors)))

    # Thin-plate spline r(u, z) with u = arc length round the axis, through the anchor rings.
    r0 = float(np.median(radius[unknown]))
    def param(ids):
        return np.stack([theta[ids] * r0, z[ids]], axis=1)
    def kernel(a, b):
        d = np.linalg.norm(a[:, None, :] - b[None, :, :], axis=2)
        with np.errstate(divide="ignore", invalid="ignore"):
            k = d * d * np.log(d)
        return np.nan_to_num(k)
    pa = param(anchors)
    m = len(anchors)
    system = np.zeros((m + 3, m + 3))
    system[:m, :m] = kernel(pa, pa) + 1e-7 * np.eye(m)
    system[:m, m] = 1.0
    system[:m, m + 1:] = pa
    system[m, :m] = 1.0
    system[m + 1:, :m] = pa.T
    rhs = np.concatenate([radius[anchors], np.zeros(3)])
    coef = np.linalg.solve(system, rhs)
    pu = param(unknown)
    fitted = kernel(pu, pa) @ coef[:m] + coef[m] + pu @ coef[m + 1:]

    new_radius = radius[unknown] + strength * (fitted - radius[unknown])
    change = new_radius - radius[unknown]
    co[unknown, 0] = new_radius * np.sin(theta[unknown])
    co[unknown, 1] = yc - new_radius * np.cos(theta[unknown])
    mesh.vertices.foreach_set("co", co.reshape(-1).astype(np.float32))
    mesh.update()
    print("[neutral-avatar] chest flattened: apex (x %.3f, z %.3f), %d vertices, %d anchors, %.1f mm in / %.1f mm out"
          % (ax, az + floor, len(unknown), m, -1000 * change.min(), 1000 * change.max()))
    return len(unknown), float(np.abs(change).max())


def remove_print_from_shirt(png_path, top_fraction=0.45, saturation=0.12, block=32, reach=3):
    """Paint over the MakeHuman print (orange figure, grey ring and lettering) on the T-shirt part of the
    clothes texture. The shirt occupies the top part of the male_casualsuit06 diffuse and is otherwise plain,
    so the saturated (orange) texels locate the print; everything around them that is darker or tinted
    compared to the shirt is repainted with the shirt colour. A plain shirt matters for the study: no brand
    or text on the neutral avatar.
    """
    import numpy as np

    image = bpy.data.images.load(png_path)
    w, h = image.size
    px = np.empty(w * h * 4, dtype=np.float32)
    image.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    rows_n = (int(h * top_fraction) // block) * block
    rows = slice(h - rows_n, h)  # Blender stores rows bottom-up: the top of the texture is the end
    region = px[rows, :, :3]
    sat = region.max(axis=2) - region.min(axis=2)
    seeds = sat > saturation
    if not seeds.any():
        print("[neutral-avatar] no print found in", png_path)
        bpy.data.images.remove(image)
        return

    # Blocks around the saturated seeds, grown by `reach` blocks, bound the whole print (ring + lettering).
    blocks = seeds.reshape(rows_n // block, block, w // block, block).any(axis=(1, 3))
    for _ in range(reach):
        grown = blocks.copy()
        grown[1:, :] |= blocks[:-1, :]
        grown[:-1, :] |= blocks[1:, :]
        grown[:, 1:] |= blocks[:, :-1]
        grown[:, :-1] |= blocks[:, 1:]
        blocks = grown
    area = np.repeat(np.repeat(blocks, block, axis=0), block, axis=1)

    lum = region.mean(axis=2)
    shirt = area & ~seeds & (sat < 0.04)
    fill = np.median(region[shirt], axis=0)
    fill_lum = float(fill.mean())
    mask = area & ((sat > 0.05) | (np.abs(lum - fill_lum) > 0.07))
    for _ in range(2):
        grown = mask.copy()
        grown[1:, :] |= mask[:-1, :]
        grown[:-1, :] |= mask[1:, :]
        grown[:, 1:] |= mask[:, :-1]
        grown[:, :-1] |= mask[:, 1:]
        mask = grown
    region[mask] = fill
    px[rows, :, :3] = region
    image.pixels.foreach_set(px.reshape(-1))
    image.filepath_raw = png_path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)
    print("[neutral-avatar] painted over", int(mask.sum()), "texels of print in", os.path.basename(png_path))


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
    TargetService = dynamic_import("mpfb.services.targetservice", "TargetService")

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

    mannequin = args.style == "mannequin"
    mannequin_rgb = [float(v) for v in args.mannequin_color.split(",")]
    if mannequin:
        assign_material(basemesh, plain_material("neutral_body", mannequin_rgb, args.mannequin_roughness))
    else:
        HumanService.set_character_skin(find_asset(AssetService, args.skin, "skins"), basemesh, skin_type="GAMEENGINE")
        rename_materials(basemesh, "neutral_body")

    HumanService.add_builtin_rig(basemesh, args.rig)

    if mannequin:
        # Eyeballs only (same plain material, named with "head" so the first-person head hider hides them).
        assets = [("eyes", args.eyes, "Eyes", "neutral_head_eyes")]
    else:
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
                if mannequin:
                    assign_material(obj, plain_material(material_name, mannequin_rgb, args.mannequin_roughness))
                else:
                    rename_materials(obj, material_name)

    # Export copy with helpers removed and modifiers baked.
    export_root = ExportService.create_character_copy(basemesh, name_suffix="_export")
    export_basemesh = ObjectService.find_object_of_type_amongst_nearest_relatives(export_root, "Basemesh")
    ExportService.bake_modifiers_remove_helpers(export_basemesh, bake_masks=True, bake_subdiv=True, remove_helpers=True, also_proxy=True)
    # Bake the body-shape targets (gender 0.5, cup size, ethnicity mix ...) into the mesh. Left as shape keys, the FBX
    # carries them as blend shapes over the unshaped MakeHuman base mesh; Unity does not import those, so it would get
    # the base shape while the rig was fitted to the shaped body: joints up to 6 cm off the hands (fingers collapse
    # as soon as they bend) and the cup-size target missing (the chest shows).
    if export_basemesh.data.shape_keys:
        TargetService.bake_targets(export_basemesh)
        print("[neutral-avatar] body-shape targets baked into the mesh")
    if args.flat_chest > 0:
        flatten_chest(export_basemesh, strength=args.flat_chest)
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

    if not args.keep_logo and not mannequin:
        clothes_png = os.path.join(out_dir, name + ".fbm", os.path.splitext(args.clothes)[0] + "_diffuse.png")
        if os.path.isfile(clothes_png):
            remove_print_from_shirt(clothes_png)

    with open(os.path.join(out_dir, "GENERATED.md"), "w", encoding="utf-8") as f:
        look = ("Style: mannequin (no face texture, no hair, no clothes; plain colour " + args.mannequin_color
                + ", roughness " + repr(args.mannequin_roughness) + "; eyeballs " + args.eyes + ")") if mannequin else (
                "Style: dressed. Skin: " + args.skin + ", hair: " + args.hair + ", clothes: " + args.clothes + " + " + args.shoes)
        f.write("# " + name + "\n\nGenerated by Tools/MakeHuman/generate_neutral_avatar.py with MakeHuman (MPFB2) in Blender "
                + bpy.app.version_string + ".\n\nMacro settings: " + repr(macro) + "\n\n"
                + ("Chest: breast mounds removed (flatten_chest, strength " + repr(args.flat_chest) + ")\n\n" if args.flat_chest > 0 else "")
                + look + ", rig: " + args.rig
                + "\n\nMakeHuman base mesh and system assets are CC0.\n")


if __name__ == "__main__":
    try:
        main()
    except Exception:  # noqa: BLE001
        traceback.print_exc()
        sys.exit(1)
