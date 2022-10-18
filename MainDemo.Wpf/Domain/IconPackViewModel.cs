﻿using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BluwolfIcons;
using MaterialDesignThemes.Wpf;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MaterialDesignDemo.Domain
{
    public class IconPackViewModel : ViewModelBase
    {
        private readonly Lazy<IEnumerable<PackIconKindGroup>> _packIconKinds;
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;

        public IconPackViewModel(ISnackbarMessageQueue snackbarMessageQueue)
        {
            _snackbarMessageQueue = snackbarMessageQueue ?? throw new ArgumentNullException(nameof(snackbarMessageQueue));

            OpenDotComCommand = new AnotherCommandImplementation(OpenDotCom);
            SearchCommand = new AnotherCommandImplementation(Search);
            CopyToClipboardCommand = new AnotherCommandImplementation(CopyToClipboard);
            DownloadAllIConsCommand = new AnotherCommandImplementation(DownloadAllICons);

            _packIconKinds = new Lazy<IEnumerable<PackIconKindGroup>>(() =>
                Enum.GetNames(typeof(PackIconKind))
                    .GroupBy(k => (PackIconKind)Enum.Parse(typeof(PackIconKind), k))
                    .Select(g => new PackIconKindGroup(g))
                    .OrderBy(x => x.Kind)
                    .ToList());

            var helper = new PaletteHelper();
            if (helper.GetThemeManager() is { } themeManager)
            {
                themeManager.ThemeChanged += ThemeManager_ThemeChanged;
            }
            SetDefaultIconColors();

            TransparentBackground = true;
        }

        private void ThemeManager_ThemeChanged(object? sender, ThemeChangedEventArgs e)
            => SetDefaultIconColors();

        public ICommand OpenDotComCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand CopyToClipboardCommand { get; }
        public ICommand DownloadAllIConsCommand { get; }

        private IEnumerable<PackIconKindGroup>? _kinds;
        private PackIconKindGroup? _group;
        private string? _kind;
        private PackIconKind _packIconKind;
        private bool _transparentBackground;

        public IEnumerable<PackIconKindGroup> Kinds
        {
            get => _kinds ??= _packIconKinds.Value;
            set => SetProperty(ref _kinds, value);
        }

        public PackIconKindGroup? Group
        {
            get => _group;
            set
            {
                if (SetProperty(ref _group, value))
                {
                    Kind = value?.Kind;
                }
            }
        }

        public string? Kind
        {
            get => _kind;
            set
            {
                if (SetProperty(ref _kind, value))
                {
                    PackIconKind = value != null ? (PackIconKind)Enum.Parse(typeof(PackIconKind), value) : default;
                }
            }
        }

        public bool TransparentBackground
        {
            get => _transparentBackground;
            set => SetProperty(ref _transparentBackground, value);
        }

        public PackIconKind PackIconKind
        {
            get => _packIconKind;
            set => SetProperty(ref _packIconKind, value);
        }

        private void OpenDotCom(object? _)
            => Link.OpenInBrowser("https://materialdesignicons.com/");

        private async void Search(object? obj)
        {
            var text = obj as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                Kinds = _packIconKinds.Value;
            }
            else
            {
                Kinds = await Task.Run(() => _packIconKinds.Value
                    .Where(x => x.Aliases.Any(a => a.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0))
                    .ToList());
            }
        }

        private void CopyToClipboard(object? obj)
        {
            var toBeCopied = $"<materialDesign:PackIcon Kind=\"{obj}\" />";
            Clipboard.SetDataObject(toBeCopied);
            _snackbarMessageQueue.Enqueue(toBeCopied + " copied to clipboard");
        }

        private void DownloadAllICons(object? obj)
        {
            var saveDialog = new FolderBrowserDialog();

            if (saveDialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(saveDialog.SelectedPath))
            {
                Enum.GetNames(typeof(PackIconKind)).ToList().ForEach(x =>
                {
                    PackIconKind kind = (PackIconKind)Enum.Parse(typeof(PackIconKind), x);
                    SaveIcon(kind, Path.Combine(saveDialog.SelectedPath, kind + ".ico"));
                });
            }
        }

        private void SetDefaultIconColors()
        {
            var helper = new PaletteHelper();
            ITheme theme = helper.GetTheme();
            GeneratedIconBackground = theme.Paper;
            GeneratedIconForeground = theme.PrimaryMid.Color;
        }

        private Color _generatedIconBackground;
        public Color GeneratedIconBackground
        {
            get
            {
                if (_transparentBackground)
                    return Brushes.Transparent.Color;

                return _generatedIconBackground;
            }
            set => SetProperty(ref _generatedIconBackground, value);
        }

        private Color _generatedIconForeground;
        public Color GeneratedIconForeground
        {
            get => _generatedIconForeground;
            set => SetProperty(ref _generatedIconForeground, value);
        }

        private ICommand? _saveIconCommand;
        public ICommand SaveIconCommand => _saveIconCommand ??= new AnotherCommandImplementation(OnSaveIcon);

        private void OnSaveIcon(object? _)
        {
            var saveDialog = new SaveFileDialog
            {
                DefaultExt = ".ico",
                Title = "Save Icon (.ico)",
                Filter = "Icon Files|*.ico|All Files|*",
                CheckPathExists = true,
                OverwritePrompt = true,
                RestoreDirectory = true
            };
            if (saveDialog.ShowDialog() != true) return;

            SaveIcon(PackIconKind, saveDialog.FileName);
        }

        private void SaveIcon(PackIconKind iconKind, string path)
        {
            var icon = new Icon();

            //TODO: Make this size list configurable
            // foreach (var size in new[] { 256, 128, 64, 48, 32, 24, 16 })
            foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256 })
            {
                RenderTargetBitmap bmp = RenderImage(size);
                icon.Images.Add(new BmpIconImage(bmp));
            }

            icon.Save(path);

            RenderTargetBitmap RenderImage(int size)
            {
                var packIcon = new PackIcon
                {
                    Kind = iconKind,
                    Background = TransparentBackground ? Brushes.Transparent : new SolidColorBrush(GeneratedIconBackground),
                    Foreground = new SolidColorBrush(GeneratedIconForeground),
                    Width = size,
                    Height = size,
                    Style = (Style)Application.Current.FindResource(typeof(PackIcon))
                };
                packIcon.Measure(new Size(size, size));
                packIcon.Arrange(new Rect(0, 0, size, size));
                packIcon.UpdateLayout();

                RenderTargetBitmap bmp = new(size, size, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(packIcon);
                return bmp;
            }
        }
    }
}
