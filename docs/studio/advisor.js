/*
 * ECP Studio Protocol Advisor Module
 * Pure logic only: no DOM, no network requests.
 */

const DEFAULT_PAYLOAD_BYTES = 200;
const DEFAULT_MESSAGES_PER_DAY = 1000;

const RECIPIENT_REPRESENTATIVE = Object.freeze({
  "1": 1,
  "2-10": 6,
  "10-100": 55,
  "100-1000": 550,
  "1000+": 1500
});

const PROTOCOL_ORDER = Object.freeze([
  "ecp-uet",
  "ecp-envelope",
  "json",
  "protobuf",
  "cap-xml",
  "messagepack"
]);

const PROTOCOL_LABELS = Object.freeze({
  "ecp-uet": "ECP UET",
  "ecp-envelope": "ECP Envelope",
  "json": "JSON",
  "protobuf": "Protobuf",
  "cap-xml": "CAP XML",
  "messagepack": "MessagePack"
});

const MESSAGE_COMPLEXITY = Object.freeze({
  "alert": 0.9,
  "telemetry": 1.0,
  "command": 1.2,
  "beacon": 0.65,
  "generic": 1.35
});

const EMERGENCY_TYPE_LOOKUP = Object.freeze({
  fire: 0,
  evacuation: 1,
  earthquake: 2,
  flood: 3,
  medical: 4,
  security: 5,
  chemical: 6,
  lockdown: 7,
  allclear: 8,
  test: 9,
  custom1: 10,
  custom2: 11,
  custom3: 12,
  custom4: 13,
  custom5: 14,
  reserved: 15
});

const PRIORITY_LOOKUP = Object.freeze({
  low: 0,
  medium: 1,
  high: 2,
  critical: 3
});

const ACTION_FLAG_BITS = Object.freeze({
  soundalarm: 1,
  flashlights: 2,
  vibrate: 4,
  playvoice: 8,
  showmessage: 16,
  lockdoors: 32,
  unlockdoors: 64,
  notifyexternal: 128
});

function clamp(value, minValue, maxValue) {
  if (value < minValue) {
    return minValue;
  }
  if (value > maxValue) {
    return maxValue;
  }
  return value;
}

function toPositiveInteger(rawValue, fallbackValue, maxValue = Number.MAX_SAFE_INTEGER) {
  const parsed = Number.parseInt(String(rawValue), 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return fallbackValue;
  }
  return clamp(parsed, 1, maxValue);
}

function toInteger(rawValue, fallbackValue, minValue, maxValue) {
  const parsed = Number.parseInt(String(rawValue), 10);
  if (!Number.isFinite(parsed)) {
    return fallbackValue;
  }
  return clamp(parsed, minValue, maxValue);
}

function normalizeOption(rawValue, allowedValues, fallbackValue) {
  const normalized = String(rawValue || "").trim().toLowerCase();
  return allowedValues.includes(normalized) ? normalized : fallbackValue;
}

function getRecipientsRepresentative(recipientsBand) {
  return RECIPIENT_REPRESENTATIVE[recipientsBand] ?? RECIPIENT_REPRESENTATIVE["2-10"];
}

function getSemanticScores(messageKind) {
  switch (messageKind) {
    case "alert":
      return {
        "ecp-uet": 96,
        "ecp-envelope": 89,
        "json": 76,
        "protobuf": 72,
        "cap-xml": 83,
        "messagepack": 70
      };
    case "telemetry":
      return {
        "ecp-uet": 30,
        "ecp-envelope": 76,
        "json": 71,
        "protobuf": 88,
        "cap-xml": 36,
        "messagepack": 84
      };
    case "command":
      return {
        "ecp-uet": 26,
        "ecp-envelope": 71,
        "json": 73,
        "protobuf": 91,
        "cap-xml": 30,
        "messagepack": 81
      };
    case "beacon":
      return {
        "ecp-uet": 93,
        "ecp-envelope": 84,
        "json": 69,
        "protobuf": 74,
        "cap-xml": 20,
        "messagepack": 78
      };
    default:
      return {
        "ecp-uet": 18,
        "ecp-envelope": 63,
        "json": 78,
        "protobuf": 92,
        "cap-xml": 34,
        "messagepack": 84
      };
  }
}

function getReadabilityScores(humanReadable) {
  if (humanReadable === "yes") {
    return {
      "ecp-uet": 22,
      "ecp-envelope": 36,
      "json": 95,
      "protobuf": 49,
      "cap-xml": 88,
      "messagepack": 45
    };
  }
  if (humanReadable === "no") {
    return {
      "ecp-uet": 90,
      "ecp-envelope": 88,
      "json": 58,
      "protobuf": 84,
      "cap-xml": 34,
      "messagepack": 80
    };
  }
  return {
    "ecp-uet": 74,
    "ecp-envelope": 84,
    "json": 78,
    "protobuf": 82,
    "cap-xml": 62,
    "messagepack": 79
  };
}

function getTransportScores(transport) {
  if (transport === "ble") {
    return {
      "ecp-uet": 95,
      "ecp-envelope": 90,
      "json": 42,
      "protobuf": 72,
      "cap-xml": 20,
      "messagepack": 74
    };
  }
  if (transport === "lora") {
    return {
      "ecp-uet": 97,
      "ecp-envelope": 93,
      "json": 34,
      "protobuf": 68,
      "cap-xml": 14,
      "messagepack": 70
    };
  }
  if (transport === "satellite") {
    return {
      "ecp-uet": 96,
      "ecp-envelope": 92,
      "json": 40,
      "protobuf": 70,
      "cap-xml": 18,
      "messagepack": 72
    };
  }
  if (transport === "sms") {
    return {
      "ecp-uet": 95,
      "ecp-envelope": 90,
      "json": 38,
      "protobuf": 66,
      "cap-xml": 10,
      "messagepack": 68
    };
  }
  if (transport === "mixed") {
    return {
      "ecp-uet": 84,
      "ecp-envelope": 86,
      "json": 72,
      "protobuf": 80,
      "cap-xml": 40,
      "messagepack": 79
    };
  }
  return {
    "ecp-uet": 76,
    "ecp-envelope": 78,
    "json": 85,
    "protobuf": 83,
    "cap-xml": 65,
    "messagepack": 80
  };
}

function estimateProtocolSizes(answers) {
  const payloadBytes = answers.payloadSizeBytes;
  const complexity = MESSAGE_COMPLEXITY[answers.messageKind] ?? 1.0;
  const ecpInnerPayload = (answers.messageKind === "alert" || answers.messageKind === "beacon")
    ? 8
    : Math.round(Math.max(12, (payloadBytes * 0.24) + (complexity * 10)));

  const jsonBytes = Math.round(Math.max(48, (payloadBytes * 1.08) + 28 + (complexity * 22)));
  const protobufBytes = Math.round(Math.max(16, (payloadBytes * 0.58) + 14 + (complexity * 10)));
  const messagePackBytes = Math.round(Math.max(14, (payloadBytes * 0.72) + 12 + (complexity * 12)));
  const capXmlBytes = Math.round(Math.max(240, (payloadBytes * 2.8) + 220 + (complexity * 30)));

  return {
    "ecp-uet": 8,
    "ecp-envelope": 22 + ecpInnerPayload + 12,
    "json": jsonBytes,
    "protobuf": protobufBytes,
    "cap-xml": capXmlBytes,
    "messagepack": messagePackBytes
  };
}

function calculateSizeScore(estimatedBytes, minBytes, maxBytes) {
  if (maxBytes <= minBytes) {
    return 100;
  }
  const ratio = (estimatedBytes - minBytes) / (maxBytes - minBytes);
  return clamp(100 - (ratio * 100), 0, 100);
}

function calculateRecipientScore(estimatedBytes, recipientsCount) {
  const byteScore = clamp(100 - (estimatedBytes / 8), 0, 100);
  if (recipientsCount <= 1) {
    return clamp(60 + (byteScore * 0.25), 0, 100);
  }
  if (recipientsCount <= 10) {
    return clamp(48 + (byteScore * 0.42), 0, 100);
  }
  if (recipientsCount <= 100) {
    return clamp(32 + (byteScore * 0.58), 0, 100);
  }
  if (recipientsCount <= 1000) {
    return clamp(20 + (byteScore * 0.72), 0, 100);
  }
  return clamp(14 + (byteScore * 0.8), 0, 100);
}

function getFitnessBucket(score) {
  if (score >= 86) {
    return "BEST";
  }
  if (score >= 72) {
    return "GOOD";
  }
  if (score >= 56) {
    return "OK";
  }
  return "POOR";
}

function applyHonestyAdjustments(scoreByProtocol, answers) {
  if (answers.transport === "wifi" && answers.recipientsBand === "1" && answers.humanReadable === "yes") {
    scoreByProtocol.json += 14;
    scoreByProtocol["ecp-uet"] -= 8;
    scoreByProtocol["ecp-envelope"] -= 6;
    scoreByProtocol["cap-xml"] -= 4;
  }

  const constrainedLink = answers.transport === "ble"
    || answers.transport === "lora"
    || answers.transport === "satellite"
    || answers.transport === "sms";
  if (constrainedLink && answers.recipientsCount >= 100 && answers.humanReadable !== "yes") {
    scoreByProtocol["ecp-uet"] += 14;
    scoreByProtocol["ecp-envelope"] += 16;
    scoreByProtocol.json -= 10;
    scoreByProtocol["cap-xml"] -= 16;
    scoreByProtocol.protobuf += 2;
  }

  const protobufFriendly = (answers.messageKind === "generic" || answers.messageKind === "command" || answers.messageKind === "telemetry")
    && answers.payloadSizeBytes >= 320
    && answers.humanReadable === "no"
    && (answers.transport === "wifi" || answers.transport === "mixed");
  if (protobufFriendly) {
    scoreByProtocol.protobuf += 12;
    scoreByProtocol["ecp-envelope"] -= 5;
    scoreByProtocol.json -= 4;
  }

  if (answers.messageKind !== "alert" && answers.messageKind !== "beacon") {
    scoreByProtocol["ecp-uet"] -= 22;
  }

  PROTOCOL_ORDER.forEach((protocolId) => {
    scoreByProtocol[protocolId] = clamp(scoreByProtocol[protocolId], 0, 100);
  });
}

function buildReason(protocolId, answers, isTopProtocol) {
  const constrainedLink = answers.transport === "ble"
    || answers.transport === "lora"
    || answers.transport === "satellite"
    || answers.transport === "sms";

  if (protocolId === "json") {
    if (answers.transport === "wifi" && answers.recipientsBand === "1" && answers.humanReadable === "yes") {
      return "JSON is fine: single recipient on unconstrained transport with readability required.";
    }
    if (isTopProtocol) {
      return "Readable and easy to integrate; estimated overhead is acceptable for this profile.";
    }
    return "Great for readability and APIs, but estimated byte overhead grows under constrained links.";
  }

  if (protocolId === "protobuf") {
    if (isTopProtocol) {
      return "Best fit for structured machine messages with strong schema flexibility and compact encoding.";
    }
    return "Compact and schema-driven; often better than JSON for machine-to-machine payloads.";
  }

  if (protocolId === "ecp-uet") {
    if (answers.messageKind !== "alert" && answers.messageKind !== "beacon") {
      return "Fixed 8-byte token is ultra-compact but carries limited semantics for this message type.";
    }
    if (isTopProtocol && constrainedLink) {
      return "Strong fit: fixed 8-byte payload minimizes airtime on constrained channels.";
    }
    return "Smallest wire size (8 bytes) for compact alert/beacon signaling.";
  }

  if (protocolId === "ecp-envelope") {
    if (isTopProtocol) {
      return "Balanced binary framing with metadata and predictable overhead; ideal for constrained fan-out.";
    }
    return "Adds routing metadata + optional HMAC while staying significantly smaller than text formats.";
  }

  if (protocolId === "messagepack") {
    if (isTopProtocol) {
      return "Good compromise when you want generic payload structure with lower overhead than JSON.";
    }
    return "Compact binary alternative to JSON with flexible object mapping.";
  }

  return "CAP XML remains interoperable in legacy alerting ecosystems, but estimated size overhead is high.";
}

function buildHybridSuggestion(bestProtocolId, answers) {
  if (bestProtocolId === "json") {
    return "Hybrid suggestion: keep JSON for this flow; introduce ECP only on constrained or high fan-out emergency paths.";
  }
  if (bestProtocolId === "protobuf") {
    return "Hybrid suggestion: use Protobuf for internal machine channels and keep JSON at external REST boundaries.";
  }
  if (bestProtocolId === "messagepack") {
    return "Hybrid suggestion: MessagePack for internal compact payloads, JSON for public-facing APIs.";
  }
  if (bestProtocolId === "cap-xml") {
    return "Hybrid suggestion: keep CAP XML where interoperability is mandatory, and bridge to ECP internally for transport efficiency.";
  }
  if (answers.humanReadable === "yes" || answers.humanReadable === "partial") {
    return "Hybrid suggestion: use ECP for constrained links and keep JSON for human-readable logs and external APIs.";
  }
  return "Hybrid suggestion: use ECP internally for machine signaling, with JSON only where external integrations require text payloads.";
}

function buildEstimatedLabel(estimatedBytes) {
  return `${estimatedBytes} B`;
}

export function normalizeAdvisorAnswers(rawAnswers = {}) {
  const messageKind = normalizeOption(
    rawAnswers.messageKind,
    ["alert", "telemetry", "command", "beacon", "generic"],
    "alert"
  );
  const transport = normalizeOption(
    rawAnswers.transport,
    ["wifi", "ble", "lora", "satellite", "sms", "mixed"],
    "wifi"
  );
  const recipientsBand = normalizeOption(
    rawAnswers.recipientsBand,
    ["1", "2-10", "10-100", "100-1000", "1000+"],
    "2-10"
  );
  const humanReadable = normalizeOption(
    rawAnswers.humanReadable,
    ["yes", "no", "partial"],
    "partial"
  );
  const payloadUnknown = Boolean(rawAnswers.payloadUnknown);
  const payloadSizeBytes = payloadUnknown
    ? DEFAULT_PAYLOAD_BYTES
    : toPositiveInteger(rawAnswers.payloadSizeBytes, DEFAULT_PAYLOAD_BYTES, 65535);

  const messagesPerDay = toPositiveInteger(rawAnswers.messagesPerDay, DEFAULT_MESSAGES_PER_DAY, 5000000);
  const recipientsCount = getRecipientsRepresentative(recipientsBand);

  return {
    messageKind,
    payloadSizeBytes,
    payloadUnknown,
    transport,
    recipientsBand,
    recipientsCount,
    humanReadable,
    messagesPerDay
  };
}

export function evaluateProtocols(rawAnswers = {}) {
  const answers = normalizeAdvisorAnswers(rawAnswers);
  const estimatedBytes = estimateProtocolSizes(answers);
  const minBytes = Math.min(...PROTOCOL_ORDER.map((protocolId) => estimatedBytes[protocolId]));
  const maxBytes = Math.max(...PROTOCOL_ORDER.map((protocolId) => estimatedBytes[protocolId]));
  const semanticScores = getSemanticScores(answers.messageKind);
  const readabilityScores = getReadabilityScores(answers.humanReadable);
  const transportScores = getTransportScores(answers.transport);

  const scoreByProtocol = {};
  PROTOCOL_ORDER.forEach((protocolId) => {
    const sizeScore = calculateSizeScore(estimatedBytes[protocolId], minBytes, maxBytes);
    const recipientScore = calculateRecipientScore(estimatedBytes[protocolId], answers.recipientsCount);
    const semanticScore = semanticScores[protocolId];
    const readabilityScore = readabilityScores[protocolId];
    const transportScore = transportScores[protocolId];

    scoreByProtocol[protocolId] = (
      (sizeScore * 0.32) +
      (semanticScore * 0.24) +
      (readabilityScore * 0.17) +
      (transportScore * 0.15) +
      (recipientScore * 0.12)
    );
  });

  applyHonestyAdjustments(scoreByProtocol, answers);

  const sorted = PROTOCOL_ORDER
    .map((protocolId) => ({
      protocolId,
      score: Number(scoreByProtocol[protocolId].toFixed(2))
    }))
    .sort((left, right) => right.score - left.score);

  const bestProtocolId = sorted[0]?.protocolId ?? "json";

  const rows = PROTOCOL_ORDER.map((protocolId) => {
    const rawScore = Number(scoreByProtocol[protocolId].toFixed(2));
    const isTopProtocol = protocolId === bestProtocolId;
    const fitness = isTopProtocol
      ? "BEST"
      : getFitnessBucket(rawScore);

    return {
      protocolId,
      label: PROTOCOL_LABELS[protocolId],
      estimatedBytes: estimatedBytes[protocolId],
      estimatedLabel: buildEstimatedLabel(estimatedBytes[protocolId]),
      score: rawScore,
      fitness,
      isTopProtocol,
      reason: buildReason(protocolId, answers, isTopProtocol)
    };
  });

  return {
    answers,
    rows,
    bestProtocolId,
    bestProtocolLabel: PROTOCOL_LABELS[bestProtocolId],
    hybridSuggestion: buildHybridSuggestion(bestProtocolId, answers)
  };
}

function formatBytes(bytes) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(2)} KB`;
  }
  if (bytes < 1024 * 1024 * 1024) {
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

export function calculateBandwidth(rawAnswers = {}) {
  const evaluation = evaluateProtocols(rawAnswers);
  const answers = evaluation.answers;
  const rowsById = Object.fromEntries(evaluation.rows.map((row) => [row.protocolId, row]));

  const ecpProtocolId = (answers.messageKind === "alert" || answers.messageKind === "beacon")
    ? "ecp-uet"
    : "ecp-envelope";
  const jsonBytes = rowsById.json.estimatedBytes;
  const ecpBytes = rowsById[ecpProtocolId].estimatedBytes;

  const jsonDailyBytes = jsonBytes * answers.messagesPerDay * answers.recipientsCount;
  const ecpDailyBytes = ecpBytes * answers.messagesPerDay * answers.recipientsCount;
  const savingsPercent = jsonDailyBytes <= 0
    ? 0
    : Math.max(0, ((jsonDailyBytes - ecpDailyBytes) / jsonDailyBytes) * 100);

  return {
    answers,
    jsonDailyBytes,
    ecpDailyBytes,
    savingsPercent,
    jsonDailyLabel: formatBytes(jsonDailyBytes),
    ecpDailyLabel: formatBytes(ecpDailyBytes),
    summary: `With your scenario (${answers.messagesPerDay} msg/day x ${answers.recipientsCount} recipients), JSON ~ ${formatBytes(jsonDailyBytes)}/day, ECP ~ ${formatBytes(ecpDailyBytes)}/day, savings ${savingsPercent.toFixed(2)}%/day.`
  };
}

export function selectStrategy(rawInput = {}) {
  const recipients = toInteger(rawInput.recipients, 12, 1, 1000);
  const messageSizeBytes = toInteger(rawInput.messageSizeBytes, 42, 1, 65535);
  const hasTemplate = Boolean(rawInput.hasTemplate);
  const hasDictionary = Boolean(rawInput.hasDictionary);

  const directThreshold = 4;
  const miniCascadeThreshold = 12;
  const fanOut = 2.5;
  const templateSavings = hasTemplate ? 0.55 : 1;
  const dictionarySavings = hasDictionary ? 0.78 : 1;

  const effectiveMessageBytes = Math.max(8, Math.round(messageSizeBytes * templateSavings * dictionarySavings));
  const directCostBytes = recipients * effectiveMessageBytes;

  const cascadeHops = recipients <= 1
    ? 1
    : Math.max(2, Math.ceil(Math.log(recipients) / Math.log(fanOut)));
  const cascadeEdgeMultiplier = recipients <= miniCascadeThreshold ? 0.86 : 0.62;
  const cascadeCostBytes = Math.max(
    effectiveMessageBytes * 2,
    Math.round(directCostBytes * cascadeEdgeMultiplier)
  );

  let mode = "FullCascade";
  let selectedCostBytes = cascadeCostBytes;
  let reasoning = `Recipient count > ${miniCascadeThreshold} favors cascade fan-out over direct duplication.`;
  let hopCount = cascadeHops;

  if (recipients === 1 && effectiveMessageBytes <= 8) {
    mode = "UetOnly";
    selectedCostBytes = 8;
    reasoning = "Single recipient and tiny payload: UetOnly minimizes overhead.";
    hopCount = 1;
  } else if (recipients <= directThreshold) {
    mode = "Direct";
    selectedCostBytes = directCostBytes;
    reasoning = `Recipient count <= ${directThreshold}: direct fan-out is simpler and still cost-effective.`;
    hopCount = 1;
  } else if (recipients <= miniCascadeThreshold) {
    mode = "MiniCascade";
    selectedCostBytes = Math.round((directCostBytes + cascadeCostBytes) * 0.46);
    reasoning = `Recipient count between ${directThreshold + 1} and ${miniCascadeThreshold}: mini cascade reduces duplicates without full tree overhead.`;
    hopCount = Math.max(2, cascadeHops - 1);
  }

  return {
    mode,
    recipients,
    messageSizeBytes,
    effectiveMessageBytes,
    directCostBytes,
    cascadeCostBytes,
    selectedCostBytes,
    hopCount,
    reasoning,
    defaults: {
      directThreshold,
      miniCascadeThreshold,
      fanOut,
      templateSavingsDefault: 0.55,
      dictionarySavingsDefault: 0.78
    }
  };
}

function stableHashTo18Bits(rawText) {
  let hash = 0;
  for (let index = 0; index < rawText.length; index += 1) {
    hash = ((hash * 31) + rawText.charCodeAt(index)) & 0x3ffff;
  }
  return hash;
}

export function buildScenarioFromAdvisor(rawAnswers = {}) {
  const answers = normalizeAdvisorAnswers(rawAnswers);
  let fields;

  switch (answers.messageKind) {
    case "alert":
      fields = { emergencyType: 0, priority: 3, actionFlags: 1 | 2 | 16, zoneHash: 1001, timestampMinutes: 12345, confirmHash: 0 };
      break;
    case "telemetry":
      fields = { emergencyType: 10, priority: 1, actionFlags: 128, zoneHash: 120, timestampMinutes: 12345, confirmHash: 0 };
      break;
    case "command":
      fields = { emergencyType: 5, priority: 2, actionFlags: 16 | 32, zoneHash: 2400, timestampMinutes: 12345, confirmHash: 0 };
      break;
    case "beacon":
      fields = { emergencyType: 11, priority: 0, actionFlags: 0, zoneHash: 30000, timestampMinutes: 12345, confirmHash: 0 };
      break;
    default:
      fields = { emergencyType: 14, priority: 1, actionFlags: 16, zoneHash: 4096, timestampMinutes: 12345, confirmHash: 0 };
      break;
  }

  if (answers.transport === "lora" || answers.transport === "satellite" || answers.transport === "sms") {
    fields.actionFlags |= 128;
    fields.priority = Math.max(fields.priority, 2);
  }
  if (answers.recipientsCount >= 100) {
    fields.priority = Math.max(fields.priority, 2);
  }

  fields.timestampMinutes = clamp((answers.payloadSizeBytes * 3) + answers.recipientsCount, 0, 65535);
  fields.confirmHash = stableHashTo18Bits(
    `${answers.messageKind}:${answers.transport}:${answers.payloadSizeBytes}:${answers.recipientsBand}`
  );

  return fields;
}

function readFirstDefined(objectValue, keys) {
  for (const key of keys) {
    if (Object.prototype.hasOwnProperty.call(objectValue, key) && objectValue[key] !== undefined && objectValue[key] !== null) {
      return objectValue[key];
    }
  }
  return undefined;
}

function parseEmergencyType(rawValue) {
  if (typeof rawValue === "number") {
    return clamp(Math.round(rawValue), 0, 15);
  }
  if (typeof rawValue === "string") {
    const normalized = rawValue.trim().toLowerCase().replaceAll(" ", "");
    if (Object.prototype.hasOwnProperty.call(EMERGENCY_TYPE_LOOKUP, normalized)) {
      return EMERGENCY_TYPE_LOOKUP[normalized];
    }
    const parsed = Number.parseInt(normalized, 10);
    if (Number.isFinite(parsed)) {
      return clamp(parsed, 0, 15);
    }
  }
  return 0;
}

function parsePriority(rawValue) {
  if (typeof rawValue === "number") {
    return clamp(Math.round(rawValue), 0, 3);
  }
  if (typeof rawValue === "string") {
    const normalized = rawValue.trim().toLowerCase();
    if (Object.prototype.hasOwnProperty.call(PRIORITY_LOOKUP, normalized)) {
      return PRIORITY_LOOKUP[normalized];
    }
    const parsed = Number.parseInt(normalized, 10);
    if (Number.isFinite(parsed)) {
      return clamp(parsed, 0, 3);
    }
  }
  return 3;
}

function parseActionFlags(rawValue) {
  if (typeof rawValue === "number" && Number.isFinite(rawValue)) {
    return clamp(Math.round(rawValue), 0, 255);
  }

  const consumeToken = (token, currentMask) => {
    const normalized = String(token).trim().toLowerCase().replaceAll("_", "").replaceAll("-", "");
    if (!normalized) {
      return currentMask;
    }
    if (Object.prototype.hasOwnProperty.call(ACTION_FLAG_BITS, normalized)) {
      return currentMask | ACTION_FLAG_BITS[normalized];
    }
    return currentMask;
  };

  if (Array.isArray(rawValue)) {
    return rawValue.reduce((mask, token) => consumeToken(token, mask), 0);
  }

  if (typeof rawValue === "string") {
    const maybeNumber = Number.parseInt(rawValue.trim(), 10);
    if (Number.isFinite(maybeNumber)) {
      return clamp(maybeNumber, 0, 255);
    }
    const tokens = rawValue.split(/[,|;/]+/g);
    return tokens.reduce((mask, token) => consumeToken(token, mask), 0);
  }

  return 0;
}

function parseTimestampMinutes(rawValue, fallback) {
  if (typeof rawValue === "number" && Number.isFinite(rawValue)) {
    return clamp(Math.round(rawValue), 0, 65535);
  }
  if (typeof rawValue === "string") {
    const trimmed = rawValue.trim();
    const parsed = Number.parseInt(trimmed, 10);
    if (Number.isFinite(parsed)) {
      return clamp(parsed, 0, 65535);
    }
    const asDate = Date.parse(trimmed);
    if (Number.isFinite(asDate)) {
      const minutes = Math.round(asDate / 60000);
      return clamp(minutes, 0, 65535);
    }
  }
  return fallback;
}

function parseConfirmHash(rawValue, fallback) {
  if (typeof rawValue === "number" && Number.isFinite(rawValue)) {
    return clamp(Math.round(rawValue), 0, 262143);
  }
  if (typeof rawValue === "string") {
    const trimmed = rawValue.trim();
    const parsed = Number.parseInt(trimmed, 10);
    if (Number.isFinite(parsed)) {
      return clamp(parsed, 0, 262143);
    }
  }
  return fallback;
}

export function deriveBridgeFieldsFromJsonObject(jsonValue) {
  const sourceObject = (jsonValue && typeof jsonValue === "object" && !Array.isArray(jsonValue))
    ? jsonValue
    : {};
  const sourceText = JSON.stringify(sourceObject);

  const emergencyType = parseEmergencyType(
    readFirstDefined(sourceObject, ["emergencyType", "eventType", "event", "type", "kind"])
  );
  const priority = parsePriority(
    readFirstDefined(sourceObject, ["priority", "severity", "urgency"])
  );
  const actionFlags = parseActionFlags(
    readFirstDefined(sourceObject, ["actionFlags", "flags", "actions"])
  );
  const zoneHash = toInteger(
    readFirstDefined(sourceObject, ["zoneHash", "zone", "zoneId", "areaHash"]),
    1001,
    0,
    65535
  );
  const timestampMinutes = parseTimestampMinutes(
    readFirstDefined(sourceObject, ["timestampMinutes", "timestamp", "time"]),
    12345
  );
  const confirmHash = parseConfirmHash(
    readFirstDefined(sourceObject, ["confirmHash", "correlationId", "confirm"]),
    stableHashTo18Bits(sourceText)
  );

  return {
    fields: {
      emergencyType,
      priority,
      actionFlags,
      zoneHash,
      timestampMinutes,
      confirmHash
    }
  };
}

