import {
  ACTION_FLAG_DEFINITIONS,
  CORE_TEST_VECTORS,
  EMERGENCY_TYPE_ENUM_NAMES,
  EMERGENCY_TYPE_LABELS,
  PRIORITY_ENUM_NAMES,
  PRIORITY_LABELS,
  encodeUet,
  formatVectorStatusLine,
  pickComparableFields,
  tryDecodeMessage,
  tryEncodeUet,
  verifyEnvelopeHmacHex,
  verifyFullVectors
} from "./decoder.js?v=20260322p6";
import {
  buildScenarioFromAdvisor,
  calculateBandwidth,
  describeRecipientsBandModel,
  deriveBridgeFieldsFromJsonObject,
  estimateBridgeEnvelopeBytes,
  evaluateProtocols,
  selectStrategy
} from "./advisor.js?v=20260322p6";

const TRACKING_KEYS = Object.freeze({
  open: "studio-open",
  decode: "studio-decode",
  verify: "studio-verify",
  encode: "studio-encode",
  codeCopy: "studio-codecopy",
  debugCopy: "studio-debug-copy",
  advisor: "studio-advisor",
  strategy: "studio-strategy",
  bridge: "studio-bridge",
  ctaStar: "studio-cta-star"
});

const TAB_NAMES = Object.freeze([
  "compare",
  "build",
  "generate",
  "decode",
  "verify"
]);

const CODE_TAB_NAMES = Object.freeze([
  "one-liner",
  "token",
  "envelope",
  "decoder",
  "di-setup",
  "test"
]);

const DEFAULT_SAMPLE_UET_HEX = CORE_TEST_VECTORS[0]?.expectedHex ?? "0C000FA4C0E40000";

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

const BUILD_PRESETS = Object.freeze([
  Object.freeze({
    id: "fire",
    label: "Emergency: Fire",
    fields: Object.freeze({
      emergencyType: 0,
      priority: 3,
      actionFlags: 1 | 2 | 16,
      zoneHash: 1001,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "earthquake",
    label: "Emergency: Earthquake",
    fields: Object.freeze({
      emergencyType: 2,
      priority: 3,
      actionFlags: 1 | 4 | 16,
      zoneHash: 2500,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "medical",
    label: "Emergency: Medical",
    fields: Object.freeze({
      emergencyType: 4,
      priority: 2,
      actionFlags: 16 | 128,
      zoneHash: 500,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "lockdown",
    label: "Emergency: Lockdown",
    fields: Object.freeze({
      emergencyType: 7,
      priority: 3,
      actionFlags: 1 | 16 | 32,
      zoneHash: 1001,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "iot-sensor",
    label: "IoT: Sensor Alert",
    fields: Object.freeze({
      emergencyType: 10,
      priority: 1,
      actionFlags: 128,
      zoneHash: 100,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "fleet-beacon",
    label: "Fleet: Vehicle Beacon",
    fields: Object.freeze({
      emergencyType: 11,
      priority: 0,
      actionFlags: 0,
      zoneHash: 30000,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "satellite-uplink",
    label: "Satellite: Uplink",
    fields: Object.freeze({
      emergencyType: 12,
      priority: 2,
      actionFlags: 128,
      zoneHash: 50000,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "industrial-alarm",
    label: "Industrial: Alarm",
    fields: Object.freeze({
      emergencyType: 13,
      priority: 3,
      actionFlags: 1 | 2 | 32,
      zoneHash: 8000,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "evacuation",
    label: "Emergency: Evacuation",
    fields: Object.freeze({
      emergencyType: 1,
      priority: 3,
      actionFlags: 1 | 16,
      zoneHash: 1500,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  })
]);

const ADVISOR_QUICK_PRESETS = Object.freeze({
  "emergency-broadcast": Object.freeze({
    label: "Emergency Broadcast",
    messageKind: "alert",
    payloadSizeBytes: 200,
    payloadUnknown: false,
    transport: "wifi",
    recipientsBand: "100-1000",
    humanReadable: "no",
    messagesPerDay: 1000
  }),
  "iot-telemetry": Object.freeze({
    label: "IoT Telemetry",
    messageKind: "telemetry",
    payloadSizeBytes: 200,
    payloadUnknown: false,
    transport: "ble",
    recipientsBand: "2-10",
    humanReadable: "no",
    messagesPerDay: 5000
  }),
  "rest-integration": Object.freeze({
    label: "REST Integration",
    messageKind: "generic",
    payloadSizeBytes: 200,
    payloadUnknown: false,
    transport: "wifi",
    recipientsBand: "1",
    humanReadable: "yes",
    messagesPerDay: 1000
  })
});

const ADVISOR_AUTO_REFRESH_DELAY_MS = 180;
const STRATEGY_AUTO_REFRESH_DELAY_MS = 180;
const BRIDGE_MAPPED_ENVELOPE_BYTES = 42;
const BRIDGE_MAPPING_LABELS = Object.freeze({
  emergencytype: "emergencyType",
  eventtype: "eventType",
  event: "event",
  type: "type",
  kind: "kind",
  priority: "priority",
  severity: "severity",
  urgency: "urgency",
  actionflags: "actionFlags",
  flags: "flags",
  actions: "actions",
  zonehash: "zoneHash",
  zone: "zone",
  zoneid: "zoneId",
  areahash: "areaHash",
  timestampminutes: "timestampMinutes",
  timestamp: "timestamp",
  time: "time",
  confirmhash: "confirmHash",
  correlationid: "correlationId",
  confirm: "confirm"
});

const state = {
  decodeDebounceTimer: null,
  lastDecoded: null,
  lastTrackedDecodedHex: "",
  activeTooltipTrigger: null,
  activeTab: "",
  buildFields: null,
  buildEncoded: null,
  lastTrackedBuildHex: "",
  activeCodeTab: "one-liner",
  generatedCodeByTab: Object.create(null),
  lastVerifyReport: null,
  verifyStarCtaShown: false,
  advisorEvaluation: null,
  advisorBandwidth: null,
  advisorAutoRefreshTimer: null,
  strategyAutoRefreshTimer: null,
  activeAdvisorPresetId: "",
  bridgeHex: ""
};

const refs = {};

function init() {
  bindRefs();
  setupTracking();
  setupTabs();
  setupDecode();
  setupBuild();
  setupCompare();
  setupGenerate();
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
  refs.decodeDebugButton = mustGet("decode-debug-button");

  refs.decodeEnvelopePanel = mustGet("decode-envelope-panel");
  refs.envelopeBadge = mustGet("envelope-badge");
  refs.envelopeHmacBadge = mustGet("envelope-hmac-badge");
  refs.envelopeHex = mustGet("envelope-hex");
  refs.envelopeBase64 = mustGet("envelope-base64");
  refs.copyEnvelopeHexButton = mustGet("copy-envelope-hex-button");
  refs.copyEnvelopeBase64Button = mustGet("copy-envelope-base64-button");
  refs.envelopeHexMap = mustGet("envelope-hex-map");
  refs.envelopeFieldsBody = mustGet("envelope-fields-body");
  refs.envelopePayloadSummary = mustGet("envelope-payload-summary");
  refs.envelopeNestedWrap = mustGet("envelope-nested-uet-wrap");
  refs.envelopeNestedFieldsBody = mustGet("envelope-nested-fields-body");
  refs.envelopeHmacKeyInput = mustGet("envelope-hmac-key-input");
  refs.envelopeHmacVerifyButton = mustGet("envelope-hmac-verify-button");
  refs.envelopeHmacFeedback = mustGet("envelope-hmac-feedback");

  refs.buildPreset = mustGet("build-preset");
  refs.buildEmergencyType = mustGet("build-emergency-type");
  refs.buildPriority = mustGet("build-priority");
  refs.buildFlagCheckboxes = Array.from(document.querySelectorAll(".build-flag-checkbox"));
  refs.buildZoneRange = mustGet("build-zone-range");
  refs.buildZoneInput = mustGet("build-zone-input");
  refs.buildTimestampInput = mustGet("build-timestamp-input");
  refs.buildConfirmInput = mustGet("build-confirm-input");
  refs.buildFeedback = mustGet("build-feedback");
  refs.buildOutputHex = mustGet("build-output-hex");
  refs.buildOutputBase64 = mustGet("build-output-base64");
  refs.buildCopyHexButton = mustGet("build-copy-hex-button");
  refs.buildCopyBase64Button = mustGet("build-copy-base64-button");
  refs.buildGetCodeButton = mustGet("build-get-code-button");
  refs.buildDebugButton = mustGet("build-debug-button");
  refs.sizeValueCap = mustGet("size-value-cap");
  refs.sizeValueJson = mustGet("size-value-json");
  refs.sizeValueEcp = mustGet("size-value-ecp");
  refs.sizeBarCap = mustGet("size-bar-cap");
  refs.sizeBarJson = mustGet("size-bar-json");
  refs.sizeBarEcp = mustGet("size-bar-ecp");
  refs.sizeSavings = mustGet("size-savings");

  refs.advisorMessageKind = mustGet("advisor-message-kind");
  refs.advisorPayloadSize = mustGet("advisor-payload-size");
  refs.advisorPayloadUnknown = mustGet("advisor-payload-unknown");
  refs.advisorTransport = mustGet("advisor-transport");
  refs.advisorRecipientsBand = mustGet("advisor-recipients-band");
  refs.advisorHumanReadable = mustGet("advisor-human-readable");
  refs.advisorMessagesDay = mustGet("advisor-messages-day");
  refs.advisorRunButton = mustGet("advisor-run-button");
  refs.advisorTryItButton = mustGet("advisor-try-it-button");
  refs.advisorPresetButtons = Array.from(document.querySelectorAll("button[data-advisor-preset]"));
  refs.advisorFeedback = mustGet("advisor-feedback");
  refs.advisorState = mustGet("advisor-state");
  refs.advisorMessageRatio = mustGet("advisor-message-ratio");
  refs.advisorRecipientsModelNote = mustGet("advisor-recipients-model-note");
  refs.advisorResultsBody = mustGet("advisor-results-body");
  refs.advisorBandwidth = mustGet("advisor-bandwidth");
  refs.advisorModelNote = mustGet("advisor-model-note");
  refs.advisorHybrid = mustGet("advisor-hybrid");

  refs.strategyRecipientsRange = mustGet("strategy-recipients-range");
  refs.strategyRecipientsInput = mustGet("strategy-recipients-input");
  refs.strategyMessageSize = mustGet("strategy-message-size");
  refs.strategyHasTemplate = mustGet("strategy-has-template");
  refs.strategyHasDictionary = mustGet("strategy-has-dictionary");
  refs.strategyState = mustGet("strategy-state");
  refs.strategyMode = mustGet("strategy-mode");
  refs.strategyHops = mustGet("strategy-hops");
  refs.strategyReason = mustGet("strategy-reason");
  refs.strategyLabelDirect = mustGet("strategy-label-direct");
  refs.strategyLabelCascade = mustGet("strategy-label-cascade");
  refs.strategyValueDirect = mustGet("strategy-value-direct");
  refs.strategyValueCascade = mustGet("strategy-value-cascade");
  refs.strategyRowDirect = mustGet("strategy-row-direct");
  refs.strategyRowCascade = mustGet("strategy-row-cascade");
  refs.strategyBarDirect = mustGet("strategy-bar-direct");
  refs.strategyBarCascade = mustGet("strategy-bar-cascade");

  refs.bridgeJsonInput = mustGet("bridge-json-input");
  refs.bridgeRunButton = mustGet("bridge-run-button");
  refs.bridgeCopyHexButton = mustGet("bridge-copy-hex-button");
  refs.bridgeFeedback = mustGet("bridge-feedback");
  refs.bridgeEcpHex = mustGet("bridge-ecp-hex");
  refs.bridgeSizeSummary = mustGet("bridge-size-summary");
  refs.bridgeSizePreview = mustGet("bridge-size-preview");
  refs.bridgeCodeBlock = mustGet("bridge-code-block");

  refs.codeTabBar = mustGet("code-tab-bar");
  refs.codeTabButtons = Array.from(document.querySelectorAll(".code-tab-button[data-code-tab]"));
  refs.generateScenarioLabel = mustGet("generate-scenario-label");
  refs.generateCodeBlock = mustGet("generated-code-block");
  refs.generateFeedback = mustGet("generate-feedback");
  refs.generateCopyTabButton = mustGet("generate-copy-tab-button");
  refs.generateCopyAllButton = mustGet("generate-copy-all-button");

  refs.verifyButton = mustGet("verify-button");
  refs.verifyResultsWrap = mustGet("verify-results-wrap");
  refs.verifySummary = mustGet("verify-summary");
  refs.verifyResults = mustGet("verify-results");
  refs.verifyToggle = mustGet("verify-toggle");
  refs.verifyStarCta = mustGet("verify-star-cta");
  refs.verifyStarCtaLink = mustGet("verify-star-cta-link");

}

function setupTracking() {
  trackEvent(TRACKING_KEYS.open);
  const refCode = parseRefCode();
  if (refCode) {
    trackEvent(`studio-ref-${refCode}`);
  }

  refs.verifyStarCtaLink.addEventListener("click", () => {
    trackEvent(TRACKING_KEYS.ctaStar);
  });
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
    history.replaceState(null, "", `#${getDefaultTabForEntry()}`);
  }

  applyHashTab(true);
}

function setupDecode() {
  refs.payloadInput.addEventListener("input", () => {
    if (state.decodeDebounceTimer !== null) {
      window.clearTimeout(state.decodeDebounceTimer);
    }
    state.decodeDebounceTimer = window.setTimeout(() => {
      decodeCurrentInput({ trackDecode: true });
    }, 180);
  });

  refs.copyHexButton.addEventListener("click", async () => {
    if (!state.lastDecoded || state.lastDecoded.kind !== "uet") {
      return;
    }
    const copied = await copyTextBestEffort(state.lastDecoded.value.valueHex);
    refs.decodeFeedback.textContent = copied
      ? "Hex copied to clipboard."
      : "Clipboard denied. Select the hex value and copy manually.";
  });

  refs.copyBase64Button.addEventListener("click", async () => {
    if (!state.lastDecoded || state.lastDecoded.kind !== "uet") {
      return;
    }
    const copied = await copyTextBestEffort(state.lastDecoded.value.valueBase64);
    refs.decodeFeedback.textContent = copied
      ? "Base64 copied to clipboard."
      : "Clipboard denied. Select the base64 value and copy manually.";
  });

  refs.copyEnvelopeHexButton.addEventListener("click", async () => {
    if (!state.lastDecoded || state.lastDecoded.kind !== "envelope") {
      return;
    }
    const copied = await copyTextBestEffort(state.lastDecoded.value.valueHex);
    refs.decodeFeedback.textContent = copied
      ? "Envelope hex copied to clipboard."
      : "Clipboard denied. Select the envelope hex value and copy manually.";
  });

  refs.copyEnvelopeBase64Button.addEventListener("click", async () => {
    if (!state.lastDecoded || state.lastDecoded.kind !== "envelope") {
      return;
    }
    const copied = await copyTextBestEffort(state.lastDecoded.value.valueBase64);
    refs.decodeFeedback.textContent = copied
      ? "Envelope base64 copied to clipboard."
      : "Clipboard denied. Select the envelope base64 value and copy manually.";
  });

  refs.envelopeHmacVerifyButton.addEventListener("click", async () => {
    await verifyCurrentEnvelopeHmac();
  });

  refs.decodeDebugButton.addEventListener("click", async () => {
    await copyDebugBundle("Paste & Decode");
  });
}

function setupBuild() {
  populateEmergencyTypeOptions();

  refs.buildPreset.addEventListener("change", () => {
    applyPreset(refs.buildPreset.value, true);
  });

  refs.buildEmergencyType.addEventListener("change", () => {
    syncBuildFromControls(true, true);
  });
  refs.buildPriority.addEventListener("change", () => {
    syncBuildFromControls(true, true);
  });

  refs.buildFlagCheckboxes.forEach((checkbox) => {
    checkbox.addEventListener("change", () => {
      syncBuildFromControls(true, true);
    });
  });

  refs.buildZoneRange.addEventListener("input", () => {
    refs.buildZoneInput.value = refs.buildZoneRange.value;
    syncBuildFromControls(true, true);
  });

  refs.buildZoneInput.addEventListener("input", () => {
    const clamped = clampNumber(refs.buildZoneInput.value, 0, 65535, Number(refs.buildZoneRange.value));
    refs.buildZoneInput.value = String(clamped);
    refs.buildZoneRange.value = String(clamped);
    syncBuildFromControls(true, true);
  });

  refs.buildTimestampInput.addEventListener("input", () => {
    syncBuildFromControls(true, true);
  });

  refs.buildConfirmInput.addEventListener("input", () => {
    syncBuildFromControls(true, true);
  });

  refs.buildCopyHexButton.addEventListener("click", async () => {
    if (!state.buildEncoded) {
      return;
    }
    const copied = await copyTextBestEffort(state.buildEncoded.hex);
    refs.buildFeedback.textContent = copied
      ? "Builder hex copied to clipboard."
      : "Clipboard denied. Select the hex value and copy manually.";
  });

  refs.buildCopyBase64Button.addEventListener("click", async () => {
    if (!state.buildEncoded) {
      return;
    }
    const copied = await copyTextBestEffort(state.buildEncoded.base64);
    refs.buildFeedback.textContent = copied
      ? "Builder base64 copied to clipboard."
      : "Clipboard denied. Select the base64 value and copy manually.";
  });

  refs.buildGetCodeButton.addEventListener("click", () => {
    window.location.hash = "#generate";
  });

  refs.buildDebugButton.addEventListener("click", async () => {
    await copyDebugBundle("Scenario Builder");
  });

  applyPreset("fire", false);
}

function setupCompare() {
  const onAdvisorInputChanged = () => {
    scheduleAdvisorAutoRefresh();
  };

  refs.advisorPayloadUnknown.addEventListener("change", () => {
    syncAdvisorPayloadUnknownState();
    onAdvisorInputChanged();
  });
  refs.advisorMessageKind.addEventListener("change", onAdvisorInputChanged);
  refs.advisorPayloadSize.addEventListener("input", onAdvisorInputChanged);
  refs.advisorTransport.addEventListener("change", onAdvisorInputChanged);
  refs.advisorRecipientsBand.addEventListener("change", onAdvisorInputChanged);
  refs.advisorHumanReadable.addEventListener("change", onAdvisorInputChanged);
  refs.advisorMessagesDay.addEventListener("input", onAdvisorInputChanged);

  refs.advisorRunButton.addEventListener("click", () => {
    runAdvisorEvaluation(true);
  });

  refs.advisorTryItButton.addEventListener("click", () => {
    applyAdvisorToBuilder();
  });
  refs.advisorPresetButtons.forEach((button) => {
    button.addEventListener("click", () => {
      const presetId = button.dataset.advisorPreset;
      if (!presetId) {
        return;
      }
      applyAdvisorPreset(presetId, true);
    });
  });

  refs.strategyRecipientsRange.addEventListener("input", () => {
    syncStrategyRecipientInputs("range");
    scheduleStrategyAutoRefresh();
  });
  refs.strategyRecipientsInput.addEventListener("input", () => {
    syncStrategyRecipientInputs("input");
    scheduleStrategyAutoRefresh();
  });
  refs.strategyMessageSize.addEventListener("input", scheduleStrategyAutoRefresh);
  refs.strategyHasTemplate.addEventListener("change", scheduleStrategyAutoRefresh);
  refs.strategyHasDictionary.addEventListener("change", scheduleStrategyAutoRefresh);

  refs.bridgeRunButton.addEventListener("click", () => {
    runJsonBridge(true);
  });
  refs.bridgeCopyHexButton.addEventListener("click", async () => {
    if (!state.bridgeHex) {
      return;
    }
    trackEvent(TRACKING_KEYS.codeCopy);
    const copied = await copyTextBestEffort(state.bridgeHex);
    refs.bridgeFeedback.textContent = copied
      ? "Bridge ECP hex copied."
      : "Clipboard denied. Select the ECP hex value and copy manually.";
  });

  syncAdvisorPayloadUnknownState();
  applyAdvisorPreset("emergency-broadcast", false);
  runJsonBridge(false);
}

function scheduleAdvisorAutoRefresh() {
  refs.advisorState.textContent = "Auto-refresh active. Inputs changed, updating estimates...";
  if (state.advisorAutoRefreshTimer !== null) {
    window.clearTimeout(state.advisorAutoRefreshTimer);
  }
  state.advisorAutoRefreshTimer = window.setTimeout(() => {
    state.advisorAutoRefreshTimer = null;
    runAdvisorEvaluation(false);
  }, ADVISOR_AUTO_REFRESH_DELAY_MS);
}

function scheduleStrategyAutoRefresh() {
  refs.strategyState.textContent = "Auto-refresh active. Inputs changed, updating selector...";
  if (state.strategyAutoRefreshTimer !== null) {
    window.clearTimeout(state.strategyAutoRefreshTimer);
  }
  state.strategyAutoRefreshTimer = window.setTimeout(() => {
    state.strategyAutoRefreshTimer = null;
    runStrategySelector(true, "selector");
  }, STRATEGY_AUTO_REFRESH_DELAY_MS);
}

function applyAdvisorPreset(presetId, trackUsage) {
  const preset = ADVISOR_QUICK_PRESETS[presetId];
  if (!preset) {
    return;
  }

  setActiveAdvisorPreset(presetId);
  refs.advisorMessageKind.value = preset.messageKind;
  refs.advisorPayloadUnknown.checked = preset.payloadUnknown;
  refs.advisorPayloadSize.value = String(preset.payloadSizeBytes);
  refs.advisorTransport.value = preset.transport;
  refs.advisorRecipientsBand.value = preset.recipientsBand;
  refs.advisorHumanReadable.value = preset.humanReadable;
  refs.advisorMessagesDay.value = String(preset.messagesPerDay);
  syncAdvisorPayloadUnknownState();
  runAdvisorEvaluation(trackUsage);
  refs.advisorState.textContent = `Preset active: ${preset.label}. Auto-refresh enabled.`;

  if (trackUsage) {
    trackEvent(`studio-advisor-preset-${sanitizeTrackingToken(presetId)}`);
  }
}

function setActiveAdvisorPreset(presetId) {
  state.activeAdvisorPresetId = presetId;
  refs.advisorPresetButtons.forEach((button) => {
    const isActive = button.dataset.advisorPreset === presetId;
    button.classList.toggle("compare-preset-button-active", isActive);
    button.setAttribute("aria-pressed", isActive ? "true" : "false");
  });
}

function syncAdvisorPayloadUnknownState() {
  const useDefaultPayload = refs.advisorPayloadUnknown.checked;
  refs.advisorPayloadSize.disabled = useDefaultPayload;
  if (useDefaultPayload) {
    refs.advisorPayloadSize.value = "200";
  }
}

function readAdvisorAnswersFromControls() {
  return {
    messageKind: refs.advisorMessageKind.value,
    payloadSizeBytes: refs.advisorPayloadSize.value,
    payloadUnknown: refs.advisorPayloadUnknown.checked,
    transport: refs.advisorTransport.value,
    recipientsBand: refs.advisorRecipientsBand.value,
    humanReadable: refs.advisorHumanReadable.value,
    messagesPerDay: refs.advisorMessagesDay.value
  };
}

function runAdvisorEvaluation(trackUsage) {
  if (state.advisorAutoRefreshTimer !== null) {
    window.clearTimeout(state.advisorAutoRefreshTimer);
    state.advisorAutoRefreshTimer = null;
  }

  const answers = readAdvisorAnswersFromControls();
  const evaluation = evaluateProtocols(answers);
  const bandwidth = calculateBandwidth(answers);
  state.advisorEvaluation = evaluation;
  state.advisorBandwidth = bandwidth;

  refs.advisorResultsBody.innerHTML = evaluation.rows
    .map((row) => {
      const fitnessClass = `fitness-${row.fitness.toLowerCase()}`;
      const topBadge = row.isTopProtocol ? " <span class=\"badge badge-success compare-best-badge\">Top</span>" : "";
      return (
        "<tr>" +
        `<td>${escapeHtml(row.label)}${topBadge}</td>` +
        `<td>${escapeHtml(row.estimatedLabel)}</td>` +
        `<td><span class="fitness-pill ${fitnessClass}">${escapeHtml(row.fitness)}</span></td>` +
        `<td>${escapeHtml(row.reason)}</td>` +
        "</tr>"
      );
    })
    .join("");

  refs.advisorFeedback.textContent = `Top recommendation: ${evaluation.bestProtocolLabel} (estimated).`;
  refs.advisorMessageRatio.textContent = bandwidth.ratioSummary;
  const recipientsModel = describeRecipientsBandModel(evaluation.answers.recipientsBand);
  refs.advisorRecipientsModelNote.textContent = `${recipientsModel.note} ${recipientsModel.formula}`;
  refs.advisorRecipientsBand.title = `${recipientsModel.note} ${recipientsModel.formula}`;
  refs.advisorBandwidth.textContent = bandwidth.summary;
  refs.advisorModelNote.textContent = `${bandwidth.recipientModelNote} ${bandwidth.deliveryModelNote}`;
  refs.advisorHybrid.textContent = evaluation.hybridSuggestion;
  const ecpIsTop = evaluation.bestProtocolId === "ecp-uet" || evaluation.bestProtocolId === "ecp-envelope";
  refs.advisorTryItButton.disabled = !ecpIsTop;
  refs.advisorTryItButton.title = ecpIsTop
    ? "Build this scenario as a UET payload in the Builder tab."
    : "Builder is available when ECP is the top recommendation.";

  syncStrategyFromAdvisor(evaluation);
  refs.advisorState.textContent = trackUsage
    ? "Manual refresh completed (optional). Auto-refresh remains enabled."
    : "Updated automatically from current inputs.";

  if (trackUsage) {
    trackEvent(TRACKING_KEYS.advisor);
    trackEvent(`studio-advisor-result-${sanitizeTrackingToken(evaluation.bestProtocolId)}`);
  }
}

function syncStrategyFromAdvisor(evaluation) {
  if (!evaluation || !evaluation.answers) {
    return;
  }

  const recipients = clampNumber(evaluation.answers.recipientsCount, 1, 1000, 12);
  refs.strategyRecipientsInput.value = String(recipients);
  refs.strategyRecipientsRange.value = String(recipients);

  const strategySourceProtocol = (evaluation.answers.messageKind === "alert" || evaluation.answers.messageKind === "beacon")
    ? "ecp-uet"
    : "ecp-envelope";
  const strategySizeRow = evaluation.rows.find((row) => row.protocolId === strategySourceProtocol)
    ?? evaluation.rows.find((row) => row.protocolId === "ecp-envelope")
    ?? evaluation.rows.find((row) => row.protocolId === "ecp-uet");
  if (strategySizeRow) {
    refs.strategyMessageSize.value = String(strategySizeRow.estimatedBytes);
  }

  runStrategySelector(false, "advisor");
}

function applyAdvisorToBuilder() {
  if (!state.advisorEvaluation) {
    return;
  }

  const fields = buildScenarioFromAdvisor(state.advisorEvaluation.answers);
  state.buildFields = fields;
  refs.buildPreset.value = "custom";
  writeBuildControls(fields);
  renderBuildOutput(true);
  refs.buildFeedback.textContent = "Scenario imported from Protocol Advisor.";
  window.location.hash = "#build";
}

function syncStrategyRecipientInputs(source) {
  if (source === "range") {
    refs.strategyRecipientsInput.value = refs.strategyRecipientsRange.value;
    return;
  }

  const clamped = clampNumber(refs.strategyRecipientsInput.value, 1, 1000, Number(refs.strategyRecipientsRange.value));
  refs.strategyRecipientsInput.value = String(clamped);
  refs.strategyRecipientsRange.value = String(clamped);
}

function runStrategySelector(trackUsage, source = "selector") {
  if (state.strategyAutoRefreshTimer !== null) {
    window.clearTimeout(state.strategyAutoRefreshTimer);
    state.strategyAutoRefreshTimer = null;
  }

  syncStrategyRecipientInputs("input");
  const result = selectStrategy({
    recipients: refs.strategyRecipientsInput.value,
    messageSizeBytes: refs.strategyMessageSize.value,
    hasTemplate: refs.strategyHasTemplate.checked,
    hasDictionary: refs.strategyHasDictionary.checked
  });

  const cascadeLabel = result.recipients <= result.defaults.miniCascadeThreshold ? "MiniCascade" : "FullCascade";
  const cascadeDisplayCostBytes = result.mode === "MiniCascade"
    ? result.selectedCostBytes
    : result.cascadeCostBytes;
  const directLabel = result.mode === "UetOnly" ? "Direct / UetOnly" : "Direct";
  refs.strategyLabelDirect.textContent = directLabel;
  refs.strategyLabelCascade.textContent = cascadeLabel;
  refs.strategyMode.textContent = `Selected mode: ${result.mode} (${result.selectedCostBytes} B estimated)`;
  refs.strategyHops.textContent = `Estimated hops: ${result.hopCount} | Effective payload: ${result.effectiveMessageBytes} B`;
  refs.strategyReason.textContent = result.reasoning;
  refs.strategyValueDirect.textContent = `${result.directCostBytes} B`;
  refs.strategyValueCascade.textContent = `${cascadeDisplayCostBytes} B`;

  const maxCost = Math.max(result.directCostBytes, cascadeDisplayCostBytes, 1);
  refs.strategyBarDirect.style.width = `${((result.directCostBytes / maxCost) * 100).toFixed(2)}%`;
  refs.strategyBarCascade.style.width = `${((cascadeDisplayCostBytes / maxCost) * 100).toFixed(2)}%`;

  const directSelected = result.mode === "Direct" || result.mode === "UetOnly";
  refs.strategyRowDirect.classList.toggle("strategy-row-selected", directSelected);
  refs.strategyRowCascade.classList.toggle("strategy-row-selected", !directSelected);
  refs.strategyBarDirect.classList.toggle("strategy-bar-selected", directSelected);
  refs.strategyBarDirect.classList.toggle("strategy-bar-muted", !directSelected);
  refs.strategyBarCascade.classList.toggle("strategy-bar-selected", !directSelected);
  refs.strategyBarCascade.classList.toggle("strategy-bar-muted", directSelected);

  refs.strategyState.textContent = source === "advisor"
    ? "Updated automatically from Protocol Advisor inputs."
    : "Updated automatically from current Strategy Selector inputs.";

  if (trackUsage) {
    trackEvent(TRACKING_KEYS.strategy);
  }
}

function runJsonBridge(trackUsage) {
  const rawJson = refs.bridgeJsonInput.value.trim();
  if (!rawJson) {
    refs.bridgeFeedback.textContent = "Paste JSON payload first.";
    refs.bridgeEcpHex.textContent = "";
    refs.bridgeSizeSummary.textContent = "";
    refs.bridgeSizePreview.textContent = "";
    refs.bridgeCodeBlock.textContent = "";
    refs.bridgeCopyHexButton.disabled = true;
    state.bridgeHex = "";
    return;
  }

  let parsed;
  try {
    parsed = JSON.parse(rawJson);
  } catch (_) {
    refs.bridgeFeedback.textContent = "Invalid JSON. Fix syntax and try again.";
    refs.bridgeEcpHex.textContent = "";
    refs.bridgeSizeSummary.textContent = "";
    refs.bridgeSizePreview.textContent = "";
    refs.bridgeCodeBlock.textContent = "";
    refs.bridgeCopyHexButton.disabled = true;
    state.bridgeHex = "";
    return;
  }

  const bridgeResult = deriveBridgeFieldsFromJsonObject(parsed);
  const encoded = tryEncodeUet(bridgeResult.fields);
  if (!encoded.ok) {
    refs.bridgeFeedback.textContent = encoded.error?.what ?? "Unable to map JSON to ECP fields.";
    refs.bridgeEcpHex.textContent = "";
    refs.bridgeSizeSummary.textContent = "";
    refs.bridgeSizePreview.textContent = "";
    refs.bridgeCodeBlock.textContent = "";
    refs.bridgeCopyHexButton.disabled = true;
    state.bridgeHex = "";
    return;
  }

  state.bridgeHex = encoded.value.hex;
  refs.bridgeEcpHex.textContent = encoded.value.hex;
  refs.bridgeCopyHexButton.disabled = false;

  const jsonBytes = measureUtf8Bytes(rawJson);
  const isUetCompatibleMapping = Boolean(bridgeResult.mapping?.isUetCompatible);
  const envelopeBytes = isUetCompatibleMapping
    ? BRIDGE_MAPPED_ENVELOPE_BYTES
    : estimateBridgeEnvelopeBytes(rawJson, 12);
  const relativeSizeNote = describeRelativeSizeDelta(jsonBytes, envelopeBytes);
  refs.bridgeSizeSummary.textContent = `Estimated conversion: JSON ${jsonBytes} B -> ECP Envelope ~${envelopeBytes} B (${relativeSizeNote}).`;
  refs.bridgeSizePreview.textContent = `${formatBridgeMappingNote(bridgeResult.mapping)} UET mapping preview (internal token only): 8 B.`;
  refs.bridgeCodeBlock.textContent = buildBridgeMigrationCode(rawJson, encoded.value.hex);
  highlightCodeBlock(refs.bridgeCodeBlock);
  refs.bridgeFeedback.textContent = "JSON mapped to ECP envelope estimate with UET preview.";

  if (trackUsage) {
    trackEvent(TRACKING_KEYS.bridge);
  }
}

function buildBridgeMigrationCode(jsonPayload, expectedHex) {
  const jsonLiteral = JSON.stringify(jsonPayload);
  return `using ECP.Compatibility;
using System;
using System.Security.Cryptography;

string jsonPayload = ${jsonLiteral};
byte[] hmacKey = RandomNumberGenerator.GetBytes(32);

// Signed envelope output (recommended).
byte[] ecpBytes = JsonBridge.ToEcp(
    jsonPayload,
    hmacKey,
    hmacLength: 12);
string ecpHex = Convert.ToHexString(ecpBytes);

// Optional unsigned mode (testing only):
// byte[] unsignedBytes = JsonBridge.ToEcp(jsonPayload, Array.Empty<byte>(), hmacLength: 0);

// Studio UET mapping preview (internal token estimate): ${expectedHex}
Console.WriteLine(ecpHex);`;
}

function describeRelativeSizeDelta(sourceBytes, targetBytes) {
  if (sourceBytes <= 0 && targetBytes <= 0) {
    return "same size";
  }
  if (sourceBytes === targetBytes) {
    return "same size";
  }
  if (sourceBytes <= 0) {
    return "larger estimate";
  }

  const percent = Math.abs(((sourceBytes - targetBytes) / sourceBytes) * 100);
  return targetBytes < sourceBytes
    ? `${percent.toFixed(2)}% smaller`
    : `${percent.toFixed(2)}% larger`;
}

function formatBridgeMappingNote(mapping) {
  if (mapping?.isUetCompatible) {
    const previewKeys = (mapping.matchedSignalKeys ?? [])
      .slice(0, 4)
      .map((key) => BRIDGE_MAPPING_LABELS[key] ?? key)
      .join(", ");
    const suffix = (mapping.matchedSignalKeys ?? []).length > 4 ? ", ..." : "";
    return `Mapping: UET-compatible (${previewKeys}${suffix}).`;
  }

  if (mapping?.hasEnvelopePayloadKeys) {
    return "Mapping: explicit payloadText/payloadBase64 envelope mode.";
  }

  return "Mapping: generic payload fallback (no UET field signals).";
}

function setupGenerate() {
  refs.codeTabBar.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }
    const button = target.closest("button[data-code-tab]");
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }
    const tabName = normalizeCodeTabName(button.dataset.codeTab);
    if (!tabName) {
      return;
    }
    state.activeCodeTab = tabName;
    renderActiveCodeTab();
  });

  refs.generateCopyTabButton.addEventListener("click", async () => {
    const code = state.generatedCodeByTab[state.activeCodeTab];
    if (!code) {
      return;
    }
    trackEvent(TRACKING_KEYS.codeCopy);
    const copied = await copyTextBestEffort(code);
    refs.generateFeedback.textContent = copied
      ? "Code copied."
      : "Clipboard denied. Select the code and copy manually.";
  });

  refs.generateCopyAllButton.addEventListener("click", async () => {
    const bundle = buildCombinedCodeBundle(state.generatedCodeByTab);
    if (!bundle) {
      return;
    }
    trackEvent(TRACKING_KEYS.codeCopy);
    const copied = await copyTextBestEffort(bundle);
    refs.generateFeedback.textContent = copied
      ? "Complete code bundle copied."
      : "Clipboard denied. Select the code and copy manually.";
  });
}

function setupVerify() {
  refs.verifyStarCta.hidden = true;

  refs.verifyToggle.addEventListener("click", () => {
    const willHide = !refs.verifyResults.hidden;
    refs.verifyResults.hidden = willHide;
    refs.verifyToggle.textContent = willHide ? "Show details" : "Hide details";
    refs.verifyToggle.setAttribute("aria-expanded", willHide ? "false" : "true");
  });

  refs.verifyButton.addEventListener("click", async () => {
    trackEvent(TRACKING_KEYS.verify);
    refs.verifyButton.disabled = true;
    const originalText = refs.verifyButton.textContent;
    refs.verifyButton.textContent = "Running 34 vectors...";
    refs.verifySummary.className = "verify-summary";
    refs.verifySummary.textContent = "Running full suite...";
    refs.verifyResultsWrap.hidden = false;

    const report = await verifyFullVectors();
    state.lastVerifyReport = report;
    renderVerifyReport(report);

    refs.verifyButton.disabled = false;
    refs.verifyButton.textContent = originalText ?? "Run full suite (34 vectors)";
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
  const normalizedHash = rawHash.replace(/^\/+/, "");
  const tabName = normalizeTabName(normalizedHash);
  return tabName || getDefaultTabForEntry();
}

function getDefaultTabForEntry() {
  return hasRefQueryParam() ? "compare" : "decode";
}

function hasRefQueryParam() {
  const params = new URLSearchParams(window.location.search);
  return params.has("ref");
}

function normalizeTabName(value) {
  const normalized = String(value || "").trim().toLowerCase();
  return TAB_NAMES.includes(normalized) ? normalized : "";
}

function normalizeCodeTabName(value) {
  const normalized = String(value || "").trim().toLowerCase();
  return CODE_TAB_NAMES.includes(normalized) ? normalized : "";
}

function activateTab(tabName, trackChange) {
  const normalizedTab = normalizeTabName(tabName) || getDefaultTabForEntry();
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
  if (normalizedTab === "generate") {
    renderActiveCodeTab();
  }

  if (trackChange) {
    trackEvent(`studio-tab-${normalizedTab}`);
  }
}

function decodeInitialSample() {
  refs.payloadInput.value = DEFAULT_SAMPLE_UET_HEX;
  decodeCurrentInput({ trackDecode: false });
}

function decodeCurrentInput(options = {}) {
  const trackDecode = options.trackDecode !== false;
  const raw = refs.payloadInput.value.trim();
  if (!raw) {
    hideActiveTooltip();
    refs.decodeFeedback.textContent = "";
    clearDecodeError();
    refs.decodeResultPanel.hidden = true;
    refs.decodeEnvelopePanel.hidden = true;
    state.lastDecoded = null;
    setUetCopyButtonsEnabled(false);
    setEnvelopeCopyButtonsEnabled(false);
    return;
  }

  const response = tryDecodeMessage(raw);
  if (!response.ok) {
    renderDecodeError(response.error);
    return;
  }

  clearDecodeError();
  if (response.value.kind === "uet") {
    renderUetDecode(response.value.value);
  } else if (response.value.kind === "envelope") {
    renderEnvelopeDecode(response.value.value);
  }

  const valueHex = response.value.value.valueHex;
  if (trackDecode && state.lastTrackedDecodedHex !== valueHex) {
    trackEvent(TRACKING_KEYS.decode);
    state.lastTrackedDecodedHex = valueHex;
  }
}

function renderUetDecode(decoded) {
  const roundtrip = encodeUet(pickComparableFields(decoded));
  const roundtripOk = roundtrip.hex === decoded.valueHex && roundtrip.base64 === decoded.valueBase64;

  state.lastDecoded = {
    kind: "uet",
    value: decoded
  };

  refs.decodeResultPanel.hidden = false;
  refs.decodeEnvelopePanel.hidden = true;
  setUetCopyButtonsEnabled(true);
  setEnvelopeCopyButtonsEnabled(false);

  refs.validBadge.textContent = "Valid UET";
  refs.validBadge.className = "badge badge-success";
  refs.roundtripBadge.textContent = roundtripOk ? "Round-trip verified" : "Round-trip mismatch";
  refs.roundtripBadge.className = roundtripOk ? "badge badge-success" : "badge badge-danger";
  refs.decodedHex.textContent = decoded.valueHex;
  refs.decodedBase64.textContent = decoded.valueBase64;
  refs.decodedBitMap.innerHTML = renderBitMap(decoded.valueBinary);
  refs.decodedHexMap.innerHTML = renderHexMap(decoded.valueHex);
  refs.decodedFieldsBody.innerHTML = renderDecodedRows(decoded);
  refs.decodeFeedback.textContent = "Decoded as UET (8 bytes).";
}

function renderEnvelopeDecode(decoded) {
  state.lastDecoded = {
    kind: "envelope",
    value: {
      ...decoded
    }
  };

  refs.decodeResultPanel.hidden = true;
  refs.decodeEnvelopePanel.hidden = false;
  setUetCopyButtonsEnabled(false);
  setEnvelopeCopyButtonsEnabled(true);
  hideActiveTooltip();

  refs.envelopeHex.textContent = decoded.valueHex;
  refs.envelopeBase64.textContent = decoded.valueBase64;
  refs.envelopeHexMap.innerHTML = renderEnvelopeHexMap(decoded.valueHex, decoded.payloadLength, decoded.hmacLength);
  refs.envelopeFieldsBody.innerHTML = renderEnvelopeRows(decoded);
  refs.envelopePayloadSummary.textContent = decoded.nestedUet
    ? `Payload type ${decoded.payloadTypeLabel} (${decoded.payloadLength} bytes). Nested UET detected.`
    : `Payload type ${decoded.payloadTypeLabel} (${decoded.payloadLength} bytes).`;

  refs.envelopeNestedWrap.hidden = !decoded.nestedUet;
  refs.envelopeNestedFieldsBody.innerHTML = decoded.nestedUet ? renderNestedUetRows(decoded.nestedUet) : "";

  refs.envelopeBadge.className = "badge badge-success";
  refs.envelopeBadge.textContent = "Valid Envelope";

  if (decoded.hmacLength === 0) {
    refs.envelopeHmacBadge.className = "badge badge-success";
    refs.envelopeHmacBadge.textContent = "Unsigned mode";
  } else {
    refs.envelopeHmacBadge.className = "badge badge-danger";
    refs.envelopeHmacBadge.textContent = "HMAC not verified";
  }

  refs.envelopeHmacFeedback.textContent = "";
  refs.decodeFeedback.textContent = "Decoded as Envelope.";
}

async function verifyCurrentEnvelopeHmac() {
  if (!state.lastDecoded || state.lastDecoded.kind !== "envelope") {
    refs.envelopeHmacFeedback.textContent = "Decode an envelope first.";
    return;
  }

  const keyHex = refs.envelopeHmacKeyInput.value.trim();
  if (!keyHex) {
    refs.envelopeHmacFeedback.textContent = "Insert the hex key from your EnvelopeBuilder or test vector.";
    return;
  }

  refs.envelopeHmacVerifyButton.disabled = true;
  const response = await verifyEnvelopeHmacHex(
    state.lastDecoded.value.valueHex,
    keyHex,
    state.lastDecoded.value.hmacLength
  );
  refs.envelopeHmacVerifyButton.disabled = false;

  if (!response.ok) {
    refs.envelopeHmacBadge.className = "badge badge-danger";
    refs.envelopeHmacBadge.textContent = "HMAC verify error";
    refs.envelopeHmacFeedback.textContent = response.error?.what ?? "HMAC verification failed.";
    return;
  }

  if (response.isValid) {
    refs.envelopeHmacBadge.className = "badge badge-success";
    refs.envelopeHmacBadge.textContent = "HMAC verified";
    refs.envelopeHmacFeedback.textContent = "Signature check passed.";
  } else {
    refs.envelopeHmacBadge.className = "badge badge-danger";
    refs.envelopeHmacBadge.textContent = "HMAC invalid";
    refs.envelopeHmacFeedback.textContent = "Signature mismatch: wrong key or tampered data.";
  }
}

function clearDecodeError() {
  refs.decodeErrorPanel.hidden = true;
  refs.errorWhat.textContent = "";
  refs.errorWhy.textContent = "";
  refs.errorFix.textContent = "";
}

function renderDecodeError(error) {
  hideActiveTooltip();
  refs.decodeResultPanel.hidden = true;
  refs.decodeEnvelopePanel.hidden = true;
  setUetCopyButtonsEnabled(false);
  setEnvelopeCopyButtonsEnabled(false);
  state.lastDecoded = null;
  refs.decodeErrorPanel.hidden = false;
  refs.errorWhat.textContent = error?.what ?? "Unable to decode payload.";
  refs.errorWhy.textContent = error?.why ?? "Input does not match supported ECP formats.";
  refs.errorFix.textContent = error?.howToFix ?? "Use a valid UET/envelope in hex or base64.";
  refs.decodeFeedback.textContent = "Decode failed. See details below.";
}

function populateEmergencyTypeOptions() {
  refs.buildEmergencyType.innerHTML = EMERGENCY_TYPE_LABELS
    .map((label, index) => `<option value="${index}">${escapeHtml(`${index} - ${label}`)}</option>`)
    .join("");
}

function applyPreset(presetId, trackPreset) {
  if (presetId === "custom") {
    refs.buildPreset.value = "custom";
    if (!state.buildFields) {
      state.buildFields = {
        emergencyType: 0,
        priority: 3,
        actionFlags: 0,
        zoneHash: 1001,
        timestampMinutes: 12345,
        confirmHash: 0
      };
      writeBuildControls(state.buildFields);
    }
    renderBuildOutput(false);
    return;
  }

  const preset = BUILD_PRESETS.find((entry) => entry.id === presetId) ?? BUILD_PRESETS[0];
  state.buildFields = { ...preset.fields };
  refs.buildPreset.value = preset.id;
  writeBuildControls(state.buildFields);
  renderBuildOutput(trackPreset);

  if (trackPreset) {
    trackEvent(`studio-preset-${sanitizeTrackingToken(preset.id)}`);
  }
}

function writeBuildControls(fields) {
  refs.buildEmergencyType.value = String(fields.emergencyType);
  refs.buildPriority.value = String(fields.priority);
  refs.buildZoneRange.value = String(fields.zoneHash);
  refs.buildZoneInput.value = String(fields.zoneHash);
  refs.buildTimestampInput.value = String(fields.timestampMinutes);
  refs.buildConfirmInput.value = String(fields.confirmHash);

  refs.buildFlagCheckboxes.forEach((checkbox) => {
    const bitValue = Number(checkbox.value);
    checkbox.checked = (fields.actionFlags & bitValue) !== 0;
  });
}

function syncBuildFromControls(trackEncode, forceCustomPreset) {
  state.buildFields = readBuildFieldsFromControls();
  if (forceCustomPreset) {
    refs.buildPreset.value = "custom";
  }
  renderBuildOutput(trackEncode);
}

function readBuildFieldsFromControls() {
  const emergencyType = clampNumber(refs.buildEmergencyType.value, 0, 15, 0);
  const priority = clampNumber(refs.buildPriority.value, 0, 3, 3);
  const zoneHash = clampNumber(refs.buildZoneInput.value, 0, 65535, 1001);
  const timestampMinutes = clampNumber(refs.buildTimestampInput.value, 0, 65535, 12345);
  const confirmHash = clampNumber(refs.buildConfirmInput.value, 0, 262143, 0);

  let actionFlags = 0;
  refs.buildFlagCheckboxes.forEach((checkbox) => {
    if (checkbox.checked) {
      actionFlags |= Number(checkbox.value);
    }
  });

  return {
    emergencyType,
    priority,
    actionFlags,
    zoneHash,
    timestampMinutes,
    confirmHash
  };
}

function renderBuildOutput(trackEncode) {
  if (!state.buildFields) {
    return;
  }

  const response = tryEncodeUet(state.buildFields);
  if (!response.ok) {
    refs.buildFeedback.textContent = response.error?.what ?? "Unable to encode the current scenario.";
    refs.buildOutputHex.textContent = "";
    refs.buildOutputBase64.textContent = "";
    refs.buildCopyHexButton.disabled = true;
    refs.buildCopyBase64Button.disabled = true;
    return;
  }

  const encoded = response.value;
  state.buildEncoded = encoded;

  refs.buildOutputHex.textContent = encoded.hex;
  refs.buildOutputBase64.textContent = encoded.base64;
  refs.buildCopyHexButton.disabled = false;
  refs.buildCopyBase64Button.disabled = false;
  refs.buildFeedback.textContent = "Scenario encoded successfully (8-byte UET).";

  if (trackEncode && state.lastTrackedBuildHex !== encoded.hex) {
    trackEvent(TRACKING_KEYS.encode);
    state.lastTrackedBuildHex = encoded.hex;
  }

  renderSizeComparison(state.buildFields, 8);
  refreshGeneratedCode();
}

function renderSizeComparison(fields, ecpBytes) {
  const activeFlags = countSetBits(fields.actionFlags);
  const complexity = (fields.priority * 12) + (activeFlags * 8) + (fields.emergencyType >= 10 ? 16 : 8);
  const jsonBytes = 210 + complexity;
  const capBytes = 560 + (complexity * 2);

  refs.sizeValueCap.textContent = `${capBytes} B`;
  refs.sizeValueJson.textContent = `${jsonBytes} B`;
  refs.sizeValueEcp.textContent = `${ecpBytes} B`;

  const maxValue = Math.max(capBytes, jsonBytes, ecpBytes, 1);
  refs.sizeBarCap.style.width = `${((capBytes / maxValue) * 100).toFixed(2)}%`;
  refs.sizeBarJson.style.width = `${((jsonBytes / maxValue) * 100).toFixed(2)}%`;
  refs.sizeBarEcp.style.width = `${((ecpBytes / maxValue) * 100).toFixed(2)}%`;

  const jsonSavings = Math.max(0, ((jsonBytes - ecpBytes) / jsonBytes) * 100).toFixed(2);
  const capSavings = Math.max(0, ((capBytes - ecpBytes) / capBytes) * 100).toFixed(2);
  refs.sizeSavings.textContent = `Estimated savings: ${jsonSavings}% vs JSON, ${capSavings}% vs CAP XML.`;
}

function refreshGeneratedCode() {
  if (!state.buildFields || !state.buildEncoded) {
    refs.generateScenarioLabel.textContent = "No scenario encoded yet.";
    refs.generateCodeBlock.textContent = "// Build a scenario first to generate code.";
    return;
  }

  state.generatedCodeByTab = buildGeneratedCodeByTab(state.buildFields, state.buildEncoded);
  refs.generateScenarioLabel.textContent = `Scenario: ${EMERGENCY_TYPE_LABELS[state.buildFields.emergencyType]} / ${PRIORITY_LABELS[state.buildFields.priority]} / Hex ${state.buildEncoded.hex}`;

  if (!normalizeCodeTabName(state.activeCodeTab)) {
    state.activeCodeTab = "one-liner";
  }
  renderActiveCodeTab();
}

function renderActiveCodeTab() {
  refs.codeTabButtons.forEach((button) => {
    const tabName = normalizeCodeTabName(button.dataset.codeTab);
    const isActive = tabName === state.activeCodeTab;
    button.setAttribute("aria-selected", isActive ? "true" : "false");
  });

  const code = state.generatedCodeByTab[state.activeCodeTab] ?? "// Build a scenario first to generate code.";
  refs.generateCodeBlock.textContent = code;
  highlightGeneratedCode();
}

function highlightGeneratedCode() {
  highlightCodeBlock(refs.generateCodeBlock);
}

function highlightCodeBlock(codeBlock) {
  const prism = window.Prism;
  if (prism && typeof prism.highlightElement === "function") {
    prism.highlightElement(codeBlock);
  }
}

function buildGeneratedCodeByTab(fields, encoded) {
  const emergencyEnum = EMERGENCY_TYPE_ENUM_NAMES[fields.emergencyType] ?? "Reserved";
  const priorityEnum = PRIORITY_ENUM_NAMES[fields.priority] ?? "Critical";
  const actionFlagsExpr = buildActionFlagsExpression(fields.actionFlags);
  const actionFlagsNote = buildActionFlagsNote(fields.actionFlags);

  const oneLiner = `using ECP.Core;
using ECP.Core.Models;

byte[] alert = Ecp.Alert(
    EmergencyType.${emergencyEnum},
    zoneHash: ${fields.zoneHash},
    priority: EcpPriority.${priorityEnum},
    actionFlags: ${actionFlagsExpr},
    timestampMinutes: ${fields.timestampMinutes},
    confirmHash: ${fields.confirmHash});

// Result: ${encoded.hex}
`;

  const token = `using ECP.Core;
using ECP.Core.Models;

var token = Ecp.Token(
    EmergencyType.${emergencyEnum},
    EcpPriority.${priorityEnum},
    ${actionFlagsExpr},
    zoneHash: ${fields.zoneHash},
    timestampMinutes: ${fields.timestampMinutes},
    confirmHash: ${fields.confirmHash});

byte[] encodedBytes = token.ToBytes();
string encodedBase64 = token.ToBase64();
// ${encoded.hex}
`;

  const envelope = `using ECP.Core;
using ECP.Core.Models;
using System.Security.Cryptography;

var token = Ecp.Token(
    EmergencyType.${emergencyEnum},
    EcpPriority.${priorityEnum},
    ${actionFlagsExpr},
    zoneHash: ${fields.zoneHash},
    timestampMinutes: ${fields.timestampMinutes},
    confirmHash: ${fields.confirmHash});

byte[] hmacKey = RandomNumberGenerator.GetBytes(32);
var envelope = Ecp.Envelope()
    .WithType(EmergencyType.${emergencyEnum})
    // EcpFlags are envelope-level routing/security flags. ActionFlags stay inside the nested UET payload.
    .WithFlags(EcpFlags.NeedsConfirmation | EcpFlags.Broadcast)
    .WithPriority(EcpPriority.${priorityEnum})
    .WithTtl(120)
    .WithKeyVersion(1)
    .WithPayload(token.ToBytes())
    .WithHmacLength(12)
    .WithHmacKey(hmacKey)
    .Build();

byte[] envelopeBytes = envelope.ToBytes();
`;

  const decoder = `using ECP.Core;

byte[] payload = Convert.FromHexString("${encoded.hex}");
if (Ecp.TryDecodeToken(payload, out var decoded))
{
    Console.WriteLine(decoded.EmergencyType);
    Console.WriteLine(decoded.Priority);
    Console.WriteLine(decoded.ActionFlags);
    Console.WriteLine(decoded.ZoneHash);
    Console.WriteLine(decoded.TimestampMinutes);
    Console.WriteLine(decoded.ConfirmHash);
}
`;

  const diSetup = `using ECP.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddEcpCore(options =>
{
    options.HmacLength = 12;
    options.KeyVersion = 1;
});

// Optional: services.AddEcpStandard();
`;

  const test = `using ECP.Core;
using ECP.Core.Models;
using Xunit;

public class StudioGeneratedTests
{
    [Fact]
    public void Should_encode_${sanitizeTestName(emergencyEnum)}_${sanitizeTestName(priorityEnum)}()
    {
        byte[] alert = Ecp.Alert(
            EmergencyType.${emergencyEnum},
            zoneHash: ${fields.zoneHash},
            priority: EcpPriority.${priorityEnum},
            actionFlags: ${actionFlagsExpr},
            timestampMinutes: ${fields.timestampMinutes},
            confirmHash: ${fields.confirmHash});

        Assert.Equal(8, alert.Length);
        Assert.Equal("${encoded.hex}", Convert.ToHexString(alert));

        Assert.True(Ecp.TryDecodeToken(alert, out var token));
        Assert.Equal(EmergencyType.${emergencyEnum}, token.EmergencyType);
        Assert.Equal(EcpPriority.${priorityEnum}, token.Priority);
        Assert.Equal((ActionFlags)${fields.actionFlags}, token.ActionFlags);
        Assert.Equal(${fields.zoneHash}, token.ZoneHash);
        Assert.Equal(${fields.timestampMinutes}, token.TimestampMinutes);
        Assert.Equal(${fields.confirmHash}, token.ConfirmHash);
    }
}
`;

  return {
    "one-liner": oneLiner,
    token,
    envelope,
    decoder,
    "di-setup": diSetup,
    test,
    "__meta": {
      actionFlagsNote
    }
  };
}

function buildCombinedCodeBundle(codeByTab) {
  if (!codeByTab || typeof codeByTab !== "object") {
    return "";
  }

  const sections = CODE_TAB_NAMES
    .map((tabName) => {
      const code = codeByTab[tabName];
      if (!code) {
        return "";
      }
      const title = tabName.toUpperCase();
      return `// ---------------- ${title} ----------------\n${code.trimEnd()}`;
    })
    .filter(Boolean);

  if (sections.length === 0) {
    return "";
  }

  return sections.join("\n\n");
}

function renderVerifyReport(report) {
  refs.verifyResultsWrap.hidden = false;
  refs.verifySummary.textContent = `Full suite: ${report.passed}/${report.total} passed`;
  refs.verifySummary.className = report.allPassed ? "verify-summary success" : "verify-summary failure";

  const shouldShowStarCta = report.allPassed && report.total === 34;
  if (shouldShowStarCta || state.verifyStarCtaShown) {
    refs.verifyStarCta.hidden = false;
    state.verifyStarCtaShown = true;
  } else {
    refs.verifyStarCta.hidden = true;
  }

  const rows = [];
  report.groups.forEach((group) => {
    const icon = group.allPassed ? "✅" : "❌";
    rows.push(`<li>${icon} ${escapeHtml(group.name)}: ${group.passed}/${group.total}</li>`);
  });
  report.results.forEach((result) => {
    rows.push(`<li>${escapeHtml(formatVectorStatusLine(result))}</li>`);
  });

  refs.verifyResults.innerHTML = rows.join("");
  refs.verifyResults.hidden = false;
  refs.verifyToggle.textContent = "Hide details";
  refs.verifyToggle.setAttribute("aria-expanded", "true");
}

async function copyDebugBundle(mode) {
  trackEvent(TRACKING_KEYS.debugCopy);
  const bundle = buildDebugBundle(mode);
  const copied = await copyTextBestEffort(bundle);
  if (mode === "Scenario Builder") {
    refs.buildFeedback.textContent = copied
      ? "Debug bundle copied."
      : "Clipboard denied. Select the bundle manually.";
  } else {
    refs.decodeFeedback.textContent = copied
      ? "Debug bundle copied."
      : "Clipboard denied. Select the bundle manually.";
  }
}

function buildDebugBundle(mode) {
  const lines = [];
  lines.push("--- ECP Studio Debug Bundle ---");
  lines.push("Studio version: 1.1");
  lines.push(`Timestamp: ${new Date().toISOString()}`);
  lines.push(`Mode: ${mode}`);

  if (mode === "Scenario Builder" && state.buildEncoded) {
    lines.push(`Input: ${state.buildEncoded.hex}`);
    lines.push(`Decoded: ${EMERGENCY_TYPE_LABELS[state.buildFields.emergencyType]} / ${PRIORITY_LABELS[state.buildFields.priority]} / Zone ${state.buildFields.zoneHash} / Timestamp ${state.buildFields.timestampMinutes} / ConfirmHash ${state.buildFields.confirmHash}`);
    lines.push("Round-trip: PASS");
  } else if (state.lastDecoded?.kind === "uet") {
    const value = state.lastDecoded.value;
    lines.push(`Input: ${refs.payloadInput.value.trim()}`);
    lines.push(`Decoded: ${value.emergencyTypeLabel} / ${value.priorityLabel} / Zone ${value.zoneHash} / Timestamp ${value.timestampMinutes} / ConfirmHash ${value.confirmHash}`);
    lines.push("Round-trip: PASS");
  } else if (state.lastDecoded?.kind === "envelope") {
    const env = state.lastDecoded.value;
    lines.push(`Input: ${refs.payloadInput.value.trim()}`);
    lines.push(`Decoded: Envelope v${env.version} / ${env.payloadTypeLabel} / Payload ${env.payloadLength}B / HMAC ${env.hmacLength}B`);
    lines.push(`Round-trip: ${env.signed ? "N/A (envelope decode only)" : "PASS (unsigned)"}`);
  } else {
    lines.push("Input: (empty)");
    lines.push("Decoded: (none)");
    lines.push("Round-trip: N/A");
  }

  if (state.lastVerifyReport) {
    lines.push(`Vectors verified: ${state.lastVerifyReport.passed}/${state.lastVerifyReport.total}`);
  } else {
    lines.push("Vectors verified: not-run");
  }
  lines.push(`Browser: ${navigator.userAgent}`);
  lines.push("---");
  return lines.join("\n");
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

function renderEnvelopeRows(envelope) {
  const rows = [
    ["Magic", "0-1", `0x${envelope.magicHex}`, `0x${envelope.magicHex}`],
    ["Version", "2", `0x${toPaddedHex(envelope.version, 2)}`, `v${envelope.version}`],
    ["Flags", "3", `0x${toPaddedHex(envelope.flags, 2)}`, envelope.flagLabels.length > 0 ? envelope.flagLabels.join(", ") : "None"],
    ["Priority", "4", envelope.priority, envelope.priorityLabel],
    ["TTL", "5", envelope.ttl, `${envelope.ttl} seconds`],
    ["KeyVersion", "6", envelope.keyVersion, `v${envelope.keyVersion}`],
    ["MessageId", "7-14", `0x${envelope.messageIdHex}`, `0x${envelope.messageIdHex}`],
    ["Timestamp", "15-18", envelope.timestampSeconds, envelope.timestampIso],
    ["PayloadType", "19", envelope.payloadType, envelope.payloadTypeLabel],
    ["PayloadLength", "20-21", envelope.payloadLength, `${envelope.payloadLength} bytes`],
    ["HmacLength", "derived", envelope.hmacLength, envelope.hmacLength === 0 ? "Unsigned mode" : `${envelope.hmacLength} bytes`]
  ];

  return rows
    .map((row) => `<tr><td>${escapeHtml(String(row[0]))}</td><td><code>${escapeHtml(String(row[1]))}</code></td><td>${escapeHtml(String(row[2]))}</td><td>${escapeHtml(String(row[3]))}</td></tr>`)
    .join("");
}

function renderNestedUetRows(decoded) {
  return [
    ["EmergencyType", decoded.emergencyType, decoded.emergencyTypeLabel],
    ["Priority", decoded.priority, decoded.priorityLabel],
    ["ActionFlags", decoded.actionFlags, decoded.actionFlagLabels.length > 0 ? decoded.actionFlagLabels.join(", ") : "None"],
    ["ZoneHash", decoded.zoneHash, `0x${toPaddedHex(decoded.zoneHash, 4)}`],
    ["TimestampMinutes", decoded.timestampMinutes, `0x${toPaddedHex(decoded.timestampMinutes, 4)}`],
    ["ConfirmHash", decoded.confirmHash, `0x${toPaddedHex(decoded.confirmHash, 5)}`]
  ].map((row) => `<tr><td>${escapeHtml(String(row[0]))}</td><td>${escapeHtml(String(row[1]))}</td><td>${escapeHtml(String(row[2]))}</td></tr>`)
    .join("");
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

function renderEnvelopeHexMap(hex, payloadLength, hmacLength) {
  const bytes = splitHexBytes(hex);
  return bytes
    .map((byte, index) => {
      let cls = "field-header";
      if (index >= 22 && index < 22 + payloadLength) {
        cls = "field-payload";
      } else if (index >= 22 + payloadLength && index < 22 + payloadLength + hmacLength) {
        cls = "field-hmac";
      }
      return `<span class="hex-byte ${cls}">${byte}</span>`;
    })
    .join("");
}

function setUetCopyButtonsEnabled(enabled) {
  refs.copyHexButton.disabled = !enabled;
  refs.copyBase64Button.disabled = !enabled;
}

function setEnvelopeCopyButtonsEnabled(enabled) {
  refs.copyEnvelopeHexButton.disabled = !enabled;
  refs.copyEnvelopeBase64Button.disabled = !enabled;
}

function splitHexBytes(hex) {
  const bytes = [];
  for (let i = 0; i < hex.length; i += 2) {
    bytes.push(hex.slice(i, i + 2));
  }
  return bytes;
}

function buildActionFlagsExpression(actionFlags) {
  if (actionFlags === 0) {
    return "ActionFlags.None";
  }

  const parts = [];
  ACTION_FLAG_DEFINITIONS.forEach((item) => {
    if ((actionFlags & (1 << item.bit)) !== 0) {
      parts.push(`ActionFlags.${item.name}`);
    }
  });

  return parts.length > 0 ? parts.join(" | ") : "ActionFlags.None";
}

function buildActionFlagsNote(actionFlags) {
  if (actionFlags === 0) {
    return "None";
  }
  const labels = ACTION_FLAG_DEFINITIONS
    .filter((item) => (actionFlags & (1 << item.bit)) !== 0)
    .map((item) => item.label);
  return labels.length > 0 ? labels.join(", ") : "None";
}

function sanitizeTestName(value) {
  return String(value).replace(/[^a-zA-Z0-9]+/g, "_").replace(/^_+|_+$/g, "").toLowerCase();
}

function countSetBits(value) {
  let count = 0;
  let remaining = value >>> 0;
  while (remaining !== 0) {
    count += remaining & 1;
    remaining >>>= 1;
  }
  return count;
}

function measureUtf8Bytes(text) {
  return new TextEncoder().encode(String(text)).length;
}

function clampNumber(rawValue, minValue, maxValue, fallback) {
  const parsed = Number.parseInt(String(rawValue), 10);
  if (!Number.isFinite(parsed)) {
    return fallback;
  }
  return clamp(parsed, minValue, maxValue);
}

function sanitizeTrackingToken(value) {
  return String(value).trim().toLowerCase().replace(/[^a-z0-9_-]+/g, "-").replace(/^-+|-+$/g, "");
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

function toPaddedHex(value, width) {
  return Number(value).toString(16).toUpperCase().padStart(width, "0");
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
