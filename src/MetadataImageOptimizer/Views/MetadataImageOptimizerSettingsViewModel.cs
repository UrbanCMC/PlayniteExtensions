using System.Collections.Generic;
using MetadataImageOptimizer.Helpers;
using MetadataImageOptimizer.Settings;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace MetadataImageOptimizer.Views
{
    public class MetadataImageOptimizerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly MetadataImageOptimizer plugin;

        private MetadataImageOptimizerSettings editingClone;
        private MetadataImageOptimizerSettings settings;

        public MetadataImageOptimizerSettings Settings
        {
            get => settings;
            set => SetValue(ref settings, value);
        }

        public MetadataImageOptimizerSettingsViewModel(MetadataImageOptimizer plugin)
        {
            this.plugin = plugin;

            var savedSettings = plugin.LoadPluginSettings<MetadataImageOptimizerSettings>();
            Settings = savedSettings ?? new MetadataImageOptimizerSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            if (!Serialization.AreObjectsEqual(Settings, editingClone))
            {
                OptimizationCacheHelper.Clear();
            }

            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
