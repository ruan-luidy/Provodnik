using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Provodnik.Wpf.Shared.Services;

namespace Provodnik.Wpf.Features.Install
{
  public partial class InstallView : UserControl
  {
    private InstallViewModel? _model;

    public InstallView()
    {
      InitializeComponent();
      Loaded += (_, _) => Hook(DataContext as InstallViewModel);
      Unloaded += (_, _) => Hook(null);
      DataContextChanged += (_, _) => Hook(IsLoaded ? DataContext as InstallViewModel : null);
      IsVisibleChanged += (_, _) => Place();
    }

    private void Hook(InstallViewModel? model)
    {
      if (_model is { } old)
      {
        old.PropertyChanged -= OnChanged;
        old.Service.PropertyChanged -= OnChanged;
        DialogService.OpenChanged -= OnDialog;
        if (old.Browser is { } browser)
        {
          browser.Detach(ModDbPage);
          if (browser.Attention == ShowForChallenge)
            browser.Attention = null;
        }
      }

      _model = model;
      if (model is null)
        return;
      model.PropertyChanged += OnChanged;
      model.Service.PropertyChanged += OnChanged;
      DialogService.OpenChanged += OnDialog;
      if (model.Browser is { } current)
        current.Attention = ShowForChallenge;
      Place();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
      if (e.PropertyName is nameof(InstallViewModel.ModDbPageOpen) or nameof(InstallService.CurrentModDb))
        Place();
    }

    private void OnDialog(object? sender, EventArgs e) => Place();

    /// <summary>
    /// A página do ModDB vem pro cartão só aberta e na tela: no cartão fechado o navegador acharia que ela está
    /// escondida, e diálogo aberto ficaria por baixo dela (a página é uma janela do Windows por cima do WPF).
    /// </summary>
    private void Place()
    {
      if (_model?.Browser is not { } browser)
        return;
      if (IsVisible && _model.ModDbPageOpen && _model.Service.CurrentModDb is not null && !DialogService.IsOpen)
        browser.Attach(ModDbPage);
      else
        browser.Detach(ModDbPage);
    }

    /// <summary>O desafio do Cloudflare com a tela de Instalação aberta: a página abre no cartão em vez da janela solta.</summary>
    private bool ShowForChallenge()
    {
      if (_model is null || !IsVisible || _model.Service.CurrentModDb is null || DialogService.IsOpen)
        return false;
      _model.ModDbPageOpen = true;
      return true;
    }
  }
}
