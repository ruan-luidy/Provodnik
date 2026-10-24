using CommunityToolkit.Mvvm.ComponentModel;
using Provodnik.Core.Diagnostics;
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
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(Tone), nameof(IsActive), nameof(IsFinished), nameof(IsIndeterminate), nameof(Hint))]
    private ModStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Percent), nameof(IsIndeterminate))]
    private double? _fraction;

    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private string _speedText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    private string? _detail;

    public int Index { get; } = index;

    public string Title { get; } = entry?.Title ?? title ?? L.T("install.mod.fallbackTitle", index);

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

    /// <summary>O que fazer com a falha, quando o erro é conhecido.</summary>
    public string? Hint => Status == ModStatus.Failed ? ErrorExplainer.Explain(Detail)?.Hint : null;

    public double Percent => (Fraction ?? 0) * 100;

    public bool IsIndeterminate => IsActive && Status != ModStatus.Retrying && Fraction is null;

    public bool IsActive => Status is ModStatus.Resolving or ModStatus.Downloading or ModStatus.Retrying or ModStatus.Verifying or ModStatus.Extracting;

    public bool IsFinished => Status is ModStatus.Installed or ModStatus.UpToDate or ModStatus.Failed;

    public string StatusText => Status switch
    {
      ModStatus.Pending => L.T("install.status.pending"),
      ModStatus.Checking => L.T("install.status.checking"),
      ModStatus.Resolving => L.T("install.status.resolving"),
      ModStatus.Downloading => L.T("install.status.downloading"),
      ModStatus.Retrying => L.T("install.status.retrying"),
      ModStatus.Verifying => L.T("install.status.verifying"),
      ModStatus.WaitingExtraction => L.T("install.status.waiting"),
      ModStatus.Extracting => L.T("install.status.extracting"),
      ModStatus.Installed => L.T("install.status.installed"),
      ModStatus.UpToDate => L.T("install.status.upToDate"),
      ModStatus.Failed => L.T("install.status.failed"),
      _ => string.Empty,
    };

    public RowTone Tone => Status switch
    {
      ModStatus.Installed or ModStatus.UpToDate => RowTone.Success,
      ModStatus.Failed => RowTone.Error,
      ModStatus.Retrying => RowTone.Warning,
      _ => RowTone.Neutral,
    };

    public void Apply(ModProgress progress)
    {
      // depois de terminar, só um recomeço (outra rodada) muda o estado
      if (IsFinished && progress.Status is not (ModStatus.Checking or ModStatus.Pending))
        return;

      // estado novo traz o recado dele (ou nenhum); no mesmo estado, só um recado novo troca o de antes
      if (progress.Status != Status || progress.Detail is not null)
        Detail = progress.Detail;
      Status = progress.Status;
      Fraction = progress.Fraction;

      BytesPerSecond = progress.Status == ModStatus.Downloading ? progress.BytesPerSecond ?? 0 : 0;
      SpeedText = progress.Status == ModStatus.Downloading ? ByteSize.FormatSpeed(progress.BytesPerSecond) : string.Empty;
      ProgressText = progress.Status switch
      {
        ModStatus.Downloading when progress.Total is { } total => L.T("install.progress.of", ByteSize.Format(progress.Received), ByteSize.Format(total)),
        ModStatus.Downloading when progress.Received > 0 => ByteSize.Format(progress.Received),
        ModStatus.Extracting when progress.Fraction is { } f => $"{f:P0}",
        _ => string.Empty,
      };
    }
  }
}
