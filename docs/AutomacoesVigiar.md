# Automações Vigiar V1

## Focos de Calor por semana

O programa abre um navegador headless via Playwright e acessa **https://terrabrasilis.dpi.inpe.br/queimadas/bdqueimadas/#graficos**.

Filtros aplicados uma única vez na inicialização:
- Continente: América do Sul
- País: Brasil
- Estado: Rondônia

Filtros aplicados por semana:
- Data de início da semana
- Data de fim da semana

Após aplicar o filtro, expande o acordion `#box-firesByCity` (verifica estado atual antes, evitando recolher se já estiver aberto) e intercepta a resposta da requisição `firesByCity` via `RouteAsync`, extraindo os dados direto do payload.

Os dados são organizados em planilha Excel com as colunas:
- Município
- Regional
- Ano
- Semana
- Data de início da semana
- Data de fim da semana
- Quantidade de focos de calor

Processamento inicia em 2021 até o ano atual. Usa o calendário epidemiológico (SINAN) como referência para segmentação de semanas.

---

Todos os arquivos finais seguem formato longo (poucas colunas, muitas linhas), facilitando manipulação de dados e análises futuras no Power BI.rão no formato longo (Poucas colunas e muitas linhas), facilitando a manipulação dos dados e análises futuras no Power BI.