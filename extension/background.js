const API = "http://127.0.0.1:47613";
const PAGE = chrome.runtime.getURL("blocked.html");
const tabSites = new Map();  // tabId -> site being tracked (blocked page or break)
const breakTabs = new Map(); // tabId -> {site, back} while a break runs

async function check(host) {
  try {
    const r = await fetch(API + "/check?host=" + encodeURIComponent(host));
    return r.ok ? await r.json() : null;
  } catch { return null; } // app not running: do nothing
}
function post(path) { fetch(API + path, { method: "POST" }).catch(() => {}); }
function blockedUrl(site, back, expired) {
  return PAGE + "?site=" + encodeURIComponent(site) + "&back=" + encodeURIComponent(back) + (expired ? "&expired=1" : "");
}
function leave(tabId) {
  const site = tabSites.get(tabId);
  if (site) {
    tabSites.delete(tabId); breakTabs.delete(tabId);
    post("/closed?site=" + encodeURIComponent(site));
  }
}

chrome.webNavigation.onBeforeNavigate.addListener(async (d) => {
  if (d.frameId !== 0 || !/^https?:/.test(d.url)) return;
  const host = new URL(d.url).hostname;
  const prev = tabSites.get(d.tabId);
  if (prev && prev !== host) leave(d.tabId);
  const s = await check(host);
  if (!s || !s.blocked) return;
  tabSites.set(d.tabId, host);
  if (s.phase === "Break") { breakTabs.set(d.tabId, { site: host, back: d.url }); return; }
  chrome.tabs.update(d.tabId, { url: blockedUrl(host, d.url, s.phase === "Expired") });
});

chrome.tabs.onRemoved.addListener(leave);

// The blocked page tells us a break started so we can watch that tab.
chrome.runtime.onMessage.addListener((m, sender) => {
  if (m && m.type === "break" && sender.tab) {
    tabSites.set(sender.tab.id, m.site);
    breakTabs.set(sender.tab.id, { site: m.site, back: m.back });
  }
});

// Poll every 15 s during a break; at the end redirect to the Expired page.
setInterval(async () => {
  for (const [tabId, b] of [...breakTabs]) {
    const s = await check(b.site);
    if (!s) continue;
    if (s.phase === "Expired") {
      breakTabs.delete(tabId);
      chrome.tabs.update(tabId, { url: blockedUrl(b.site, b.back, true) }).catch(() => {});
    } else if (s.phase !== "Break") breakTabs.delete(tabId);
  }
}, 15000);
