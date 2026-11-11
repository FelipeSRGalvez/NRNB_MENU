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

Um único botão, **Coletar e Processar**, executa duas fases em sequência (orquestradas por `VigidesastresSecasPipeline`, que só reaproveita os dois services):

1. **Coleta** — baixa e extrai os ZIPs (esta seção). Não lê o shapefile nem depende da malha municipal.
2. **Processamento** — cruza os mapas extraídos com a malha municipal e grava a planilha mestre (seção "processamento para Power BI", abaixo).

Regras do botão:
- O processamento roda mesmo que alguns meses tenham dado erro na coleta: usa o que já está extraído, e os erros aparecem no resumo.
- Se a coleta falhar por completo (ex.: sem rede) **e** não houver nenhum mês extraído para os anos escolhidos, a tela informa e o processamento **não** é executado.
- O botão fica desabilitado durante a execução (sem execuções simultâneas).
- Idempotente: meses já baixados e meses já gravados na planilha são pulados.
- O resumo mostra as duas fases: baixados, pulados, não publicados e erros (coleta); processados, já na planilha, sem cobertura de RO, não baixados e erros (processamento).

A telemetria da ANA continua sendo tratada no Power Query e não faz parte deste módulo.

### Configuração

Em **Configurações → Vigidesastres**:
- **Pasta Arquivos Brutos Secas** (`Vigidesastres.PastaArquivosBrutosSecas`) — ZIPs e pastas extraídas, estrutura `{ano}\{mes}{ano}`. Usada pela coleta (gravação) e pelo processamento (leitura dos meses).
- **Pasta Secas Mestre** (`Vigidesastres.PastaSecasMestre`) — onde o `SECAS_MESTRE.xlsx` é lido e gravado. **Sem fallback**: se estiver vazia, a tela pede para configurar.
- **Arquivo Malha Municipal** (`Vigidesastres.ArquivoMalhaMunicipal`) — caminho completo do TopoJSON/GeoJSON dos municípios de RO; usado só pelo processamento.

As duas pastas e o arquivo da malha são verificados (`PathVerifier.Verify` / `VerifyFiles`) antes de começar.

**Compatibilidade:** versões anteriores usavam uma única chave, `PastaSecas`. Se o `config.json` ainda tiver só essa chave, o valor é usado como pasta de brutos; ao salvar as Configurações, só as chaves novas são gravadas. O `SECAS_MESTRE.xlsx` que ficava na antiga `PastaSecas` precisa ser **movido para a Pasta Secas Mestre** — senão será gerado de novo do zero.

### Funcionamento

1. Na tela **Vigidesastres → Monitor de Secas**, selecione um ou mais anos (a partir de 2021) e clique em **Coletar e Processar**.
2. Para cada ano, o programa percorre os meses já encerrados (o mês corrente nunca está publicado).
3. Para cada mês:
   - **Já baixado** (ZIP e pasta extraída existem): pula.
   - **HTTP 404**: mês ainda não publicado pela ANA; ignorado sem erro e tentado de novo na próxima execução.
   - **Caso contrário**: baixa o ZIP, valida e extrai.
4. Ao final da coleta, o programa passa ao processamento (ver regras acima).

A ANA costuma publicar o mapa de um mês por volta da metade do mês seguinte (ex.: setembro/2026 publicado em 16/10/2026).

### Estrutura gerada

```
PastaArquivosBrutosSecas\
  2026\
    setembro2026.zip      <- ZIP original
    setembro2026\         <- conteúdo extraído
      setembro26.shp / .dbf / .shx / .prj / .cpg / .qml
      setembro26_isolado.shp ...
      202609_MS_IMPACTOS.shp ...
      202609_MS_IMPACTOS_TIPO.shp ...
      Estados_monitor\UF_Brasil.shp ...
```

### Conteúdo do ZIP

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

Log de depuração da última execução (coleta e processamento): `%TEMP%\nrnb_vigidesastres_debug.log`.

## Monitor de Secas (processamento para Power BI)

Segunda fase do botão **Coletar e Processar**, com os mesmos anos selecionados. Cruza os mapas **já baixados e extraídos** (pasta de brutos) com a malha municipal de Rondônia e grava `PastaSecasMestre\SECAS_MESTRE.xlsx`.

### Malha municipal

- Arquivo TopoJSON ou GeoJSON em pasta compartilhada, informado em **Configurações → Vigidesastres → Arquivo Malha Municipal**. O programa **não embute nem baixa** a malha (nem usa API do IBGE). Se o arquivo não existir (ex.: a pasta mudou de lugar), a tela avisa e não processa.
- Formato detectado pelo conteúdo (`"type": "Topology"` = TopoJSON; TopoJSON quantizado com `transform` é suportado).
- Campos obrigatórios: `CD_MUN` (código IBGE, 7 dígitos) e `NM_MUN`. Só entram feições com `CD_MUN` iniciando em `11` (Rondônia).
- Sem CRS declarado: coordenadas tratadas como lon/lat SIRGAS 2000 / WGS 84.
- Arquivo atual: objeto `RO_Municipios_2025`, 52 polígonos. Conferência: a área calculada bate com o `AREA_KM2` do IBGE (erro máximo 0,01% por município).

### Quais meses são processados

1. Só meses encerrados dos anos selecionados.
2. **Antes de agosto/2022**: registrado como "sem cobertura de RO" e pulado (constante `InicioCoberturaRondonia`).
3. Mês **já gravado** na planilha (mesmo Ano + Mês): pulado (idempotente).
4. Mês **não baixado/extraído** em `PastaArquivosBrutosSecas\{ano}\{mes}{ano}\` (não publicado ou erro na coleta): avisado e pulado.

### Camada usada e categorias

O localizador procura os `.shp` da pasta do mês (inclusive subpastas), ignorando `Estados_monitor`, `UF_Brasil` e `*_MS_IMPACTOS*` (por nome de pasta **e** de arquivo — em ago/2022 o `Estados_monitor.shp` fica na raiz):

| Período | Camada | Tipo | Exemplos de nome |
|---|---|---|---|
| mai/2025 em diante | `{mes}{aa}_isolado.shp` | **Isolada** — polígonos disjuntos, um por categoria; a união cobre o Brasil inteiro | `maio25_isolado.shp`, `outubro2025/outubro25_isolado.shp` |
| ago/2022 a abr/2025 | `{mes}{aa}.shp` | **Cumulativa** — polígonos aninhados (`si ⊇ s0 ⊇ s1 ⊇ …`) | `agosto2022/agosto22.shp`, `abril25.shp` |

Na camada cumulativa o % de cada categoria é obtido por diferença: `%(k) = %cumulativo(k) − %cumulativo(k+1)`. Validação em set/2026 (mês que tem as duas camadas): diferença de 0,0000 p.p. entre os dois caminhos nos 52 municípios.

Campo `uf_codigo` (normalizado para minúsculas). Valores encontrados de 2022 a 2026: `si`, `s0`, `s1`, `s2`, `s3`, `s4`. Valor desconhecido = erro no mês (nada é gravado).

| Código | Texto na planilha |
|---|---|
| `si` | Sem seca |
| `s0` | Fraca |
| `s1` | Moderada |
| `s2` | Grave |
| `s3` | Extrema |
| `s4` | Excepcional |

**`si` = Sem seca:** na camada isolada de set/2026, `si` cobre 100% de PR e RS e 99,6% de SC, e completa exatamente a área do Brasil junto com as categorias de seca. Na camada cumulativa, `si` é a área monitorada inteira.

### Cálculo

- Geometrias inválidas são corrigidas antes de qualquer operação (`GeometryFixer`, o MakeValid do NTS). Polígonos com anéis malformados (ex.: `si` de jan/2026) são lidos como vieram e corrigidos, em vez de descartados.
- As camadas nacionais são recortadas na extensão da malha antes de projetar.
- Áreas em **SIRGAS 2000 / Brazil Albers** (área igual; parâmetros IBGE: lat. origem −12°, meridiano central −54°, paralelos padrão −2° e −22°), nunca em graus. A **Policônica (EPSG:5880) não é de área igual** — superestima a área de RO em ~1,2%; frente à Albers a diferença nos percentuais foi de até 0,13 p.p. em set/2026, sem mudar nenhuma categoria. Ela continua disponível em `GeoProjections`.
- Interseções com OverlayNG robusto (fallback em `Buffer(0)` se houver erro de topologia).
- Por município e mês:
  - `Pct_*` = % da área do município em cada categoria (0–100, 2 casas). A soma fica a ≤ 0,12 p.p. de 100 (bordas entre a malha IBGE e os polígonos da ANA).
  - `Categoria_Maxima` = a categoria mais grave com interseção **≥ 1% da área do município** (`LimiarCategoriaMaximaPct`, em `SecasMunicipalAnalyzer`). Evita que lascas de borda definam a categoria.
  - `Categoria_Predominante` = a categoria de maior área.
  - Se nenhuma categoria atingir o limiar / tiver área: `Sem dados`.

### Saída: `PastaSecasMestre\SECAS_MESTRE.xlsx`

Formato longo (uma linha por município e mês; 52 linhas por mês):

| Coluna | Conteúdo |
|---|---|
| Município | `NM_MUN` da malha (com acento) |
| Código IBGE | `CD_MUN` (texto, 7 dígitos) |
| Regional | `RondoniaRegionais` |
| Ano | número |
| Mês | número (1–12) |
| Data_Referencia | primeiro dia do mês (`dd/MM/yyyy`) |
| Categoria_Maxima | texto da tabela acima |
| Categoria_Predominante | texto da tabela acima |
| Pct_SemSeca, Pct_S0 … Pct_S4 | % da área (0–100) |


### Observação sobre meses repetidos

Valores iguais em meses seguidos para um município são esperados: os arquivos da ANA mudam todo mês, mas a mudança pode ser só em outros estados ou em outros municípios de RO. Ex.: mai×jun/2026 têm diferença zero dentro de RO; mar×abr/2026 e jun×jul/2026 mudam dentro de RO, mas longe de Porto Velho (verificado por SHA-256 dos arquivos e diferença simétrica das geometrias).

### Arquivos

Download:
- `Conversor_de_Arquivos/Vigidesastres/VigidesastresSecasService.cs` — lista de meses, download HTTP, orquestração.
- `Conversor_de_Arquivos/Vigidesastres/SecasDataProcessor.cs` — validação e extração segura do ZIP.
- `Conversor_de_Arquivos/Vigidesastres/VigidesastresSecasModels.cs` — modelos de mês e resumo.

Botão único:
- `Conversor_de_Arquivos/Vigidesastres/VigidesastresSecasPipeline.cs` — orquestrador fino: coleta → processamento.

Processamento:
- `Conversor_de_Arquivos/Vigidesastres/VigidesastresSecasProcessingService.cs` — orquestração por mês, cobertura de RO, idempotência.
- `Conversor_de_Arquivos/Vigidesastres/SecasCamadaLocator.cs` — localiza a camada isolada ou cumulativa.
- `Conversor_de_Arquivos/Vigidesastres/SecasMalhaMunicipal.cs` — carrega e valida a malha.
- `Conversor_de_Arquivos/Vigidesastres/SecasMunicipalAnalyzer.cs` — interseções, desacumulação, categorias.
- `Conversor_de_Arquivos/Vigidesastres/SecasMestreDataProcessor.cs` — grava `SECAS_MESTRE.xlsx` (EPPlus).
- `Conversor_de_Arquivos/Vigidesastres/SecasProcessamentoModels.cs` — categorias, resultados e resumo.

Rotinas geoespaciais genéricas (sem dependência de nenhum módulo), em `Conversor_de_Arquivos/Geo/`:
- `VectorFileReader.cs` — TopoJSON/GeoJSON → NTS.
- `ShapefileFeatureReader.cs` — shapefile → feições.
- `GeoReprojector.cs`, `GeoProjections.cs` — reprojeção (ProjNet) e WKT das projeções brasileiras.
- `GeometryTools.cs` — MakeValid, extração de polígonos, interseção robusta.
- `GeoFeature.cs` — feição genérica.

Comuns:
- `Hud_Principal/Views/Vigidesastres/VigidesastresSecasView.xaml(.cs)` — tela (download e processamento).
- `Modulo_Seguranca/Configuracao.cs` — `ConfigurationVigidesastres` (`PastaArquivosBrutosSecas`, `PastaSecasMestre`, `ArquivoMalhaMunicipal`; lê a chave antiga `PastaSecas`).
- `Modulo_Seguranca/PathVerifier.cs` — `VerifyFiles` para checar o arquivo da malha.

Pacotes: `NetTopologySuite`, `NetTopologySuite.IO.Esri.Shapefile`, `ProjNET`.
