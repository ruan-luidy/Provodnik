using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Provodnik.Wpf.Shared.Controls
{
  /// <summary>
  /// Lista suspensa (o SelectField do Терминал e do Windfinder): a caixa fechada tem o jeito dos botões do lado e a
  /// lista abre num cartão logo embaixo. No lugar do ComboBox do HandyControl, que vinha com fundo sólido, borda e a
  /// setinha dele.
  /// </summary>
  public partial class SelectField : UserControl
  {
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
      nameof(ItemsSource), typeof(IEnumerable), typeof(SelectField), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
      nameof(SelectedItem),
      typeof(object),
      typeof(SelectField),
      new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
      nameof(Placeholder), typeof(string), typeof(SelectField), new PropertyMetadata(string.Empty, OnLookChanged));

    /// <summary>Folga entre a caixa e a lista aberta (embaixo ou em cima).</summary>
    private const double Gap = 4;

    /// <summary>A margem da sombra em volta do cartão (PopupShadowRoom, 12,10,12,16): descontada ao posicionar.</summary>
    private static readonly Thickness ShadowRoom = new(12, 10, 12, 16);

    public SelectField()
    {
      InitializeComponent();
      DropDown.CustomPopupPlacementCallback = Place;
      Show();
    }

    /// <summary>
    /// Abre embaixo com <see cref="Gap"/> de folga; sem espaço na tela, abre em cima (o WPF escolhe a primeira que
    /// cabe). O cartão fica alinhado com a caixa: a margem da sombra sai pra esquerda e pra cima. Os tamanhos chegam em
    /// pixel físico, então as margens vão pela escala do DPI.
    /// </summary>
    private CustomPopupPlacement[] Place(Size popup, Size target, Point offset)
    {
      var scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
      var x = -ShadowRoom.Left * scale;
      return
      [
        new CustomPopupPlacement(new Point(x, target.Height + (Gap - ShadowRoom.Top) * scale), PopupPrimaryAxis.Horizontal),
        new CustomPopupPlacement(new Point(x, -popup.Height + (ShadowRoom.Bottom - Gap) * scale), PopupPrimaryAxis.Horizontal),
      ];
    }

    public IEnumerable? ItemsSource
    {
      get => (IEnumerable?)GetValue(ItemsSourceProperty);
      set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
      get => GetValue(SelectedItemProperty);
      set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>O que aparece na caixa enquanto nada está escolhido.</summary>
    public string Placeholder
    {
      get => (string)GetValue(PlaceholderProperty);
      set => SetValue(PlaceholderProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var field = (SelectField)d;
      field.Items.ItemsSource = field.ItemsSource;
      field.Show();
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
      ((SelectField)d).Show();

    private static void OnLookChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
      ((SelectField)d).Show();

    private void Show()
    {
      if (Items.SelectedItem != SelectedItem)
        Items.SelectedItem = SelectedItem;

      var chosen = SelectedItem is not null;
      Label.Text = chosen ? SelectedItem!.ToString() : Placeholder;
      Toggle.Foreground = (Brush)FindResource(chosen ? "PrimaryTextBrush" : "SecondaryTextBrush");
    }

    /// <summary>O zoom e o fade do menu de botão direito (o ContextMenu do Components.xaml), a cada vez que abre.</summary>
    private void DropDown_Opened(object? sender, EventArgs e)
    {
      var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
      var duration = TimeSpan.FromMilliseconds(130);
      var scale = (ScaleTransform)Card.RenderTransform;
      scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
      scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
      Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
    }

    private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (Items.SelectedItem is { } item && !Equals(item, SelectedItem))
        SelectedItem = item;

      Toggle.IsChecked = false;
    }
  }
}
