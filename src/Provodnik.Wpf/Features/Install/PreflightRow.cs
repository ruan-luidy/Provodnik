using Provodnik.Core.Text;
using Provodnik.Features.Verify;
using Provodnik.Wpf.Shared;

namespace Provodnik.Wpf.Features.Install
{
  /// <summary>Um item da checagem antes de instalar, com o botão de conserto quando tem.</summary>
  public sealed record PreflightRow(PreflightCheck Check)
  {
    public string Title => Check.Title;

    public string Message => Check.Message;

    public CheckFix Fix => Check.Fix;

    public bool HasFix => Check.Fix != CheckFix.None;

    public string FixText => Check.Fix switch
    {
      CheckFix.RunAsAdmin => L.T("install.fix.runAsAdmin"),
      CheckFix.EnableLongPaths => L.T("install.fix.enableLongPaths"),
      _ => string.Empty,
    };

    public RowTone Tone => Check.Level switch
    {
      CheckLevel.Ok => RowTone.Success,
      CheckLevel.Warning => RowTone.Warning,
      _ => RowTone.Error,
    };
  }
}
