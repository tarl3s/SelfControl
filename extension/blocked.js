const API = "http://127.0.0.1:47613";
const p = new URLSearchParams(location.search);
const site = p.get("site") || "", back = p.get("back") || "";
document.getElementById("site").textContent = site;
const sel = document.getElementById("min");
[5, 10, 15, 20, 30, 45, 60].forEach(m => sel.add(new Option(m + " min", m)));
function expired() {
  document.getElementById("warn").hidden = true;
  document.getElementById("exp").hidden = false;
}
document.getElementById("close").onclick = () => chrome.tabs.getCurrent(t => chrome.tabs.remove(t.id));
document.getElementById("go").onclick = async () => {
  await fetch(API + "/break?site=" + encodeURIComponent(site) + "&min=" + sel.value, { method: "POST" });
  chrome.runtime.sendMessage({ type: "break", site, back });
  location.replace(back);
};
if (p.get("expired")) expired();
else fetch(API + "/check?host=" + encodeURIComponent(site)).then(r => r.json()).then(s => {
  if (s.phase === "Expired") return expired();
  const ul = document.getElementById("tasks");
  (s.tasks || []).forEach(t => { const li = document.createElement("li"); li.textContent = t; ul.appendChild(li); });
}).catch(() => {});
