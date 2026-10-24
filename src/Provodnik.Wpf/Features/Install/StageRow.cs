using CommunityToolkit.Mvvm.ComponentModel;
using Provodnik.Core.Install;
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
      new(InstallStage.Preparing, "Conferir a pasta", "GAMMA, ferramentas, MO2 fechado e espaço em disco"),
      new(InstallStage.Repositories, "Repositórios", "Stalker_GAMMA, arquivos grandes e Gunslinger pelo git"),
      new(InstallStage.Definition, "Definição do modpack", "A pasta G.A.M.M.A e a versão"),
      new(InstallStage.ModList, "Lista de mods", "O mods.txt de stalker-gamma.com"),
      new(InstallStage.Mods, "Addons", "Baixar, conferir e extrair cada mod"),
      new(InstallStage.Extras, "Extras", "Arquivos grandes, Gunslinger e modpack_addons"),
      new(InstallStage.Profile, "Perfil do MO2", "profiles\\G.A.M.M.A e a ordem dos mods"),
      new(InstallStage.Patches, "Patches do Anomaly", "bin, appdata, db e gamedata do GAMMA"),
      new(InstallStage.Shortcuts, "Atalhos", "Na área de trabalho"),
      new(InstallStage.Verify, "Conferência final", "Todo mod ativo no MO2 tem pasta com conteúdo") { IsLast = true },
    ];
  }
}
