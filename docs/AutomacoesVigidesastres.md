# Automações Vigidesastres V1

## Monitor de Secas (download mensal)

O programa baixa os mapas mensais do **Monitor de Secas** da ANA, publicados como shapefile compactado em arquivos ZIP públicos:

```
https://ana-monitor-secas-files.s3.sa-east-1.amazonaws.com/uploads/mapas/{mes}{ano}.zip
```

O nome do mês vai em minúsculas e sem acento: `janeiro`, `fevereiro`, `marco`, `abril`, `maio`, `junho`, `julho`, `agosto`, `setembro`, `outubro`, `novembro`, `dezembro`. Exemplos: `setembro2026.zip`, `marco2026.zip`.

O ano nem sempre tem 4 dígitos. Para cada mês o programa tenta `{mes}{ano}` e, em 404, `{mes}{aa}`; só considera o mês não publicado se as duas derem 404. Localmente o nome é sempre `{mes}{ano}`, qualquer que seja a variante encontrada.

Variantes por ano (levantamento de 11/2026):

| Ano | `{mes}{ano}` | `{mes}{aa}` | `{Mes}{aa}` (inicial maiúscula) |
|---|---|---|---|
| 2014 | — | — | jul–dez (jan–jun não existem) |
| 2015 | — | ago, set | todos |
| 2016 | — | jan–mai, jul, set | todos |
| 2017 | — | — | todos |
| 2018 | — | fev–dez | jan |
| 2019 | abr–dez | jan–mar, ago | — |
| 2020 | jan–out | nov, dez | — |
| 2021 | abr–out, dez | jan, fev, mar, nov | — |
| 2022 | todos menos fev | fev | — |
| 2023+ | todos | — | — |

A variante `{Mes}{aa}` só ocorre até jan/2018 e não é implementada, pois a lista de anos começa em 2021.

**Cobertura espacial:** os mapas só passam a cobrir Rondônia a partir de **agosto/2022** (antes disso a área monitorada termina a leste do estado; de 2014 a 2020 cobria só Nordeste/Sudeste parcial). A lista de anos começa em 2021; os meses de **2021 e jan–jul/2022** são baixados normalmente, mas **não contêm Rondônia**.

Não usa navegador (Playwright), API autenticada nem credenciais — apenas `HttpClient` e `System.IO.Compression`.

### Escopo

Este módulo **somente baixa e extrai** os ZIPs. Ele não lê, não converte nem filtra o shapefile, não gera planilha e não depende da malha municipal. O processamento (cruzamento com a malha de Rondônia, planilha mestre) será definido depois.

A telemetria da ANA continua sendo tratada no Power Query e não faz parte deste módulo.

### Configuração

Em **Configurações → Vigidesastres**, defina a **Pasta Secas** (`Vigidesastres.PastaSecas` no `config.json`).

### Funcionamento

1. Na tela **Vigidesastres → Monitor de Secas**, selecione um ou mais anos (a partir de 2021) e clique em **Iniciar download**.
2. Para cada ano, o programa percorre os meses já encerrados (o mês corrente nunca está publicado).
3. Para cada mês:
   - **Já baixado** (ZIP e pasta extraída existem): pula.
   - **HTTP 404**: mês ainda não publicado pela ANA; ignorado sem erro e tentado de novo na próxima execução.
   - **Caso contrário**: baixa o ZIP, valida e extrai.
4. Ao final, a tela mostra o resumo: baixados, pulados, não publicados e erros.

A ANA costuma publicar o mapa de um mês por volta da metade do mês seguinte (ex.: setembro/2026 publicado em 16/10/2026).

### Estrutura gerada

```
PastaSecas\
  2026\
    setembro2026.zip      <- ZIP original
    setembro2026\         <- conteúdo extraído
      setembro26.shp / .dbf / .shx / .prj / .cpg / .qml
      setembro26_isolado.shp ...
      202609_MS_IMPACTOS.shp ...
      202609_MS_IMPACTOS_TIPO.shp ...
      Estados_monitor\UF_Brasil.shp ...
```

### Conteúdo do ZIP (referência para o processamento futuro)

Inspeção de `setembro2026.zip`:

| Camada | Geometria | Campos | CRS |
|---|---|---|---|
| `{mes}{aa}.shp` | Polígono | `uf_codigo` (C) = categoria (`si`, `s0`, `s1`, `s2`...), `Valor` (N) = 0, 1, 2, 3... | WGS 84 |
| `{mes}{aa}_isolado.shp` | Polígono | mesmos campos | WGS 84 |
| `{aaaamm}_MS_IMPACTOS.shp` | Linha | `FID` | WGS 84 |
| `{aaaamm}_MS_IMPACTOS_TIPO.shp` | Ponto | `Type` (C, L, CL) | WGS 84 |
| `Estados_monitor/UF_Brasil.shp` | Polígono | `CD_UF`, `NM_UF`, `SIGLA_UF`, ... | SIRGAS 2000 |

Observações:
- Os polígonos de seca cobrem o Brasil inteiro, com um registro por categoria; não há camada municipal no ZIP.
- O campo `uf_codigo` **não** é código de UF: ele guarda a categoria de seca.
- `UF_Brasil.cpg` declara UTF-8, mas os dados estão em Latin-1.
- Os nomes dos arquivos internos variam por mês (`{mes}{aa}`, `{aaaamm}`); localizar as camadas por padrão, não por nome fixo.

### Segurança e robustez

- **Assinatura ZIP**: o arquivo baixado só é aceito se começar com `PK\x03\x04`.
- **Zip-slip**: entradas cujo caminho resolvido saia da pasta de destino são rejeitadas e o mês é marcado como erro.
- **Limites**: no máximo 2000 entradas e 1 GB descompactado por ZIP.
- **Gravação atômica**: o download vai para `.zip.part` e a extração para `{mes}{ano}.tmp\`; só são renomeados para o nome final após sucesso. Uma execução interrompida nunca deixa um mês "parecendo completo".
- **ZIP inválido**: se a extração falhar, o ZIP é apagado para ser baixado de novo na próxima execução.
- Erro em um mês não interrompe os demais.

Log de depuração da última execução: `%TEMP%\nrnb_vigidesastres_debug.log`.

### Arquivos

- `Conversor_de_Arquivos/Vigidesastres/VigidesastresSecasService.cs` — lista de meses, download HTTP, orquestração.
- `Conversor_de_Arquivos/Vigidesastres/SecasDataProcessor.cs` — validação e extração segura do ZIP.
- `Conversor_de_Arquivos/Vigidesastres/VigidesastresSecasModels.cs` — modelos de mês e resumo.
- `Hud_Principal/Views/Vigidesastres/VigidesastresSecasView.xaml(.cs)` — tela.
- `Modulo_Seguranca/Configuracao.cs` — `ConfigurationVigidesastres` (`PastaSecas`).
