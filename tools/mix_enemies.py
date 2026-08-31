import os
import re
import copy
import xml.etree.ElementTree as ET

GAME_TMX = r"C:/Program Files (x86)/Steam/steamapps/common/20XX/tmx"

BIOMES = ["arctech", "flamelab", "glory", "nine", "skytemple", "spacejungle", "tutorial"]

# Conservative whitelist: real hostile/creature enemy types only (no bosses, no NPCs,
# no chests, no hazards/mechanisms, no UI/meta objects).
ENEMY_TYPES = [
    "AIRFOE", "AIRFOEWEAK", "BAT", "BEE", "BOMBHIVE", "CERBDISH", "CERBY",
    "CHARGECLONE", "CLOUDGUY", "CREEPER", "DASHCLONE", "DEATHSWARM", "DOOMFACE",
    "FATBRO", "GORILLA", "GORILLACLONE", "GROUNDFOE", "GROUNDFOETOP", "ICEBAT",
    "LOTUS", "MACEGUY", "MANNON", "PANTHER", "PENGUIN", "ROLLY", "SNAKE",
    "SPIRIT", "TALLBRO", "TURRET", "VCLONE", "WHEEL",
]

# Skip anything that's not a normal playable room chunk.
SKIP_NAME_FRAGMENTS = [
    "bosschunk", "challenge", "tutorial", "debug", "charsel", "endchunk",
    "facechunk", "bossload", "bosspreload", "dailychallenge", "weeklychallenge",
    "bossbchunk",
]


def list_tmx_files(biome):
    biome_dir = os.path.join(GAME_TMX, biome)
    if not os.path.isdir(biome_dir):
        return []
    return [os.path.join(biome_dir, f) for f in os.listdir(biome_dir) if f.lower().endswith(".tmx")]


def is_normal_chunk(path):
    name = os.path.basename(path).lower()
    return not any(frag in name for frag in SKIP_NAME_FRAGMENTS)


def find_object_groups(root):
    return root.findall("objectgroup")


def main():
    # Pass 1: build a catalog of one real sample object per enemy type, and figure out
    # which enemy types are already native to which biome.
    catalog = {}  # type -> (source_path, Element)
    native_types = {b: set() for b in BIOMES}

    for biome in BIOMES:
        for path in list_tmx_files(biome):
            try:
                tree = ET.parse(path)
            except ET.ParseError as e:
                print("SKIP (parse error):", path, e)
                continue
            root = tree.getroot()
            for og in find_object_groups(root):
                for obj in og.findall("object"):
                    t = obj.get("type")
                    if t in ENEMY_TYPES:
                        native_types[biome].add(t)
                        if t not in catalog:
                            catalog[t] = (path, copy.deepcopy(obj))

    print("Enemy types found in the wild:", sorted(catalog.keys()))
    for b in BIOMES:
        print(" native to", b, "->", sorted(native_types[b]))

    missing = [t for t in ENEMY_TYPES if t not in catalog]
    if missing:
        print("NOTE: no sample found anywhere for:", missing, "(skipping those, nothing to copy)")

    # Pass 2: for each biome, inject every non-native enemy type into every normal chunk
    # that already hosts at least one native enemy (so we know it's a real playable room).
    total_files_changed = 0
    total_objects_added = 0

    for biome in BIOMES:
        foreign_types = [t for t in ENEMY_TYPES if t in catalog and t not in native_types[biome]]
        if not foreign_types:
            continue

        for path in list_tmx_files(biome):
            if not is_normal_chunk(path):
                continue
            try:
                tree = ET.parse(path)
            except ET.ParseError:
                continue
            root = tree.getroot()
            if root.tag != "map":
                continue

            map_w = int(root.get("width", "0")) * int(root.get("tilewidth", "64"))
            map_h = int(root.get("height", "0")) * int(root.get("tileheight", "64"))

            object_groups = find_object_groups(root)
            if not object_groups:
                continue

            # anchor positions = existing native enemy objects in this chunk
            anchor_positions = []
            target_group = None
            for og in object_groups:
                for obj in og.findall("object"):
                    if obj.get("type") in native_types[biome]:
                        anchor_positions.append((float(obj.get("x", "0")), float(obj.get("y", "0"))))
                        target_group = og

            if not anchor_positions or target_group is None:
                continue  # not a populated enemy room, leave it alone

            changed = False
            for i, t in enumerate(foreign_types):
                src_path, sample = catalog[t]
                ax, ay = anchor_positions[i % len(anchor_positions)]
                offset = 64 * (1 + (i // len(anchor_positions)))
                new_x = ax + offset
                new_y = ay

                obj_w = float(sample.get("width", "64"))
                obj_h = float(sample.get("height", "64"))
                # clamp inside the map bounds so it isn't placed off the playable area
                new_x = max(0, min(new_x, max(0, map_w - obj_w)))
                new_y = max(0, min(new_y, max(0, map_h - obj_h)))

                new_obj = copy.deepcopy(sample)
                new_obj.set("x", str(new_x))
                new_obj.set("y", str(new_y))
                target_group.append(new_obj)
                changed = True
                total_objects_added += 1

            if changed:
                tree.write(path, encoding="utf-8", xml_declaration=False)
                total_files_changed += 1

    print("Files changed:", total_files_changed)
    print("Enemy objects added:", total_objects_added)


if __name__ == "__main__":
    main()
