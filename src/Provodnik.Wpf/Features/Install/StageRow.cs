using CommunityToolkit.Mvvm.ComponentModel;
using Provodnik.Core.Install;
using Provodnik.Core.Text;
using Provodnik.Wpf.Shared;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>Uma etapa na lista da esquerda da tela de instalação.</summary>
  public sealed partial class StageRow(InstallStage stage, string title, string description) : ObservableObject
  {
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tone), nameof(IsRunning), nameof(IsDone))]
    private StageState _state;

    [ObservableProperty] private string? _detail;

    public InstallStage Stage { get; } = stage;

    public string Title { get; } = title;

    public string Description { get; } = description;

    /// <summary>A última da lista: sem o traço que liga à próxima.</summary>
    public bool IsLast { get; init; }

    public bool IsRunning => State == StageState.Running;

    public bool IsDone => State is StageState.Done or StageState.Warning or StageState.Failed or StageState.Skipped;

    public RowTone Tone => State switch
    {
      StageState.Done => RowTone.Success,
      StageState.Warning => RowTone.Warning,
      StageState.Failed => RowTone.Error,
      _ => RowTone.Neutral,
    };

    public static IReadOnlyList<StageRow> CreateAll() =>
    [
      new(InstallStage.Preparing, L.T("install.stage.preparing"), L.T("install.stage.preparing.tip")),
      new(InstallStage.Repositories, L.T("install.stage.repositories"), L.T("install.stage.repositories.tip")),
      new(InstallStage.Definition, L.T("install.stage.definition"), L.T("install.stage.definition.tip")),
      new(InstallStage.ModList, L.T("install.stage.modList"), L.T("install.stage.modList.tip")),
      new(InstallStage.Mods, L.T("install.stage.mods"), L.T("install.stage.mods.tip")),
      new(InstallStage.Extras, L.T("install.stage.extras"), L.T("install.stage.extras.tip")),
      new(InstallStage.Profile, L.T("install.stage.profile"), L.T("install.stage.profile.tip")),
      new(InstallStage.Patches, L.T("install.stage.patches"), L.T("install.stage.patches.tip")),
      new(InstallStage.Shortcuts, L.T("install.stage.shortcuts"), L.T("install.stage.shortcuts.tip")),
      new(InstallStage.Verify, L.T("install.stage.verify"), L.T("install.stage.verify.tip")) { IsLast = true },
    ];
  }
}
