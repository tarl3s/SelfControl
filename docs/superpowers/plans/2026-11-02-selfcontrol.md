# SelfControl Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray app (Avalonia) with Home / Rotina / Eventos / Tarefas screens, toast reminders, and honour-based app + site blocking with Breaktime.

**Architecture:** All rules live in a pure `SelfControl.Core` library (no UI, no Windows API, clock injected) so they are unit-tested and reused by the future Android head. `SelfControl.App` holds the Avalonia views (net8.0, portable). `SelfControl.Desktop` is the Windows exe that wires Windows implementations (process watcher, toasts, registry autostart, localhost bridge for the browser extension). `extension/` is a Manifest V3 Chromium extension.

**Tech Stack:** .NET 8 SDK 8.0.425, Avalonia 11.3.22 (+ Desktop, Themes.Fluent, Headless.XUnit), xUnit 2.5.3, System.Text.Json, `Microsoft.Toolkit.Uwp.Notifications` (toasts; only NuGet beyond Avalonia/xUnit), MV3 extension in plain JS.

**Spec:** `docs/superpowers/specs/2026-11-02-selfcontrol-design.md`

## Global Constraints

- No Visual Studio; build with `dotnet` CLI only. Minimal NuGet (list above).
- UI text in Brazilian Portuguese; code, comments, commits in English.
- Look: black backgrounds (#000000 / #0D0D0D / #1A1A1A layers), white primary text, #9A9A9A secondary, white selection/focus/current-time line, primary buttons white with black text. No green.
- Data in `%APPDATA%\SelfControl\data.json` (versioned JSON, stable GUID ids). Each device independent; export/import a single file (import replaces after confirmation).
- Routine: 7 columns Mon–Sun, default block 1 h, 15-min snap, resize by top and bottom edge, move by body (also across days).
- Block warnings exact copy: first `"Termine suas tarefas antes de abrir"`, after breaktime `"Breaktime acabou, volte às tarefas"`.
- Closing either warning kills the blocked app; the second warning has only the close button. Breaktime is per item.
- Bypass stays easy: Quit in tray menu, no password, no service.
- Day boundary local midnight; missed notifications are not replayed.
- Repo is local only (no GitHub push unless the owner asks).

## Review Focus

1. Steam/Discord run many processes (helpers, updaters) — matching is by exe name, all instances share one breaktime and all are killed on close.
2. Lookalike domains: `notyoutube.com` must not match `youtube.com`; `m.youtube.com` must.
3. Corrupt/missing `data.json` must not crash startup: move it to `data.corrupt-<timestamp>.json` and start empty.
4. PC sleep/clock jump: events whose time passed more than 2 minutes ago are skipped, not fired in a burst; breaktime ends by wall clock.
5. Block list must never kill SelfControl itself or the browser process (sites go through the extension only); adding `SelfControl.exe`, `explorer.exe`, `brave.exe`/`chrome.exe`/`msedge.exe`/`opera.exe` to the app list is refused.

---

## File Structure

```
SelfControl.sln
src/SelfControl.Core/        Models.cs, DateRule.cs, RoutineOps.cs, RoutineResolver.cs, TaskRules.cs,
                             BlockRules.cs, BreaktimeTracker.cs, EventScheduler.cs, DataStore.cs, IPlatform.cs
src/SelfControl.App/         App.axaml(.cs), Theme.axaml, MainWindow.axaml(.cs), AppState.cs,
                             Views/HomeView, RoutineView (+ RoutineGrid.cs custom control), EventsView, TasksView,
                             Views/BlockWarningWindow
src/SelfControl.Desktop/     Program.cs, WindowsBlocker.cs, ToastNotifier.cs, RegistryAutostart.cs, ExtensionBridge.cs
extension/                   manifest.json, background.js, blocked.html, blocked.js
tests/SelfControl.Tests/     one test file per Core file
tests/SelfControl.UiTests/   headless Avalonia tests
```

### Task 1: Solution scaffold, models, date rules

**Files:** Create solution and the 5 projects above (Desktop: `net8.0-windows10.0.17763.0`, WinExe; others `net8.0`; Nullable enable). Create `Core/Models.cs`, `Core/DateRule.cs`, `tests/.../DateRuleTests.cs`.

**Interfaces — Produces:**
- `record DateRule(DateOnly? Date, DayOfWeek[] Weekdays)`; `bool Matches(DateOnly d)`; statics `DateRule.On(DateOnly)`, `DateRule.Every(params DayOfWeek[])`, `DateRule.Weekdays()` (Mon–Fri), `DateRule.Weekend()`.
- Models (classes with `Guid Id`): `Routine{Name, List<RoutineBlock> Blocks}`, `RoutineBlock{Title, DayOfWeek Day, int StartMin, int EndMin, string? Color}`, `RoutineSchedule{Guid RoutineId, DateOnly WeekStart (Monday)}`, `ReminderEvent{Message, TimeOnly Time, DateRule Rule, bool Enabled}`, `TaskItem{Title, DateRule? Rule, DateOnly? TempDate, HashSet<DateOnly> DoneOn}`, `BlockList{List<string> Apps (exe names, lowercase, no path), List<string> Sites (domains)}`, `HomeBlock{HomeBlockKind Kind, string? Text, string? ImagePath}` with `enum HomeBlockKind{Text, Image, TodayRoutine, NextEvents, TodayTasks, LockStatus}`, root `AppData{int Version=1, routines, Guid? ActiveRoutineId, schedules, List<WeekOverride>(WeekStart, RoutineId), events, tasks, BlockList, home}`.

- [ ] Step 1: tests: `Weekdays_MatchesMonToFri_NotSat`, `On_MatchesOnlyThatDate`, `Weekend_MatchesSatSun`.
- [ ] Step 2: `dotnet test` → FAIL (missing types).
- [ ] Step 3: implement.
- [ ] Step 4: `dotnet build SelfControl.sln` 0 errors; `dotnet test` PASS.
- [ ] Step 5: commit `Scaffold solution, models and date rules`.

### Task 2: Routine editing ops and active-routine resolver

**Files:** `Core/RoutineOps.cs`, `Core/RoutineResolver.cs`, tests for each.

**Interfaces — Produces:**
- `static class RoutineOps`: `const int Snap=15, DefaultLen=60, DayLen=1440`; `int SnapMin(int m)`; `RoutineBlock Create(DayOfWeek d, int clickMin)` (start snapped, end = start+60 clamped to 1440); `void ResizeTop(RoutineBlock b, int newStart)`, `ResizeBottom(b, int newEnd)` (snap, clamp 0..1440, min length 15); `Move(b, DayOfWeek day, int newStart)` (keeps length, clamped inside the day); `List<List<RoutineBlock>> OverlapColumns(IEnumerable<RoutineBlock> dayBlocks)` (side-by-side lanes).
- `static Routine? RoutineResolver.For(AppData data, DateOnly date)`: week override for that week → schedule for that week → `ActiveRoutineId`. Manual switch during a scheduled week writes a `WeekOverride`.

- [ ] Step 1: tests: `Create_At1600_EndsAt1700`; `ResizeBottom_To1830_Gives150Min` (start 960, end 1110); `ResizeTop_CannotPassEndMinus15`; `Move_KeepsLength_ClampsAtMidnight` (2h block moved to 23:00 → 22:00–24:00); `ResizeBottom_Snaps_1837To1830`; `Resolver_ScheduledWeekWins_OverActive`; `Resolver_OverrideWins_OverSchedule`; `OverlapColumns_TwoOverlapping_TwoLanes`.
- [ ] Step 2: run → FAIL. Step 3: implement. Step 4: run → PASS. Step 5: commit `Routine ops and resolver`.

### Task 3: Tasks, lock state, block matching, breaktime

**Files:** `Core/TaskRules.cs`, `Core/BlockRules.cs`, `Core/BreaktimeTracker.cs`, tests.

**Interfaces — Produces:**
- `TaskRules.Today(AppData, DateOnly) -> IReadOnlyList<TaskItem>` (temp tasks with `TempDate==date` + rule tasks matching); `IsDone(TaskItem, DateOnly)`; `SetDone(TaskItem, DateOnly, bool)`; `IsLocked(AppData, DateOnly)` = any today task not done.
- `BlockRules.MatchesSite(string host, string domain)` (equal or ends with "."+domain, case-insensitive, strips `www.`); `BlockRules.NormalizeApp(string pathOrName) -> "steam.exe"`; `BlockRules.Protected` set (Review Focus 5) and `bool CanBlockApp(string exe)`.
- `BreaktimeTracker(Func<DateTime> now)`; per key (app exe or site domain): `enum BlockPhase{Free, Warning, Break, Expired}`; `BlockPhase OnSeen(string key, bool locked)` (not locked → Free; first sight → Warning; in Break past end → Expired); `void StartBreak(string key, int minutes)` (only from Warning; 1..120); `void OnClosed(string key)` (forget key → next open is Warning again); `TimeSpan? Remaining(key)`.

- [ ] Step 1: tests: `Temp_TaskOnlyToday`; `Rule_Tuesday_AppearsUncheckedEachTuesday` (done on one Tuesday, not done next Tuesday); `Locked_UntilAllDone`; `NoTasks_NotLocked`; `Site_Subdomain_Matches_Lookalike_DoesNot`; `CannotBlock_ProtectedExe`; `Break_Expires_ToExpired`; `Expired_CannotStartBreak`; `Closed_ThenReopen_IsWarningAgain`; `Break_PerKey_OtherStillWarning`; `Unlocked_IsFreeEvenDuringWarning`.
- [ ] Step 2–4: FAIL → implement → PASS. Step 5: commit `Task lock rules and breaktime tracker`.

### Task 4: Event scheduler and data store

**Files:** `Core/EventScheduler.cs`, `Core/DataStore.cs`, `Core/IPlatform.cs`, tests.

**Interfaces — Produces:**
- `EventScheduler.Due(IEnumerable<ReminderEvent>, DateTime lastCheck, DateTime now) -> List<ReminderEvent>`: enabled events whose occurrence lies in `(lastCheck, now]` and `now - occurrence <= 2 min`. `NextOccurrence(ReminderEvent, DateTime from) -> DateTime?`.
- `DataStore(string folder)`: `AppData Load()` (missing → new; corrupt → rename to `data.corrupt-yyyyMMddHHmmss.json`, new), `void Save(AppData)` (write temp file then replace), `void Export(AppData, string path)`, `AppData Import(string path)` (throws `InvalidDataException` on bad file/version > 1).
- `IPlatform.cs`: `interface IBlocker{ event Action<string exe> AppSeen; event Action<string exe> AppClosed; void Kill(string exe); void SetWatched(IEnumerable<string> exes); }`, `interface INotifier{ void Show(string title, string message); }`, `interface IAutostart{ bool Enabled{get;set;} }`.

- [ ] Step 1: tests: `MonFri2130_FiresMonday_NotSaturday`; `Missed_After10Min_NotFired`; `Disabled_NeverDue`; `SaveLoad_RoundTrip` (DateOnly/TimeOnly/DateRule survive); `Corrupt_File_Renamed_StartsEmpty`; `Import_FutureVersion_Throws`.
- [ ] Step 2–4. Step 5: commit `Event scheduler and data store`.

### Task 5: Windows platform services + extension bridge + extension

**Files:** `Desktop/WindowsBlocker.cs`, `ToastNotifier.cs`, `RegistryAutostart.cs`, `ExtensionBridge.cs`; `extension/*`.

- `WindowsBlocker`: 1 s timer over `Process.GetProcesses()`; raises `AppSeen` when a watched exe name appears (once per appearance), `AppClosed` when no process of it remains; `Kill` kills all processes of that name (ignore access errors).
- `RegistryAutostart`: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `SelfControl` = `"<exe path>" --tray`.
- `ToastNotifier`: `ToastContentBuilder` title "SelfControl".
- `ExtensionBridge`: `HttpListener` on `http://127.0.0.1:47613/`. `GET /check?host=h` → `{"blocked":bool,"phase":"Free|Warning|Break|Expired","tasks":[...],"remaining":sec}`; `POST /break?site=d&min=n`; `POST /closed?site=d`. Answers only requests with an `Origin` of `chrome-extension://`.
- Extension (MV3, permissions `tabs`, `webNavigation`, host `http://127.0.0.1:47613/*`): on each top-frame navigation asks `/check`; if blocked redirects the tab to `blocked.html?site=&back=` showing the first warning (tasks + minutes picker + Breaktime) or, when phase is Expired, only the close message/button (closes tab). During Break, background polls every 15 s and redirects to Expired page at end. Tab closed/left domain → `/closed`. App not running → do nothing.

- [ ] Step 1: Manual smoke check script `tools/smoke.ps1`: starts Desktop with `--smoke`, which watches `notepad.exe`, opens Notepad, asserts `AppSeen` logged, calls Kill, asserts closed; shows one toast; GETs `/check?host=youtube.com`. Expected output `SMOKE OK`.
- [ ] Step 2: implement; Step 3: run `tools/smoke.ps1` → `SMOKE OK`. Load `extension/` unpacked in Brave (`brave://extensions`, developer mode) and verify youtube.com shows the warning while a task is pending.
- [ ] Step 4: commit `Windows services, extension bridge and browser extension`.

### Task 6: Avalonia shell, theme, tray, app state

**Files:** `App/App.axaml(.cs)`, `Theme.axaml`, `MainWindow.axaml(.cs)`, `AppState.cs`, `Desktop/Program.cs`, `UiTests/ShellTests.cs`.

- `AppState`: holds `AppData`, `DataStore`, `BreaktimeTracker`, platform services; `Save()` debounced 500 ms; a 30 s tick runs `EventScheduler.Due` → `INotifier.Show`, refreshes watched apps from `BlockList`, and at midnight refreshes views.
- Main window: left nav (Início, Rotina, Eventos, Tarefas) in white-on-black, selected item inverted (white background, black text). Closing hides to tray; tray menu: Abrir, Sair. `--tray` starts hidden. Single instance (named mutex; second launch brings the window up). First run enables autostart.
- [ ] Step 1: UiTests: `Nav_Switches_Views`, `Close_HidesInsteadOfExit`. Step 2: FAIL. Step 3: implement. Step 4: PASS + `dotnet run --project src/SelfControl.Desktop` opens the window. Step 5: commit `App shell, theme and tray`.

### Task 7: Rotina screen

**Files:** `Views/RoutineView.axaml(.cs)`, `Views/RoutineGrid.cs`, `UiTests/RoutineGridTests.cs`.

- Top bar: routine picker, ＋ Nova, Renomear, Duplicar, Excluir, "Ativa" toggle, "Agendar semanas…" (list of week starts → routine).
- `RoutineGrid` custom control: 7 columns, 00–24 h, hour height 48 px, opens scrolled to current hour, white current-time line on today's column. Click empty → `RoutineOps.Create`; pointer within 6 px of top/bottom edge → resize cursor and `ResizeTop/Bottom`; body drag → `Move` (column under pointer = day). Double-click → edit title/colour/delete. Lanes from `OverlapColumns`.
- [ ] Step 1: UiTests (headless pointer events): `Click_At1600_Creates1600To1700`, `DragBottomEdge_To1830`, `DragTopEdge_Up_ExtendsStart`, `DragBody_ToTuesday_MovesDay`. Step 2–4. Step 5: commit `Routine screen`.

### Task 8: Eventos and Tarefas screens + block warnings

**Files:** `Views/EventsView`, `Views/TasksView`, `Views/BlockWarningWindow`, `UiTests/TasksTests.cs`.

- Eventos: list (message, time, rule summary e.g. "Seg–Sex 21:30", toggle), editor with time picker, "Data específica" date picker or weekday chips + shortcuts "Seg–Sex", "Todo dia", "Fim de semana".
- Tarefas: "Hoje" checklist; add task as "Só hoje" or "Agendada" (same rule editor); Block list editor: apps (pick from running processes or browse .exe; protected refused with message), sites (type domain). Status line: Bloqueado / Liberado.
- `BlockWarningWindow`: topmost, borderless, covers the monitor of the blocked app's window, re-asserts topmost every 500 ms while open. Warning: copy + pending tasks + minutes picker (5,10,15,20,30,45,60) + "Breaktime" + "Fechar" (Fechar → `Kill` + `OnClosed`). Expired: copy + only "Fechar" (kill). Driven by `BreaktimeTracker` phases from `AppSeen` and the 1 s tick.
- [ ] Step 1: UiTests: `CheckingLastTask_Unlocks`, `Warning_Fechar_KillsApp` (fake IBlocker), `Expired_HasOnlyCloseButton`. Step 2–4. Step 5: commit `Events and tasks screens, block warnings`.

### Task 9: Início screen, export/import, release build

**Files:** `Views/HomeView`, `UiTests/HomeTests.cs`.

- Vertical page of `HomeBlock`s; "＋ Bloco" menu (Texto, Imagem, Rotina de hoje, Próximos eventos, Tarefas de hoje, Status do bloqueio); drag handle to reorder; ⋯ to delete; text edited inline; image copied into `%APPDATA%\SelfControl\images\`. Default page: a text block "Bem-vindo", today's routine, today's tasks.
- Settings (gear in nav): Exportar dados…, Importar dados… (confirm replace), Iniciar com o Windows toggle.
- [ ] Step 1: UiTests: `AddTextBlock_Persists`, `Reorder_ChangesOrder`, `TodayTasks_Block_ChecksSameState`. Step 2–4.
- [ ] Step 5: `dotnet publish src/SelfControl.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish`; run it, confirm tray + autostart entry. Desktop shortcut to `publish\SelfControl.exe`.
- [ ] Step 6: commit `Home screen, export/import, release build`.
