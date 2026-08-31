using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static SaveMod20XX.Program;

namespace SaveMod20XX
{
    /// <summary>
    /// Interaction logic for SaveModGUI.xaml
    /// </summary>
    public partial class SaveModGUI : Window
    {
        public ObservableCollection<Item> AllItems { get; set; } = new ObservableCollection<Item>();
        internal Settings SettingsFile { get; set; }
        internal string SaveNameAndPathToUse { get; set; }

        public SaveModGUI()
        {
            InitializeComponent();

            Loaded += SaveModGUI_Loaded;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void SaveModGUI_Loaded(object sender, RoutedEventArgs e)
        {
            // Columns are now defined explicitly in XAML (Icon / Augment / Availability),
            // so we no longer need to strip auto-generated HexValue/Lockable columns.
            List<Item> removeItems = AllItems.Where((item) => item.Name == String.Empty || item.Lockable == false).ToList();
            foreach (Item item in removeItems)
            {
                AllItems.Remove(item); // <-- remove placeholders and non-lockable items
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SettingsFile.SaveToFile(Program.SettingsFilePath);
                MessageBox.Show("Your toggle choices were saved for next time.", "Settings Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save settings:\n\n" + ex.Message, "Settings",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunModification_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // count how many items are actually being changed, for friendly feedback
                int locking = AllItems.Count(i => i.Availability == LockState.Locked);
                int unlocking = AllItems.Count(i => i.Availability == LockState.Unlocked);

                ErrorState modificationStatus = PerformSaveModification(SaveNameAndPathToUse, SettingsFile);

                if (modificationStatus == ErrorState.NoError)
                {
                    MessageBox.Show(
                        "Save modified successfully!\n\n" +
                        "Disabled (locked): " + locking + "\n" +
                        "Enabled (unlocked): " + unlocking + "\n\n" +
                        "A backup of your previous save was made automatically.\n" +
                        "Restart 20XX (fully close and relaunch) to see the changes.",
                        "Done", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Modification finished with status: " + modificationStatus,
                        "Done", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save modification failed:\n\n" + ex.Message +
                    "\n\nYour save was not changed (or use Restore Backup).",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Makes a timestamped copy of the current save file (never overwrites an existing backup).
        /// </summary>
        private void BackupSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string src = SaveNameAndPathToUse;
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string dst = src + ".manualbackup_" + stamp;
                System.IO.File.Copy(src, dst, false);
                MessageBox.Show("Backup created:\n\n" + dst, "Backup Save", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Backup failed:\n\n" + ex.Message, "Backup Save", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Lets the user pick a backup file and restores it over the current save (with confirmation).
        /// </summary>
        private void RestoreSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    InitialDirectory = System.IO.Path.GetDirectoryName(SaveNameAndPathToUse),
                    Title = "Choose a backup to restore",
                    Filter = "Save & backups (*.sav;*.backup*;*.manualbackup*)|*.sav;*.backup*;*.manualbackup*|All files (*.*)|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    MessageBoxResult confirm = MessageBox.Show(
                        "Overwrite your CURRENT save with:\n\n" + dlg.FileName + "\n\nAre you sure?",
                        "Restore Backup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm == MessageBoxResult.Yes)
                    {
                        // safety: snapshot the current save before overwriting it
                        try { System.IO.File.Copy(SaveNameAndPathToUse, SaveNameAndPathToUse + ".prerestore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), false); }
                        catch { /* non-fatal */ }

                        System.IO.File.Copy(dlg.FileName, SaveNameAndPathToUse, true);
                        MessageBox.Show("Save restored.\n\nRestart 20XX to load the restored data.", "Restore Backup", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Restore failed:\n\n" + ex.Message, "Restore Backup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Click-to-toggle for the Availability column. Flips between Enabled (Unlocked) and Disabled (Locked) -
        /// the button is seeded from the save file's actual current bit, so this always reflects real state.
        /// </summary>
        private void ToggleAvailability_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            Item item = btn.DataContext as Item;
            if (item == null) { return; }

            item.Availability = (item.Availability == LockState.Unlocked) ? LockState.Locked : LockState.Unlocked;
            StyleAvailabilityButton(btn, item.Availability);
        }

        private void AvailabilityButton_Loaded(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            Item item = btn.DataContext as Item;
            if (item != null) { StyleAvailabilityButton(btn, item.Availability); }
        }

        /// <summary>
        /// The DataGrid can recycle row containers as you scroll - a recycled button keeps its old
        /// styling because Loaded only fires once per container, not once per row it's reused for.
        /// This re-syncs the button's look every time its DataContext actually changes.
        /// </summary>
        private void AvailabilityButton_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Button btn = (Button)sender;
            Item item = btn.DataContext as Item;
            if (item != null) { StyleAvailabilityButton(btn, item.Availability); }
        }

        /// <summary>
        /// Colors the toggle button by state and sets its label.
        /// </summary>
        private static void StyleAvailabilityButton(Button btn, LockState state)
        {
            switch (state)
            {
                case LockState.Unlocked:
                    btn.Content = "Enabled";
                    btn.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // green
                    btn.Foreground = Brushes.White;
                    break;
                case LockState.Locked:
                    btn.Content = "Disabled";
                    btn.Background = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // crimson
                    btn.Foreground = Brushes.White;
                    break;
                default: // NoChange - not used for lockable items anymore, kept as a safe fallback
                    btn.Content = "No Change";
                    btn.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x4A)); // gray
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(0xFD, 0xDD, 0x93)); // yellow
                    break;
            }
        }
    }
}
