using OfficeOpenXml;

namespace Conversor_de_Arquivos
{
    // Licença do EPPlus 8+. O antigo "ExcelPackage.LicenseContext = LicenseContext.NonCommercial"
    // compila (obsoleto), mas lança LicenseContextPropertyObsoleteException em tempo de execução.
    // Todos os processadores devem chamar ExcelLicense.Ensure() antes de abrir/criar um ExcelPackage.
    public static class ExcelLicense
    {
        // Só letras, números, espaço e hífen (o EPPlus rejeita "/" e outros símbolos).
        public const string Organization = "AGEVISA GTVAM-NRNB";

        private static readonly object Sync = new();
        private static bool _configured;

        public static void Ensure()
        {
            if (_configured) return;
            lock (Sync)
            {
                if (_configured) return;
                ExcelPackage.License.SetNonCommercialOrganization(Organization);
                _configured = true;
            }
        }
    }
}
