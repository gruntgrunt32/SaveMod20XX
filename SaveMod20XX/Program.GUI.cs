using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SaveMod20XX
{
    partial class Program
    {
        public static Application WinApp { get; private set; }
        public static SaveModGUI MainWindow { get; private set; }

        /// <summary>
        /// Maps an item's internal Name to its icon file (without extension) in the /icons folder.
        /// Icons are the game's own art, converted from .dds to .png.
        /// </summary>
        internal static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>()
        {
            // --- Basic Augments (icon = game internal id or shop art) ---
            { "PowerEnhancer", "power-enhancer" },
            { "HeartContainer", "heartcontainer" },
            { "BlueLander", "bluelander" },
            { "PlumberHat", "plumberhat" },
            { "ForcemetalShell", "tank" },
            { "XCalibur", "XCalibur" },
            { "GlassCannon", "glass cannon" },
            { "BrainFoodLunch", "brain food lunch" },
            { "Zephyr", "bantamdance" },
            { "ScrapmetalScavenger", "scrapmetal-scavenger" },
            { "SevenLeafClover", "seven leaf clover" },
            { "SpilloverMatrix", "spillover matrix" },
            { "HealthNut", "health nut" },
            { "VitalityScavenger", "healthfinder 8000" },
            { "EnergyScavenger", "enerscoop alpha" },
            { "NutReplicator", "nut-replicator" },
            { "MinimechOGrinder", "fam_guardian" },
            { "Murderdrone", "fam_shooter" },
            { "SkitterySmuggler", "fam_gobbler" },
            { "ChargedNuts", "charged nut" },
            { "Gapminder", "fam_rolly" },
            { "TheRebeginner", "fam_bat" },
            { "ShockwaveSidekick", "fam_penguin" }, // description says "Penguin fires ice" - was mismatched with ReFlapp
            { "Vendsmasher", "vendsmasher" },
            { "ShinierToken", "supertoken" },
            { "ReFlapp", "fam_gorilla" }, // "Headbutts foes" fits gorilla far better than penguin
            { "TinyFlamespewer", "fam_dragon" },
            { "ArmorativePlating", "armorpl" },
            { "ThriftActuator", "thrift" },
            { "CrisisOverdrive", "overdrive" },
            { "NutsavingStringwire", "nutsaving-stringwire" },
            { "ArmorNut", "armor-nut" },
            { "RegenerativePlating", "regenpl" },
            { "EnerativePlating", "enerpl" },
            { "ArmorBloom", "armor-bloom" },
            { "Meganut", "meganut" },
            { "PureFlame", "blueflame" },
            { "ForgottenMemento", "memento" },
            { "CrisisTimestopper", "timestop" },
            { "SystemRestore", "cursebreaker" },
            { "ArmorSpreader", "armorspreader" },
            { "Choicebooster", "choicebooster" },
            { "ArmorScavenger", "armor-scavenger" },
            { "EntropyLock", "entropylock" },
            { "BracerofBattle", "bracer-of-battle" },
            { "Hypersash", "hypersash" },
            { "Megaheart", "bigheart" },
            { "TheVolunteer", "volunteer" },
            { "Thrillseeker", "thrillseeker" },
            { "QuantumSpook", "spook" },
            { "HoardersMight", "hoarder" },
            { "StrikingFeather", "feather" },
            { "BandofMight", "silver-band" },
            { "MistimedProtector", "poorlytimedshieldmodule" },
            { "WorldSlug", "slug" },
            { "LeafmetalPlating", "leafpetal" },
            { "ZookeepersSigil", "sigil" },
            { "ContractorPlus", "contractorplus" },
            { "ContractorOmega", "contractoromega" },
            { "MixmatchMastery", "mismatch" },
            { "ChargingMagnet", "magneato" },
            // Vibroreserve / ThunderousBoon: no distinct shop icon in this game version.

            // --- Newly-added augments (28) not in the original tool ---
            { "LifeExtender", "smallheart" },
            { "PotatoBattery", "potatobattery" },
            { "StrongarmBand", "strongarm-band" },
            { "ReclaimedSpark", "reclaimedspark" },
            { "NinjaSash", "ninja sash" },
            { "Vitaboost", "vitaboost" },
            { "Armorall", "armorall" },
            { "SpicyIncense", "spicyIncense" },
            { "ThornedHull", "thornedHull" },
            { "Vibrofocus", "vibrofocus" },
            { "Utilifier", "utilifier" },
            { "Intensifier", "intensifier" },
            { "Unflappable", "unflappable" },
            { "FortuneStabilizer", "fortuneStabilizer" },
            { "ForceResonator", "resoarm" },
            { "CaseResonator", "resobody" },
            { "CranialResonator", "resohead" },
            { "KineticResonator", "resoleg" },
            { "KineticConverter", "kineticconverter" },
            { "StaticClicklets", "staticClicklets" },
            { "PatchworkConnector", "patchworkConnector" },
            { "DeconstructorsMight", "decon" },
            { "Leafpetal", "leafpetal" },
            { "Boltdash", "dashbolt" },
            { "SplinteringTwinifier", "twinifier" },
            { "JuicedReserves", "empoweredReserves" },
            { "ZookeepersZeal", "zoozeal" },
            { "CrisisLifebank", "lifebank" },

            // --- Primary Weapons (weapons/ subfolder) ---
            { "WaveBeam", "weapons/wavebeam" },
            { "Scatterblast", "weapons/scatterblast" },
            { "NBuster", "weapons/n-buster" },
            { "StarBeam", "weapons/starbeam" },
            { "TheForkalator", "weapons/forkalator" },
            { "SpinningGlaive", "weapons/glaive" },
            { "SharpSharpSpear", "weapons/spear" },
            { "ASaber", "weapons/a-saber" },
            { "RipplingAxe", "weapons/axe" },
            { "PlasmaBlender", "weapons/rapier" },

            // --- Core Augments (cores/ subfolder; Oxjack=mobi, Owlhawk=power, Dracopent=atk, Armatort=tank) ---
            { "ArmatortsShell", "cores/tankbody" },
            { "OxjacksGuile", "cores/mobibody" },
            { "DracopentsPride", "cores/atkbody" },
            { "OwlhawksReign", "cores/powerbody" },
            { "ArmatortsMomentum", "cores/tanklegs" },
            { "OxjacksBlitz", "cores/mobilegs" },
            { "DracopentsBound", "cores/atklegs" },
            { "OwlhawksFeather", "cores/powerlegs" },
            { "ArmatortsDome", "cores/tankhead" },
            { "OxjacksKen", "cores/mobihead" },
            { "DracopentsFang", "cores/atkhead" },
            { "OwlhawksFocus", "cores/powerhead" },
            { "ArmatortsPound", "cores/tankarms" },
            { "OxjacksFury", "cores/mobiarms" },
            { "DracopentsClaw", "cores/atkarms" },
            { "OwlhawksTalon", "cores/powerarms" },

            // --- Prototypes (curse* icons) ---
            { "BrutishAugmentation", "cursemight" },
            { "FocusingSagelens", "cursebrilliance" },
            { "EarthmetalPlating", "cursedura" },
            { "InterestingTimes", "curseinteresting" },
            { "SanityConverter", "curseinsanity" },
            { "ViolenceEnhancer", "curseviolence" },
            { "DefiantDecree", "decree" },
            { "UnchargingForce", "curseforce" },
            { "FinalShell", "cursefinality" },
            // ("Zookeeper's Sigil" prototype shares the Name "ZookeepersSigil" -> already mapped above to "sigil")
            { "ConsumingFury", "curseburnout" },
        };

        /// <summary>
        /// Maps an item's internal Name to its in-game description (from the game's c.bin item table,
        /// which is the same text the wiki uses).
        /// </summary>
        internal static readonly Dictionary<string, string> DescMap = new Dictionary<string, string>()
        {
            { "PowerEnhancer", "Power Strength +3" },
            { "HeartContainer", "Max Health +2" },
            { "BlueLander", "Max Health +1, full heal" },
            { "PlumberHat", "Jump Height +4" },
            { "ForcemetalShell", "Max Health +2, Attack +2, Speed -2" },
            { "XCalibur", "Max Health +2, Attack +2, Max Energy -2, Power -2" },
            { "GlassCannon", "Max Health -2, Attack +2, Power +2" },
            { "BrainFoodLunch", "Max Health +2, Max Energy +4, full NRG" },
            { "Zephyr", "Attack speed up (also power fire rate)" },
            { "ScrapmetalScavenger", "Nut Find up (enemies burst extra nuts)" },
            { "SevenLeafClover", "Luck up: better nut/health/energy drops" },
            { "SpilloverMatrix", "Each excess Health becomes +2 Energy" },
            { "HealthNut", "Nuts sometimes restore 1 Health" },
            { "VitalityScavenger", "Health Find up, +2 Health" },
            { "EnergyScavenger", "Energy Find up, Power Strength +1" },
            { "NutReplicator", "Unspent nuts pay 20%/level (cap +100)" },
            { "MinimechOGrinder", "Guardian bit orbits you, deflects shots" },
            { "Murderdrone", "Blaster bit attacks foes (1.0x Attack)" },
            { "SkitterySmuggler", "Collects nuts, then bursts into 2 augs" },
            { "ChargedNuts", "Nuts sometimes restore 1 Energy" },
            { "Gapminder", "Prevents all fall damage" },
            { "TheRebeginner", "On death, revives you at half HP" },
            { "ShockwaveSidekick", "Penguin fires ice 4 ways (2.0x Power)" },
            { "Vendsmasher", "Smashed machines drop extra pickups" },
            { "ShinierToken", "Counts as 3 tokens" },
            { "ReFlapp", "Headbutts foes (1.0x Attack; 10x vs Flapps)" },
            { "TinyFlamespewer", "Fires 3 fireballs at foes (1.5x Power)" },
            { "ArmorativePlating", "+2 Armor at level start" },
            { "ThriftActuator", "1 shop item always on sale (stacks x6)" },
            { "CrisisOverdrive", "In red HP: +3 ATK, +3 PWR, +4 Speed, +4 Jump" },
            { "NutsavingStringwire", "50% chance to use machines for free" },
            { "ArmorNut", "Nuts sometimes give 1 Armor" },
            { "RegenerativePlating", "+2 HP at level start" },
            { "EnerativePlating", "Full Energy at level start" },
            { "ArmorBloom", "+6 Armor (+2 per Armor Spreader)" },
            { "Meganut", "+25 Nuts (triggers nut augs)" },
            { "PureFlame", "Power Strength +1 (and +1 each new level)" },
            { "ForgottenMemento", "Power Strength +3; nuts fuel powers at 0 NRG" },
            { "CrisisTimestopper", "Freeze all foes ~3s when knocked to red HP" },
            { "SystemRestore", "Cures acquired Prototype penalties" },
            { "ArmorSpreader", "Armor capsules give +1 Armor" },
            { "Choicebooster", "Shops contain +1 item" },
            { "Vibroreserve", "Max Energy +6" },
            { "ArmorScavenger", "Armor Find up, +1 Armor" },
            { "EntropyLock", "Slot machines never whiff" },
            { "BracerofBattle", "Attack Strength +5" },
            { "Hypersash", "Run Speed +5" },
            { "Megaheart", "Max Health +4" },
            { "TheVolunteer", "Future stages get Very Safe Labs" },
            { "Thrillseeker", "Future stages get Glory Zones" },
            { "QuantumSpook", "Attacks and powers ignore walls/shields" },
            { "HoardersMight", "Every 20 nuts held: +1 ATK / +1 PWR" },
            { "StrikingFeather", "Max Health +2, Max Energy +2, Power +2, Attack -2" },
            { "ThunderousBoon", "Power Strength +6" },
            { "BandofMight", "Attack Strength +2" },
            { "MistimedProtector", "Longer invulnerability frames" },
            { "WorldSlug", "Platforms/hazards slow down (next level)" },
            { "LeafmetalPlating", "Max Health -2, Speed +4, Jump +4" },
            { "ZookeepersSigil", "Repros deal +25% damage" },
            { "ContractorPlus", "+1 to ALL stats" },
            { "ContractorOmega", "+3 to ALL stats" },
            { "MixmatchMastery", "Mixed Core sets boost ATK/PWR/Speed/Jump" },
            { "ChargingMagnet", "Charging pulls in pickups" },
            { "LifeExtender", "Max Health +1" },
            { "PotatoBattery", "Max Energy +3" },
            { "StrongarmBand", "Attack Strength +1" },
            { "ReclaimedSpark", "Power Strength +1, Max Energy +1" },
            { "NinjaSash", "Run Speed +2" },
            { "Vitaboost", "Max Health +1, Max Energy +1, Armor +1" },
            { "Armorall", "Armor +3, Max Energy +1, Speed +1" },
            { "SpicyIncense", "Attack +1 (and +1 each new level)" },
            { "ThornedHull", "+1 Attack per 4 Health" },
            { "Vibrofocus", "Max Health +1, Power +4, Max Energy -4" },
            { "Utilifier", "Max Energy +12, Power -5" },
            { "Intensifier", "Charged attacks +20% damage per charge level" },
            { "Unflappable", "Immune to Flapp damage/knockback" },
            { "FortuneStabilizer", "Shops and slots always spawn (next level)" },
            { "ForceResonator", "Boosts equipped Arm Cores" },
            { "CaseResonator", "Boosts equipped Body Cores" },
            { "CranialResonator", "Boosts equipped Head Cores" },
            { "KineticResonator", "Boosts equipped Leg Cores" },
            { "KineticConverter", "Speed +4, Jump +4, Power -1, Attack -2" },
            { "StaticClicklets", "Cling to walls (hold Up)" },
            { "PatchworkConnector", "Core set bonuses need 1 fewer piece" },
            { "DeconstructorsMight", "Slain enemies deal 12 dmg to neighbors" },
            { "Leafpetal", "Run Speed +2, Jump +1" },
            { "Boltdash", "Dashes complete 40% quicker" },
            { "SplinteringTwinifier", "Twinifies all future Repros" },
            { "JuicedReserves", "+1 Power Strength per 4 Energy" },
            { "ZookeepersZeal", "Repros attack 25% faster" },
            { "CrisisLifebank", "Stores health, releases it at red HP" },
            { "WaveBeam", "Megaparticles pierce foes and walls! Also, wavy." },
            { "Scatterblast", "Hyperparticles devastate targets up close!" },
            { "NBuster", "Basic buster." },
            { "StarBeam", "Nina shoots in all four cardinal directions!" },
            { "TheForkalator", "Triangle shot! Forkalicious." },
            { "SpinningGlaive", "Circle slash while airborne!" },
            { "SharpSharpSpear", "Grants additional reach and damage!" },
            { "ASaber", "Basic blade." },
            { "RipplingAxe", "Swings slowly for MASSIVE damage!" },
            { "PlasmaBlender", "Shreds enemies up-close!" },
            { "ArmatortsShell", "Core (Body): Grants immunity to knockback!" },
            { "OxjacksGuile", "Core (Body): Dashing grants a shield!" },
            { "DracopentsPride", "Core (Body): Attack kills might restore health!" },
            { "OwlhawksReign", "Core (Body): Power kills might restore health!" },
            { "ArmatortsMomentum", "Core (Legs): 2 second Hover! Tap JUMP in midair to cancel." },
            { "OxjacksBlitz", "Core (Legs): 4-way dash in mid-air!" },
            { "DracopentsBound", "Core (Legs): Double jump!" },
            { "OwlhawksFeather", "Core (Legs): 1 second Fly! Tap JUMP in midair to cancel." },
            { "ArmatortsDome", "Core (Head): Health pickups might grant Armor!" },
            { "OxjacksKen", "Core (Head): Dashing fires a powerful blast!" },
            { "DracopentsFang", "Core (Head): Charging up boosts your next attack, too!" },
            { "OwlhawksFocus", "Core (Head): 50% of powers cast FREE!" },
            { "ArmatortsPound", "Core (Arms): Your charged attacks vaporize enemy shots!" },
            { "OxjacksFury", "Core (Arms): Charge time reduced!" },
            { "DracopentsClaw", "Core (Arms): Charge your attacks even chargier!" },
            { "OwlhawksTalon", "Core (Arms): Your attack kills might restore energy!" },
            { "BrutishAugmentation", "Cast your Powers aside. Embrace your primal power." },
            { "FocusingSagelens", "Unlock true wisdom. Cast aside your savage nature." },
            { "EarthmetalPlating", "Become one with the Planet. May reduce output." },
            { "InterestingTimes", "Ebb and flow with the times." },
            { "SanityConverter", "Powers consume maximum energy and deal tremendous damage." },
            { "ViolenceEnhancer", "Damage massively increased. For everyone." },
            { "DefiantDecree", "Bosses gain +50% HP and grant an extra Chest." },
            { "UnchargingForce", "Gain incredible strength. Never hold it in again." },
            { "FinalShell", "Incredible protection shields the frailest core." },
            { "ConsumingFury", "The flame that shines brightest..." },
        };

        /// <summary>
        /// Resolves the absolute path to an item's icon PNG, or null if we have no icon for it.
        /// </summary>
        internal static string ResolveIconPath(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) { return null; }
            string file;
            if (!IconMap.TryGetValue(itemName, out file)) { return null; }

            string full = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", file + ".png");
            return File.Exists(full) ? full : null;
        }

        /// <summary>
        /// Resolves an item's description text, or empty string if unknown.
        /// </summary>
        internal static string ResolveDescription(string itemName)
        {
            string desc;
            return (itemName != null && DescMap.TryGetValue(itemName, out desc)) ? desc : "";
        }

        /// <summary>
        /// Reads each lockable item's real current state straight out of the save file, so the GUI opens
        /// showing what's actually Enabled/Disabled right now instead of a meaningless "NoChange" default.
        /// Non-lockable items (permanently fixed by the game/tool) are left untouched.
        /// </summary>
        private static void SeedCurrentAvailability(byte[] saveBytes, IEnumerable<Item> items, long unlockOffset, long dataLoreOffset, long size)
        {
            long offset = unlockOffset >= 0 ? unlockOffset : dataLoreOffset;
            if (offset < 0) { return; } // neither offset applies to this section

            byte[] sectionBytes = GetOriginalData(offset, size, saveBytes);
            BigInteger sectionValue = Settings.GetBigIntFromRawBytes(sectionBytes);

            foreach (Item item in items)
            {
                if (!item.Lockable) { continue; }

                BigInteger mask = Settings.GetAsBigInt(item.HexValue);
                bool isEnabled = mask != 0 && (sectionValue & mask) == mask;
                item.Availability = isEnabled ? LockState.Unlocked : LockState.Locked;
            }
        }

        static void RunGui(Settings programSettings, string saveNameAndPathToUse)
        {
            WinApp = new Application();
            MainWindow = new SaveModGUI();

            byte[] currentSaveBytes = ReadInFile(saveNameAndPathToUse, new FileInfo(saveNameAndPathToUse));
            SeedCurrentAvailability(currentSaveBytes, programSettings.BasicAugments, programSettings.UnlockByteOffsets.BasicAugments, programSettings.DataLoreByteOffsets.BasicAugments, programSettings.DataSizes.BasicAugments);
            SeedCurrentAvailability(currentSaveBytes, programSettings.PrimaryWeapons, programSettings.UnlockByteOffsets.PrimaryWeapons, programSettings.DataLoreByteOffsets.PrimaryWeapons, programSettings.DataSizes.PrimaryWeapons);
            SeedCurrentAvailability(currentSaveBytes, programSettings.CoreAugs, programSettings.UnlockByteOffsets.CoreAugs, programSettings.DataLoreByteOffsets.CoreAugs, programSettings.DataSizes.CoreAugs);
            SeedCurrentAvailability(currentSaveBytes, programSettings.Prototypes, programSettings.UnlockByteOffsets.Protoypes, programSettings.DataLoreByteOffsets.Protoypes, programSettings.DataSizes.Protoypes);

            foreach (Item item in programSettings.BasicAugments.Concat(programSettings.CoreAugs).Concat(programSettings.PrimaryWeapons).Concat(programSettings.Prototypes))
            {
                item.ImagePath = ResolveIconPath(item.Name);
                item.Description = ResolveDescription(item.Name);
                MainWindow.AllItems.Add(item);
            }
            MainWindow.SaveNameAndPathToUse = saveNameAndPathToUse;
            MainWindow.SettingsFile = programSettings;
            WinApp.Run(MainWindow); // note: blocking call
        }
    }
}
