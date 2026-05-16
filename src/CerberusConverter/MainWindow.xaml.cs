using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CerberusConverter.Models;
using CerberusConverter.Services;
using Forms = System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using DataFormats = System.Windows.DataFormats;

namespace CerberusConverter;

public partial class MainWindow : Window
{
    private static readonly string CerberusOutputRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "cerberus");

    private readonly ObservableCollection<ConversionItem> _imageItems = [];
    private readonly ImageConversionService _imageService = new();
    private int _estimateVersion;
    private bool _isInitializing = true;
    private UserPreferences _preferences = new();
    private static readonly string PreferencesFolder = Path.Combine(Path.GetTempPath(), "CerberusConverter");
    private static readonly string PreferencesPath = Path.Combine(PreferencesFolder, "preferences.json");

    public MainWindow()
    {
        InitializeComponent();
        _preferences = LoadPreferences();
        Directory.CreateDirectory(CerberusOutputRoot);

        ImageDataGrid.ItemsSource = _imageItems;
        ImageFormatCombo.ItemsSource = ImageConversionService.OutputFormats;
        ImageFormatCombo.SelectedItem = "webp";
        ImageQualitySlider.Value = Math.Clamp(_preferences.ImageQuality ?? 82, 1, 100);
        ImageOutputFolderTextBox.Text = string.IsNullOrWhiteSpace(_preferences.CustomOutputFolder)
            ? CerberusOutputRoot
            : _preferences.CustomOutputFolder;

        RefreshQualityLabel();
        SetStatus("Pronto. Arraste imagens ou use Adicionar.");
        UpdateItemCount();
        _isInitializing = false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        EnableWindows11TitleBar();
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Imagens|*.jpg;*.jpeg;*.png;*.webp;*.avif;*.bmp;*.gif;*.ico;*.heic;*.heif;*.tif;*.tiff;*.wbmp|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await AddImagesAsync(dialog.FileNames);
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Escolha a pasta com imagens") is { } folder)
        {
            await AddImagesAsync(Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories));
        }
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in ImageDataGrid.SelectedItems.Cast<ConversionItem>().ToList())
        {
            _imageItems.Remove(item);
        }

        UpdateItemCount();
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: ConversionItem item })
        {
            _imageItems.Remove(item);
            SetStatus($"Imagem removida: {item.FileName}");
            UpdateItemCount();
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _imageItems.Clear();
        SetStatus("Fila de imagens limpa.");
        UpdateItemCount();
    }

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        await ConvertImagesAsync();
    }

    private void BrowseImageOutput_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Pasta de saida das imagens") is { } folder)
        {
            ImageOutputFolderTextBox.Text = folder;
            _preferences.CustomOutputFolder = PathEquals(folder, CerberusOutputRoot) ? null : folder;
            SavePreferences();
            SetStatus($"Pasta de saida atualizada: {folder}");
        }
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        await AddImagesAsync(ExpandPaths(paths));
    }

    private void ImageSettings_Changed(object sender, RoutedEventArgs e)
    {
        RefreshQualityLabel();
        if (!_isInitializing)
        {
            _preferences.ImageQuality = (int)Math.Round(ImageQualitySlider.Value);
            SavePreferences();
        }

        _ = AutoEstimateImagesAsync();
    }

    private async Task AddImagesAsync(IEnumerable<string> paths)
    {
        var options = GetImageOptions();
        var added = 0;

        foreach (var path in paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_imageItems.Any(item => item.SourcePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var item = new ConversionItem(path);
            item.PreviewImage = await CreatePreviewImageAsync(path);
            _imageItems.Add(item);

            try
            {
                var metadata = await _imageService.ReadMetadataAsync(path);
                item.SourceBytes = metadata.Bytes;
                item.InputFormat = metadata.Format;
                item.SizeText = FormatHelpers.FormatBytes(metadata.Bytes);
                item.DimensionsText = $"{metadata.Width}x{metadata.Height}";
                await EstimateImageAsync(item, options);
                added++;
            }
            catch (Exception ex)
            {
                item.Status = $"Nao suportado: {TrimError(ex.Message)}";
                item.InputFormat = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                item.SourceBytes = new FileInfo(path).Length;
                item.SizeText = FormatHelpers.FormatBytes(item.SourceBytes);
            }
        }

        SetStatus(added == 1 ? "1 imagem adicionada." : $"{added} imagens adicionadas.");
        UpdateItemCount();
    }

    private async Task AutoEstimateImagesAsync()
    {
        if (_imageItems.Count == 0 || ImageFormatCombo?.SelectedItem is null)
        {
            return;
        }

        var version = ++_estimateVersion;
        var options = GetImageOptions();

        foreach (var item in _imageItems)
        {
            if (version != _estimateVersion)
            {
                return;
            }

            await EstimateImageAsync(item, options);
        }

        if (version == _estimateVersion)
        {
            SetStatus("Estimativas atualizadas automaticamente.");
        }
    }

    private async Task EstimateImageAsync(ConversionItem item, ImageConversionOptions options)
    {
        try
        {
            item.Status = "Estimando";
            var bytes = await _imageService.EstimateOutputBytesAsync(item.SourcePath, options.OutputFormat, options.Quality);
            item.EstimatedOutputText = FormatHelpers.FormatBytes(bytes);
            item.ReductionText = FormatHelpers.FormatReduction(item.SourceBytes, bytes);
            item.Status = "Na fila";
        }
        catch (Exception ex)
        {
            item.Status = $"Erro na estimativa: {TrimError(ex.Message)}";
        }
    }

    private async Task ConvertImagesAsync()
    {
        if (_imageItems.Count == 0)
        {
            SetStatus("Adicione imagens antes de converter.");
            return;
        }

        var options = GetImageOptions();
        var outputFolder = ResolveImageOutputFolderForConversion(options.OutputFolder);
        if (outputFolder is null)
        {
            SetStatus("Conversao de imagens cancelada.");
            return;
        }

        SetStatus("Convertendo imagens...");

        foreach (var item in _imageItems)
        {
            try
            {
                item.IsBusy = true;
                item.Progress = 8;
                item.Status = "Convertendo";

                var outputPath = FormatHelpers.BuildUniqueOutputPath(
                    item.SourcePath,
                    outputFolder,
                    ImageConversionService.GetExtension(options.OutputFormat));

                var outputBytes = await _imageService.ConvertAsync(item.SourcePath, outputPath, options.OutputFormat, options.Quality);
                item.EstimatedOutputText = FormatHelpers.FormatBytes(outputBytes);
                item.ReductionText = FormatHelpers.FormatReduction(item.SourceBytes, outputBytes);
                item.Progress = 100;
                item.Status = $"Concluido: {Path.GetFileName(outputPath)}";
            }
            catch (Exception ex)
            {
                item.Status = $"Erro: {TrimError(ex.Message)}";
            }
            finally
            {
                item.IsBusy = false;
            }
        }

        SetStatus("Conversao de imagens finalizada.");
    }

    private ImageConversionOptions GetImageOptions()
    {
        var format = ImageFormatCombo.SelectedItem as string ?? "webp";
        var quality = (int)Math.Round(ImageQualitySlider.Value);
        return new ImageConversionOptions(format, quality, EnsureFolder(ImageOutputFolderTextBox.Text));
    }

    private string? ResolveImageOutputFolderForConversion(string defaultOutputFolder)
    {
        if (_imageItems.Count <= 1)
        {
            Directory.CreateDirectory(defaultOutputFolder);
            return defaultOutputFolder;
        }

        var result = System.Windows.MessageBox.Show(
            this,
            "Voce esta convertendo varias imagens. Deseja salvar em uma pasta separada dentro da pasta cerberus?",
            "Salvar em pasta separada",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            return null;
        }

        if (result == MessageBoxResult.No)
        {
            Directory.CreateDirectory(defaultOutputFolder);
            return defaultOutputFolder;
        }

        var folder = BuildNextNumberedFolderPath(CerberusOutputRoot);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string EnsureFolder(string folder)
    {
        return string.IsNullOrWhiteSpace(folder) ? CerberusOutputRoot : folder;
    }

    private static string BuildNextNumberedFolderPath(string parentFolder)
    {
        Directory.CreateDirectory(parentFolder);

        var nextNumber = Directory.EnumerateDirectories(parentFolder)
            .Select(Path.GetFileName)
            .Select(name => int.TryParse(name, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        for (var i = nextNumber; i < nextNumber + 10_000; i++)
        {
            var candidate = Path.Combine(parentFolder, i.ToString());
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Nao foi possivel criar uma pasta separada para as imagens.");
    }

    private void RefreshQualityLabel()
    {
        if (ImageQualityValue is not null)
        {
            ImageQualityValue.Text = $"{(int)Math.Round(ImageQualitySlider.Value)}%";
        }
    }

    private void UpdateItemCount()
    {
        ItemCountText.Text = $"{_imageItems.Count} imagem(ns) na fila";
    }

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }
            else
            {
                yield return path;
            }
        }
    }

    private static string? PickFolder(string description)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static string TrimError(string message)
    {
        var clean = message.ReplaceLineEndings(" ").Trim();
        return clean.Length <= 110 ? clean : clean[..110] + "...";
    }

    private async Task<BitmapImage?> CreatePreviewImageAsync(string path)
    {
        try
        {
            var bytes = await _imageService.CreatePreviewPngAsync(path);
            if (bytes is null)
            {
                return null;
            }

            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private static UserPreferences LoadPreferences()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
            {
                return new UserPreferences();
            }

            var json = File.ReadAllText(PreferencesPath);
            return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }
        catch
        {
            return new UserPreferences();
        }
    }

    private void SavePreferences()
    {
        try
        {
            Directory.CreateDirectory(PreferencesFolder);
            var json = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PreferencesPath, json);
        }
        catch
        {
            SetStatus("Nao foi possivel salvar as preferencias temporarias.");
        }
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private void EnableWindows11TitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var darkMode = 1;
            _ = DwmSetWindowAttribute(hwnd, DwmWindowAttribute.UseImmersiveDarkMode, ref darkMode, sizeof(int));

            if (Environment.OSVersion.Version.Build >= 22000)
            {
                var cornerPreference = DwmWindowCornerPreference.Round;
                _ = DwmSetWindowAttribute(
                    hwnd,
                    DwmWindowAttribute.WindowCornerPreference,
                    ref cornerPreference,
                    Marshal.SizeOf<DwmWindowCornerPreference>());
            }
        }
        catch
        {
            // The app can run normally if this Windows build does not expose the DWM attributes.
        }
    }

    private enum DwmWindowAttribute
    {
        UseImmersiveDarkMode = 20,
        WindowCornerPreference = 33
    }

    private enum DwmWindowCornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        DwmWindowAttribute attribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        DwmWindowAttribute attribute,
        ref DwmWindowCornerPreference pvAttribute,
        int cbAttribute);

    private sealed class UserPreferences
    {
        public string? CustomOutputFolder { get; set; }

        public int? ImageQuality { get; set; }
    }
}
