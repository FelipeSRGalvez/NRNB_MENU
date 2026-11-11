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
                TxtPasswordVisible.Text = config.Vigiagua.Password;
                TxtRawFolder.Text = config.Vigiagua.RawFilesFolder;
                TxtMonthlyDirectiveFolder.Text = config.Vigiagua.MonthlyDirectiveFolder;
                TxtAnnualDirectiveFolder.Text = config.Vigiagua.AnnualDirectiveFolder;
                TxtAnnualImplementationFolder.Text = config.Vigiagua.AnnualImplementationFolder;
                TxtControlFolder.Text = config.Vigiagua.ControlFolder;
                TxtAnalyzedSamplesFolder.Text = config.Vigiagua.AnalyzedSamplesFolder;

                TxtVigiarWildfiresFolder.Text = config.Vigiar.WildfiresFolder;
                TxtVigiarRawFolder.Text = config.Vigiar.RawFilesFolder;

                TxtVigidesastresRawFolder.Text = config.Vigidesastres.RawFilesFolder;
                TxtVigidesastresMasterFolder.Text = config.Vigidesastres.MasterFolder;
                TxtVigidesastresMeshFile.Text = config.Vigidesastres.MunicipalMeshFile;

                TxtGalUsername.Text = config.Gal.Username;
                TxtGalPassword.Password = config.Gal.Password;
                TxtGalPasswordVisible.Text = config.Gal.Password;
                TxtGalModule.Text = config.Gal.Module;
                TxtGalLaboratory.Text = config.Gal.Laboratory;
                TxtGalRawFolder.Text = config.Gal.GalMonthlyRawFolder;
                TxtGalMonthlyFolder.Text = config.Gal.GalMonthlyFolder;
            }
            catch (FileNotFoundException)
            {
                // config.json ainda não criado — mantém os campos com os valores padrão
            }
            catch (Exception)
            {
                // Configuração malformada — mantém os campos com os valores padrão
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var service = new ConfigurationService();
            Configuration config;
            try { config = service.Load(); }
            catch { config = new Configuration(); }

            config.Vigiagua.Email = TxtEmail.Text;
            config.Vigiagua.Password = ReadPassword(TxtPassword, TxtPasswordVisible);
            config.Vigiagua.RawFilesFolder = TxtRawFolder.Text;
            config.Vigiagua.MonthlyDirectiveFolder = TxtMonthlyDirectiveFolder.Text;
            config.Vigiagua.AnnualDirectiveFolder = TxtAnnualDirectiveFolder.Text;
            config.Vigiagua.AnnualImplementationFolder = TxtAnnualImplementationFolder.Text;
            config.Vigiagua.ControlFolder = TxtControlFolder.Text;
            config.Vigiagua.AnalyzedSamplesFolder = TxtAnalyzedSamplesFolder.Text;

            config.Vigiar.WildfiresFolder = TxtVigiarWildfiresFolder.Text;
            config.Vigiar.RawFilesFolder = TxtVigiarRawFolder.Text;

            config.Vigidesastres.RawFilesFolder = TxtVigidesastresRawFolder.Text;
            config.Vigidesastres.MasterFolder = TxtVigidesastresMasterFolder.Text;
            config.Vigidesastres.MunicipalMeshFile = TxtVigidesastresMeshFile.Text;

            config.Gal.Username = TxtGalUsername.Text;
            config.Gal.Password = ReadPassword(TxtGalPassword, TxtGalPasswordVisible);
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

        private void BtnVigiarSection_Click(object sender, RoutedEventArgs e)
            => ToggleSection(VigiarSection);

        private void BtnVigiarWildfiresFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigiarWildfiresFolder);

        private void BtnVigiarRawFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigiarRawFolder);

        private void BtnVigidesastresSection_Click(object sender, RoutedEventArgs e)
            => ToggleSection(VigidesastresSection);

        private void BtnVigidesastresRawFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigidesastresRawFolder);

        private void BtnVigidesastresMasterFolder_Click(object sender, RoutedEventArgs e)
            => SelectFolder(TxtVigidesastresMasterFolder);

        private void BtnVigidesastresMeshFile_Click(object sender, RoutedEventArgs e)
            => SelectFile(TxtVigidesastresMeshFile,
                "Malha municipal (*.json;*.topojson;*.geojson)|*.json;*.topojson;*.geojson|Todos os arquivos (*.*)|*.*");

        private static void SelectFile(TextBox field, string filter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (!string.IsNullOrWhiteSpace(field.Text) && Directory.Exists(Path.GetDirectoryName(field.Text)))
                dialog.InitialDirectory = Path.GetDirectoryName(field.Text);
            if (dialog.ShowDialog() == true)
                field.Text = dialog.FileName;
        }

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
            => TogglePasswordVisibility(TxtGalPassword, TxtGalPasswordVisible);

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
            => TogglePasswordVisibility(TxtPassword, TxtPasswordVisible);

        // O controle visível no momento contém o último valor digitado pelo usuário.
        private static string ReadPassword(PasswordBox pwd, TextBox visible)
            => visible.Visibility == Visibility.Visible ? visible.Text : pwd.Password;

        // Copia o valor atual para o controle que será exibido antes de alternar,
        // para que os dois controles tenham sempre a mesma senha.
        private static void TogglePasswordVisibility(PasswordBox pwd, TextBox visible)
        {
            string current = ReadPassword(pwd, visible);
            pwd.Password = current;
            visible.Text = current;

            bool showPlainText = visible.Visibility != Visibility.Visible;
            visible.Visibility = showPlainText ? Visibility.Visible : Visibility.Collapsed;
            pwd.Visibility = showPlainText ? Visibility.Collapsed : Visibility.Visible;
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