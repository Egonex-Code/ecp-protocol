/*
 * ECP Studio - Phase A Decoder Module
 * Pure logic only: no DOM, no tracking, no network requests.
 */

const UET_BYTE_LENGTH = 8;
const UET_HEX_LENGTH = UET_BYTE_LENGTH * 2;
const UET_TOTAL_BITS = 64n;
const UET_TOTAL_BITS_NUMBER = 64;
const BYTE_SHIFT = 8n;
const BYTE_MASK = 0xffn;

const UET_SHIFTS = Object.freeze({
  confirmHash: 0n,
  timestampMinutes: 18n,
  zoneHash: 34n,
  actionFlags: 50n,
  priority: 58n,
  emergencyType: 60n
});

const UET_MASKS = Object.freeze({
  emergencyType: 0xfn,
  priority: 0x3n,
  actionFlags: 0xffn,
  zoneHash: 0xffffn,
  timestampMinutes: 0xffffn,
  confirmHash: 0x3ffffn
});

const UET_FIELD_LIMITS = Object.freeze({
  emergencyType: Number(UET_MASKS.emergencyType),
  priority: Number(UET_MASKS.priority),
  actionFlags: Number(UET_MASKS.actionFlags),
  zoneHash: Number(UET_MASKS.zoneHash),
  timestampMinutes: Number(UET_MASKS.timestampMinutes),
  confirmHash: Number(UET_MASKS.confirmHash)
});

const UET_FIELD_NAMES = Object.freeze([
  "emergencyType",
  "priority",
  "actionFlags",
  "zoneHash",
  "timestampMinutes",
  "confirmHash"
]);

const EMERGENCY_TYPE_LABELS = Object.freeze([
  "Fire",
  "Evacuation",
  "Earthquake",
  "Flood",
  "Medical Emergency",
  "Security Incident",
  "Chemical Incident",
  "Lockdown",
  "All Clear",
  "Test Message",
  "Custom1 (IoT Sensor)",
  "Custom2 (Fleet Beacon)",
  "Custom3 (Satellite Uplink)",
  "Custom4 (Industrial Alarm)",
  "Custom5 (User Defined)",
  "Reserved"
]);

const PRIORITY_LABELS = Object.freeze(["Low", "Medium", "High", "Critical"]);

const ACTION_FLAG_DEFINITIONS = Object.freeze([
  Object.freeze({ bit: 0, name: "SoundAlarm", label: "Sound Alarm" }),
  Object.freeze({ bit: 1, name: "FlashLights", label: "Flash Lights" }),
  Object.freeze({ bit: 2, name: "Vibrate", label: "Vibrate Device" }),
  Object.freeze({ bit: 3, name: "PlayVoice", label: "Play Voice" }),
  Object.freeze({ bit: 4, name: "ShowMessage", label: "Show Message" }),
  Object.freeze({ bit: 5, name: "LockDoors", label: "Lock Doors" }),
  Object.freeze({ bit: 6, name: "UnlockDoors", label: "Unlock Doors" }),
  Object.freeze({ bit: 7, name: "NotifyExternal", label: "Notify External" })
]);

const CORE_TEST_VECTORS = Object.freeze([
  Object.freeze({
    id: "uet-fire-critical-001",
    hex: "0C000FA4C0E40000",
    base64: "DAAPpMDkAAA=",
    fields: Object.freeze({
      emergencyType: 0,
      priority: 3,
      actionFlags: 0,
      zoneHash: 1001,
      timestampMinutes: 12345,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "uet-security-high-flags-002",
    hex: "584EFBBF50C6AAAA",
    base64: "WE77v1DGqqo=",
    fields: Object.freeze({
      emergencyType: 5,
      priority: 2,
      actionFlags: 19,
      zoneHash: 48879,
      timestampMinutes: 54321,
      confirmHash: 174762
    })
  }),
  Object.freeze({
    id: "uet-allclear-low-allflags-003",
    hex: "83FC00040007FFFF",
    base64: "g/wABAAH//8=",
    fields: Object.freeze({
      emergencyType: 8,
      priority: 0,
      actionFlags: 255,
      zoneHash: 1,
      timestampMinutes: 1,
      confirmHash: 262143
    })
  }),
  Object.freeze({
    id: "uet-custom1-medium-004",
    hex: "A493FFFFFFFC0001",
    base64: "pJP////8AAE=",
    fields: Object.freeze({
      emergencyType: 10,
      priority: 1,
      actionFlags: 36,
      zoneHash: 65535,
      timestampMinutes: 65535,
      confirmHash: 1
    })
  }),
  Object.freeze({
    id: "uet-reserved-critical-max-005",
    hex: "FFFFFFFFFFFFFFFF",
    base64: "//////////8=",
    fields: Object.freeze({
      emergencyType: 15,
      priority: 3,
      actionFlags: 255,
      zoneHash: 65535,
      timestampMinutes: 65535,
      confirmHash: 262143
    })
  }),
  Object.freeze({
    id: "uet-zero-min-006",
    hex: "0000000000000000",
    base64: "AAAAAAAAAAA=",
    fields: Object.freeze({
      emergencyType: 0,
      priority: 0,
      actionFlags: 0,
      zoneHash: 0,
      timestampMinutes: 0,
      confirmHash: 0
    })
  }),
  Object.freeze({
    id: "uet-confirmhash-nonzero-007",
    hex: "240048D159E1ABCD",
    base64: "JABI0Vnhq80=",
    fields: Object.freeze({
      emergencyType: 2,
      priority: 1,
      actionFlags: 0,
      zoneHash: 4660,
      timestampMinutes: 22136,
      confirmHash: 109517
    })
  }),
  Object.freeze({
    id: "uet-bitlayout-reference-008",
    hex: "5AB048D159E2AAAA",
    base64: "WrBI0Vniqqo=",
    fields: Object.freeze({
      emergencyType: 5,
      priority: 2,
      actionFlags: 172,
      zoneHash: 4660,
      timestampMinutes: 22136,
      confirmHash: 174762
    })
  })
]);

class DecoderError extends Error {
  constructor(code, what, why, howToFix, details) {
    const message = [what, why, howToFix].filter(Boolean).join(" ");
    super(message);
    this.name = "DecoderError";
    this.code = code;
    this.what = what;
    this.why = why;
    this.howToFix = howToFix;
    this.details = details ?? null;
  }
}

function createDecoderError(code, what, why, howToFix, details) {
  return new DecoderError(code, what, why, howToFix, details);
}

function serializeError(error) {
  if (error instanceof DecoderError) {
    return {
      code: error.code,
      what: error.what,
      why: error.why,
      howToFix: error.howToFix,
      message: error.message,
      details: error.details
    };
  }

  const fallbackMessage = error instanceof Error ? error.message : "Unexpected decoder failure.";
  return {
    code: "UNEXPECTED_ERROR",
    what: "An unexpected error occurred while processing the input.",
    why: fallbackMessage,
    howToFix: "Retry with a valid UET hex/base64 payload and report this issue if it persists.",
    message: fallbackMessage,
    details: null
  };
}

function removeWhitespace(value) {
  return value.replace(/\s+/g, "");
}

function isHexCharacters(value) {
  return /^[0-9a-fA-F]+$/.test(value);
}

function normalizeHexInput(rawInput) {
  const collapsed = removeWhitespace(rawInput);
  const hasHexPrefix = /^0x/i.test(collapsed);
  const withoutPrefix = hasHexPrefix ? collapsed.slice(2) : collapsed;
  const wasProbablyHex = hasHexPrefix || /\s/.test(rawInput) || /^[0-9a-fA-F]+$/.test(withoutPrefix);

  if (!withoutPrefix) {
    return { ok: false, wasProbablyHex };
  }

  if (!isHexCharacters(withoutPrefix)) {
    return { ok: false, wasProbablyHex };
  }

  if (withoutPrefix.length % 2 !== 0) {
    return {
      ok: false,
      wasProbablyHex: true,
      error: createDecoderError(
        "HEX_ODD_LENGTH",
        `Hex input has ${withoutPrefix.length} characters.`,
        "Hex requires 2 characters per byte, so the length must be even.",
        "Add the missing hex character or remove the extra one."
      )
    };
  }

  return {
    ok: true,
    format: "hex",
    normalized: withoutPrefix.toUpperCase()
  };
}

function normalizeBase64Input(rawInput) {
  const collapsed = removeWhitespace(rawInput);
  if (!collapsed) {
    return { ok: false };
  }

  const looksLikeBase64Url = /[-_]/.test(collapsed);
  const replaced = collapsed.replace(/-/g, "+").replace(/_/g, "/");
  const strippedPadding = replaced.replace(/=+$/, "");
  const hasInvalidChars = /[^A-Za-z0-9+/=]/.test(replaced);
  const invalidPadding = /=/.test(strippedPadding);

  if (hasInvalidChars || invalidPadding) {
    return { ok: false };
  }

  const remainder = strippedPadding.length % 4;
  if (remainder === 1) {
    return {
      ok: false,
      error: createDecoderError(
        "BASE64_INVALID_LENGTH",
        "Base64 input length is invalid.",
        "Valid base64 payloads are encoded in 4-character groups (with optional '=' padding).",
        "Check for missing/truncated characters and ensure the full payload is copied."
      )
    };
  }

  const padded = remainder === 0 ? strippedPadding : strippedPadding + "=".repeat(4 - remainder);
  return {
    ok: true,
    format: looksLikeBase64Url ? "base64url" : "base64",
    normalized: padded
  };
}

function normalizeInput(rawInput) {
  if (typeof rawInput !== "string") {
    throw createDecoderError(
      "INPUT_NOT_STRING",
      "Input must be text.",
      `Received type '${typeof rawInput}'.`,
      "Pass a hex/base64 string. Example: 0C000FA4C0E40000 or DAAPpMDkAAA=."
    );
  }

  const trimmed = rawInput.trim();
  if (!trimmed) {
    throw createDecoderError(
      "INPUT_EMPTY",
      "Input is empty.",
      "The decoder needs a UET payload to parse.",
      "Paste a 16-char hex UET or an 8-byte base64 payload."
    );
  }

  const hexNormalized = normalizeHexInput(trimmed);
  if (hexNormalized.ok) {
    return hexNormalized;
  }

  if (hexNormalized.wasProbablyHex && hexNormalized.error) {
    throw hexNormalized.error;
  }

  const base64Normalized = normalizeBase64Input(trimmed);
  if (!base64Normalized.ok) {
    if (base64Normalized.error) {
      throw base64Normalized.error;
    }

    throw createDecoderError(
      "INPUT_PARSE_FAILED",
      "Cannot parse input as hex or base64.",
      "The payload does not match supported formats.",
      "Use hex (with optional spaces/0x) or base64/base64url."
    );
  }

  return base64Normalized;
}

function hexToBytes(hex) {
  const normalized = normalizeHexInput(hex);
  if (!normalized.ok) {
    if (normalized.error) {
      throw normalized.error;
    }

    throw createDecoderError(
      "HEX_INVALID",
      "Hex input contains invalid characters.",
      "Hex allows only 0-9 and A-F.",
      "Remove non-hex symbols and retry."
    );
  }

  const bytes = new Uint8Array(normalized.normalized.length / 2);
  for (let i = 0; i < normalized.normalized.length; i += 2) {
    const byteIndex = i / 2;
    bytes[byteIndex] = Number.parseInt(normalized.normalized.slice(i, i + 2), 16);
  }
  return bytes;
}

function bytesToHex(bytes) {
  assertByteArray(bytes);
  let hex = "";
  for (let i = 0; i < bytes.length; i += 1) {
    hex += bytes[i].toString(16).padStart(2, "0");
  }
  return hex.toUpperCase();
}

function bytesToBase64(bytes) {
  assertByteArray(bytes);

  if (typeof Buffer !== "undefined") {
    return Buffer.from(bytes).toString("base64");
  }

  if (typeof btoa === "function") {
    let binary = "";
    for (let i = 0; i < bytes.length; i += 1) {
      binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
  }

  throw createDecoderError(
    "BASE64_UNSUPPORTED_ENV",
    "Base64 encoding is not available in this environment.",
    "Neither Buffer nor btoa is accessible.",
    "Run in a modern browser or Node.js runtime."
  );
}

function base64ToBytes(base64) {
  const normalized = normalizeBase64Input(base64);
  if (!normalized.ok) {
    if (normalized.error) {
      throw normalized.error;
    }

    throw createDecoderError(
      "BASE64_INVALID",
      "Base64 input contains invalid characters.",
      "Only base64/base64url characters are supported.",
      "Use a valid base64 string such as DAAPpMDkAAA=."
    );
  }

  try {
    if (typeof Buffer !== "undefined") {
      return Uint8Array.from(Buffer.from(normalized.normalized, "base64"));
    }

    if (typeof atob === "function") {
      const binary = atob(normalized.normalized);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i += 1) {
        bytes[i] = binary.charCodeAt(i);
      }
      return bytes;
    }
  } catch (error) {
    throw createDecoderError(
      "BASE64_DECODE_FAILED",
      "Base64 decoding failed.",
      error instanceof Error ? error.message : "The input cannot be decoded.",
      "Ensure the payload is complete and not truncated."
    );
  }

  throw createDecoderError(
    "BASE64_UNSUPPORTED_ENV",
    "Base64 decoding is not available in this environment.",
    "Neither Buffer nor atob is accessible.",
    "Run in a modern browser or Node.js runtime."
  );
}

function parseInputToBytes(rawInput) {
  const normalized = normalizeInput(rawInput);
  const bytes = normalized.format === "hex"
    ? hexToBytes(normalized.normalized)
    : base64ToBytes(normalized.normalized);

  return {
    format: normalized.format,
    normalizedInput: normalized.normalized,
    bytes,
    byteLength: bytes.length,
    hex: bytesToHex(bytes),
    base64: bytesToBase64(bytes)
  };
}

function parseUetInput(rawInput) {
  const parsed = parseInputToBytes(rawInput);
  if (parsed.byteLength !== UET_BYTE_LENGTH) {
    throw createDecoderError(
      "UET_INVALID_LENGTH",
      `Input is ${parsed.byteLength} bytes (${parsed.hex.length} hex chars).`,
      `UET requires exactly ${UET_BYTE_LENGTH} bytes (${UET_HEX_LENGTH} hex chars).`,
      "Check for missing/extra characters in the payload."
    );
  }
  return parsed;
}

function assertByteArray(bytes) {
  if (!(bytes instanceof Uint8Array)) {
    throw createDecoderError(
      "BYTES_TYPE_INVALID",
      "Expected Uint8Array bytes input.",
      `Received '${typeof bytes}' instead.`,
      "Convert the payload to Uint8Array before decoding."
    );
  }
}

function bytesToBigInt(bytes) {
  assertByteArray(bytes);
  let value = 0n;
  for (let i = 0; i < bytes.length; i += 1) {
    value = (value << BYTE_SHIFT) | BigInt(bytes[i]);
  }
  return value;
}

function bigIntToBytes(value, byteLength) {
  if (typeof value !== "bigint") {
    throw createDecoderError(
      "BIGINT_REQUIRED",
      "BigInt value is required.",
      `Received '${typeof value}' instead.`,
      "Pass a BigInt UET value before converting to bytes."
    );
  }

  if (!Number.isInteger(byteLength) || byteLength <= 0) {
    throw createDecoderError(
      "BYTE_LENGTH_INVALID",
      "Byte length must be a positive integer.",
      "The converter needs a deterministic output length.",
      "Use byteLength = 8 for UET payloads."
    );
  }

  const bytes = new Uint8Array(byteLength);
  let remaining = value;
  for (let index = byteLength - 1; index >= 0; index -= 1) {
    bytes[index] = Number(remaining & BYTE_MASK);
    remaining >>= BYTE_SHIFT;
  }
  return bytes;
}

function extractField(value, fieldName) {
  return Number((value >> UET_SHIFTS[fieldName]) & UET_MASKS[fieldName]);
}

function decodeActionFlags(actionFlags) {
  const labels = [];
  for (let i = 0; i < ACTION_FLAG_DEFINITIONS.length; i += 1) {
    const item = ACTION_FLAG_DEFINITIONS[i];
    if ((actionFlags & (1 << item.bit)) !== 0) {
      labels.push(item.label);
    }
  }
  return labels;
}

function decodeUetFromBytes(bytes) {
  assertByteArray(bytes);
  if (bytes.length !== UET_BYTE_LENGTH) {
    throw createDecoderError(
      "UET_BYTES_LENGTH_INVALID",
      `Input has ${bytes.length} bytes.`,
      `UET decoding requires exactly ${UET_BYTE_LENGTH} bytes.`,
      "Provide an 8-byte payload before attempting UET decode."
    );
  }

  const value = bytesToBigInt(bytes);
  const emergencyType = extractField(value, "emergencyType");
  const priority = extractField(value, "priority");
  const actionFlags = extractField(value, "actionFlags");
  const zoneHash = extractField(value, "zoneHash");
  const timestampMinutes = extractField(value, "timestampMinutes");
  const confirmHash = extractField(value, "confirmHash");

  return {
    emergencyType,
    emergencyTypeLabel: EMERGENCY_TYPE_LABELS[emergencyType] ?? "Unknown",
    priority,
    priorityLabel: PRIORITY_LABELS[priority] ?? "Unknown",
    actionFlags,
    actionFlagLabels: decodeActionFlags(actionFlags),
    zoneHash,
    timestampMinutes,
    confirmHash,
    valueBigInt: value,
    valueHex: bytesToHex(bytes),
    valueBase64: bytesToBase64(bytes),
    valueBinary: value.toString(2).padStart(UET_TOTAL_BITS_NUMBER, "0")
  };
}

function decodeUet(rawInput) {
  const parsed = parseUetInput(rawInput);
  const decoded = decodeUetFromBytes(parsed.bytes);
  return {
    ...decoded,
    inputFormat: parsed.format,
    normalizedInput: parsed.normalizedInput
  };
}

function decodeUetFromHex(hex) {
  return decodeUetFromBytes(hexToBytes(hex));
}

function decodeUetFromBase64(base64) {
  return decodeUetFromBytes(base64ToBytes(base64));
}

function normalizeFieldNumber(fields, fieldName) {
  const rawValue = fields[fieldName];
  if (!Number.isInteger(rawValue)) {
    throw createDecoderError(
      "FIELD_NOT_INTEGER",
      `Field '${fieldName}' must be an integer.`,
      `Received '${String(rawValue)}'.`,
      `Set '${fieldName}' to an integer between 0 and ${UET_FIELD_LIMITS[fieldName]}.`
    );
  }

  const maxValue = UET_FIELD_LIMITS[fieldName];
  if (rawValue < 0 || rawValue > maxValue) {
    throw createDecoderError(
      "FIELD_OUT_OF_RANGE",
      `Field '${fieldName}' is ${rawValue}.`,
      `Allowed range is 0..${maxValue}.`,
      `Provide a value within the allowed range for '${fieldName}'.`
    );
  }

  return rawValue;
}

function normalizeUetFields(fields) {
  if (!fields || typeof fields !== "object") {
    throw createDecoderError(
      "FIELDS_INVALID",
      "UET fields are missing.",
      "Encoding requires all UET field values.",
      "Pass an object with emergencyType, priority, actionFlags, zoneHash, timestampMinutes, confirmHash."
    );
  }

  return {
    emergencyType: normalizeFieldNumber(fields, "emergencyType"),
    priority: normalizeFieldNumber(fields, "priority"),
    actionFlags: normalizeFieldNumber(fields, "actionFlags"),
    zoneHash: normalizeFieldNumber(fields, "zoneHash"),
    timestampMinutes: normalizeFieldNumber(fields, "timestampMinutes"),
    confirmHash: normalizeFieldNumber(fields, "confirmHash")
  };
}

function encodeUetToBigInt(fields) {
  const normalized = normalizeUetFields(fields);
  let value = 0n;
  value |= BigInt(normalized.emergencyType) << UET_SHIFTS.emergencyType;
  value |= BigInt(normalized.priority) << UET_SHIFTS.priority;
  value |= BigInt(normalized.actionFlags) << UET_SHIFTS.actionFlags;
  value |= BigInt(normalized.zoneHash) << UET_SHIFTS.zoneHash;
  value |= BigInt(normalized.timestampMinutes) << UET_SHIFTS.timestampMinutes;
  value |= BigInt(normalized.confirmHash) << UET_SHIFTS.confirmHash;
  return value;
}

function encodeUetToBytes(fields) {
  return bigIntToBytes(encodeUetToBigInt(fields), UET_BYTE_LENGTH);
}

function encodeUetToHex(fields) {
  return bytesToHex(encodeUetToBytes(fields));
}

function encodeUetToBase64(fields) {
  return bytesToBase64(encodeUetToBytes(fields));
}

function encodeUet(fields) {
  const normalized = normalizeUetFields(fields);
  const value = encodeUetToBigInt(normalized);
  const bytes = bigIntToBytes(value, UET_BYTE_LENGTH);
  return {
    ...normalized,
    valueBigInt: value,
    bytes,
    hex: bytesToHex(bytes),
    base64: bytesToBase64(bytes)
  };
}

function pickComparableFields(decoded) {
  return {
    emergencyType: decoded.emergencyType,
    priority: decoded.priority,
    actionFlags: decoded.actionFlags,
    zoneHash: decoded.zoneHash,
    timestampMinutes: decoded.timestampMinutes,
    confirmHash: decoded.confirmHash
  };
}

function compareUetFields(left, right) {
  for (let i = 0; i < UET_FIELD_NAMES.length; i += 1) {
    const fieldName = UET_FIELD_NAMES[i];
    if (left[fieldName] !== right[fieldName]) {
      return false;
    }
  }
  return true;
}

function verifySingleCoreVector(vector) {
  const result = {
    id: vector.id,
    decodePassed: false,
    roundtripPassed: false,
    base64Passed: false,
    passed: false,
    expectedHex: vector.hex,
    expectedBase64: vector.base64,
    roundtripHex: "",
    roundtripBase64: "",
    error: null
  };

  try {
    const decoded = decodeUetFromHex(vector.hex);
    const decodedFields = pickComparableFields(decoded);
    result.decodePassed = compareUetFields(decodedFields, vector.fields);

    if (!result.decodePassed) {
      throw createDecoderError(
        "VECTOR_DECODE_MISMATCH",
        `Decoded fields mismatch for vector '${vector.id}'.`,
        "The extracted values do not match the expected deterministic fields.",
        "Review shifts/masks and Big-Endian extraction."
      );
    }

    const encoded = encodeUet(decodedFields);
    result.roundtripHex = encoded.hex;
    result.roundtripBase64 = encoded.base64;
    result.roundtripPassed = encoded.hex === vector.hex;
    result.base64Passed = encoded.base64 === vector.base64;
    result.passed = result.decodePassed && result.roundtripPassed && result.base64Passed;

    if (!result.roundtripPassed || !result.base64Passed) {
      throw createDecoderError(
        "VECTOR_ROUNDTRIP_MISMATCH",
        `Roundtrip mismatch for vector '${vector.id}'.`,
        "Re-encoded payload differs from expected deterministic output.",
        "Review encode shifts/masks and byte conversion logic."
      );
    }
  } catch (error) {
    result.error = serializeError(error);
    result.passed = false;
  }

  return result;
}

function verifyCoreVectors() {
  const results = CORE_TEST_VECTORS.map(verifySingleCoreVector);
  const passed = results.filter((entry) => entry.passed).length;

  return {
    suiteId: "core-uet-phase-a",
    total: CORE_TEST_VECTORS.length,
    passed,
    failed: CORE_TEST_VECTORS.length - passed,
    allPassed: passed === CORE_TEST_VECTORS.length,
    results
  };
}

function formatVectorStatusLine(result) {
  const icon = result.passed ? "✅" : "❌";
  const decodeText = result.decodePassed ? "decode ✓" : "decode ✗";
  const roundtripText = result.roundtripPassed ? "roundtrip ✓" : "roundtrip ✗";
  return `${icon} ${result.id}    ${decodeText}  ${roundtripText}`;
}

function tryDecodeUet(rawInput) {
  try {
    return {
      ok: true,
      value: decodeUet(rawInput),
      error: null
    };
  } catch (error) {
    return {
      ok: false,
      value: null,
      error: serializeError(error)
    };
  }
}

function tryEncodeUet(fields) {
  try {
    return {
      ok: true,
      value: encodeUet(fields),
      error: null
    };
  } catch (error) {
    return {
      ok: false,
      value: null,
      error: serializeError(error)
    };
  }
}

const EcpStudioDecoder = Object.freeze({
  UET_BYTE_LENGTH,
  UET_HEX_LENGTH,
  UET_TOTAL_BITS,
  UET_SHIFTS,
  UET_MASKS,
  UET_FIELD_LIMITS,
  UET_FIELD_NAMES,
  EMERGENCY_TYPE_LABELS,
  PRIORITY_LABELS,
  ACTION_FLAG_DEFINITIONS,
  CORE_TEST_VECTORS,
  DecoderError,
  createDecoderError,
  serializeError,
  normalizeInput,
  parseInputToBytes,
  parseUetInput,
  hexToBytes,
  bytesToHex,
  base64ToBytes,
  bytesToBase64,
  decodeUetFromBytes,
  decodeUetFromHex,
  decodeUetFromBase64,
  decodeUet,
  encodeUetToBigInt,
  encodeUetToBytes,
  encodeUetToHex,
  encodeUetToBase64,
  encodeUet,
  pickComparableFields,
  compareUetFields,
  verifySingleCoreVector,
  verifyCoreVectors,
  formatVectorStatusLine,
  tryDecodeUet,
  tryEncodeUet
});

if (typeof globalThis !== "undefined") {
  globalThis.EcpStudioDecoder = EcpStudioDecoder;
}

export {
  UET_BYTE_LENGTH,
  UET_HEX_LENGTH,
  UET_TOTAL_BITS,
  UET_SHIFTS,
  UET_MASKS,
  UET_FIELD_LIMITS,
  UET_FIELD_NAMES,
  EMERGENCY_TYPE_LABELS,
  PRIORITY_LABELS,
  ACTION_FLAG_DEFINITIONS,
  CORE_TEST_VECTORS,
  DecoderError,
  createDecoderError,
  serializeError,
  normalizeInput,
  parseInputToBytes,
  parseUetInput,
  hexToBytes,
  bytesToHex,
  base64ToBytes,
  bytesToBase64,
  decodeUetFromBytes,
  decodeUetFromHex,
  decodeUetFromBase64,
  decodeUet,
  encodeUetToBigInt,
  encodeUetToBytes,
  encodeUetToHex,
  encodeUetToBase64,
  encodeUet,
  pickComparableFields,
  compareUetFields,
  verifySingleCoreVector,
  verifyCoreVectors,
  formatVectorStatusLine,
  tryDecodeUet,
  tryEncodeUet,
  EcpStudioDecoder
};
