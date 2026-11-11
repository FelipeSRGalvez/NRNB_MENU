using Modulo_Seguranca;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

namespace Hud_Principal.Views
{
    public partial class ConfigView : BaseView
    {
        public ConfigView()
        {
            InitializeComponent();
            Loaded += ConfigView_Loaded;
        }

        private void ConfigView_Loaded(object sender, RoutedEventArgs e)
        {
            var service = new ConfigurationService();

            try
            {
                var config = service.Load();

                TxtEmail.Text = config.Vigiagua.Email;
                TxtPassword.Password = config.Vigiagua.Password;
                TxtRawFolder.Text = config.Vigiagua.RawFilesFolder;
                TxtMonthlyDirectiveFolder.Text = config.Vigiagua.MonthlyDirectiveFolder;
                TxtAnnualDirectiveFolder.Text = config.Vigiagua.AnnualDirectiveFolder;
                TxtAnnualImplementationFolder.Text = config.Vigiagua.AnnualImplementationFolder;
                TxtControlFolder.Text = config.Vigiagua.ControlFolder;
                TxtAnalyzedSamplesFolder.Text = config.Vigiagua.AnalyzedSamplesFolder;

                TxtVigiarWildfiresFolder.Text = config.Vigiar.WildfiresFolder;
                TxtVigiarRawFolder.Text = config.Vigiar.RawFilesFolder;
                TxtVigiarIqArFolder.Text = config.Vigiar.IqArFolder;

                TxtSisamMestreFolder.Text = config.Sisam.PastaSisamMestre;

                TxtVigidesastresDroughtFolder.Text = config.Vigidesastres.DroughtFolder;

                TxtGalUsername.Text = config.Gal.Username;
                TxtGalPassword.Password = config.Gal.Password;
                TxtGalModule.Text = config.Gal.Module;
                TxtGalLaboratory.Text = config.Gal.Laboratory;
                TxtGalRawFolder.Text = config.Gal.GalMonthlyRawFolder;
                TxtGalMonthlyFolder.Text = config.Gal.GalMonthlyFolder;
            }
            catch (FileNotFoundException)
            {
                // config.json not yet created — leave fields at defaults
            }
            catch (Exception)
            {
                // Malformed config — leave fields at defaults
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var service = new ConfigurationService();
            Configuration config;
            try { config = service.Load(); }
            catch { config = new Configuration(); }

            config.Vigiagua.Email = TxtEmail.Text;
            config.Vigiagua.Password = TxtPassword.Password;
            config.Vigiagua.RawFilesFolder = TxtRawFolder.Text;
            config.Vigiagua.MonthlyDirectiveFolder = TxtMonthlyDirectiveFolder.Text;
            config.Vigiagua.AnnualDirectiveFolder = TxtAnnualDirectiveFolder.Text;
            config.Vigiagua.AnnualImplementationFolder = TxtAnnualImplementationFolder.Text;
            config.Vigiagua.ControlFolder = TxtControlFolder.Text;
            config.Vigiagua.AnalyzedSamplesFolder = TxtAnalyzedSamplesFolder.Text;

            config.Vigiar.WildfiresFolder = TxtVigiarWildfiresFolder.Text;
            config.Vigiar.RawFilesFolder = TxtVigiarRawFolder.Text;
            config.Vigiar.IqArFolder = TxtVigiarIqArFolder.Text;

            config.Sisam.PastaSisamMestre = TxtSisamMestreFolder.Text;

            config.Vigidesastres.DroughtFolder = TxtVigidesastresDroughtFolder.Text;

            config.Gal.Username = TxtGalUsername.Text;
            config.Gal.Password = TxtGalPassword.Password;
            config.Gal.Module = TxtGalModule.Text;
            config.Gal.Laboratory = TxtGalLaboratory.Text;
            config.Gal.GalMonthlyRawFolder = TxtGalRawFolder.Text;
            config.Gal.GalMonthlyFolder = TxtGalMonthlyFolder.Text;

            service.Save(config);
            MessageBox.Show("Configurações salvas com sucesso!");
        }

        private void BtnVigiaguaSection_Click(object sender, RoutedEventArgs e)
            => ToggleSection(VigiaguaSection);

        private void BtnGalSection_Click(object sender, RoutedEventArgs e)
            => ToggleSection(GalSection);

        private void BtnSisamSection_Click(object sender, RoutedEventArgs e)
            => ToggleSection(SisamSection);

        private void BtnSisamMestreFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtSisamMestreFolder);

        private void BtnVigiarSection_Click(object sender, RoutedEventArgs e)
            => ToggleSection(VigiarSection);

        private void BtnVigiarWildfiresFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigiarWildfiresFolder);

        private void BtnVigiarRawFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigiarRawFolder);

        private void BtnVigiarIqArFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigiarIqArFolder);

        private void BtnVigidesastresSection_Click(object sender, RoutedEventArgs e)
            => ToggleSection(VigidesastresSection);

        private void BtnVigidesastresDroughtFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigidesastresDroughtFolder);

        private void ToggleSection(StackPanel section)
        {
            if (section.MaxHeight == 0)
            {
                section.Visibility = Visibility.Visible;

                // Limpa qualquer animação ativa deixada pelo fechamento anterior —
                // senão o SetValue abaixo é ignorado (animação ativa tem prioridade
                // sobre atribuição direta) e o Measure() continua preso em 0.
                section.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

                section.MaxHeight = double.PositiveInfinity;
                section.Measure(new System.Windows.Size(section.ActualWidth > 0 ? section.ActualWidth : double.PositiveInfinity,
                    double.PositiveInfinity));
                double targetHeight = section.DesiredSize.Height;
                section.MaxHeight = 0;

                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, targetHeight, TimeSpan.FromSeconds(0.25));
                section.BeginAnimation(FrameworkElement.MaxHeightProperty, anim);
            }
            else
            {
                double currentHeight = section.ActualHeight;
                var anim = new System.Windows.Media.Animation.DoubleAnimation(currentHeight, 0, TimeSpan.FromSeconds(0.2));
                anim.Completed += (s, e) => section.Visibility = Visibility.Collapsed;
                section.BeginAnimation(FrameworkElement.MaxHeightProperty, anim);
            }
        }

        private void BtnToggleGalPassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtGalPasswordVisible.Visibility == Visibility.Collapsed)
            {
                TxtGalPasswordVisible.Text = TxtGalPassword.Password;
                TxtGalPasswordVisible.Visibility = Visibility.Visible;
                TxtGalPassword.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtGalPassword.Password = TxtGalPasswordVisible.Text;
                TxtGalPassword.Visibility = Visibility.Visible;
                TxtGalPasswordVisible.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (TxtPasswordVisible.Visibility == Visibility.Collapsed)
            {
                TxtPasswordVisible.Text = TxtPassword.Password;
                TxtPasswordVisible.Visibility = Visibility.Visible;
                TxtPassword.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtPassword.Password = TxtPasswordVisible.Text;
                TxtPassword.Visibility = Visibility.Visible;
                TxtPasswordVisible.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnRawFolderDirective_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtRawFolder);

        private void BtnMonthlyDirectiveFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtMonthlyDirectiveFolder);

        private void BtnAnnualDirectiveFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtAnnualDirectiveFolder);

        private void BtnAnnualImplementationFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtAnnualImplementationFolder);

        private void BtnControlFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtControlFolder);

        private void BtnAnalyzedSamplesFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtAnalyzedSamplesFolder);

        private void BtnGalRawFolderClick(object sender, RoutedEventArgs e)
            => SelectFolder(TxtGalRawFolder);

        private void BtnGalMonthlyFolderClick(object sender, RoutedEventArgs e)
            => SelectFolder(TxtGalMonthlyFolder);

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private void SelectFolder(TextBox field)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                field.Text = dialog.SelectedPath;
        }
    }
}