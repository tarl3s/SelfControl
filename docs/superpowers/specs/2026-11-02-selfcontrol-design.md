# SelfControl — design

Date: 2026-11-02. Status: approved by owner.

## Intent

SelfControl is the owner's **personal** routine organizer (not part of the "Easy" series; may or may not be published
on GitHub — keep the repo private-ready: no secrets, but no public-facing polish required). It starts with Windows
every day and helps the owner plan the week, get reminders, and keep distractions locked until the day's tasks are
done. Enforcement is **honour-based**: the app never checks whether a task was really done, and bypassing it
(closing the app, disabling the extension) is deliberately easy — "vai da pessoa".

Platform now: **Windows desktop**. Later: an **Android port** (including app/site blocking there). The tech choice
must make that port cheap.

## Goals

1. Four screens: **Home**, **Routine**, **Events/Notifications**, **Task locks**.
2. Starts automatically with Windows, lives in the system tray, keeps running when the window is closed.
3. App + website blocking on Windows while any of today's tasks is unchecked, with a "Breaktime" escape hatch.
4. Black-dominant look with white text/selection accents (explicitly NOT the green-on-black of the Easy apps).

## Non-goals / anti-goals

- No anti-tamper: no password to quit, no admin-protected service, no extension lock. Making bypass hard would be a
  failure of intent.
- No verification of task completion.
- No cloud account/sync (local data only) — owner's call not stated; builder's default, see "Calls made".
- Android is out of scope for this build, but nothing may be designed in a way that blocks it.

## Tech (owner approved: C# + Avalonia)

- **C# / .NET 8 + Avalonia UI** (same stack as EasyPixel; no Visual Studio needed, the .NET SDK is already installed).
  Avalonia also targets Android, so UI and logic carry over to the future port. Keep NuGet dependencies minimal.
- Structure: a core library (models, schedules, lock rules, storage — no UI, no Windows APIs), the Avalonia UI
  project, and platform-specific pieces behind interfaces (`IBlocker`, `INotifier`, `IAutostart`), Windows
  implementations now and Android later:
  - App blocking: a background watcher polling running processes (~1 s) by executable name.
  - Notifications: Windows toast notifications.
  - Autostart: HKCU `Run` key; tray via Avalonia `TrayIcon`; closing the window hides it to the tray.
- Website blocking: a **Chromium extension** (Manifest V3) that works on Brave (owner's browser), Chrome, Edge, Opera.
  The extension talks to the app over a localhost HTTP/WebSocket endpoint served by the app (e.g. `127.0.0.1:<port>`),
  asking "is this domain blocked right now?" and showing the block page. Firefox: later, if needed.
- Storage: local JSON or SQLite in `%APPDATA%\SelfControl\`.
- UI language: Brazilian Portuguese (personal app for a PT-BR speaker) — builder's call; code/comments in English.

## Look

Pure/near-black backgrounds (#000–#121212 layers), white primary text, grey secondary text, white for selection,
focus rings, the current-time line and primary buttons (black text on white). No colour accent by default; routine
blocks may take user-chosen muted colours so the week stays readable.

## Screen 1 — Home (customizable, Notion-like)

A page built from blocks the owner adds, removes, reorders by drag, and edits in place:
- **Text / quote** block (a phrase), **Image** block (pick a local file; copied into app data),
- **Today's routine** (today's column of the active routine, with a "now" marker),
- builder's suggestions (owner didn't object, not confirmed): **Next notifications**, **Today's tasks** (checkable,
  same state as screen 4), **Lock status** (locked/free, breaktime remaining).
Single-column vertical page is enough for v1; side-by-side columns are optional.

## Screen 2 — Routine

- A routine is a **weekly grid: 7 day columns (Mon–Sun) side by side**, vertical time axis 00:00–24:00 (scrollable,
  opens around the current hour).
- Click an empty slot → new block starting there, **default duration 1 h**, with a title (and optional colour/note).
- Drag the **bottom edge** to change the end; drag the **top edge** to change the start; drag the **whole block** to
  move it to another time (builder's call: also to another day). Snapping to 15 min (builder's call).
  Example: a block at 16:00 stretched to 18:30 = 2 h 30.
- Overlapping blocks are shown side by side (builder's call).
- **Several routines**: create, rename, duplicate, delete. Which one is in effect:
  - **Manual**: mark one as the active routine.
  - **Scheduled**: assign a routine to specific weeks (e.g. "week of 10/11 → Provas"); in a scheduled week it is used
    automatically, otherwise the manual active one. A manual switch made during a scheduled week overrides it for
    that week (builder's call).
- Routine blocks do not notify by themselves (not requested); notifications come from screen 3.

## Screen 3 — Events / notifications

- Each event: message text, time, and a date rule: **one specific date**, or **repeat on chosen weekdays**
  (e.g. Mon–Fri at 21:30 → "dormir"). Builder may add "every day" as a shortcut.
- Fires a Windows toast at the time, as long as the app is running (it starts with Windows and lives in the tray).
- List with enable/disable toggle, edit, delete. Missed events while the PC was off are not replayed (builder's call).

## Screen 4 — Task locks

- **Tasks** with checkboxes. Two kinds:
  - **Temporary**: just for today.
  - **Scheduled**: a specific date, every chosen weekday (e.g. every Tuesday), or weekends; on each matching day it
    appears **unchecked**.
- **One block list** (shared by all tasks): apps (e.g. Steam, Discord, Epic Games — picked from running processes or
  an .exe file, matched by process name) and sites (e.g. youtube.com, instagram.com — domain incl. subdomains).
- The list is **locked while any of today's tasks is unchecked**; when all are checked (or there are none), all is free.
- Day boundary = local midnight (builder's call).

### Blocking behaviour (same for apps and sites)

1. Opening a blocked app/site while locked → a warning appears: **"Termine suas tarefas antes de abrir"**, listing the
   pending tasks, with a **Breaktime** button next to it.
   - App: SelfControl shows a full-cover, always-on-top modal over it; the app cannot be used behind it. Buttons:
     **Breaktime** and **Fechar** (closing the warning closes the blocked app — owner's rule, applied to both warnings).
   - Site: the extension replaces the page with the warning page.
2. **Breaktime**: the owner picks a duration in minutes; that app/site is usable until the timer ends.
3. When breaktime ends → a second full-cover modal: "Breaktime acabou, volte às tarefas". It blocks all use of the
   app until clicked, and its **only button closes the blocked app** (process killed). No more time can be requested.
4. Breaktime is **per item**: a breaktime on Steam frees only Steam (all its processes), not Discord or YouTube.
5. The app can be used again only by finishing the tasks, or by reopening it (which shows the first warning again, so a
   new breaktime is possible). For a site: closing the tab and reopening it (builder's call).
6. Bypass stays easy by design (quit from tray, disable extension).

## Data and devices

Each device keeps its own local data (no sync). Data can be **transferred**: export everything to a single file and
import it on another device (replace or merge — builder's call: replace, with a confirmation). The format is versioned
JSON with stable ids so the Android port can import it.

## Day rules

Scheduled tasks appear unchecked at local midnight of each matching day; temporary tasks expire at the end of their day
even if unchecked (builder's calls).

## Calls made by the builder (owner to check)

C# + Avalonia (owner); local-only storage with export/import; PT-BR UI; 15-min snapping; drag across days; scheduled week overrides manual unless
switched during it; Home extra blocks (notifications, tasks, lock status); localhost bridge for the extension;
day boundary at midnight; no missed-notification replay.
