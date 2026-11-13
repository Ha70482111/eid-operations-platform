// Eid Charity platform helpers (language direction, printing, downloads).
window.eid = {
  setLang(lang) {
    const html = document.documentElement;
    html.lang = lang;
    html.dir = lang === "ar" ? "rtl" : "ltr";
    document.cookie = `eid.lang=${lang};path=/;max-age=31536000;samesite=lax`;
    fetch(`/api/lang/${lang}`, { method: "POST", credentials: "same-origin" }).catch(() => {});
  },
  print() { window.print(); },
  download(fileName, contentType, text) {
    const blob = new Blob(["﻿" + text], { type: contentType });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    setTimeout(() => { URL.revokeObjectURL(a.href); a.remove(); }, 500);
  },
};
document.addEventListener("click", (e) => {
  const btn = e.target.closest("[data-lang-toggle]");
  if (!btn) return;
  const next = document.documentElement.lang === "ar" ? "en" : "ar";
  document.cookie = `eid.lang=${next};path=/;max-age=31536000;samesite=lax`;
  location.reload();
});
