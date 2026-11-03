<p align="center">
  <img src="docs/images/icon.png" width="96" alt="Logo do SelfControl">
</p>

<h1 align="center">SelfControl</h1>

<p align="center">
  <b>Planeje sua semana, receba lembretes e deixe as distrações bloqueadas até terminar as tarefas do dia.</b>
</p>

<p align="center">
  <a href="https://github.com/tarl3s/SelfControl/releases/latest/download/SelfControl-win-x64.zip">
    <img src="https://img.shields.io/badge/Baixar-Windows%2010%2F11-000000?style=for-the-badge&logo=windows&logoColor=white" alt="Baixar para Windows">
  </a>
  <br>
  <sub><a href="https://github.com/tarl3s/SelfControl/releases/latest">Última versão e novidades</a> · <a href="README.md">Read in English</a></sub>
</p>

---

O SelfControl é um app pequeno para Windows que abre junto com o PC e fica na bandeja. Preto e branco, sem conta,
sem nuvem: tudo fica no seu computador.

## O que ele faz

- **Início** — uma página sua, montada em blocos: uma frase, uma imagem, a rotina de hoje, os próximos lembretes, as
  tarefas de hoje e se está bloqueado. Adicione, remova e reordene.
- **Rotina** — uma grade semanal (segunda a domingo). Clique para criar uma atividade (1 hora por padrão), puxe a
  borda de cima ou de baixo para aumentar ou diminuir, arraste para outro horário ou dia. Tenha várias rotinas e
  escolha qual está ativa, ou agende uma para semanas específicas.
- **Eventos** — notificações do Windows numa data, ou repetindo em dias da semana ("Seg–Sex 21:30: dormir"). Os que
  acontecem uma vez só se apagam sozinhos depois de tocar.
- **Bloqueio por tarefas** — escreva as tarefas do dia (só hoje, ou agendadas: toda terça, fim de semana…). Enquanto
  alguma estiver desmarcada, os programas e sites da sua lista ficam bloqueados:
  - ao abrir um, aparece *"Termine suas tarefas antes de abrir"* por cima; **Fechar** fecha o programa;
  - **Breaktime** libera só aquele programa ou site pelos minutos que você escolher; quando acaba, um segundo aviso
    só deixa fechar;
  - escolha os programas da lista de apps instalados (igual à tela *Aplicativos instalados* do Windows) ou qualquer `.exe`.
- **Pausas a cada hora** — depois de cada hora no computador ele sugere alongamentos guiados ou um treino dos olhos
  (uma bolinha para seguir com os olhos: lados, cima e baixo, círculos, perto e longe).
- **Tempo de uso** — a barra lateral mostra quanto tempo você usou o computador hoje (passe o mouse para ver os últimos
  7 dias). Ele e a pausa de cada hora só contam uso real: não contam com a tela bloqueada, o PC suspenso ou 15 minutos sem uso.
- **Ctrl+Z** desfaz a última mudança, **Delete** apaga o bloco selecionado da rotina, **Ctrl+C / Ctrl+V** copiam e colam, e dá para exportar / importar
  todos os dados nas Configurações. Em português e inglês.

Ele é na base da confiança, de propósito: o SelfControl nunca confere se você fez a tarefa, e é fácil de fechar. Ele
está aí para te ajudar, não para brigar com você.

## Como começar

1. **[Baixe o SelfControl](https://github.com/tarl3s/SelfControl/releases/latest/download/SelfControl-win-x64.zip)**
   (Windows 10/11, 64 bits), descompacte onde quiser e abra o **`SelfControl.exe`**. Não precisa instalar mais nada.
   - O Windows pode dizer *"O Windows protegeu o computador"* porque o app não é assinado: clique em
     **Mais informações → Executar assim mesmo**.
2. A partir daí ele abre com o Windows (dá para desligar em **⚙ Configurações**) e procura versões novas sozinho.
3. Para bloquear **sites**, instale a extensão do navegador (Brave, Chrome, Edge, Opera): abra `brave://extensions`
   (ou `chrome://extensions`, `edge://extensions`), ligue o **Modo do desenvolvedor**, clique em
   **Carregar sem compactação** e escolha a pasta `extension` do download.

Seus dados ficam em `%APPDATA%\SelfControl\data.json`; as atualizações nunca mexem neles.

## Compilar do código

Só precisa do [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (sem Visual Studio).

```bash
dotnet test                                   # testes
dotnet run --project src/SelfControl.Desktop  # abrir
```

## Licença

MIT
