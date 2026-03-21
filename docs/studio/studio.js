import {
  encodeUet,
  formatVectorStatusLine,
  pickComparableFields,
  tryDecodeUet,
  verifyCoreVectors
} from "./decoder.js";

const TRACKING_KEYS = Object.freeze({
  open: "studio-open",
  decode: "studio-decode",
  verify: "studio-verify"
});

const TAB_NAMES = Object.freeze([
  "decode",
  "build",
  "compare",
  "generate",
  "verify"
]);

const DEFAULT_SAMPLE_UET_HEX = "0C000FA4C0E40000";

const FIELD_SPECS = Object.freeze([
  Object.freeze({
    key: "emergencyType",
    label: "EmergencyType",
    bits: "63-60",
    what: "Type of emergency class.",
    formula: "(value >> 60) & 0xF",
    values: "0=Fire ... 15=Reserved",
    rawHexWidth: 1,
    decodedValue(decoded) {
      return decoded.emergencyTypeLabel;
    }
  }),
  Object.freeze({
    key: "priority",
    label: "Priority",
    bits: "59-58",
    what: "Urgency level for delivery decisions.",
    formula: "(value >> 58) & 0x3",
    values: "0=Low, 1=Medium, 2=High, 3=Critical",
    rawHexWidth: 1,
    decodedValue(decoded) {
      return decoded.priorityLabel;
    }
  }),
  Object.freeze({
    key: "actionFlags",
    label: "ActionFlags",
    bits: "57-50",
    what: "8-bit command bitmap for device behavior.",
    formula: "(value >> 50) & 0xFF",
    values: "bit0..bit7 -> SoundAlarm..NotifyExternal",
    rawHexWidth: 2,
    decodedValue(decoded) {
      return decoded.actionFlagLabels.length > 0 ? decoded.actionFlagLabels.join(", ") : "None";
    }
  }),
  Object.freeze({
    key: "zoneHash",
    label: "ZoneHash",
    bits: "49-34",
    what: "16-bit zone routing hash.",
    formula: "(value >> 34) & 0xFFFF",
    values: "0..65535",
    rawHexWidth: 4,
    decodedValue(decoded) {
      return `0x${toPaddedHex(decoded.zoneHash, 4)}`;
    }
  }),
  Object.freeze({
    key: "timestampMinutes",
    label: "TimestampMinutes",
    bits: "33-18",
    what: "16-bit timestamp window in minutes.",
    formula: "(value >> 18) & 0xFFFF",
    values: "0..65535",
    rawHexWidth: 4,
    decodedValue(decoded) {
      return `0x${toPaddedHex(decoded.timestampMinutes, 4)}`;
    }
  }),
  Object.freeze({
    key: "confirmHash",
    label: "ConfirmHash",
    bits: "17-0",
    what: "18-bit confirmation correlation hash.",
    formula: "(value >> 0) & 0x3FFFF",
    values: "0..262143",
    rawHexWidth: 5,
    decodedValue(decoded) {
      return `0x${toPaddedHex(decoded.confirmHash, 5)}`;
    }
  })
]);

const state = {
  decodeDebounceTimer: null,
  lastDecoded: null,
  lastTrackedDecodedHex: "",
  activeTooltipTrigger: null,
  activeTab: ""
};

const refs = {};

function init() {
  bindRefs();
  setupTracking();
  setupTabs();
  setupDecode();
  setupVerify();
  setupTooltipOverlay();
  decodeInitialSample();
}

function bindRefs() {
  refs.tabBar = mustGet("tab-bar");
  refs.tabButtons = Array.from(document.querySelectorAll(".tab-button[data-tab]"));
  refs.tabPanels = Object.freeze({
    decode: mustGet("tab-decode"),
    build: mustGet("tab-build"),
    compare: mustGet("tab-compare"),
    generate: mustGet("tab-generate"),
    verify: mustGet("tab-verify")
  });

  refs.payloadInput = mustGet("payload-input");
  refs.copyHexButton = mustGet("copy-hex-button");
  refs.copyBase64Button = mustGet("copy-base64-button");
  refs.decodeFeedback = mustGet("decode-feedback");
  refs.decodeErrorPanel = mustGet("decode-error-panel");
  refs.errorWhat = mustGet("error-what");
  refs.errorWhy = mustGet("error-why");
  refs.errorFix = mustGet("error-fix");
  refs.decodeResultPanel = mustGet("decode-result-panel");
  refs.validBadge = mustGet("valid-badge");
  refs.roundtripBadge = mustGet("roundtrip-badge");
  refs.decodedHex = mustGet("decoded-hex");
  refs.decodedBase64 = mustGet("decoded-base64");
  refs.decodedBitMap = mustGet("decoded-bit-map");
  refs.decodedHexMap = mustGet("decoded-hex-map");
  refs.decodedFieldsBody = mustGet("decoded-fields-body");

  refs.verifyButton = mustGet("verify-button");
  refs.verifyResultsWrap = mustGet("verify-results-wrap");
  refs.verifySummary = mustGet("verify-summary");
  refs.verifyResults = mustGet("verify-results");
  refs.verifyToggle = mustGet("verify-toggle");
}

function setupTracking() {
  trackEvent(TRACKING_KEYS.open);
  const refCode = parseRefCode();
  if (refCode) {
    trackEvent(`studio-ref-${refCode}`);
  }
}

function setupTabs() {
  refs.tabBar.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }
    const button = target.closest("button[data-tab]");
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }
    const tabName = normalizeTabName(button.dataset.tab);
    if (!tabName) {
      return;
    }
    const nextHash = `#${tabName}`;
    if (window.location.hash !== nextHash) {
      window.location.hash = nextHash;
      return;
    }
    activateTab(tabName, true);
  });

  window.addEventListener("hashchange", () => {
    applyHashTab(true);
  });

  if (!window.location.hash) {
    history.replaceState(null, "", "#decode");
  }

  applyHashTab(true);
}

function applyHashTab(trackChange) {
  const tabName = getTabFromHash();
  const normalizedHash = `#${tabName}`;
  if (window.location.hash !== normalizedHash) {
    history.replaceState(null, "", normalizedHash);
  }
  activateTab(tabName, trackChange);
}

function getTabFromHash() {
  const rawHash = window.location.hash.startsWith("#")
    ? window.location.hash.slice(1)
    : window.location.hash;
  const tabName = normalizeTabName(rawHash);
  return tabName || "decode";
}

function normalizeTabName(value) {
  const normalized = String(value || "").trim().toLowerCase();
  return TAB_NAMES.includes(normalized) ? normalized : "";
}

function activateTab(tabName, trackChange) {
  const normalizedTab = normalizeTabName(tabName) || "decode";
  if (state.activeTab === normalizedTab) {
    return;
  }

  refs.tabButtons.forEach((button) => {
    const buttonTab = normalizeTabName(button.dataset.tab);
    const isActive = buttonTab === normalizedTab;
    button.classList.toggle("tab-button-active", isActive);
    button.setAttribute("aria-selected", isActive ? "true" : "false");
    button.tabIndex = isActive ? 0 : -1;
  });

  TAB_NAMES.forEach((name) => {
    const panel = refs.tabPanels[name];
    const isActive = name === normalizedTab;
    panel.classList.toggle("tab-active", isActive);
    panel.setAttribute("aria-hidden", isActive ? "false" : "true");
  });

  state.activeTab = normalizedTab;
  hideActiveTooltip();

  if (trackChange) {
    trackEvent(`studio-tab-${normalizedTab}`);
  }
}

function setupDecode() {
  refs.payloadInput.addEventListener("input", () => {
    if (state.decodeDebounceTimer !== null) {
      window.clearTimeout(state.decodeDebounceTimer);
    }
    state.decodeDebounceTimer = window.setTimeout(() => {
      decodeCurrentInput();
    }, 180);
  });

  refs.copyHexButton.addEventListener("click", async () => {
    if (!state.lastDecoded) {
      return;
    }
    const copied = await copyTextBestEffort(state.lastDecoded.valueHex);
    refs.decodeFeedback.textContent = copied
      ? "Hex copied to clipboard."
      : "Clipboard denied. Select the hex value and copy manually.";
  });

  refs.copyBase64Button.addEventListener("click", async () => {
    if (!state.lastDecoded) {
      return;
    }
    const copied = await copyTextBestEffort(state.lastDecoded.valueBase64);
    refs.decodeFeedback.textContent = copied
      ? "Base64 copied to clipboard."
      : "Clipboard denied. Select the base64 value and copy manually.";
  });
}

function setupVerify() {
  refs.verifyToggle.addEventListener("click", () => {
    const willHide = !refs.verifyResults.hidden;
    refs.verifyResults.hidden = willHide;
    refs.verifyToggle.textContent = willHide ? "Show details" : "Hide details";
    refs.verifyToggle.setAttribute("aria-expanded", willHide ? "false" : "true");
  });

  refs.verifyButton.addEventListener("click", () => {
    trackEvent(TRACKING_KEYS.verify);
    const report = verifyCoreVectors();
    refs.verifyResultsWrap.hidden = false;
    refs.verifySummary.textContent = `Core suite: ${report.passed}/${report.total} passed`;
    refs.verifySummary.className = report.allPassed ? "verify-summary success" : "verify-summary failure";
    refs.verifyResults.innerHTML = report.results
      .map((result) => `<li>${escapeHtml(formatVectorStatusLine(result))}</li>`)
      .join("");
    refs.verifyResults.hidden = false;
    refs.verifyToggle.textContent = "Hide details";
    refs.verifyToggle.setAttribute("aria-expanded", "true");
  });
}

function setupTooltipOverlay() {
  refs.decodeResultPanel.addEventListener("mouseover", handleTooltipMouseOver);
  refs.decodeResultPanel.addEventListener("mouseout", handleTooltipMouseOut);
  refs.decodeResultPanel.addEventListener("focusin", handleTooltipFocusIn);
  refs.decodeResultPanel.addEventListener("focusout", handleTooltipFocusOut);
  window.addEventListener("resize", refreshActiveTooltipPosition);
  window.addEventListener("scroll", refreshActiveTooltipPosition, true);
}

function handleTooltipMouseOver(event) {
  const target = event.target;
  if (!(target instanceof HTMLElement)) {
    return;
  }
  const trigger = target.closest(".tooltip-trigger");
  if (trigger instanceof HTMLButtonElement) {
    openTooltip(trigger);
  }
}

function handleTooltipMouseOut(event) {
  const target = event.target;
  if (!(target instanceof HTMLElement)) {
    return;
  }
  const trigger = target.closest(".tooltip-trigger");
  if (!(trigger instanceof HTMLButtonElement)) {
    return;
  }
  const relatedTarget = event.relatedTarget;
  if (relatedTarget instanceof Node && trigger.contains(relatedTarget)) {
    return;
  }
  closeTooltip(trigger);
}

function handleTooltipFocusIn(event) {
  const target = event.target;
  if (target instanceof HTMLButtonElement && target.classList.contains("tooltip-trigger")) {
    openTooltip(target);
  }
}

function handleTooltipFocusOut(event) {
  const target = event.target;
  if (!(target instanceof HTMLButtonElement) || !target.classList.contains("tooltip-trigger")) {
    return;
  }
  const relatedTarget = event.relatedTarget;
  if (relatedTarget instanceof Node && target.contains(relatedTarget)) {
    return;
  }
  closeTooltip(target);
}

function openTooltip(trigger) {
  const tooltip = getTooltipElement(trigger);
  if (!(tooltip instanceof HTMLElement)) {
    return;
  }
  hideActiveTooltip(trigger);
  tooltip.classList.add("open");
  positionTooltip(trigger, tooltip);
  state.activeTooltipTrigger = trigger;
}

function closeTooltip(trigger) {
  const tooltip = getTooltipElement(trigger);
  if (!(tooltip instanceof HTMLElement)) {
    return;
  }
  tooltip.classList.remove("open");
  resetTooltipInlinePosition(tooltip);
  if (state.activeTooltipTrigger === trigger) {
    state.activeTooltipTrigger = null;
  }
}

function hideActiveTooltip(exceptTrigger) {
  const activeTrigger = state.activeTooltipTrigger;
  if (!(activeTrigger instanceof HTMLButtonElement)) {
    return;
  }
  if (exceptTrigger instanceof HTMLButtonElement && activeTrigger === exceptTrigger) {
    return;
  }
  closeTooltip(activeTrigger);
}

function refreshActiveTooltipPosition() {
  const trigger = state.activeTooltipTrigger;
  if (!(trigger instanceof HTMLButtonElement)) {
    return;
  }
  if (!document.contains(trigger)) {
    state.activeTooltipTrigger = null;
    return;
  }
  const tooltip = getTooltipElement(trigger);
  if (!(tooltip instanceof HTMLElement) || !tooltip.classList.contains("open")) {
    return;
  }
  positionTooltip(trigger, tooltip);
}

function getTooltipElement(trigger) {
  const wrap = trigger.closest(".tooltip-wrap");
  if (!(wrap instanceof HTMLElement)) {
    return null;
  }
  const tooltip = wrap.querySelector(".tooltip-text");
  if (!(tooltip instanceof HTMLElement)) {
    return null;
  }
  return tooltip;
}

function positionTooltip(trigger, tooltip) {
  const viewportPadding = 8;
  const verticalOffset = 6;
  const triggerRect = trigger.getBoundingClientRect();

  tooltip.style.visibility = "hidden";
  tooltip.style.left = "0px";
  tooltip.style.top = "0px";
  const tooltipRect = tooltip.getBoundingClientRect();

  let left = triggerRect.left + (triggerRect.width / 2) - (tooltipRect.width / 2);
  const maxLeft = window.innerWidth - tooltipRect.width - viewportPadding;
  left = clamp(left, viewportPadding, Math.max(viewportPadding, maxLeft));

  const spaceBelow = window.innerHeight - triggerRect.bottom - viewportPadding;
  const spaceAbove = triggerRect.top - viewportPadding;
  const placeBelow = spaceBelow >= tooltipRect.height || spaceBelow >= spaceAbove;

  let top = placeBelow
    ? triggerRect.bottom + verticalOffset
    : triggerRect.top - tooltipRect.height - verticalOffset;
  const maxTop = window.innerHeight - tooltipRect.height - viewportPadding;
  top = clamp(top, viewportPadding, Math.max(viewportPadding, maxTop));

  tooltip.style.left = `${Math.round(left)}px`;
  tooltip.style.top = `${Math.round(top)}px`;
  tooltip.style.visibility = "";
}

function resetTooltipInlinePosition(tooltip) {
  tooltip.style.left = "";
  tooltip.style.top = "";
  tooltip.style.visibility = "";
}

function decodeInitialSample() {
  refs.payloadInput.value = DEFAULT_SAMPLE_UET_HEX;
  decodeCurrentInput();
}

function decodeCurrentInput() {
  const raw = refs.payloadInput.value.trim();
  if (!raw) {
    hideActiveTooltip();
    refs.decodeFeedback.textContent = "";
    refs.decodeErrorPanel.hidden = true;
    refs.decodeResultPanel.hidden = true;
    state.lastDecoded = null;
    setCopyButtonsEnabled(false);
    return;
  }

  const result = tryDecodeUet(raw);
  if (!result.ok) {
    renderDecodeError(result.error);
    return;
  }

  const decoded = result.value;
  const roundtrip = encodeUet(pickComparableFields(decoded));
  const roundtripOk = roundtrip.hex === decoded.valueHex && roundtrip.base64 === decoded.valueBase64;

  state.lastDecoded = decoded;
  refs.decodeErrorPanel.hidden = true;
  refs.decodeResultPanel.hidden = false;
  setCopyButtonsEnabled(true);

  refs.validBadge.textContent = "Valid UET";
  refs.validBadge.className = "badge badge-success";
  refs.roundtripBadge.textContent = roundtripOk ? "Round-trip verified" : "Round-trip mismatch";
  refs.roundtripBadge.className = roundtripOk ? "badge badge-success" : "badge badge-danger";
  refs.decodedHex.textContent = decoded.valueHex;
  refs.decodedBase64.textContent = decoded.valueBase64;
  refs.decodedBitMap.innerHTML = renderBitMap(decoded.valueBinary);
  refs.decodedHexMap.innerHTML = renderHexMap(decoded.valueHex);
  refs.decodedFieldsBody.innerHTML = renderDecodedRows(decoded);
  refs.decodeFeedback.textContent = "Decode completed. Fields and roundtrip status updated.";

  if (state.lastTrackedDecodedHex !== decoded.valueHex) {
    trackEvent(TRACKING_KEYS.decode);
    state.lastTrackedDecodedHex = decoded.valueHex;
  }
}

function renderDecodeError(error) {
  hideActiveTooltip();
  refs.decodeResultPanel.hidden = true;
  setCopyButtonsEnabled(false);
  refs.decodeErrorPanel.hidden = false;
  refs.errorWhat.textContent = error?.what ?? "Unable to decode payload.";
  refs.errorWhy.textContent = error?.why ?? "Input does not match supported UET formats.";
  refs.errorFix.textContent = error?.howToFix ?? "Use a valid 8-byte UET in hex or base64.";
  refs.decodeFeedback.textContent = "Decode failed. See details below.";
}

function renderDecodedRows(decoded) {
  return FIELD_SPECS.map((spec, index) => {
    const raw = Number(decoded[spec.key]);
    const tooltipId = `tooltip-${spec.key}-${index}`;
    const rawHex = `0x${toPaddedHex(raw, spec.rawHexWidth)}`;
    const decodedValue = spec.decodedValue(decoded);

    return (
      "<tr>" +
      "<td>" +
      `<div class="field-cell">${escapeHtml(spec.label)}` +
      "<span class=\"tooltip-wrap\">" +
      `<button type="button" class="tooltip-trigger" aria-describedby="${tooltipId}">?</button>` +
      `<span class="tooltip-text" role="tooltip" id="${tooltipId}">` +
      `<strong>What:</strong> ${escapeHtml(spec.what)}<br>` +
      `<strong>Bits:</strong> ${escapeHtml(spec.bits)}<br>` +
      `<strong>Formula:</strong> <code>${escapeHtml(spec.formula)}</code><br>` +
      `<strong>Values:</strong> ${escapeHtml(spec.values)}` +
      "</span></span></div></td>" +
      `<td><code>${escapeHtml(spec.bits)}</code></td>` +
      `<td>${raw} <code>(${rawHex})</code></td>` +
      `<td>${escapeHtml(decodedValue)}</td>` +
      "</tr>"
    );
  }).join("");
}

function renderBitMap(binary64) {
  const segments = [
    { label: "ET", bits: binary64.slice(0, 4), css: "bit-map-segment segment-emergency" },
    { label: "Pr", bits: binary64.slice(4, 6), css: "bit-map-segment segment-priority" },
    { label: "Action", bits: binary64.slice(6, 14), css: "bit-map-segment segment-action" },
    { label: "Zone", bits: binary64.slice(14, 30), css: "bit-map-segment segment-zone" },
    { label: "Time", bits: binary64.slice(30, 46), css: "bit-map-segment segment-time" },
    { label: "Confirm", bits: binary64.slice(46, 64), css: "bit-map-segment segment-confirm" }
  ];

  return segments
    .map((segment) => `<span class="${segment.css}">${segment.label}:${segment.bits}</span>`)
    .join("");
}

function renderHexMap(hex) {
  const classes = [
    "field-mixed",
    "field-mixed",
    "field-zone",
    "field-mixed",
    "field-time",
    "field-mixed",
    "field-confirm",
    "field-confirm"
  ];

  return splitHexBytes(hex)
    .map((byte, index) => `<span class="hex-byte ${classes[index] || ""}">${byte}</span>`)
    .join("");
}

function setCopyButtonsEnabled(enabled) {
  refs.copyHexButton.disabled = !enabled;
  refs.copyBase64Button.disabled = !enabled;
}

function splitHexBytes(hex) {
  const bytes = [];
  for (let i = 0; i < hex.length; i += 2) {
    bytes.push(hex.slice(i, i + 2));
  }
  return bytes;
}

function shouldTrack() {
  const dnt = String(
    navigator.doNotTrack || window.doNotTrack || navigator.msDoNotTrack || ""
  ).toLowerCase();
  return dnt !== "1" && dnt !== "yes";
}

function trackEvent(key) {
  if (!shouldTrack() || !key) {
    return;
  }

  const endpoint = "https://countapi.mileshilliard.com/api/v1/hit/" + encodeURIComponent(key);
  fetch(endpoint, {
    method: "GET",
    cache: "no-store",
    mode: "no-cors",
    keepalive: true
  }).catch(() => {
    // Tracking is best effort and must never block UX.
  });
}

function parseRefCode() {
  const params = new URLSearchParams(window.location.search);
  const rawRef = params.get("ref");
  if (!rawRef) {
    return "";
  }
  const normalized = rawRef.trim().toLowerCase();
  return /^[a-z0-9_-]{1,32}$/.test(normalized) ? normalized : "";
}

async function copyTextBestEffort(text) {
  if (!text) {
    return false;
  }

  try {
    if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch (_) {
    // Fallback below.
  }

  try {
    const helper = document.createElement("textarea");
    helper.value = text;
    helper.setAttribute("readonly", "");
    helper.style.position = "fixed";
    helper.style.opacity = "0";
    helper.style.pointerEvents = "none";
    document.body.appendChild(helper);
    helper.select();
    const ok = document.execCommand("copy");
    document.body.removeChild(helper);
    return ok;
  } catch (_) {
    return false;
  }
}

function toPaddedHex(value, width) {
  return value.toString(16).toUpperCase().padStart(width, "0");
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;")
    .replaceAll("'", "&#39;");
}

function clamp(value, min, max) {
  if (value < min) {
    return min;
  }
  if (value > max) {
    return max;
  }
  return value;
}

function mustGet(id) {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing required element: ${id}`);
  }
  return element;
}

init();
