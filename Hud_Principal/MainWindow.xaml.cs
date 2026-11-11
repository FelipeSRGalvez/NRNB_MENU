using Hud_Principal.Views;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;


namespace Hud_Principal
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Lista com os ids dos paineis
            _panels = [HomePanel, ConfigPanel, MonthlySisaguaPanel, AnnualSisaguaPanel, AnnualSisaguaImplementationPanel, ParameterSisaguaPanel, MonthlyGalPanel, VigiarFocosCalorPanel, VigiarIqArPanel, SisamPrevisaoPanel, VigidesastresSecasPanel];

            // Puxa a função para o view para voltar ao HomeView
            ConfigPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            MonthlySisaguaPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            AnnualSisaguaPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            AnnualSisaguaImplementationPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            ParameterSisaguaPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            MonthlyGalPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            VigiarFocosCalorPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            VigiarIqArPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            SisamPrevisaoPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
            VigidesastresSecasPanel.RequestHome += (s, e) => ShowPanel(HomePanel);
        }

        // Função que dará animação e delay de abertura e fechamento de submenu (sidepanel) para dar mais fluidez
        private void ToggleSubmenu(StackPanel submenu, RotateTransform chevronRotation)
        {
            bool opening = submenu.MaxHeight == 0;

            if (opening)
            {
                submenu.Visibility = Visibility.Visible;
                var anim = new DoubleAnimation(0, 200, TimeSpan.FromSeconds(0.25));
                submenu.BeginAnimation(FrameworkElement.MaxHeightProperty, anim);
            }
            else
            {
                var anim = new DoubleAnimation(200, 0, TimeSpan.FromSeconds(0.2));
                anim.Completed += (s, e) => submenu.Visibility = Visibility.Collapsed;
                submenu.BeginAnimation(FrameworkElement.MaxHeightProperty, anim);
            }

            var rotateAnim = new DoubleAnimation(opening ? 0 : 180, TimeSpan.FromSeconds(0.25));
            chevronRotation.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
        }

        // Informando que a função acima (ToggleSubmenu) estará nos ementos de id SisaguaSubmenu....
        private void BtnSisagua_Click(object sender, RoutedEventArgs e)
            => ToggleSubmenu(SisaguaSubmenu, SisaguaChevronRotation);

        private void BtnGal_Click(object sender, RoutedEventArgs e)
            => ToggleSubmenu(GalSubmenu, GalChevronRotation);

        private void BtnIdaron_Click(object sender, RoutedEventArgs e)
            => ToggleSubmenu(IdaronSubmenu, IdaronChevronRotation);

        // Lógica para navegação de links na página xaml
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });

            e.Handled = true;
        }

        // Lista com todos os painéis da grid (x:Name="[Nome]")
        private List<FrameworkElement> _panels;

        // Colapsa todos e abre somente o solicitado
        private void ShowPanel(FrameworkElement panel)
        {
            foreach (var i in _panels)
                i.Visibility = Visibility.Collapsed;

            panel.Visibility = Visibility.Visible;
        }

        // Acessa a grid das configurações
        private void BtnConfig_Click(object sender, RoutedEventArgs e)
            => ShowPanel(ConfigPanel);

        // Função para as setas para voltar ao homepage
        private void BtnHome_Click(object sender, RoutedEventArgs e)
            => ShowPanel(HomePanel);

        private void BtnMontlhySisagua_Click(object sender, RoutedEventArgs e)
            => ShowPanel(MonthlySisaguaPanel);

        private void BtnAnnualSisagua_Click(object sender, RoutedEventArgs e)
            => ShowPanel(AnnualSisaguaPanel);

        private void BtnAnnualSisaguaImplementation_Click(object sender, RoutedEventArgs e)
            => ShowPanel(AnnualSisaguaImplementationPanel);

        private void BtnParameterSisagua_Click(object sender, RoutedEventArgs e)
            => ShowPanel(ParameterSisaguaPanel);

        private void BtnMonthlyGal_CLick(object sender, RoutedEventArgs e)
            => ShowPanel(MonthlyGalPanel);

        private void BtnVigiar_Click(object sender, RoutedEventArgs e)
            => ToggleSubmenu(VigiarSubmenu, VigiarChevronRotation);

        private void BtnFocosCalor_Click(object sender, RoutedEventArgs e)
            => ShowPanel(VigiarFocosCalorPanel);

        private void BtnIqAr_Click(object sender, RoutedEventArgs e)
            => ShowPanel(VigiarIqArPanel);

        private void BtnSisam_Click(object sender, RoutedEventArgs e)
            => ToggleSubmenu(SisamSubmenu, SisamChevronRotation);

        private void BtnSisamPrevisao_Click(object sender, RoutedEventArgs e)
            => ShowPanel(SisamPrevisaoPanel);

        private void BtnVigidesastres_Click(object sender, RoutedEventArgs e)
            => ToggleSubmenu(VigidesastresSubmenu, VigidesastresChevronRotation);

        private void BtnVigidesastresSecas_Click(object sender, RoutedEventArgs e)
            => ShowPanel(VigidesastresSecasPanel);
    }
}