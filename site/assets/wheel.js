/* Action Wheel website: the live wheel, and the download button.
   The wheel follows the app's own rules (RingGeometry.TagForDirection): tag 1 at the top,
   then clockwise every 45 degrees; a small dead zone separates "flick" from "click". */

(() => {
  "use strict";

  const strings = JSON.parse(document.getElementById("wheel-strings").textContent);
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  // ---------- icons (drawn for this site, 24x24, stroked) ----------
  const ICONS = {
    close: '<path d="M6 6l12 12M18 6L6 18"/>',
    copy: '<rect x="9" y="9" width="11" height="11" rx="2.2"/><path d="M15 9V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h3"/>',
    paste: '<path d="M9 4h6v3H9z"/><path d="M15 5h2a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h2"/>',
    cut: '<circle cx="6.5" cy="17.5" r="2.8"/><circle cx="17.5" cy="17.5" r="2.8"/><path d="M8.6 15.6L18 4M15.4 15.6L6 4"/>',
    shot: '<path d="M4 9V7a2 2 0 0 1 2-2h2M16 5h2a2 2 0 0 1 2 2v2M20 15v2a2 2 0 0 1-2 2h-2M8 19H6a2 2 0 0 1-2-2v-2"/><circle cx="12" cy="12" r="3"/>',
    gear: '<circle cx="12" cy="12" r="3"/><path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M5.6 18.4l1.8-1.8M16.6 7.4l1.8-1.8"/>',
    tasks: '<rect x="3.5" y="5" width="7.5" height="6" rx="1.3"/><rect x="13" y="5" width="7.5" height="6" rx="1.3"/><rect x="3.5" y="13" width="7.5" height="6" rx="1.3"/><rect x="13" y="13" width="7.5" height="6" rx="1.3"/>',
    folder: '<path d="M3.5 7.5a2 2 0 0 1 2-2h3.8l2 2.2h7.2a2 2 0 0 1 2 2v7.8a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z"/>',
    lock: '<rect x="5" y="10.5" width="14" height="10" rx="2.2"/><path d="M8.2 10.5V8a3.8 3.8 0 0 1 7.6 0v2.5"/>',
  };
  const svg = (name) =>
    `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONS[name]}</svg>`;

  // The app's nine default buttons, in tag order.
  const ACTIONS = ["close", "copy", "paste", "cut", "shot", "gear", "tasks", "folder", "lock"];

  // ---------- the wheel ----------
  const stage = document.getElementById("stage");
  if (stage) initWheel();

  function initWheel() {
    const toast = stage.querySelector(".toast");
    const animSelect = document.getElementById("anim");
    const hoverSelect = document.getElementById("hover");
    const kbButton = document.getElementById("open-kb");

    const ORBIT = 100;     // px, the app's 88 DIP scaled up slightly for the web
    const DEAD_ZONE = 16;  // px, RingGeometry.GestureDeadZone in spirit

    let wheel = null;      // the open wheel element
    let label = null;      // the label under the highlighted button
    let origin = null;     // where the wheel opened, stage coordinates
    let armed = false;     // a press is still down: the drag gesture is live
    let activeTag = null;  // the button the gesture points at
    let toastTimer = 0;

    const slotOffset = (tag) => {
      if (tag === 0) return [0, 0];
      const a = ((tag - 1) * 45 - 90) * (Math.PI / 180);
      return [Math.cos(a) * ORBIT, Math.sin(a) * ORBIT];
    };

    // Same mapping as the app: angle measured clockwise from straight up, 8 slices of 45 degrees.
    const tagForDirection = (dx, dy) => {
      if (Math.hypot(dx, dy) < DEAD_ZONE) return null;
      let deg = (Math.atan2(dx, -dy) * 180) / Math.PI;
      if (deg < 0) deg += 360;
      return (Math.round(deg / 45) % 8) + 1;
    };

    const stagePoint = (e) => {
      const r = stage.getBoundingClientRect();
      return [e.clientX - r.left, e.clientY - r.top];
    };

    function open(x, y, viaKeyboard) {
      close(true);
      const r = stage.getBoundingClientRect();
      // Clamped so the whole ring stays inside the stage, as the app clamps to the monitor.
      const m = ORBIT + 38;
      x = Math.min(Math.max(x, m), r.width - m);
      y = Math.min(Math.max(y, m), r.height - m);
      origin = [x, y];

      wheel = document.createElement("div");
      wheel.className = "wheel";
      wheel.dataset.hover = hoverSelect.value;
      wheel.style.left = `${x}px`;
      wheel.style.top = `${y}px`;
      wheel.setAttribute("role", "menu");
      wheel.setAttribute("aria-label", strings.menuLabel);

      ACTIONS.forEach((name, tag) => {
        const [dx, dy] = slotOffset(tag);
        const slot = document.createElement("div");
        slot.className = "slot";
        slot.style.left = `${dx}px`;
        slot.style.top = `${dy}px`;
        const b = document.createElement("button");
        b.type = "button";
        b.className = tag === 0 ? "wbtn center" : "wbtn";
        b.dataset.tag = String(tag);
        b.setAttribute("role", "menuitem");
        b.setAttribute("aria-label", strings.actions[tag].label);
        b.innerHTML = svg(name);
        b.addEventListener("pointerenter", () => showLabel(tag));
        b.addEventListener("pointerleave", () => hideLabel());
        b.addEventListener("focus", () => showLabel(tag));
        b.addEventListener("click", (e) => {
          e.stopPropagation();
          invoke(tag);
        });
        slot.appendChild(b);
        wheel.appendChild(slot);
      });

      stage.appendChild(wheel);
      stage.classList.add("has-wheel");
      playOpening(animSelect.value);
      if (viaKeyboard) wheel.querySelector('[data-tag="1"]').focus();
    }

    function close(immediate) {
      armed = false;
      setActive(null);
      hideLabel();
      if (!wheel) return;
      const old = wheel;
      wheel = null;
      stage.classList.remove("has-wheel");
      if (immediate || reduceMotion) {
        old.remove();
      } else {
        old.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 110, easing: "ease-out" }).onfinish = () => old.remove();
      }
    }

    function setActive(tag) {
      if (activeTag === tag) return;
      if (wheel && activeTag !== null) wheel.querySelector(`[data-tag="${activeTag}"]`)?.classList.remove("on");
      activeTag = tag;
      if (wheel && tag !== null) {
        wheel.querySelector(`[data-tag="${tag}"]`)?.classList.add("on");
        showLabel(tag);
      } else {
        hideLabel();
      }
    }

    function showLabel(tag) {
      if (!wheel) return;
      hideLabel();
      const [dx, dy] = slotOffset(tag);
      label = document.createElement("div");
      label.className = "wheel-label";
      label.textContent = strings.actions[tag].label;
      label.style.left = `${origin[0] + dx}px`;
      label.style.top = `${origin[1] + dy + (tag === 0 ? 30 : 34)}px`;
      stage.appendChild(label);
    }

    function hideLabel() {
      label?.remove();
      label = null;
    }

    function invoke(tag) {
      const b = wheel?.querySelector(`[data-tag="${tag}"]`);
      b?.classList.add("pressed");
      const action = strings.actions[tag];
      window.setTimeout(() => {
        close(false);
        if (tag !== 0) say(`<b>${action.label}</b> — ${action.does}`);
      }, reduceMotion ? 0 : 90);
    }

    function say(html) {
      toast.innerHTML = html;
      toast.classList.add("show");
      window.clearTimeout(toastTimer);
      toastTimer = window.setTimeout(() => toast.classList.remove("show"), 3200);
    }

    // The opening animations, after the app's RingOpenAnimation presets (about 200 ms each).
    function playOpening(kind) {
      if (reduceMotion || kind === "none") return;
      const slots = [...wheel.querySelectorAll(".slot")];
      const centre = slots[0];
      const outer = slots.slice(1);
      const ease = "cubic-bezier(.2,.8,.2,1)";
      // A slot sits at its place through left/top; its transform only centres it. Animations
      // start from an extra offset (k = 0: on the centre, k > 1: further out) and end on base.
      const base = () => "translate(-50%,-50%)";
      const rel = (s, k) => {
        const x = parseFloat(s.style.left) * (k - 1), y = parseFloat(s.style.top) * (k - 1);
        return `translate(-50%,-50%) translate(${x}px,${y}px)`;
      };

      switch (kind) {
        case "fade":
          wheel.animate([{ opacity: 0, transform: "scale(.86)" }, { opacity: 1, transform: "scale(1)" }], { duration: 120, easing: ease });
          slots.forEach((s, i) => s.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 90, delay: i * 12, easing: "ease-out", fill: "backwards" }));
          break;
        case "pop":
          slots.forEach((s, i) => s.animate(
            [{ transform: `${base(s)} scale(.25)`, opacity: 0 }, { transform: `${base(s)} scale(1.08)`, opacity: 1, offset: 0.7 }, { transform: `${base(s)} scale(1)`, opacity: 1 }],
            { duration: 170, delay: i * 10, easing: "ease-out", fill: "backwards" }));
          break;
        case "radiate":
          centre.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 80, fill: "backwards" });
          outer.forEach((s, i) => s.animate([{ transform: rel(s, 0), opacity: 0 }, { transform: base(s), opacity: 1 }], { duration: 160, delay: i * 9, easing: ease, fill: "backwards" }));
          break;
        case "converge":
          outer.forEach((s, i) => s.animate([{ transform: rel(s, 1.7), opacity: 0 }, { transform: base(s), opacity: 1 }], { duration: 160, delay: i * 9, easing: ease, fill: "backwards" }));
          centre.animate([{ opacity: 0, transform: `${base(centre)} scale(.6)` }, { opacity: 1, transform: `${base(centre)} scale(1)` }], { duration: 110, delay: 110, easing: "ease-out", fill: "backwards" });
          break;
        case "sweep":
        case "counter": {
          const turn = kind === "sweep" ? -45 : 45;
          wheel.animate([{ transform: `rotate(${turn}deg)`, opacity: 0.2 }, { transform: "rotate(0deg)", opacity: 1 }], { duration: 190, easing: ease });
          // Icons stay upright while the ring turns, as in the app.
          wheel.querySelectorAll(".wbtn svg").forEach((icon) =>
            icon.animate([{ transform: `rotate(${-turn}deg)` }, { transform: "rotate(0deg)" }], { duration: 190, easing: ease }));
          break;
        }
      }
    }

    // ---------- input ----------

    // A middle press would start the browser's autoscroll; the stage is the app's desktop.
    stage.addEventListener("mousedown", (e) => { if (e.button === 1) e.preventDefault(); });
    stage.addEventListener("auxclick", (e) => { if (e.button === 1) e.preventDefault(); });
    stage.addEventListener("contextmenu", (e) => { if (wheel) e.preventDefault(); });

    stage.addEventListener("pointerdown", (e) => {
      // A left press on a button is that button's click. A middle press anywhere reopens the ring.
      if (e.button === 0 && e.target.closest(".wbtn")) return;
      if (e.button !== 0 && e.button !== 1) return;
      const [x, y] = stagePoint(e);

      // A press away from an open ring dismisses it, as a click elsewhere does in the app.
      // The middle button reopens it at the new spot instead, also as in the app.
      if (wheel && e.button === 0) {
        close(false);
        return;
      }

      open(x, y, false);
      armed = true;
      stage.setPointerCapture(e.pointerId);
    });

    stage.addEventListener("pointermove", (e) => {
      if (!armed || !wheel) return;
      const [x, y] = stagePoint(e);
      setActive(tagForDirection(x - origin[0], y - origin[1]));
    });

    const release = (e) => {
      if (!armed) return;
      armed = false;
      if (stage.hasPointerCapture(e.pointerId)) stage.releasePointerCapture(e.pointerId);
      // Past the dead zone: the flick chose a button. Otherwise the ring stays up to be clicked.
      if (activeTag !== null) {
        const tag = activeTag;
        setActive(null);
        invoke(tag);
      }
    };
    stage.addEventListener("pointerup", release);
    stage.addEventListener("pointercancel", (e) => { armed = false; setActive(null); release(e); });

    // Esc closes the ring wherever focus is, as the app's keyboard hook does: the ring also
    // opens by itself on scroll, when focus is usually still on the page body.
    document.addEventListener("keydown", (e) => {
      if (e.key !== "Escape" || !wheel) return;
      e.preventDefault();
      const hadFocus = stage.contains(document.activeElement);
      close(false);
      if (hadFocus) stage.focus();
    });

    stage.addEventListener("keydown", (e) => {
      if ((e.key === "Enter" || e.key === " ") && !wheel && e.target === stage) {
        e.preventDefault();
        const r = stage.getBoundingClientRect();
        open(r.width / 2, r.height / 2, true);
      }
    });

    kbButton?.addEventListener("click", () => {
      const r = stage.getBoundingClientRect();
      open(r.width / 2, r.height / 2, true);
    });

    // Open the ring once by itself the first time the stage scrolls into view, so the page shows
    // the product instead of an empty box. After that the visitor drives it.
    if ("IntersectionObserver" in window) {
      const seen = new IntersectionObserver((entries) => {
        if (!entries.some((e) => e.isIntersecting)) return;
        seen.disconnect();
        if (!wheel) {
          const r = stage.getBoundingClientRect();
          open(r.width / 2, r.height / 2 - 14, false);
        }
      }, { threshold: 0.6 });
      seen.observe(stage);
    }

    hoverSelect.addEventListener("change", () => { if (wheel) wheel.dataset.hover = hoverSelect.value; });
    animSelect.addEventListener("change", () => {
      const r = stage.getBoundingClientRect();
      open(origin ? origin[0] : r.width / 2, origin ? origin[1] : r.height / 2, false);
    });
  }

  // ---------- download button: always the latest release ----------
  // Falls back to the releases page as written in the HTML if GitHub cannot be reached.
  (async () => {
    const links = document.querySelectorAll("[data-download]");
    if (!links.length) return;
    try {
      const res = await fetch("https://api.github.com/repos/cmm-cmm/Action-Wheel/releases/latest", {
        headers: { Accept: "application/vnd.github+json" },
      });
      if (!res.ok) return;
      const release = await res.json();
      const zip = release.assets.find((a) => /-win-x64\.zip$/i.test(a.name));
      const sha = release.assets.find((a) => /-win-x64\.zip\.sha256$/i.test(a.name));
      const version = String(release.tag_name || "").replace(/^v/i, "");
      if (zip) links.forEach((a) => (a.href = zip.browser_download_url));
      if (version) document.querySelectorAll("[data-version]").forEach((el) => (el.textContent = `v${version}`));
      if (zip) document.querySelectorAll("[data-size]").forEach((el) => (el.textContent = `${Math.round(zip.size / 1048576)} MB`));
      if (sha) document.querySelectorAll("[data-sha]").forEach((a) => (a.href = sha.browser_download_url));
    } catch {
      /* Offline or rate-limited: the static links still work. */
    }
  })();
})();
