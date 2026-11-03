<p align="center">
  <img src="docs/images/icon.png" width="96" alt="SelfControl logo">
</p>

<h1 align="center">SelfControl</h1>

<p align="center">
  <b>Plan your week, get reminded, and keep distractions locked until today's tasks are done.</b>
</p>

<p align="center">
  <a href="https://github.com/tarl3s/SelfControl/releases/latest/download/SelfControl-win-x64.zip">
    <img src="https://img.shields.io/badge/Download-Windows%2010%2F11-000000?style=for-the-badge&logo=windows&logoColor=white" alt="Download for Windows">
  </a>
  <br>
  <sub><a href="https://github.com/tarl3s/SelfControl/releases/latest">Latest release &amp; what's new</a> · <a href="README.pt-BR.md">Leia em português</a></sub>
</p>

---

SelfControl is a small Windows app that starts with your PC and lives in the tray. Black and white, no account, no
cloud: everything stays on your computer.

## What it does

- **Home** — your own page made of blocks: a quote, a picture, today's routine, your next reminders, today's tasks and
  whether things are locked. Add, remove and reorder them.
- **Routine** — a weekly grid (Monday to Sunday). Click to add an activity (1 hour by default), drag its top or bottom
  edge to make it longer or shorter, drag it to another time or day. Keep several routines and pick which one is
  active, or schedule one for specific weeks.
- **Reminders** — Windows notifications on a date, or repeating on chosen weekdays ("Mon–Fri 21:30: sleep").
  One-time reminders delete themselves after they ring.
- **Task locks** — write today's tasks (just for today, or scheduled: every Tuesday, weekends…). While any of them is
  unchecked, the programs and sites on your block list are locked:
  - opening one shows *"Finish your tasks before opening"* over it; **Close** closes the program;
  - **Breaktime** unlocks just that program or site for the minutes you pick; when it ends, a second warning only
    lets you close it;
  - pick programs from your installed apps (like Windows' *Installed apps* list) or any `.exe`.
- **Hourly breaks** — after each hour at the computer it suggests guided stretches or an eye exercise (a ball to
  follow with your eyes: side to side, up and down, circles, near and far).
- **Usage** — a bar chart of your time at the computer per day (this week, one of the 3 before, or the last 4 weeks)
  and the programs and sites you used most (sites need the browser extension). It and the
  hourly break count only real use: not while the screen is locked, the PC sleeps, or after 15 minutes without input.
- **Ctrl+Z** undoes the last change, **Delete** removes the selected routine block, **Ctrl+C / Ctrl+V** copy and paste it, and you can export / import all
  your data in Settings. English and Portuguese.

It's honour-based on purpose: SelfControl never checks whether you really did a task, and it's easy to quit. It's
there to help you, not to fight you.

## Get started

1. **[Download SelfControl](https://github.com/tarl3s/SelfControl/releases/latest/download/SelfControl-win-x64.zip)**
   (Windows 10/11, 64-bit), unzip it anywhere and open **`SelfControl.exe`**. Nothing else to install.
   - Windows may say *"Windows protected your PC"* because the app isn't signed: click **More info → Run anyway**.
2. It now starts with Windows (you can turn that off in **⚙ Settings**) and checks for new versions by itself.
3. To block **websites**, install the browser extension (Brave, Chrome, Edge, Opera):
   open `brave://extensions` (or `chrome://extensions`, `edge://extensions`), turn on **Developer mode**, click
   **Load unpacked** and choose the `extension` folder from the download.

Your data lives in `%APPDATA%\SelfControl\data.json`; updates never touch it.

## Build from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) only (no Visual Studio).

```bash
dotnet test                                   # unit + UI tests
dotnet run --project src/SelfControl.Desktop  # run it
```

`tools/Start-SelfControl.ps1` rebuilds `publish/SelfControl.exe` whenever the code changed and then opens it.
`tools/Publish-Release.ps1` makes a GitHub release. The code is split so an Android version can reuse it:
`SelfControl.Core` (rules, no UI), `SelfControl.App` (Avalonia UI), `SelfControl.Desktop` (Windows pieces).

## License

MIT
