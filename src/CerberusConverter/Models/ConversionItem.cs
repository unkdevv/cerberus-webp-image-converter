using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CerberusConverter.Models;

public sealed class ConversionItem : INotifyPropertyChanged
{
    private string _inputFormat = "Detectando";
    private string _sizeText = "-";
    private string _dimensionsText = "-";
    private string _estimatedOutputText = "-";
    private string _reductionText = "-";
    private string _status = "Na fila";
    private double _progress;
    private bool _isBusy;
    private ImageSource? _previewImage;

    public ConversionItem(string sourcePath)
    {
        SourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SourcePath { get; }

    public string FileName { get; }

    public long SourceBytes { get; set; }

    public ImageSource? PreviewImage
    {
        get => _previewImage;
        set => SetField(ref _previewImage, value);
    }

    public string InputFormat
    {
        get => _inputFormat;
        set => SetField(ref _inputFormat, value);
    }

    public string SizeText
    {
        get => _sizeText;
        set => SetField(ref _sizeText, value);
    }

    public string DimensionsText
    {
        get => _dimensionsText;
        set => SetField(ref _dimensionsText, value);
    }

    public string EstimatedOutputText
    {
        get => _estimatedOutputText;
        set => SetField(ref _estimatedOutputText, value);
    }

    public string ReductionText
    {
        get => _reductionText;
        set => SetField(ref _reductionText, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
