using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SaveMod20XX
{
    partial class Program
    {
        /// <summary>
        /// For decoding program return codes
        /// </summary>
        public enum ErrorState : int
        {
            /// <summary>
            /// As per the DOS specification, a return of 0 is "OK" or "Success"
            /// </summary>
            NoError = 0,

            NoSaveFileFound = 1,
            BadProgramArgument = 2,
            WrongFileSpecified = 3,

            /// <summary>
            /// We can't tell if the GUI was error-free (without a lot more work)
            /// </summary>
            GuiExit = -1,
        }

        /// <summary>
        /// This program will open the settings file, creating one if it doesn't exist.
        /// Then it will locate the savegame, perform a backup and, finally, modify it.
        /// </summary>
        /// <param name="args">Arg[0], if it exists, should be the savegame file name and path.</param>
        /// <returns>0 for success, anything else for error</returns>
        [STAThread] // <-- Needed to run GUI elements within Windows
        static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "--export-csv")
            {
                string[] saveArgs = args.Length >= 2 ? new string[] { args[1] } : new string[0];
                string outputCsvPath = args.Length >= 3 ? args[2] : "export.csv";
                return (int)ExportCsv(saveArgs, outputCsvPath, withIcons: true);
            }

            Settings programSettings = LoadDefaultSettingsFile();

            string SaveNameAndPathToUse = "";
            ErrorState saveFoundStatus = LocateSaveGameFile(args, out SaveNameAndPathToUse);
            if (saveFoundStatus != ErrorState.NoError)
            { return (int)saveFoundStatus; } // Early exit - error

            BackupOriginalSaveFile(SaveNameAndPathToUse);

            if (programSettings.UseGraphicalUserInterface)
            {
                RunGui(programSettings, SaveNameAndPathToUse);
                return (int)ErrorState.GuiExit;
            }
            else
            {
                ErrorState modificationStatus = PerformSaveModification(SaveNameAndPathToUse, programSettings);

                Console.WriteLine("Program completion code " + modificationStatus);
                return (int)modificationStatus;
            }
        }

        /// <summary>
        /// Reads the save file's real current state for every lockable item and writes it out as a CSV,
        /// so it can be checked off against what's actually showing in-game.
        /// </summary>
        private static ErrorState ExportCsv(string[] saveArgs, string outputCsvPath, bool withIcons)
        {
            Settings settings = LoadDefaultSettingsFile();

            string saveNameAndPathToUse;
            ErrorState saveFoundStatus = LocateSaveGameFile(saveArgs, out saveNameAndPathToUse);
            if (saveFoundStatus != ErrorState.NoError)
            { return saveFoundStatus; }

            byte[] saveBytes = ReadInFile(saveNameAndPathToUse, new FileInfo(saveNameAndPathToUse));

            List<string> csvRows = new List<string>();
            csvRows.Add("Section,Name,HexValue,ToolSaysCurrently,Description,ActualInGame(Enabled/Disabled),Match(Y/N)");

            List<string> htmlRows = new List<string>();

            AppendSection(csvRows, htmlRows, saveBytes, "BasicAugments", settings.BasicAugments, settings.UnlockByteOffsets.BasicAugments, settings.DataLoreByteOffsets.BasicAugments, settings.DataSizes.BasicAugments);
            AppendSection(csvRows, htmlRows, saveBytes, "PrimaryWeapons", settings.PrimaryWeapons, settings.UnlockByteOffsets.PrimaryWeapons, settings.DataLoreByteOffsets.PrimaryWeapons, settings.DataSizes.PrimaryWeapons);
            AppendSection(csvRows, htmlRows, saveBytes, "CoreAugs", settings.CoreAugs, settings.UnlockByteOffsets.CoreAugs, settings.DataLoreByteOffsets.CoreAugs, settings.DataSizes.CoreAugs);
            AppendSection(csvRows, htmlRows, saveBytes, "Prototypes", settings.Prototypes, settings.UnlockByteOffsets.Protoypes, settings.DataLoreByteOffsets.Protoypes, settings.DataSizes.Protoypes);

            File.WriteAllLines(outputCsvPath, csvRows);
            Console.WriteLine("Exported " + (csvRows.Count - 1) + " rows to " + outputCsvPath);

            if (withIcons)
            {
                string htmlPath = Path.ChangeExtension(outputCsvPath, ".html");
                WriteHtmlReport(htmlPath, htmlRows);
                Console.WriteLine("Wrote visual reference to " + htmlPath);
            }

            return ErrorState.NoError;
        }

        private static void AppendSection(List<string> csvRows, List<string> htmlRows, byte[] saveBytes, string sectionName, IList<Item> items, long unlockOffset, long dataLoreOffset, long size)
        {
            long offset = unlockOffset >= 0 ? unlockOffset : dataLoreOffset;
            if (offset < 0) { return; }

            byte[] sectionBytes = GetOriginalData(offset, size, saveBytes);
            BigInteger sectionValue = Settings.GetBigIntFromRawBytes(sectionBytes);

            foreach (Item item in items)
            {
                if (!item.Lockable || string.IsNullOrEmpty(item.Name)) { continue; }

                BigInteger mask = Settings.GetAsBigInt(item.HexValue);
                bool isEnabled = mask != 0 && (sectionValue & mask) == mask;
                string state = isEnabled ? "Enabled" : "Disabled";
                string desc = ResolveDescription(item.Name);

                csvRows.Add(sectionName + "," + item.Name + "," + item.HexValue + "," + state + ",\"" + desc.Replace(",", ";").Replace("\"", "'") + "\",,");

                string iconDataUri = ResolveIconDataUri(item.Name);
                string stateClass = isEnabled ? "enabled" : "disabled";
                string rowKey = sectionName + ":" + item.Name;
                htmlRows.Add(
                    "<tr class=\"" + stateClass + "\" data-key=\"" + rowKey + "\">" +
                    "<td>" + (iconDataUri != null ? "<img src=\"" + iconDataUri + "\" />" : "<div class=\"noicon\">?</div>") + "</td>" +
                    "<td>" + sectionName + "</td>" +
                    "<td class=\"name\">" + item.Name + "</td>" +
                    "<td>" + desc + "</td>" +
                    "<td class=\"state\">" + state + "</td>" +
                    "<td class=\"verdictcell\"><button type=\"button\" class=\"verdict-btn\" onclick=\"cycleVerdict(this)\">Unmarked</button></td>" +
                    "<td class=\"notescell\"><input type=\"text\" class=\"notes\" placeholder=\"note if different...\" oninput=\"saveNote(this)\" /></td>" +
                    "</tr>");
            }
        }

        private static string ResolveIconDataUri(string itemName)
        {
            string path = ResolveIconPath(itemName);
            if (path == null) { return null; }
            byte[] bytes = File.ReadAllBytes(path);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        private static void WriteHtmlReport(string htmlPath, List<string> htmlRows)
        {
            string html =
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>20XX Save State</title><style>" +
                "body{font-family:Segoe UI,Arial,sans-serif;background:#1b1b22;color:#eee;padding:16px;}" +
                "table{border-collapse:collapse;width:100%;}" +
                "th,td{padding:6px 10px;border-bottom:1px solid #333;text-align:left;vertical-align:middle;}" +
                "th{position:sticky;top:0;background:#25252f;}" +
                "img{width:40px;height:40px;object-fit:contain;background:#000;border-radius:4px;}" +
                ".noicon{width:40px;height:40px;display:flex;align-items:center;justify-content:center;background:#000;border-radius:4px;color:#666;}" +
                ".name{font-weight:bold;}" +
                ".state{font-weight:bold;}" +
                "tr.enabled .state{color:#4caf50;}" +
                "tr.disabled .state{color:#e53935;}" +
                ".verdictcell{min-width:110px;}" +
                ".notescell{min-width:180px;}" +
                ".notes{width:100%;box-sizing:border-box;background:#111;color:#eee;border:1px solid #444;border-radius:4px;padding:4px 6px;}" +
                ".verdict-btn{width:100px;padding:5px 8px;border:none;border-radius:4px;cursor:pointer;font-weight:bold;color:#fff;background:#444;}" +
                ".verdict-btn.match{background:#2e7d32;}" +
                ".verdict-btn.mismatch{background:#c62828;}" +
                "#toolbar{position:sticky;top:0;z-index:2;background:#1b1b22;padding:8px 0;display:flex;gap:16px;align-items:center;border-bottom:2px solid #444;margin-bottom:8px;}" +
                "#toolbar button{padding:8px 14px;border:none;border-radius:4px;background:#3a3a4a;color:#fdda93;cursor:pointer;font-weight:bold;}" +
                "#summary{font-weight:bold;}" +
                "</style></head><body>" +
                "<div id=\"toolbar\"><h2 style=\"margin:0;\">20XX Save State</h2>" +
                "<span id=\"summary\"></span>" +
                "<button onclick=\"exportResults()\">Download marked results (.csv)</button>" +
                "<button onclick=\"if(confirm('Clear all marks/notes?')) clearAll();\">Clear all marks</button>" +
                "</div>" +
                "<p>Click a row's button to mark it Match / Mismatch as you check it in-game. Use the notes box for what's actually there. Everything autosaves in this browser.</p>" +
                "<table><thead><tr><th>Icon</th><th>Section</th><th>Name</th><th>Description</th><th>Tool Says</th><th>Verdict</th><th>Notes (if different)</th></tr></thead><tbody>" +
                string.Join("", htmlRows) +
                "</tbody></table>" +
                "<script>" +
                "var STORAGE_KEY='20xx_savestate_marks';" +
                "function loadState(){try{return JSON.parse(localStorage.getItem(STORAGE_KEY))||{};}catch(e){return {};}}" +
                "function persistState(s){localStorage.setItem(STORAGE_KEY, JSON.stringify(s));}" +
                "function cycleVerdict(btn){" +
                "  var tr=btn.closest('tr'); var key=tr.getAttribute('data-key');" +
                "  var s=loadState(); var cur=(s[key]&&s[key].verdict)||'';" +
                "  var next = cur==='' ? 'match' : (cur==='match' ? 'mismatch' : '');" +
                "  s[key]=s[key]||{}; s[key].verdict=next; persistState(s);" +
                "  applyVerdict(btn, next); updateSummary();" +
                "}" +
                "function applyVerdict(btn, verdict){" +
                "  btn.classList.remove('match','mismatch');" +
                "  if(verdict==='match'){btn.classList.add('match'); btn.textContent='Match';}" +
                "  else if(verdict==='mismatch'){btn.classList.add('mismatch'); btn.textContent='Mismatch';}" +
                "  else {btn.textContent='Unmarked';}" +
                "}" +
                "function saveNote(input){" +
                "  var tr=input.closest('tr'); var key=tr.getAttribute('data-key');" +
                "  var s=loadState(); s[key]=s[key]||{}; s[key].note=input.value; persistState(s);" +
                "}" +
                "function updateSummary(){" +
                "  var s=loadState(); var total=document.querySelectorAll('tbody tr').length;" +
                "  var m=0, mm=0;" +
                "  Object.keys(s).forEach(function(k){ if(s[k].verdict==='match') m++; else if(s[k].verdict==='mismatch') mm++; });" +
                "  document.getElementById('summary').textContent = total + ' items - ' + m + ' match, ' + mm + ' mismatch, ' + (total-m-mm) + ' unmarked';" +
                "}" +
                "function clearAll(){ localStorage.removeItem(STORAGE_KEY); location.reload(); }" +
                "function csvEscape(v){ v=(v==null?'':String(v)); return '\"' + v.replace(/\"/g,'\"\"') + '\"'; }" +
                "function exportResults(){" +
                "  var s=loadState(); var rows=[['Section','Name','ToolSaysCurrently','Verdict','Notes']];" +
                "  document.querySelectorAll('tbody tr').forEach(function(tr){" +
                "    var key=tr.getAttribute('data-key'); var parts=key.split(':');" +
                "    var toolState=tr.querySelector('.state').textContent;" +
                "    var v=(s[key]&&s[key].verdict)||''; var note=(s[key]&&s[key].note)||'';" +
                "    rows.push([parts[0], parts[1], toolState, v, note]);" +
                "  });" +
                "  var csv=rows.map(function(r){return r.map(csvEscape).join(',');}).join('\\r\\n');" +
                "  var blob=new Blob([csv], {type:'text/csv'});" +
                "  var a=document.createElement('a'); a.href=URL.createObjectURL(blob); a.download='20XX_marked_results.csv';" +
                "  document.body.appendChild(a); a.click(); document.body.removeChild(a);" +
                "}" +
                "(function init(){" +
                "  var s=loadState();" +
                "  document.querySelectorAll('tbody tr').forEach(function(tr){" +
                "    var key=tr.getAttribute('data-key'); var entry=s[key];" +
                "    if(entry){" +
                "      applyVerdict(tr.querySelector('.verdict-btn'), entry.verdict||'');" +
                "      if(entry.note) tr.querySelector('.notes').value=entry.note;" +
                "    }" +
                "  });" +
                "  updateSummary();" +
                "})();" +
                "</script>" +
                "</body></html>";
            File.WriteAllText(htmlPath, html);
        }

        /// <summary>
        /// Loads the default settings file, creating one if it cannot be found
        /// </summary>
        /// <returns>The settings file</returns>
        private static Settings LoadDefaultSettingsFile()
        {
            Settings programSettings = null;
            do
            {
                if (File.Exists(SettingsFilePath) == false)
                {
                    Settings defaultSettings = new Settings();
                    defaultSettings.SaveToFile(SettingsFilePath);
                    defaultSettings = null;
                }
                programSettings = Settings.LoadFromFile(SettingsFilePath);

                Version oldVersion = new Version(programSettings.CreatedWithProgramVersion);
                Version currentVersion = Assembly.GetCallingAssembly().GetName().Version;
                if (oldVersion.Major != currentVersion.Major || oldVersion.Minor != currentVersion.Minor )
                {
                    programSettings = null;
                    File.Move(SettingsFilePath, SettingsFilePath + BackupAppend);
                    Console.WriteLine("Your settings file was out of date.\n  I have renamed it \"" + SettingsFilePath + BackupAppend + "' if you care to recover data from it.\n    It will be erased the next time you have an out of date settings file.");
                }
            } while (programSettings == null);
            return programSettings;
        }

        /// <summary>
        /// We figure out which save file to use
        /// </summary>
        /// <returns>The error state. 0 is All-OK.</returns>
        private static ErrorState LocateSaveGameFile(string[] args, out string SaveNameAndPathToUse)
        {
            ErrorState errorState;
            SaveNameAndPathToUse = "";
            

            // If they specified a file, use that one
            if (args.Length >= 1)
            {
                if (File.Exists(args[0]) && args[0].ToLower().EndsWith(SaveGameExtension))
                {
                    SaveNameAndPathToUse = args[0];
                    errorState = ErrorState.NoError;
                }
                else if (args[0].ToLower().EndsWith(".xml"))
                {
                    Console.Error.WriteLine("Error: You seem to have accidentally provided an XML file as ");
                    Console.Error.WriteLine("    an argument to this program: stahpit. ");
                    Console.Error.WriteLine("  Try providing a '" + SaveGameExtension + "' file, instead.");
                    errorState = ErrorState.WrongFileSpecified;
                }
                else
                {
                    Console.Error.WriteLine("Error: Unexpected first argument. Unable to proceed.");
                    errorState = ErrorState.BadProgramArgument;
                }
            } // If they didn't, check the MyDocuments default directory
            else if (Directory.Exists(SaveGamePath) && File.Exists(SaveGamePath + "\\" + SaveFileName))
            {
                SaveNameAndPathToUse = SaveGamePath + "\\" + SaveFileName;
                errorState = ErrorState.NoError;
            } // If it's none of those things, error. We cannot continue.
            else
            {
                Console.Error.WriteLine("Error: Unable to find default Save File location.");
                Console.Error.WriteLine("  Please enter a full file name and path for the 20XX save file as argument[0]");
                Console.Error.WriteLine("  You may drag-and-drop the desired save file onto this .exe for simplicity.");
                errorState = ErrorState.NoSaveFileFound;
            }

            return errorState;
        }

        /// <summary>
        /// Exactly what it says on the tin.
        /// This new version appends an incrementing counter to always save your old file rather than overwrite it
        /// </summary>
        private static void BackupOriginalSaveFile(string inputFileNameAndPath)
        {
            Console.WriteLine("Backing up previous save file...");

            // Find a unique save backup name -- no more overwriting old save files!
            int appendCounter = 0;
            while (File.Exists(inputFileNameAndPath + BackupAppend + appendCounter))
            { ++appendCounter; }

            File.Copy(inputFileNameAndPath, inputFileNameAndPath + BackupAppend + appendCounter, false);
            Console.WriteLine("... backup complete.");
        }

        /// <summary>
        /// This does the actual modification of the save file according to your XML settings file
        /// </summary>
        /// <param name="inputFileNameAndPath">Where's our save file?</param>
        /// <param name="settings">The loaded XML settings file</param>
        /// <returns>Windows completion code--0 is success, anything else is an error of some kind</returns>
        internal static ErrorState PerformSaveModification(string inputFileNameAndPath, Settings settings)
        {
            FileInfo fi = new FileInfo(inputFileNameAndPath);
            byte[] fileBytes = new byte[fi.Length];
            fileBytes = ReadInFile(inputFileNameAndPath, fi);

            PerformUnlocks(fileBytes, settings);
            PerformDataLore(fileBytes, settings);

            WriteOutFile(inputFileNameAndPath, fileBytes);

            // Everything OK!
            return ErrorState.NoError;
        }


        /// <summary>
        /// This does the "Datalore" section unlocks
        /// </summary>
        /// <param name="fileBytes">The contents of the save file</param>
        /// <param name="settings">The loaded XML settings file</param>
        private static void PerformDataLore(byte[] fileBytes, Settings settings)
        {
            List<Tuple<long, byte[]>> DataLores = new List<Tuple<long, byte[]>>();

            DataLores.Add(new Tuple<long, byte[]>(settings.DataLoreByteOffsets.BasicAugments,
                                                  CongealItems(settings.BasicAugments, GetOriginalData(settings.DataLoreByteOffsets.BasicAugments, settings.DataSizes.BasicAugments, fileBytes))));
            DataLores.Add(new Tuple<long, byte[]>(settings.DataLoreByteOffsets.PrimaryWeapons,
                                                  CongealItems(settings.PrimaryWeapons, GetOriginalData(settings.DataLoreByteOffsets.PrimaryWeapons, settings.DataSizes.PrimaryWeapons, fileBytes))));
            DataLores.Add(new Tuple<long, byte[]>(settings.DataLoreByteOffsets.CoreAugs,
                                                  CongealItems(settings.CoreAugs, GetOriginalData(settings.DataLoreByteOffsets.CoreAugs, settings.DataSizes.CoreAugs, fileBytes))));
            DataLores.Add(new Tuple<long, byte[]>(settings.DataLoreByteOffsets.Protoypes,
                                                  CongealItems(settings.Prototypes, GetOriginalData(settings.DataLoreByteOffsets.Protoypes, settings.DataSizes.Protoypes, fileBytes))));

            DoAllModifications(fileBytes, DataLores);
        }

        /// <summary>
        /// Performs the unlocks for the "Unlocks" section
        /// </summary>
        /// <param name="fileBytes">The contents of the save file</param>
        /// <param name="settings">The loaded XML settings file</param>
        private static void PerformUnlocks(byte[] fileBytes, Settings settings)
        {
            List<Tuple<long, byte[]>> Unlocks = new List<Tuple<long, byte[]>>();

            Unlocks.Add(new Tuple<long, byte[]>(settings.UnlockByteOffsets.BasicAugments,
                                                CongealItems(settings.BasicAugments, GetOriginalData(settings.UnlockByteOffsets.BasicAugments, settings.DataSizes.BasicAugments, fileBytes))));
            Unlocks.Add(new Tuple<long, byte[]>(settings.UnlockByteOffsets.PrimaryWeapons,
                                                CongealItems(settings.PrimaryWeapons, GetOriginalData(settings.UnlockByteOffsets.PrimaryWeapons, settings.DataSizes.PrimaryWeapons, fileBytes))));

            DoAllModifications(fileBytes, Unlocks);
        }

        /// <summary>
        /// Performs the actual modifications to the raw file data
        /// </summary>
        /// <param name="rawData">The contents of the save file</param>
        /// <param name="mods">The prepared set of modifications: the ulong is their starting file location, the byte[] is the modified data to place at that location</param>
        public static void DoAllModifications(byte[] rawData, List<Tuple<long, byte[]>> mods)
        {
            foreach (var mod in mods)
            {
                DoModification(mod.Item1, rawData, mod.Item2);
            }
        }

        /// <summary>
        /// Performs each specific modification to the raw file data
        /// </summary>
        /// <param name="offset">Where in the file is this modification</param>
        /// <param name="fileBytes">The contents of the save file</param>
        /// <param name="changed">What has changed at this file location?</param>
        public static void DoModification(long offset, byte[] data, byte[] changed)
        {
            Array.Copy(changed, (long)0, data, offset, (long)changed.Length);
        }

        /// <summary>
        /// Gets the data sub-array at the specified offset
        /// </summary>
        /// <param name="offset">Where in the file is this modification</param>
        /// <param name="size">How much to copy out</param>
        /// <param name="data">The contents of the save file</param>
        public static byte[] GetOriginalData(long offset, long size, byte[] data)
        {
            byte[] subData = new byte[size];
            Array.Copy(data, (long)offset, subData, (long)0, (long)subData.Length);
            return subData;
        }

        /// <summary>
        /// Congeals all item bit-flags for a specific section of the output data
        /// </summary>
        /// <param name="itemsToCongeal">Here's the bit-flags and toggle-state of the items for this section</param>
        /// <param name="originalData">The original data bytes</param>
        /// <returns>The combined bit-flags for this file section</returns>
        private static byte[] CongealItems(IList<Item> itemsToCongeal, byte[] originalData)
        {
            BigInteger allItems = Settings.GetBigIntFromRawBytes(originalData);
            foreach (Item item in itemsToCongeal)
            {
                if (item.Availability == LockState.Unlocked)
                {
                    allItems |= Settings.GetAsBigInt(item.HexValue);
                }
                else if (item.Availability == LockState.Locked)
                {
                    allItems &= ~Settings.GetAsBigInt(item.HexValue);
                }
                else
                {
                    ; // do nothing, leave it as-is
                }
            }

            // Force the result back to exactly the section width (big-endian, left-padded with zeros).
            // The original code returned BigInteger.ToByteArray() directly, whose length varies with the
            // value (dropping leading zero bytes / adding a sign byte). With the widened 13-byte augment
            // field that could misalign the write, so we normalize to originalData.Length here.
            byte[] raw = Settings.GetRawBytesFromBigInt(allItems);
            if (raw.Length == originalData.Length) { return raw; }

            byte[] fixedWidth = new byte[originalData.Length];
            if (raw.Length < originalData.Length)
            {
                Array.Copy(raw, 0, fixedWidth, originalData.Length - raw.Length, raw.Length);
            }
            else
            {
                // drop any extra high-order bytes (e.g. a two's-complement sign byte)
                Array.Copy(raw, raw.Length - originalData.Length, fixedWidth, 0, originalData.Length);
            }
            return fixedWidth;
        }

        /// <summary>
        /// Simply writes the file. Nothing to see here.
        /// </summary>
        private static void WriteOutFile(string outputFileNameAndPath, byte[] fileBytes)
        {
            using (FileStream fs = new FileStream(outputFileNameAndPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write(fileBytes);
            }
        }

        /// <summary>
        /// Simply reads the file, nothing to see here.
        /// </summary>
        /// <returns>The file contents</returns>
        private static byte[] ReadInFile(string inputFileNameAndPath, FileInfo fi)
        {
            byte[] fileBytes;
            using (FileStream fs = new FileStream(inputFileNameAndPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                fileBytes = br.ReadBytes((int)fi.Length);
            }

            return fileBytes;
        }
    }
}
