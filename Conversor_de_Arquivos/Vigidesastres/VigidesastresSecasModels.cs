namespace Conversor_de_Arquivos.Vigidesastres
{
    public enum SecasMonthStatus
    {
        Downloaded,
        Skipped,
        NotPublished,
        Error
    }

    // Um mês do Monitor de Secas. Localmente o nome é sempre {mes}{ano}, ex.: setembro2026.
    public record SecasMonth(int Year, int Month, string MonthName)
    {
        public string FileStem => $"{MonthName}{Year}";

        // Nomes tentados na URL, em ordem. A ANA publica alguns meses com ano de 2 dígitos
        // (ex.: janeiro21, fevereiro22) e a maioria com 4 dígitos.
        public IReadOnlyList<string> RemoteStems =>
            [FileStem, $"{MonthName}{Year % 100:D2}"];
    }

    public record SecasMonthResult(SecasMonth Month, SecasMonthStatus Status, string? Detail = null);

    public class SecasRunSummary
    {
        public List<SecasMonthResult> Results { get; } = [];

        public int Downloaded   => Results.Count(r => r.Status == SecasMonthStatus.Downloaded);
        public int Skipped      => Results.Count(r => r.Status == SecasMonthStatus.Skipped);
        public int NotPublished => Results.Count(r => r.Status == SecasMonthStatus.NotPublished);
        public int Errors       => Results.Count(r => r.Status == SecasMonthStatus.Error);
    }
}
