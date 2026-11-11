# Regras para o Claude neste repositório

## Git (regras permanentes)

- **NUNCA** executar `git commit`, `git push`, `git merge`, `git rebase`, `git tag` nem qualquer comando que altere o histórico. Commits são sempre manuais, feitos pelo usuário.
- **NUNCA** trabalhar direto na `main`.
- Só criar ou trocar de branch quando o usuário mandar — e, mesmo assim, sem commitar.
- Comandos destrutivos (`git reset --hard`, `git clean`, `git checkout -- <arquivo>`, apagar branch) são proibidos, salvo pedido explícito do usuário.
- Ao terminar uma tarefa, mostrar `git status` para o usuário conferir e commitar.

## Projeto

- Ler `README.md` e `docs/` antes de começar.
- Solução `NRNB_MENU.slnx`: `Hud_Principal` (WPF, net10.0-windows), `Conversor_de_Arquivos` (net10.0), `Modulo_Seguranca` (net10.0), `WebAutomation` (net8.0).
- Rodar `dotnet build NRNB_MENU.slnx` ao final de cada etapa e corrigir erros e avisos.
- Excel (EPPlus 8+): chamar `ExcelLicense.Ensure()` antes de usar `ExcelPackage`. `ExcelPackage.LicenseContext` lança exceção em tempo de execução no EPPlus 8.
- Sem segredos no código nem no `config.template.json`.
