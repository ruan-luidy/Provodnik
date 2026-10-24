using CommunityToolkit.Mvvm.ComponentModel;
using Provodnik.Core.Install;
using Provodnik.Core.Modpack;
using Provodnik.Core.Text;
using Provodnik.Wpf.Shared;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>Um mod durante a instalação: o estado, a barra e o recado da vez.</summary>
  public sealed partial class ModRow(int index, ModEntry? entry, string? title = null) : ObservableObject
  {
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(Tone), nameof(IsActive), nameof(IsFinished), nameof(IsIndeterminate))]
    private ModStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Percent), nameof(IsIndeterminate))]
    private double? _fraction;

    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private string _speedText = string.Empty;
    [ObservableProperty] private string? _detail;

    public int Index { get; } = index;

    public string Title { get; } = entry?.Title ?? title ?? $"Mod {index}";

    public string Author { get; } = entry?.Author ?? string.Empty;

    public string Source { get; } = entry?.Kind switch
    {
      ModKind.ModDb => "ModDB",
      ModKind.GitHub => "GitHub",
      ModKind.GoFile => "gofile",
      ModKind.LargeFiles => "GitHub",
      _ => string.Empty,
    };

    public double BytesPerSecond { get; private set; }

    public double Percent => (Fraction ?? 0) * 100;

    public bool IsIndeterminate => IsActive && Fraction is null;

    public bool IsActive => Status is ModStatus.Resolving or ModStatus.Downloading or ModStatus.Verifying or ModStatus.Extracting;

    public bool IsFinished => Status is ModStatus.Installed or ModStatus.UpToDate or ModStatus.Failed;

    public string StatusText => Status switch
    {
      ModStatus.Pending => "Na fila",
      ModStatus.Checking => "Conferindo",
      ModStatus.Resolving => "Pedindo o link",
      ModStatus.Downloading => "Baixando",
      ModStatus.Verifying => "Conferindo md5",
      ModStatus.WaitingExtraction => "Esperando",
      ModStatus.Extracting => "Extraindo",
      ModStatus.Installed => "Instalado",
      ModStatus.UpToDate => "Em dia",
      ModStatus.Failed => "Falhou",
      _ => string.Empty,
    };

    public RowTone Tone => Status switch
    {
      ModStatus.Installed or ModStatus.UpToDate => RowTone.Success,
      ModStatus.Failed => RowTone.Error,
      _ => RowTone.Neutral,
    };

    public void Apply(ModProgress progress)
    {
      // depois de terminar, só um recomeço (outra rodada) muda o estado
      if (IsFinished && progress.Status is not (ModStatus.Checking or ModStatus.Pending))
        return;

      Status = progress.Status;
      Fraction = progress.Fraction;
      if (progress.Detail is not null || progress.Status != ModStatus.Downloading)
        Detail = progress.Detail;

      BytesPerSecond = progress.Status == ModStatus.Downloading ? progress.BytesPerSecond ?? 0 : 0;
      SpeedText = progress.Status == ModStatus.Downloading ? ByteSize.FormatSpeed(progress.BytesPerSecond) : string.Empty;
      ProgressText = progress.Status switch
      {
        ModStatus.Downloading when progress.Total is { } total => $"{ByteSize.Format(progress.Received)} de {ByteSize.Format(total)}",
        ModStatus.Downloading when progress.Received > 0 => ByteSize.Format(progress.Received),
        ModStatus.Extracting when progress.Fraction is { } f => $"{f:P0}",
        _ => string.Empty,
      };
    }
  }
}
