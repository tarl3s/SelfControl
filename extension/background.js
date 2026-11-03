const API = "http://127.0.0.1:47613";
const PAGE = chrome.runtime.getURL("blocked.html");
let failUntil = 0;
let chain = Promise.resolve();
const serial = (fn) => (chain = chain.then(fn, fn).catch(() => {}));

// State survives worker suspension: { [tabId]: { site, back, breakEnd } }
async function load() { return (await chrome.storage.session.get("tabs")).tabs || {}; }
function save(tabs) { return chrome.storage.session.set({ tabs }); }

async function check(host) {
  if (Date.now() < failUntil) return null;
  try {
    const r = await fetch(API + "/check?host=" + encodeURIComponent(host));
    if (!r.ok) throw 0;
    return await r.json();
  } catch { failUntil = Date.now() + 30000; return null; }
}
function post(path) { fetch(API + path, { method: "POST" }).catch(() => {}); }
function blockedUrl(site, back, expired) {
  return PAGE + "?site=" + encodeURIComponent(site) + "&back=" + encodeURIComponent(back) + (expired ? "&expired=1" : "");
}
// Drop a tab's state; tell the app only when no other tab still has that site.
function leave(tabs, tabId) {
  const t = tabs[tabId];
  if (!t) return;
  delete tabs[tabId];
  if (!Object.values(tabs).some((o) => o.site === t.site)) post("/closed?site=" + encodeURIComponent(t.site));
}

async function evaluate(tabId, url) {
  const tabs = await load();
  const known = tabs[tabId];
  if (!url || !/^https?:/.test(url)) {
    if (known && !(url || "").startsWith(PAGE)) { leave(tabs, tabId); await save(tabs); }
    return;
  }
  const host = new URL(url).hostname;
  if (known && known.site !== host) leave(tabs, tabId);
  const s = await check(host);
  if (!s || !s.blocked) {
    if (tabs[tabId]) leave(tabs, tabId);
    await save(tabs); return;
  }
  const cur = tabs[tabId] || { site: host, back: url };
  cur.site = host; cur.back = url;
  cur.breakEnd = s.phase === "Break" ? Date.now() + (s.remaining || 0) * 1000 : 0;
  tabs[tabId] = cur;
  await save(tabs);
  if (s.phase !== "Break") chrome.tabs.update(tabId, { url: blockedUrl(host, url, s.phase === "Expired") }).catch(() => {});
}

// Break tabs whose time is up (or unknown) are re-checked and sent to the Expired page.
async function checkBreaks(onlyTab) {
  const tabs = await load();
  for (const [id, t] of Object.entries(tabs)) {
    if (!t.breakEnd || Date.now() < t.breakEnd) continue;
    if (onlyTab !== undefined && Number(id) !== onlyTab) continue;
    const s = await check(t.site);
    if (!s) continue;
    if (s.phase === "Expired") {
      t.breakEnd = 0; await save(tabs);
      chrome.tabs.update(Number(id), { url: blockedUrl(t.site, t.back, true) }).catch(() => {});
    } else if (s.phase === "Break") { t.breakEnd = Date.now() + (s.remaining || 0) * 1000; await save(tabs); }
  }
}

chrome.webNavigation.onBeforeNavigate.addListener((d) => { if (d.frameId === 0) serial(() => evaluate(d.tabId, d.url)); });
chrome.webNavigation.onHistoryStateUpdated.addListener((d) => { if (d.frameId === 0) serial(() => evaluate(d.tabId, d.url)); });
chrome.tabs.onUpdated.addListener((id, info, tab) => {
  if (info.status === "complete" || info.url) serial(async () => { await checkBreaks(id); if (info.url) await evaluate(id, tab.url); });
});
chrome.tabs.onActivated.addListener((a) => serial(() => checkBreaks(a.tabId)));
chrome.tabs.onRemoved.addListener((id) => serial(async () => { const tabs = await load(); leave(tabs, id); await save(tabs); }));

chrome.runtime.onMessage.addListener((m, sender) => {
  if (m && m.type === "break" && sender.tab) serial(async () => {
    const tabs = await load();
    tabs[sender.tab.id] = { site: m.site, back: m.back, breakEnd: Date.now() + m.min * 60000 };
    await save(tabs);
  });
});

chrome.alarms.create("poll", { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener((a) => { if (a.name === "poll") serial(async () => { await checkBreaks(); await scanTabs(); }); });

// An already-open blocked site gets its warning too: evaluate http(s) tabs the state does not know yet.
async function scanTabs() {
  const known = await load();
  const all = await chrome.tabs.query({});
  for (const t of all) {
    if (t.id !== undefined && t.url && /^https?:/.test(t.url) && !known[t.id]) await evaluate(t.id, t.url);
  }
}

// Usage time per site: tell the app which site is in the active tab of the focused window ("" when none).
let lastActive = null;
async function reportActive(force) {
  let host = "";
  try {
    const [t] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    if (t && t.url && /^https?:/.test(t.url)) host = new URL(t.url).hostname;
  } catch {}
  if (!force && host === lastActive) return;
  lastActive = host;
  post("/active?site=" + encodeURIComponent(host));
}
chrome.tabs.onActivated.addListener(() => reportActive());
chrome.tabs.onUpdated.addListener((id, info) => { if (info.url) reportActive(); });
chrome.windows.onFocusChanged.addListener(() => reportActive());
chrome.alarms.onAlarm.addListener((a) => { if (a.name === "poll") reportActive(true); });
