using System.Text.Json.Serialization;

namespace Modulo_Seguranca
{
    public class ConfigurationVigiagua
    {
        public const string UrlLogin = "https://sisagua.saude.gov.br/sisagua/paginaExterna.jsf";
        public const string UrlMonthlyDirective = "https://sisagua.saude.gov.br/sisagua/paginas/seguro/relatorioDiretrizNacional/relDiretrizNacionalParametrosBasicos.jsf?faces-redirect=true";

        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("Senha")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("PastaArquivosBrutos")]
        public string RawFilesFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaCumprimentoDaDiretrizMensal")]
        public string MonthlyDirectiveFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaCumprimentoDaDiretrizAnual")]
        public string AnnualDirectiveFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaImplementacaoAnual")]
        public string AnnualImplementationFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaControle")]
        public string ControlFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaAmostrasAnalisadas")]
        public string AnalyzedSamplesFolder { get; set; } = string.Empty;
    }

    public class ConfigurationVigiar
    {
        public const string UrlIqAr = "https://shiny.icict.fiocruz.br/alertarsaude/";
        public const string UrlWildfiresDatabase = "https://terrabrasilis.dpi.inpe.br/queimadas/bdqueimadas/#exportar-dados";

        [JsonPropertyName("PastaArquivosBrutos")]
        public string RawFilesFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaQueimadas")]
        public string WildfiresFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaIqAr")]
        public string IqArFolder { get; set; } = string.Empty;
    }

    public class ConfigurationGal
    {
        public const string UrlLogin = "https://gal.rondonia.sus.gov.br";

        [JsonPropertyName("Usuario")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("Senha")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("Modulo")]
        public string Module { get; set; } = string.Empty;

        [JsonPropertyName("Laboratorio")]
        public string Laboratory { get; set; } = string.Empty;

        [JsonPropertyName("PastaBrutaRelatoriosMensalGal")]
        public string GalMonthlyRawFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaRelatoriosMensalGal")]
        public string GalMonthlyFolder { get; set; } = string.Empty;
    }

    public class ConfigurationSisam
    {
        [JsonPropertyName("PastaArquivosBrutosSisam")]
        public string RawFilesFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaSisamMestre")]
        public string PastaSisamMestre { get; set; } = string.Empty;
    }

    public class ConfigurationVigidesastres
    {
        // Shapefiles mensais públicos do Monitor de Secas (ANA). Arquivo: {mes}{ano}.zip
        public const string UrlBaseSecas = "https://ana-monitor-secas-files.s3.sa-east-1.amazonaws.com/uploads/mapas/";

        [JsonPropertyName("PastaSecas")]
        public string DroughtFolder { get; set; } = string.Empty;
    }

    public class Configuration
    {
        [JsonPropertyName("ExtensoesBloqueadas")]
        public List<string> BlockedExtensions { get; set; } = [];

        public ConfigurationVigiagua Vigiagua { get; set; } = new ConfigurationVigiagua();
        public ConfigurationGal Gal { get; set; } = new ConfigurationGal();
        public ConfigurationVigiar Vigiar { get; set; } = new ConfigurationVigiar();
        public ConfigurationSisam Sisam { get; set; } = new ConfigurationSisam();
        public ConfigurationVigidesastres Vigidesastres { get; set; } = new ConfigurationVigidesastres();
    }
}