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
        public const string UrlWildfiresDatabase = "https://terrabrasilis.dpi.inpe.br/queimadas/bdqueimadas/#exportar-dados";

        [JsonPropertyName("PastaArquivosBrutos")]
        public string RawFilesFolder { get; set; } = string.Empty;

        [JsonPropertyName("PastaQueimadas")]
        public string WildfiresFolder { get; set; } = string.Empty;
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

    public class ConfigurationVigidesastres
    {
        // Shapefiles mensais públicos do Monitor de Secas (ANA). Arquivo: {mes}{ano}.zip
        public const string UrlBaseSecas = "https://ana-monitor-secas-files.s3.sa-east-1.amazonaws.com/uploads/mapas/";

        private string _rawFilesFolder = string.Empty;
        private string? _legacyDroughtFolder;

        // ZIPs e pastas extraídas do Monitor de Secas, estrutura {ano}\{mes}{ano}.
        // Compatibilidade: se vazia, usa o valor da chave antiga "PastaSecas" (só leitura).
        [JsonPropertyName("PastaArquivosBrutosSecas")]
        public string RawFilesFolder
        {
            get => string.IsNullOrWhiteSpace(_rawFilesFolder) ? _legacyDroughtFolder ?? string.Empty : _rawFilesFolder;
            set => _rawFilesFolder = value ?? string.Empty;
        }

        // Onde fica o SECAS_MESTRE.xlsx. Sem fallback: precisa ser configurada.
        [JsonPropertyName("PastaSecasMestre")]
        public string MasterFolder { get; set; } = string.Empty;

        // Chave antiga (uma pasta só para tudo). Lida de config.json antigos, nunca gravada:
        // ao salvar, o valor já aparece em PastaArquivosBrutosSecas.
        [JsonPropertyName("PastaSecas")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LegacyDroughtFolder
        {
            get => null;
            set => _legacyDroughtFolder = value;
        }

        // Caminho completo do TopoJSON/GeoJSON dos municípios de RO (campos CD_MUN e NM_MUN).
        // Fica em pasta compartilhada; não é embutido nem baixado pelo programa.
        [JsonPropertyName("ArquivoMalhaMunicipal")]
        public string MunicipalMeshFile { get; set; } = string.Empty;
    }

    public class Configuration
    {
        [JsonPropertyName("ExtensoesBloqueadas")]
        public List<string> BlockedExtensions { get; set; } = [];

        public ConfigurationVigiagua Vigiagua { get; set; } = new ConfigurationVigiagua();
        public ConfigurationGal Gal { get; set; } = new ConfigurationGal();
        public ConfigurationVigiar Vigiar { get; set; } = new ConfigurationVigiar();
        public ConfigurationVigidesastres Vigidesastres { get; set; } = new ConfigurationVigidesastres();
    }
}