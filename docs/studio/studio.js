import {
  decodeUet,
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

const AUTOPLAY_DATA = Object.freeze({
  scenario: "FIRE / Building A / Critical / Zone 1001",
  uetHex: "0C000FA4C0E40000",
  uetBase64: "DAAPpMDkAAA=",
  envelopeHex: "EC50010303780101020304050607086553F1000000080C000FA4C0E400003A034F4B73B4D65E5B4AF650",
  vectorId: "uet-fire-critical-001",
  envelopeVectorId: "env-signed-uet-h12-001",
  capBytes: 669,
  jsonBytes: 270,
  envelopeBytes: 42,
  uetBytes: 8
});

const AUTOPLAY_STEPS = Object.freeze([
  Object.freeze({
    id: "scenario",
    title: "1) Scenario input",
    description: "Canonical deterministic scenario used for reproducible checks.",
    source: "Source vector: uet-fire-critical-001",
    durationMs: 2200
  }),
  Object.freeze({
    id: "uet-encode",
    title: "2) UET encoding",
    description: "UET bytes are revealed progressively from the canonical vector.",
    source: "Source vector: uet-fire-critical-001",
    durationMs: 2400
  }),
  Object.freeze({
    id: "decode-preview",
    title: "3) Decode preview",
    description: "Decoded fields are computed from the exact same UET bytes.",
    source: "Source vector: uet-fire-critical-001",
    durationMs: 2200
  }),
  Object.freeze({
    id: "envelope-wrap",
    title: "4) Envelope wrapping",
    description: "Header + payload + HMAC are appended deterministically.",
    source: "Source vector: env-signed-uet-h12-001",
    durationMs: 3000
  }),
  Object.freeze({
    id: "size-breakdown",
    title: "5) Byte breakdown",
    description: "42 bytes = header 22 + payload 8 + HMAC 12.",
    source: "Source vector: env-signed-uet-h12-001",
    durationMs: 2000
  }),
  Object.freeze({
    id: "signature-status",
    title: "6) Signature status",
    description: "Phase A shows deterministic precomputed signature evidence.",
    source: "Source vector: env-signed-uet-h12-001",
    durationMs: 2100
  }),
  Object.freeze({
    id: "comparison",
    title: "7) CAP vs JSON vs ECP",
    description: "Same scenario, measured and compared transparently.",
    source: "Source: samples/ProofCard + vectors",
    durationMs: 2500
  })
]);

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
  autoplayIndex: 0,
  autoplayPlaying: true,
  autoplaySpeed: 1,
  autoplayStepTimer: null,
  autoplayAnimationTimer: null,
  decodeDebounceTimer: null,
  lastDecoded: null,
  lastTrackedDecodedHex: ""
};

const refs = {};

function init() {
  bindRefs();
  setupTracking();
  setupAutoplay();
  setupDecode();
  setupVerify();
  decodeInitialSample();
}

function bindRefs() {
  refs.autoplaySteps = mustGet("autoplay-steps");
  refs.autoplayStepTitle = mustGet("autoplay-step-title");
  refs.autoplayStepDescription = mustGet("autoplay-step-description");
  refs.autoplayEvidence = mustGet("autoplay-evidence");
  refs.autoplaySource = mustGet("autoplay-source");
  refs.autoplayPlayPause = mustGet("autoplay-play-pause");
  refs.autoplaySpeed = mustGet("autoplay-speed");
  refs.autoplayReset = mustGet("autoplay-reset");

  refs.payloadInput = mustGet("payload-input");
  refs.decodeButton = mustGet("decode-button");
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
  refs.verifySummary = mustGet("verify-summary");
  refs.verifyResults = mustGet("verify-results");
}

function setupTracking() {
  trackEvent(TRACKING_KEYS.open);
  const refCode = parseRefCode();
  if (refCode) {
    trackEvent(`studio-ref-${refCode}`);
  }
}

function setupAutoplay() {
  refs.autoplaySteps.innerHTML = AUTOPLAY_STEPS.map((step, index) => (
    `<li><button type="button" class="autoplay-step-button" data-step-index="${index}">${escapeHtml(step.title)}</button></li>`
  )).join("");

  refs.autoplaySteps.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }
    const button = target.closest("button[data-step-index]");
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }
    const index = Number(button.getAttribute("data-step-index"));
    if (Number.isInteger(index)) {
      state.autoplayIndex = clamp(index, 0, AUTOPLAY_STEPS.length - 1);
      renderAutoplayStep();
      scheduleAutoplayStep();
    }
  });

  refs.autoplayPlayPause.addEventListener("click", () => {
    state.autoplayPlaying = !state.autoplayPlaying;
    refs.autoplayPlayPause.textContent = state.autoplayPlaying ? "Pause" : "Play";
    if (state.autoplayPlaying) {
      scheduleAutoplayStep();
    } else {
      clearAutoplayTimers();
    }
  });

  refs.autoplaySpeed.addEventListener("change", () => {
    const speed = Number.parseFloat(refs.autoplaySpeed.value);
    state.autoplaySpeed = Number.isFinite(speed) && speed > 0 ? speed : 1;
    scheduleAutoplayStep();
  });

  refs.autoplayReset.addEventListener("click", () => {
    clearAutoplayTimers();
    state.autoplayIndex = 0;
    state.autoplayPlaying = true;
    refs.autoplayPlayPause.textContent = "Pause";
    refs.autoplaySpeed.value = "1";
    state.autoplaySpeed = 1;
    renderAutoplayStep();
    scheduleAutoplayStep();
  });

  renderAutoplayStep();
  scheduleAutoplayStep();
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

  refs.decodeButton.addEventListener("click", () => {
    decodeCurrentInput();
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
  refs.verifyButton.addEventListener("click", () => {
    trackEvent(TRACKING_KEYS.verify);
    const report = verifyCoreVectors();
    refs.verifySummary.textContent = `Core suite: ${report.passed}/${report.total} passed`;
    refs.verifySummary.className = report.allPassed ? "verify-summary success" : "verify-summary failure";
    refs.verifyResults.innerHTML = report.results
      .map((result) => `<li>${escapeHtml(formatVectorStatusLine(result))}</li>`)
      .join("");
  });
}

function decodeInitialSample() {
  refs.payloadInput.value = AUTOPLAY_DATA.uetHex;
  decodeCurrentInput();
}

function decodeCurrentInput() {
  const raw = refs.payloadInput.value.trim();
  if (!raw) {
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
  refs.decodeResultPanel.hidden = true;
  setCopyButtonsEnabled(false);
  refs.decodeErrorPanel.hidden = false;
  refs.errorWhat.textContent = error?.what ?? "Unable to decode payload.";
  refs.errorWhy.textContent = error?.why ?? "Input does not match supported UET formats.";
  refs.errorFix.textContent = error?.howToFix ?? "Use a valid 8-byte UET in hex or base64.";
  refs.decodeFeedback.textContent = "Decode failed. See details below.";
}

function renderAutoplayStep() {
  const step = AUTOPLAY_STEPS[state.autoplayIndex];
  refs.autoplayStepTitle.textContent = step.title;
  refs.autoplayStepDescription.textContent = step.description;
  refs.autoplaySource.textContent = step.source;

  Array.from(refs.autoplaySteps.querySelectorAll("button[data-step-index]")).forEach((button, index) => {
    button.classList.toggle("active", index === state.autoplayIndex);
  });

  clearAutoplayAnimation();

  switch (step.id) {
    case "scenario":
      refs.autoplayEvidence.innerHTML = (
        `<p><strong>Scenario:</strong> ${escapeHtml(AUTOPLAY_DATA.scenario)}</p>` +
        `<p><strong>Canonical UET:</strong> <code>${AUTOPLAY_DATA.uetHex}</code></p>` +
        `<p><strong>Canonical base64:</strong> <code>${AUTOPLAY_DATA.uetBase64}</code></p>`
      );
      break;
    case "uet-encode":
      animateHexBytes(AUTOPLAY_DATA.uetHex, (index) => `hex-byte ecp byte-${index}`);
      break;
    case "decode-preview": {
      const decoded = decodeUet(AUTOPLAY_DATA.uetHex);
      refs.autoplayEvidence.innerHTML = (
        "<div class=\"table-wrap\"><table class=\"decoded-table\"><tbody>" +
        `<tr><td>EmergencyType</td><td>${decoded.emergencyType}</td><td>${escapeHtml(decoded.emergencyTypeLabel)}</td></tr>` +
        `<tr><td>Priority</td><td>${decoded.priority}</td><td>${escapeHtml(decoded.priorityLabel)}</td></tr>` +
        `<tr><td>ZoneHash</td><td>${decoded.zoneHash}</td><td>0x${toPaddedHex(decoded.zoneHash, 4)}</td></tr>` +
        `<tr><td>TimestampMinutes</td><td>${decoded.timestampMinutes}</td><td>0x${toPaddedHex(decoded.timestampMinutes, 4)}</td></tr>` +
        "</tbody></table></div>"
      );
      break;
    }
    case "envelope-wrap":
      animateHexBytes(AUTOPLAY_DATA.envelopeHex, (index) => {
        if (index < 22) {
          return "hex-byte header";
        }
        if (index < 30) {
          return "hex-byte payload";
        }
        return "hex-byte hmac";
      });
      break;
    case "size-breakdown":
      refs.autoplayEvidence.innerHTML = (
        "<div class=\"byte-stream\">" +
        `<span class="hex-byte header">Header: 22B</span>` +
        `<span class="hex-byte payload">Payload: 8B</span>` +
        `<span class="hex-byte hmac">HMAC: 12B</span>` +
        "</div>" +
        `<p><strong>Total envelope:</strong> ${AUTOPLAY_DATA.envelopeBytes} bytes</p>`
      );
      break;
    case "signature-status":
      refs.autoplayEvidence.innerHTML = (
        "<p><strong>Signature verification:</strong> ✅ PASS</p>" +
        "<p>Phase A evidence uses precomputed deterministic vectors. Interactive Web Crypto is added in Phase B.</p>"
      );
      break;
    case "comparison":
      refs.autoplayEvidence.innerHTML = (
        "<div class=\"byte-stream\">" +
        `<span class="hex-byte cap">CAP XML: ${AUTOPLAY_DATA.capBytes}B</span>` +
        `<span class="hex-byte json">JSON: ${AUTOPLAY_DATA.jsonBytes}B</span>` +
        `<span class="hex-byte payload">ECP Envelope: ${AUTOPLAY_DATA.envelopeBytes}B</span>` +
        `<span class="hex-byte ecp">ECP UET: ${AUTOPLAY_DATA.uetBytes}B</span>` +
        "</div>"
      );
      break;
    default:
      refs.autoplayEvidence.textContent = "";
      break;
  }
}

function scheduleAutoplayStep() {
  clearAutoplayTimers();
  if (!state.autoplayPlaying) {
    return;
  }

  const step = AUTOPLAY_STEPS[state.autoplayIndex];
  const delay = Math.max(800, Math.round(step.durationMs / state.autoplaySpeed));

  state.autoplayStepTimer = window.setTimeout(() => {
    state.autoplayIndex = (state.autoplayIndex + 1) % AUTOPLAY_STEPS.length;
    renderAutoplayStep();
    scheduleAutoplayStep();
  }, delay);
}

function animateHexBytes(hex, classResolver) {
  clearAutoplayAnimation();
  const bytes = splitHexBytes(hex);
  let visibleCount = 0;

  const renderBytes = () => {
    const html = bytes
      .slice(0, visibleCount)
      .map((byte, index) => `<span class="${classResolver(index)}">${byte}</span>`)
      .join("");
    refs.autoplayEvidence.innerHTML = `<div class="byte-stream">${html || "<span class=\"hex-byte\">...</span>"}</div>`;
  };

  renderBytes();
  state.autoplayAnimationTimer = window.setInterval(() => {
    visibleCount += 1;
    renderBytes();
    if (visibleCount >= bytes.length) {
      clearAutoplayAnimation();
    }
  }, Math.max(45, Math.round(95 / state.autoplaySpeed)));
}

function clearAutoplayTimers() {
  if (state.autoplayStepTimer !== null) {
    window.clearTimeout(state.autoplayStepTimer);
    state.autoplayStepTimer = null;
  }
  clearAutoplayAnimation();
}

function clearAutoplayAnimation() {
  if (state.autoplayAnimationTimer !== null) {
    window.clearInterval(state.autoplayAnimationTimer);
    state.autoplayAnimationTimer = null;
  }
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
      `<span class="tooltip-wrap">` +
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
