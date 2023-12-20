using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MetadataImageOptimizer.Settings;

namespace MetadataImageOptimizer.Controls
{
    public partial class ImageTypeSettingsGroupBox : UserControl
    {
        public ImageTypeSettingsGroupBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty AvailableImageFormatsProperty = DependencyProperty.Register(
            nameof(AvailableImageFormats)
            , typeof(Dictionary<string, string>)
            , typeof(ImageTypeSettingsGroupBox)
            , new PropertyMetadata(default(Dictionary<string, string>)));

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            nameof(Header)
            , typeof(string)
            , typeof(ImageTypeSettingsGroupBox)
            , new PropertyMetadata(""));

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            nameof(Settings)
            , typeof(ImageTypeSettings)
            , typeof(ImageTypeSettingsGroupBox)
            , new PropertyMetadata(default(ImageTypeSettings)));

        public Dictionary<string, string> AvailableImageFormats
        {
            get => (Dictionary<string, string>)GetValue(AvailableImageFormatsProperty);
            set => SetValue(AvailableImageFormatsProperty, value);
        }

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public ImageTypeSettings Settings
        {
            get => (ImageTypeSettings)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }
    }
}

