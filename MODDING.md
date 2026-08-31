# Modding the base game (level/enemy/boss data)

This is separate from `SaveMod20XX` (the save-file editor above). This covers editing the game's own
level files directly.

## The wall: `data/manifest.omx`

20XX's level chunks live as plain [Tiled Map Editor](https://www.mapeditor.org/) `.tmx` files under
`tmx/<biome>/*.tmx` (biomes: `arctech`, `flamelab`, `glory`, `nine`, `skytemple`, `spacejungle`,
`tutorial`). Enemies, bosses, and hazards are just typed objects in each chunk's `objectgroup` layer
(e.g. `type="BAT"`, `type="PENGUINBOSS"`), genuinely data-driven, editable by hand or with Tiled.

The catch: on boot the game checksums every file listed in `data/manifest.omx`
(format: line 1 = decimal count `N`, then `N` lines of `relative/path.tmx checksum_uint32`) and
refuses to start if anything doesn't match:

```
ERROR: map data does not match manifest. exiting.
```

This fires on **any** byte-level change, not just big ones. A one-attribute edit trips it exactly
like a total rewrite. Confirmed it's a custom hash, not a known one: brute-forced 28 standard
checksum algorithms (CRC32 in every common variant, Adler32, Fletcher-32/16, FNV-1/1a 32/64, DJB2,
sdbm, Java-style `h*31+b`, Jenkins one-at-a-time, Murmur2/3, plain additive/XOR sums, each tried
against both raw content and content-with-path-prepended) against all 546 manifest entries. Zero
matches on any of them. There's no public precedent of anyone modding map data for this game either;
the existing modding community only does texture/image (`.png`/`.dds`) swaps.

## The fix: binary patch, not checksum-matching

Rather than reverse-engineer the exact custom hash, the manifest check itself was located and
disabled directly in `20XX.exe` using [radare2](https://rada.re/) (portable, no install). Once
patched, `manifest.omx` is never consulted again, so any `.tmx` file can be edited freely.

**Tool used:** portable radare2 5.9.8, extracted to `C:\Users\Nomad\tools\r2extract\radare2-5.9.8-w64\`
(zip extract only, no installer, no registry changes).

**What was found:** both manifest-error strings
(`"ERROR: map data does not match manifest. exiting."` and a second "data appears to have been
altered" variant) are referenced from one large routine (`fcn.0068d800`, about 3000 instructions,
the manifest load/parse/verify function). Both error paths are gated by the return value (in `AL`) of
a validation call to a subroutine at `0x455db0`, at two call sites, each immediately followed by a
`test`/`jne` pair with the identical byte pattern `0F 85 89 00 00 00` (a 6-byte conditional jump).

**The patch:** flip both conditional jumps to unconditional jumps, so the "checksum passed" branch is
always taken regardless of what the validator actually returns.

| Site | Virtual address | File offset | Original bytes | Patched bytes |
|---|---|---|---|---|
| 1 | `0x0068e2c0` | `0x28d6c0` | `0F 85 89 00 00 00` (`jne`) | `E9 8A 00 00 00 90` (`jmp` + `nop`) |
| 2 | `0x0068f082` | `0x28e482` | `0F 85 89 00 00 00` (`jne`) | `E9 8A 00 00 00 90` (`jmp` + `nop`) |

### Reapplying (e.g. after a Steam "verify integrity" silently restores the original exe)

```
"C:\Users\Nomad\tools\r2extract\radare2-5.9.8-w64\bin\radare2.exe" -w -q -c "wx e98a00000090 @ 0x68e2c0; wx e98a00000090 @ 0x68f082; px 6 @ 0x68e2c0; px 6 @ 0x68f082" "C:\Program Files (x86)\Steam\steamapps\common\20XX\20XX.exe"
```

The `px 6 @ ...` reads confirm both sites now show `e9 8a 00 00 00 90`. If radare2's PE virtual-address
mapping ever behaves differently in a future r2 version, use the raw file offsets instead (equivalent,
verified against the live patched exe):

```
"C:\Users\Nomad\tools\r2extract\radare2-5.9.8-w64\bin\radare2.exe" -w -q -c "s 0x28d6c0; wx e98a00000090; s 0x28e482; wx e98a00000090" "C:\Program Files (x86)\Steam\steamapps\common\20XX\20XX.exe"
```

### Backups

- Unpatched exe: `C:\Program Files (x86)\Steam\steamapps\common\20XX\20XX.exe.premodpatch`
- Full pristine install (everything, pre-any-edit): kept separately outside this repo, dated
  `20XX_original_<date>`. Check with whoever ran the mod session for the exact path if needed; the
  exe backup above is the fast path for undoing just the patch.

## `tools/mix_enemies.py`

Bulk-edits the live game's `tmx/` chunks so every biome's normal (non-boss) rooms get a copy of every
other biome's enemy types dropped in, alongside the room's existing native enemies. Each injected
enemy is a verbatim copy (including any extra `<properties>` it needs) taken from wherever that type
naturally occurs, positioned near an existing enemy in the target room, not a bare guessed object.
Bosses and ambiguous NPC-like types (`AL`, `DALLY`, `PENGPLUSH`, etc.) are left untouched on purpose.

Requires the manifest patch above to already be applied. Otherwise the game will refuse to boot after
this script runs.

```
python tools/mix_enemies.py
```

Edit the `GAME_TMX`, `ENEMY_TYPES`, and `SKIP_NAME_FRAGMENTS` constants at the top of the script to
point at a different install or change scope.
