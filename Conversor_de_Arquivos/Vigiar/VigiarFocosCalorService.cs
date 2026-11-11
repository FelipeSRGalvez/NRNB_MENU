using System.IO;

namespace Conversor_de_Arquivos
{
    // Coleta os focos de calor do BDQueimadas por semana epidemiológica, organizados em sessões de coleta.
    //
    // Uma sessão é identificada pela última semana encerrada (ex.: semana 43/2026). Dentro da mesma
    // sessão, cada semana baixada com sucesso fica salva como JSON bruto, então uma execução que falha
    // na semana 34 retoma na semana 34. Quando uma nova semana se encerra, começa uma nova sessão e todas
    // as semanas dos anos selecionados são baixadas de novo desde a semana 1 (o INPE revisa semanas
    // anteriores); essas semanas são então substituídas na planilha mestre pelos dados novos.
    public class VigiarFocosCalorService
    {
        private const int DelayBetweenCallsMs = 400;

        // Novas tentativas por semana: 3 tentativas, esperando 5 s e depois 15 s entre elas.
        private const int MaxAttempts = 3;
        private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)];

        // Depois de tantas semanas seguidas esgotarem as tentativas, considera-se o INPE fora do ar:
        // a execução para de baixar (não adianta esperar por todas as semanas restantes) e consolida.
        private const int MaxConsecutiveFailedWeeks = 3;

        private const string SessionFolderPrefix = "sessao_";

        private readonly FocosCalorDataProcessor _processor = new();

        public async Task ExecuteAsync(
            string masterPath,
            string rawFilesFolder,
            List<int> years,
            Func<DateTime, DateTime, Task<string>> fetchWeekJson,
            Func<Task>? resetSession,
            IProgress<string> progress,
            DateTime? referenceDate = null)
        {
            // Só semanas encerradas: a semana atual (incompleta) nunca é baixada.
            var lastClosed = EpidemiologicalWeek.LastClosedWeek(referenceDate ?? DateTime.Now);
            var closedWeeks = BuildClosedWeeksForYears(years, lastClosed);

            if (closedWeeks.Count == 0)
            {
                progress.Report("Nenhuma semana encerrada nos anos selecionados.");
                return;
            }

            string baseFolder = Path.Combine(rawFilesFolder, "FocosCalor");
            string sessionName = $"{SessionFolderPrefix}{lastClosed.Year}_SE{lastClosed.Week:D2}";
            var store = new FocosCalorRawWeekStore(Path.Combine(baseFolder, sessionName));

            var pending = closedWeeks.Where(w => !store.TryRead(w.Year, w.Week, out _)).ToList();
            var totalByYear = closedWeeks.GroupBy(w => w.Year).ToDictionary(g => g.Key, g => g.Count());
            var doneByYear = totalByYear.ToDictionary(
                kv => kv.Key, kv => kv.Value - pending.Count(w => w.Year == kv.Key));
            // "retomando" só quando esta sessão já tinha semanas de uma execução anterior.
            var doneBeforeRun = new Dictionary<int, int>(doneByYear);

            string sessionLabel = $"sessão da semana {lastClosed.Week}/{lastClosed.Year}";
            var failed = new List<EpidemiologicalWeek.WeekInfo>();

            if (pending.Count == 0)
            {
                progress.Report($"A {sessionLabel} já está completa: todas as {closedWeeks.Count} semana(s) foram baixadas.");
            }
            else if (pending.Count == closedWeeks.Count)
            {
                progress.Report($"Nova {sessionLabel} (última semana encerrada). " +
                                $"Baixando as {closedWeeks.Count} semana(s) desde o início.");
            }
            else
            {
                progress.Report($"Retomando a {sessionLabel}: {closedWeeks.Count - pending.Count} de " +
                                $"{closedWeeks.Count} semana(s) já baixada(s). " +
                                $"Continuando a partir da semana {pending[0].Week}/{pending[0].Year}.");
            }

            int consecutiveFailures = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                var w = pending[i];
                string resuming = doneBeforeRun[w.Year] > 0 ? ", retomando" : string.Empty;
                progress.Report($"Semana {w.Week}/{totalByYear[w.Year]} de {w.Year} — {doneByYear[w.Year]} já baixada(s){resuming} " +
                                $"({w.Start:dd/MM/yyyy} a {w.End:dd/MM/yyyy})...");

                if (await FetchWithRetryAsync(w, store, fetchWeekJson, resetSession, progress))
                {
                    doneByYear[w.Year]++;
                    consecutiveFailures = 0;
                }
                else
                {
                    failed.Add(w);
                    if (++consecutiveFailures >= MaxConsecutiveFailedWeeks)
                    {
                        failed.AddRange(pending.Skip(i + 1));
                        progress.Report($"{MaxConsecutiveFailedWeeks} semanas seguidas falharam: o servidor do INPE " +
                                        "parece indisponível. Interrompendo; a próxima execução retoma desta sessão.");
                        break;
                    }
                }

                if (i < pending.Count - 1)
                    await Task.Delay(DelayBetweenCallsMs);
            }

            await ConsolidateAsync(closedWeeks, store, masterPath, progress);

            if (failed.Count > 0)
            {
                string list = string.Join(", ", failed.Select(w => $"{w.Week}/{w.Year}"));
                progress.Report($"Sessão incompleta: {failed.Count} semana(s) pendente(s): {list}. " +
                                "Execute novamente para continuar desta sessão.");
            }
            else
            {
                DeleteOtherSessions(baseFolder, sessionName, progress);
                progress.Report($"Coleta concluída. A {sessionLabel} está completa e a planilha mestre foi atualizada.");
            }
        }

        private static async Task<bool> FetchWithRetryAsync(
            EpidemiologicalWeek.WeekInfo w,
            FocosCalorRawWeekStore store,
            Func<DateTime, DateTime, Task<string>> fetchWeekJson,
            Func<Task>? resetSession,
            IProgress<string> progress)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    string json = await fetchWeekJson(w.Start, w.End);
                    store.Save(w.Year, w.Week, json); // valida antes de marcar a semana como concluída
                    progress.Report($"Semana {w.Week}/{w.Year} baixada.");
                    return true;
                }
                catch (Exception ex)
                {
                    progress.Report($"Semana {w.Week}/{w.Year}: falha na tentativa {attempt}/{MaxAttempts} — " +
                                    FirstLine(ex.Message));

                    if (attempt == MaxAttempts) break;

                    // Uma página/navegador com defeito falharia em todas as tentativas: abre uma nova sessão do navegador.
                    if (resetSession != null)
                    {
                        try { await resetSession(); }
                        catch (Exception resetEx) { progress.Report($"Não foi possível reiniciar o navegador: {FirstLine(resetEx.Message)}"); }
                    }

                    var delay = RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)];
                    progress.Report($"Nova tentativa em {delay.TotalSeconds:0} s...");
                    await Task.Delay(delay);
                }
            }

            progress.Report($"Semana {w.Week}/{w.Year} ficou pendente após {MaxAttempts} tentativas.");
            return false;
        }

        // Grava no mestre todas as semanas baixadas nesta sessão, substituindo os dados antigos
        // dessas semanas. Semanas ainda não baixadas mantêm as linhas anteriores.
        private async Task ConsolidateAsync(
            List<EpidemiologicalWeek.WeekInfo> closedWeeks,
            FocosCalorRawWeekStore store,
            string masterPath,
            IProgress<string> progress)
        {
            var weeks = new List<WeekHotspots>();
            foreach (var w in closedWeeks)
            {
                if (store.TryRead(w.Year, w.Week, out var hotspots))
                    weeks.Add(new WeekHotspots(w.Year, w.Week, w.Start, w.End, hotspots));
            }

            if (weeks.Count == 0)
            {
                progress.Report("Nenhuma semana baixada nesta sessão para gravar na planilha mestre.");
                return;
            }

            progress.Report($"Atualizando {weeks.Count} semana(s) em {Path.GetFileName(masterPath)}...");
            await _processor.UpsertWeeksAsync(weeks, masterPath);
            progress.Report("Planilha mestre atualizada.");
        }

        // Com a sessão atual completa, as pastas de sessões antigas não são mais necessárias.
        private static void DeleteOtherSessions(string baseFolder, string currentSession, IProgress<string> progress)
        {
            if (!Directory.Exists(baseFolder)) return;

            foreach (var dir in Directory.GetDirectories(baseFolder, SessionFolderPrefix + "*"))
            {
                if (string.Equals(Path.GetFileName(dir), currentSession, StringComparison.OrdinalIgnoreCase))
                    continue;
                try { Directory.Delete(dir, recursive: true); }
                catch (Exception ex) { progress.Report($"Não foi possível remover a sessão antiga {Path.GetFileName(dir)}: {FirstLine(ex.Message)}"); }
            }
        }

        private static List<EpidemiologicalWeek.WeekInfo> BuildClosedWeeksForYears(
            List<int> years, EpidemiologicalWeek.WeekInfo lastClosed)
        {
            var list = new List<EpidemiologicalWeek.WeekInfo>();
            foreach (int year in years.OrderBy(y => y))
            {
                var d = EpidemiologicalWeek.WeekStart(year, 1);
                while (true)
                {
                    var w = EpidemiologicalWeek.Calculate(d);
                    if (w.Year > year) break;
                    if (w.Year > lastClosed.Year || (w.Year == lastClosed.Year && w.Week > lastClosed.Week)) break;
                    list.Add(w);
                    d = d.AddDays(7);
                }
            }
            return list;
        }

        private static string FirstLine(string message)
            => (message ?? string.Empty).Split('\n')[0].Trim();
    }
}
