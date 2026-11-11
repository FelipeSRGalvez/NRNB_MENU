namespace Conversor_de_Arquivos.Vigidesastres
{
    public sealed record SecasPipelineSummary(
        SecasRunSummary Collection,
        SecasProcessSummary? Processing,
        string? ProcessingSkippedReason);

    // Orquestrador fino do botão "Coletar e Processar":
    // (1) coleta (download/extração) e (2) processamento para SECAS_MESTRE.xlsx.
    // Toda a lógica fica nos services existentes; aqui só se decide se a fase 2 roda.
    public class VigidesastresSecasPipeline
    {
        public async Task<SecasPipelineSummary> ExecuteAsync(
            string rawFilesFolder,
            string masterFolder,
            string meshPath,
            string baseUrl,
            List<int> years,
            IProgress<string> progress)
        {
            progress.Report("=== Fase 1/2: coleta ===");
            var collection = await new VigidesastresSecasService()
                .ExecuteAsync(rawFilesFolder, baseUrl, years, progress);

            // Meses disponíveis para processar = baixados agora + já existentes (pulados).
            // Erros em alguns meses não impedem o processamento do que existe.
            if (collection.Downloaded + collection.Skipped == 0)
            {
                string reason = collection.Errors > 0
                    ? "A coleta falhou em todos os meses e não há nenhum mês extraído para os anos selecionados " +
                      "(verifique a conexão). Processamento não executado."
                    : "Nenhum mês publicado/extraído para os anos selecionados. Processamento não executado.";
                progress.Report(reason);
                return new SecasPipelineSummary(collection, null, reason);
            }

            progress.Report("=== Fase 2/2: processamento ===");
            var processing = await new VigidesastresSecasProcessingService()
                .ExecuteAsync(rawFilesFolder, masterFolder, meshPath, years, progress);

            return new SecasPipelineSummary(collection, processing, null);
        }
    }
}
