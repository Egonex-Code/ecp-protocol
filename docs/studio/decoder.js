/*
 * ECP Studio Decoder Module
 * Pure logic only: no DOM, no tracking, no network requests.
 */

const UET_BYTE_LENGTH = 8;
const UET_HEX_LENGTH = UET_BYTE_LENGTH * 2;
const UET_TOTAL_BITS = 64n;
const UET_TOTAL_BITS_NUMBER = 64;
const BYTE_SHIFT = 8n;
const BYTE_MASK = 0xffn;

const ENVELOPE_MAGIC = 0xec50;
const ENVELOPE_VERSION = 0x01;
const ENVELOPE_HEADER_SIZE = 22;
const ENVELOPE_DEFAULT_HMAC_LENGTH = 12;
const ENVELOPE_MIN_HMAC_LENGTH = 8;
const ENVELOPE_MAX_HMAC_LENGTH = 16;

const ENVELOPE_OFFSETS = Object.freeze({
  magic: 0,
  version: 2,
  flags: 3,
  priority: 4,
  ttl: 5,
  keyVersion: 6,
  messageId: 7,
  timestamp: 15,
  payloadType: 19,
  payloadLength: 20
});

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

const EMERGENCY_TYPE_ENUM_NAMES = Object.freeze([
  "Fire",
  "Evacuation",
  "Earthquake",
  "Flood",
  "Medical",
  "Security",
  "Chemical",
  "Lockdown",
  "AllClear",
  "Test",
  "Custom1",
  "Custom2",
  "Custom3",
  "Custom4",
  "Custom5",
  "Reserved"
]);

const PRIORITY_LABELS = Object.freeze(["Low", "Medium", "High", "Critical"]);
const PRIORITY_ENUM_NAMES = Object.freeze(["Low", "Medium", "High", "Critical"]);

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

const PAYLOAD_TYPE_LABELS = Object.freeze([
  "Alert",
  "Confirmation",
  "Ping",
  "Cascade",
  "CapabilityNegotiation",
  "KeyRotation"
]);

const ENVELOPE_FLAG_DEFINITIONS = Object.freeze([
  Object.freeze({ bit: 0, name: "NeedsConfirmation", label: "Needs Confirmation" }),
  Object.freeze({ bit: 1, name: "Broadcast", label: "Broadcast" }),
  Object.freeze({ bit: 2, name: "Encrypted", label: "Encrypted" }),
  Object.freeze({ bit: 3, name: "Compressed", label: "Compressed" }),
  Object.freeze({ bit: 4, name: "Cascade", label: "Cascade" }),
  Object.freeze({ bit: 5, name: "RetryAllowed", label: "Retry Allowed" })
]);

const CORE_TEST_VECTORS = Object.freeze([
  {
    id: "uet-fire-critical-001",
    fields: {
      emergencyType: 0,
      priority: 3,
      actionFlags: 0,
      zoneHash: 1001,
      timestampMinutes: 12345,
      confirmHash: 0
    },
    expectedHex: "0C000FA4C0E40000",
    expectedBase64: "DAAPpMDkAAA="
  },
  {
    id: "uet-security-high-flags-002",
    fields: {
      emergencyType: 5,
      priority: 2,
      actionFlags: 19,
      zoneHash: 48879,
      timestampMinutes: 54321,
      confirmHash: 174762
    },
    expectedHex: "584EFBBF50C6AAAA",
    expectedBase64: "WE77v1DGqqo="
  },
  {
    id: "uet-allclear-low-allflags-003",
    fields: {
      emergencyType: 8,
      priority: 0,
      actionFlags: 255,
      zoneHash: 1,
      timestampMinutes: 1,
      confirmHash: 262143
    },
    expectedHex: "83FC00040007FFFF",
    expectedBase64: "g/wABAAH//8="
  },
  {
    id: "uet-custom1-medium-004",
    fields: {
      emergencyType: 10,
      priority: 1,
      actionFlags: 36,
      zoneHash: 65535,
      timestampMinutes: 65535,
      confirmHash: 1
    },
    expectedHex: "A493FFFFFFFC0001",
    expectedBase64: "pJP////8AAE="
  },
  {
    id: "uet-reserved-critical-max-005",
    fields: {
      emergencyType: 15,
      priority: 3,
      actionFlags: 255,
      zoneHash: 65535,
      timestampMinutes: 65535,
      confirmHash: 262143
    },
    expectedHex: "FFFFFFFFFFFFFFFF",
    expectedBase64: "//////////8="
  },
  {
    id: "uet-zero-min-006",
    fields: {
      emergencyType: 0,
      priority: 0,
      actionFlags: 0,
      zoneHash: 0,
      timestampMinutes: 0,
      confirmHash: 0
    },
    expectedHex: "0000000000000000",
    expectedBase64: "AAAAAAAAAAA="
  },
  {
    id: "uet-confirmhash-nonzero-007",
    fields: {
      emergencyType: 2,
      priority: 1,
      actionFlags: 0,
      zoneHash: 4660,
      timestampMinutes: 22136,
      confirmHash: 109517
    },
    expectedHex: "240048D159E1ABCD",
    expectedBase64: "JABI0Vnhq80="
  },
  {
    id: "uet-bitlayout-reference-008",
    fields: {
      emergencyType: 5,
      priority: 2,
      actionFlags: 172,
      zoneHash: 4660,
      timestampMinutes: 22136,
      confirmHash: 174762
    },
    expectedHex: "5AB048D159E2AAAA",
    expectedBase64: "WrBI0Vniqqo="
  }
]);

const ENVELOPE_TEST_VECTORS = Object.freeze([
  {
    id: "env-signed-uet-h12-001",
    input: {
      flags: 3,
      priority: 3,
      ttl: 120,
      keyVersion: 1,
      messageId: "0x0102030405060708",
      timestampSeconds: 1700000000,
      payloadType: 0,
      payloadHex: "0C000FA4C0E40000",
      hmacLength: 12,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC50010303780101020304050607086553F1000000080C000FA4C0E400003A034F4B73B4D65E5B4AF650",
    expectedBase64: "7FABAwN4AQECAwQFBgcIZVPxAAAACAwAD6TA5AAAOgNPS3O01l5bSvZQ"
  },
  {
    id: "env-signed-uet-h8-002",
    input: {
      flags: 3,
      priority: 3,
      ttl: 120,
      keyVersion: 1,
      messageId: "0x0102030405060708",
      timestampSeconds: 1700000000,
      payloadType: 0,
      payloadHex: "0C000FA4C0E40000",
      hmacLength: 8,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC50010303780101020304050607086553F1000000080C000FA4C0E400003A034F4B73B4D65E",
    expectedBase64: "7FABAwN4AQECAwQFBgcIZVPxAAAACAwAD6TA5AAAOgNPS3O01l4="
  },
  {
    id: "env-signed-uet-h10-003",
    input: {
      flags: 3,
      priority: 3,
      ttl: 120,
      keyVersion: 1,
      messageId: "0x0102030405060708",
      timestampSeconds: 1700000000,
      payloadType: 0,
      payloadHex: "0C000FA4C0E40000",
      hmacLength: 10,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC50010303780101020304050607086553F1000000080C000FA4C0E400003A034F4B73B4D65E5B4A",
    expectedBase64: "7FABAwN4AQECAwQFBgcIZVPxAAAACAwAD6TA5AAAOgNPS3O01l5bSg=="
  },
  {
    id: "env-signed-uet-h16-004",
    input: {
      flags: 3,
      priority: 3,
      ttl: 120,
      keyVersion: 1,
      messageId: "0x0102030405060708",
      timestampSeconds: 1700000000,
      payloadType: 0,
      payloadHex: "0C000FA4C0E40000",
      hmacLength: 16,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC50010303780101020304050607086553F1000000080C000FA4C0E400003A034F4B73B4D65E5B4AF650DC800E0B",
    expectedBase64: "7FABAwN4AQECAwQFBgcIZVPxAAAACAwAD6TA5AAAOgNPS3O01l5bSvZQ3IAOCw=="
  },
  {
    id: "env-unsigned-uet-005",
    input: {
      flags: 0,
      priority: 3,
      ttl: 120,
      keyVersion: 0,
      messageId: "0x0102030405060708",
      timestampSeconds: 1700000000,
      payloadType: 0,
      payloadHex: "0C000FA4C0E40000",
      hmacLength: 0,
      hmacKeyHex: ""
    },
    expectedHex: "EC50010003780001020304050607086553F1000000080C000FA4C0E40000",
    expectedBase64: "7FABAAN4AAECAwQFBgcIZVPxAAAACAwAD6TA5AAA"
  },
  {
    id: "env-signed-empty-payload-006",
    input: {
      flags: 0,
      priority: 2,
      ttl: 30,
      keyVersion: 1,
      messageId: "0x1111111111111111",
      timestampSeconds: 1700000100,
      payloadType: 0,
      payloadHex: "",
      hmacLength: 12,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC500100021E0111111111111111116553F16400000044049575B07C04F15CCD31FA",
    expectedBase64: "7FABAAIeARERERERERERZVPxZAAAAEQElXWwfATxXM0x+g=="
  },
  {
    id: "env-signed-text-payload-007",
    input: {
      flags: 0,
      priority: 2,
      ttl: 45,
      keyVersion: 2,
      messageId: "0x2222222222222222",
      timestampSeconds: 1700000200,
      payloadType: 0,
      payloadHex: "464952452047415445204232",
      hmacLength: 12,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC500100022D0222222222222222226553F1C800000C464952452047415445204232495946439E638D8C76D187E7",
    expectedBase64: "7FABAAItAiIiIiIiIiIiZVPxyAAADEZJUkUgR0FURSBCMklZRkOeY42MdtGH5w=="
  },
  {
    id: "env-flags-needsconfirm-broadcast-008",
    input: {
      flags: 3,
      priority: 2,
      ttl: 60,
      keyVersion: 3,
      messageId: "0x3333333333333333",
      timestampSeconds: 1700000300,
      payloadType: 1,
      payloadHex: "01020304",
      hmacLength: 12,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC500103023C0333333333333333336553F22C01000401020304A3D6C732A48CE4021B37E64B",
    expectedBase64: "7FABAwI8AzMzMzMzMzMzZVPyLAEABAECAwSj1scypIzkAhs35ks="
  },
  {
    id: "env-keyversion-2-009",
    input: {
      flags: 0,
      priority: 1,
      ttl: 20,
      keyVersion: 2,
      messageId: "0x4444444444444444",
      timestampSeconds: 1700000400,
      payloadType: 5,
      payloadHex: "AA55",
      hmacLength: 12,
      hmacKeyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F"
    },
    expectedHex: "EC50010001140244444444444444446553F290050002AA55A23D840E30CAB37F45391F27",
    expectedBase64: "7FABAAEUAkREREREREREZVPykAUAAqpVoj2EDjDKs39FOR8n"
  }
]);

const HMAC_TEST_VECTORS = Object.freeze([
  {
    id: "hmac-data-8-001",
    kind: "hmac-compute",
    input: {
      keyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F",
      dataHex: "DEADBEEFCAFEBABE",
      hmacLength: 8
    },
    expectedTagHex: "9815E43D048CC88D"
  },
  {
    id: "hmac-data-12-002",
    kind: "hmac-compute",
    input: {
      keyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F",
      dataHex: "DEADBEEFCAFEBABE",
      hmacLength: 12
    },
    expectedTagHex: "9815E43D048CC88DE7405E98"
  },
  {
    id: "hmac-data-16-003",
    kind: "hmac-compute",
    input: {
      keyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F",
      dataHex: "DEADBEEFCAFEBABE",
      hmacLength: 16
    },
    expectedTagHex: "9815E43D048CC88DE7405E9873F75737"
  },
  {
    id: "hmac-empty-payload-12-004",
    kind: "hmac-compute",
    input: {
      keyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F",
      dataHex: "",
      hmacLength: 12
    },
    expectedTagHex: "E116AA44F445EA1FE07AF769"
  },
  {
    id: "hmac-zero-length-005",
    kind: "hmac-compute",
    input: {
      keyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F",
      dataHex: "DEADBEEFCAFEBABE",
      hmacLength: 0
    },
    expectedTagHex: ""
  },
  {
    id: "hmac-wrong-key-verify-006",
    kind: "hmac-verify",
    input: {
      keyHex: "F0E1D2C3B4A5968778695A4B3C2D1E0F00112233445566778899AABBCCDDEEFF",
      dataHex: "DEADBEEFCAFEBABE",
      tagHex: "9815E43D048CC88DE7405E98"
    },
    expectedVerify: false
  },
  {
    id: "hmac-tampered-data-verify-007",
    kind: "hmac-verify",
    input: {
      keyHex: "00112233445566778899AABBCCDDEEFF102132435465768798A9BACBDCEDFE0F",
      dataHex: "DEADBEEFCAFEBABF",
      tagHex: "9815E43D048CC88DE7405E98"
    },
    expectedVerify: false
  }
]);

const NEGATIVE_TEST_VECTORS = Object.freeze([
  {
    id: "neg-uet-invalid-length-001",
    kind: "try-decode",
    inputHex: "00000000000000",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-invalid-magic-002",
    kind: "try-decode",
    inputHex: "0000010003780001020304050607086553F1000000080C000FA4C0E40000",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-invalid-version-003",
    kind: "try-decode",
    inputHex: "EC50020003780001020304050607086553F1000000080C000FA4C0E40000",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-truncated-header-004",
    kind: "try-decode",
    inputHex: "EC500100037800010203",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-payload-length-mismatch-005",
    kind: "try-decode",
    inputHex: "EC50010003780001020304050607086553F1000000100C000FA4C0E40000",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-derived-hmaclen-5-006",
    kind: "try-decode",
    inputHex: "EC50010003780001020304050607086553F1000000080C000FA4C0E400000102030405",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-length-short-007",
    kind: "try-decode",
    inputHex: "EC50010003780001020304050607086553F1000000080C000FA4C0E400",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-length-long-008",
    kind: "try-decode",
    inputHex: "EC50010003780001020304050607086553F1000000080C000FA4C0E4000000",
    expected: { tryDecode: false }
  },
  {
    id: "neg-envelope-wrong-key-verify-009",
    kind: "try-decode-envelope",
    inputHex: "EC50010303780101020304050607086553F1000000080C000FA4C0E400003A034F4B73B4D65E5B4AF650",
    input: {
      hmacKeyHex: "F0E1D2C3B4A5968778695A4B3C2D1E0F00112233445566778899AABBCCDDEEFF",
      hmacLength: 12
    },
    expected: {
      tryDecodeEnvelope: true,
      isValid: false
    }
  },
  {
    id: "neg-random-garbage-010",
    kind: "try-decode",
    inputHex: "DEADBEEF00010203FF",
    expected: { tryDecode: false }
  }
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
    howToFix: "Retry with a valid ECP payload and report this issue if it persists.",
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
      "Pass a hex/base64 string."
    );
  }

  const trimmed = rawInput.trim();
  if (!trimmed) {
    throw createDecoderError(
      "INPUT_EMPTY",
      "Input is empty.",
      "The decoder needs a payload to parse.",
      "Paste a UET or envelope payload in hex/base64."
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

function hexToBytes(hex) {
  if (typeof hex === "string" && removeWhitespace(hex).length === 0) {
    return new Uint8Array(0);
  }

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
    bytes[i / 2] = Number.parseInt(normalized.normalized.slice(i, i + 2), 16);
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
      "Use a valid base64 string."
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
      "Pass a BigInt value before converting to bytes."
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

function toPaddedHex(value, width) {
  return Number(value).toString(16).toUpperCase().padStart(width, "0");
}

function toPaddedHexBigInt(value, width) {
  return value.toString(16).toUpperCase().padStart(width, "0");
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
      "Pass emergencyType, priority, actionFlags, zoneHash, timestampMinutes, confirmHash."
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
    emergencyTypeEnum: EMERGENCY_TYPE_ENUM_NAMES[emergencyType] ?? "Reserved",
    priority,
    priorityLabel: PRIORITY_LABELS[priority] ?? "Unknown",
    priorityEnum: PRIORITY_ENUM_NAMES[priority] ?? "Critical",
    actionFlags,
    actionFlagLabels: decodeActionFlags(actionFlags),
    zoneHash,
    timestampMinutes,
    confirmHash,
    valueBigInt: value,
    valueHex: bytesToHex(bytes),
    valueBase64: bytesToBase64(bytes),
    valueBinary: value.toString(2).padStart(UET_TOTAL_BITS_NUMBER, "0"),
    bytes
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

function readUInt16BE(bytes, offset) {
  return (bytes[offset] << 8) | bytes[offset + 1];
}

function readUInt32BE(bytes, offset) {
  return (
    ((bytes[offset] << 24) >>> 0) |
    (bytes[offset + 1] << 16) |
    (bytes[offset + 2] << 8) |
    bytes[offset + 3]
  ) >>> 0;
}

function readUInt64BE(bytes, offset) {
  let value = 0n;
  for (let i = 0; i < 8; i += 1) {
    value = (value << 8n) | BigInt(bytes[offset + i]);
  }
  return value;
}

function writeUInt16BE(bytes, offset, value) {
  bytes[offset] = (value >>> 8) & 0xff;
  bytes[offset + 1] = value & 0xff;
}

function writeUInt32BE(bytes, offset, value) {
  bytes[offset] = (value >>> 24) & 0xff;
  bytes[offset + 1] = (value >>> 16) & 0xff;
  bytes[offset + 2] = (value >>> 8) & 0xff;
  bytes[offset + 3] = value & 0xff;
}

function writeUInt64BE(bytes, offset, value) {
  let remaining = value;
  for (let i = 7; i >= 0; i -= 1) {
    bytes[offset + i] = Number(remaining & 0xffn);
    remaining >>= 8n;
  }
}

function normalizeEnvelopeNumber(value, fieldName, minValue, maxValue) {
  if (!Number.isInteger(value)) {
    throw createDecoderError(
      "ENVELOPE_FIELD_NOT_INTEGER",
      `Field '${fieldName}' must be an integer.`,
      `Received '${String(value)}'.`,
      `Provide an integer in range ${minValue}..${maxValue}.`
    );
  }
  if (value < minValue || value > maxValue) {
    throw createDecoderError(
      "ENVELOPE_FIELD_OUT_OF_RANGE",
      `Field '${fieldName}' is ${value}.`,
      `Allowed range is ${minValue}..${maxValue}.`,
      `Adjust '${fieldName}' to a valid value.`
    );
  }
  return value;
}

function parseMessageId(messageId) {
  if (typeof messageId === "bigint") {
    if (messageId < 0n || messageId > 0xFFFFFFFFFFFFFFFFn) {
      throw createDecoderError(
        "ENVELOPE_MESSAGE_ID_RANGE",
        "MessageId is outside uint64 range.",
        "MessageId must be between 0x0000000000000000 and 0xFFFFFFFFFFFFFFFF.",
        "Provide a valid uint64 message id."
      );
    }
    return messageId;
  }

  if (typeof messageId === "number" && Number.isInteger(messageId)) {
    return parseMessageId(BigInt(messageId));
  }

  if (typeof messageId === "string") {
    const normalized = messageId.trim().replace(/^0x/i, "");
    if (!/^[0-9A-Fa-f]{1,16}$/.test(normalized)) {
      throw createDecoderError(
        "ENVELOPE_MESSAGE_ID_INVALID",
        "MessageId hex format is invalid.",
        "MessageId must be a 1-16 digit hex string, optional 0x prefix.",
        "Example: 0x0102030405060708"
      );
    }
    return BigInt(`0x${normalized}`);
  }

  throw createDecoderError(
    "ENVELOPE_MESSAGE_ID_TYPE",
    "MessageId type is not supported.",
    `Received type '${typeof messageId}'.`,
    "Provide messageId as bigint, integer number, or hex string."
  );
}

function validateHmacLength(hmacLength) {
  if (!Number.isInteger(hmacLength)) {
    throw createDecoderError(
      "HMAC_LENGTH_INVALID",
      "HMAC length must be an integer.",
      `Received '${String(hmacLength)}'.`,
      "Use 0 or a value between 8 and 16."
    );
  }
  if (hmacLength !== 0 && (hmacLength < ENVELOPE_MIN_HMAC_LENGTH || hmacLength > ENVELOPE_MAX_HMAC_LENGTH)) {
    throw createDecoderError(
      "HMAC_LENGTH_OUT_OF_RANGE",
      `HMAC length ${hmacLength} is invalid.`,
      "Allowed values are 0 (unsigned) or 8..16.",
      "Use 0, 8, 10, 12, 14, or 16."
    );
  }
}

function normalizeEnvelopeInput(input) {
  if (!input || typeof input !== "object") {
    throw createDecoderError(
      "ENVELOPE_INPUT_INVALID",
      "Envelope input is missing.",
      "Encoding requires all envelope fields.",
      "Provide flags, priority, ttl, keyVersion, messageId, timestampSeconds, payloadType, payloadHex, hmacLength."
    );
  }

  const flags = normalizeEnvelopeNumber(input.flags, "flags", 0, 255);
  const priority = normalizeEnvelopeNumber(input.priority, "priority", 0, 3);
  const ttl = normalizeEnvelopeNumber(input.ttl, "ttl", 0, 255);
  const keyVersion = normalizeEnvelopeNumber(input.keyVersion, "keyVersion", 0, 255);
  const payloadType = normalizeEnvelopeNumber(input.payloadType, "payloadType", 0, 255);
  const timestampSeconds = normalizeEnvelopeNumber(input.timestampSeconds, "timestampSeconds", 0, 0xFFFFFFFF);
  const messageId = parseMessageId(input.messageId);
  const payloadBytes = hexToBytes(input.payloadHex ?? "");
  const hmacLength = input.hmacLength ?? ENVELOPE_DEFAULT_HMAC_LENGTH;
  validateHmacLength(hmacLength);

  if (payloadBytes.length > 0xFFFF) {
    throw createDecoderError(
      "ENVELOPE_PAYLOAD_TOO_LARGE",
      `Payload has ${payloadBytes.length} bytes.`,
      "Envelope payload length field is uint16.",
      "Payload must be <= 65535 bytes."
    );
  }

  let hmacKeyBytes = new Uint8Array(0);
  if (hmacLength > 0) {
    if (typeof input.hmacKeyHex !== "string" || input.hmacKeyHex.trim().length === 0) {
      throw createDecoderError(
        "ENVELOPE_HMAC_KEY_REQUIRED",
        "Signed envelope requires an HMAC key.",
        "hmacLength > 0 but hmacKeyHex is missing.",
        "Provide hmacKeyHex when hmacLength is between 8 and 16."
      );
    }
    hmacKeyBytes = hexToBytes(input.hmacKeyHex);
  }

  return {
    flags,
    priority,
    ttl,
    keyVersion,
    messageId,
    timestampSeconds,
    payloadType,
    payloadBytes,
    payloadLength: payloadBytes.length,
    hmacLength,
    hmacKeyBytes
  };
}

function hasEnvelopeMagic(bytes) {
  return bytes.length >= 2 && bytes[0] === 0xEC && bytes[1] === 0x50;
}

function decodeEnvelopeFlags(flags) {
  const labels = [];
  for (let i = 0; i < ENVELOPE_FLAG_DEFINITIONS.length; i += 1) {
    const def = ENVELOPE_FLAG_DEFINITIONS[i];
    if ((flags & (1 << def.bit)) !== 0) {
      labels.push(def.label);
    }
  }
  return labels;
}

function getSubtleCrypto() {
  const subtle = globalThis?.crypto?.subtle;
  if (!subtle) {
    throw createDecoderError(
      "WEB_CRYPTO_UNAVAILABLE",
      "Web Crypto API is not available.",
      "HMAC verification requires crypto.subtle support.",
      "Use a modern browser or Node runtime with Web Crypto support."
    );
  }
  return subtle;
}

function fixedTimeEquals(left, right) {
  assertByteArray(left);
  assertByteArray(right);
  if (left.length !== right.length) {
    return false;
  }
  let diff = 0;
  for (let i = 0; i < left.length; i += 1) {
    diff |= left[i] ^ right[i];
  }
  return diff === 0;
}

async function computeHmac(keyBytes, dataBytes, hmacLength = ENVELOPE_DEFAULT_HMAC_LENGTH) {
  assertByteArray(keyBytes);
  assertByteArray(dataBytes);
  validateHmacLength(hmacLength);

  if (hmacLength === 0) {
    return new Uint8Array(0);
  }

  const subtle = getSubtleCrypto();
  const cryptoKey = await subtle.importKey(
    "raw",
    keyBytes,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  const full = new Uint8Array(await subtle.sign("HMAC", cryptoKey, dataBytes));
  return full.slice(0, hmacLength);
}

async function verifyHmac(keyBytes, dataBytes, hmacBytes) {
  assertByteArray(keyBytes);
  assertByteArray(dataBytes);
  assertByteArray(hmacBytes);
  validateHmacLength(hmacBytes.length);

  if (hmacBytes.length === 0) {
    return true;
  }

  const expected = await computeHmac(keyBytes, dataBytes, hmacBytes.length);
  return fixedTimeEquals(expected, hmacBytes);
}

async function encodeEnvelope(input) {
  const normalized = normalizeEnvelopeInput(input);
  const totalLength = ENVELOPE_HEADER_SIZE + normalized.payloadLength + normalized.hmacLength;
  const bytes = new Uint8Array(totalLength);

  writeUInt16BE(bytes, ENVELOPE_OFFSETS.magic, ENVELOPE_MAGIC);
  bytes[ENVELOPE_OFFSETS.version] = ENVELOPE_VERSION;
  bytes[ENVELOPE_OFFSETS.flags] = normalized.flags;
  bytes[ENVELOPE_OFFSETS.priority] = normalized.priority;
  bytes[ENVELOPE_OFFSETS.ttl] = normalized.ttl;
  bytes[ENVELOPE_OFFSETS.keyVersion] = normalized.keyVersion;
  writeUInt64BE(bytes, ENVELOPE_OFFSETS.messageId, normalized.messageId);
  writeUInt32BE(bytes, ENVELOPE_OFFSETS.timestamp, normalized.timestampSeconds);
  bytes[ENVELOPE_OFFSETS.payloadType] = normalized.payloadType;
  writeUInt16BE(bytes, ENVELOPE_OFFSETS.payloadLength, normalized.payloadLength);
  bytes.set(normalized.payloadBytes, ENVELOPE_HEADER_SIZE);

  const signedDataBytes = bytes.slice(0, ENVELOPE_HEADER_SIZE + normalized.payloadLength);
  let hmacBytes = new Uint8Array(0);
  if (normalized.hmacLength > 0) {
    hmacBytes = await computeHmac(normalized.hmacKeyBytes, signedDataBytes, normalized.hmacLength);
    bytes.set(hmacBytes, ENVELOPE_HEADER_SIZE + normalized.payloadLength);
  }

  return {
    ...normalized,
    bytes,
    hex: bytesToHex(bytes),
    base64: bytesToBase64(bytes),
    headerHex: bytesToHex(bytes.slice(0, ENVELOPE_HEADER_SIZE)),
    payloadHex: bytesToHex(normalized.payloadBytes),
    hmacHex: bytesToHex(hmacBytes),
    messageIdHex: toPaddedHexBigInt(normalized.messageId, 16),
    signedDataBytes
  };
}

async function tryEncodeEnvelope(input) {
  try {
    return {
      ok: true,
      value: await encodeEnvelope(input),
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

function decodeEnvelopeFromBytes(bytes, options = {}) {
  assertByteArray(bytes);

  if (bytes.length < ENVELOPE_HEADER_SIZE) {
    throw createDecoderError(
      "ENVELOPE_TRUNCATED_HEADER",
      `Input is ${bytes.length} bytes.`,
      `Envelope header requires at least ${ENVELOPE_HEADER_SIZE} bytes.`,
      "Provide a full envelope with header, payload, and optional HMAC."
    );
  }

  const magic = readUInt16BE(bytes, ENVELOPE_OFFSETS.magic);
  if (magic !== ENVELOPE_MAGIC) {
    throw createDecoderError(
      "ENVELOPE_MAGIC_MISMATCH",
      `Found magic 0x${toPaddedHex(magic, 4)}.`,
      `Expected magic 0x${toPaddedHex(ENVELOPE_MAGIC, 4)}.`,
      "Ensure the payload starts with a valid ECP envelope."
    );
  }

  const version = bytes[ENVELOPE_OFFSETS.version];
  if (version !== ENVELOPE_VERSION) {
    throw createDecoderError(
      "ENVELOPE_UNSUPPORTED_VERSION",
      `Found version 0x${toPaddedHex(version, 2)}.`,
      `Expected version 0x${toPaddedHex(ENVELOPE_VERSION, 2)}.`,
      "Use a payload encoded with envelope version 0x01."
    );
  }

  const flags = bytes[ENVELOPE_OFFSETS.flags];
  const priority = bytes[ENVELOPE_OFFSETS.priority];
  const ttl = bytes[ENVELOPE_OFFSETS.ttl];
  const keyVersion = bytes[ENVELOPE_OFFSETS.keyVersion];
  const messageId = readUInt64BE(bytes, ENVELOPE_OFFSETS.messageId);
  const timestampSeconds = readUInt32BE(bytes, ENVELOPE_OFFSETS.timestamp);
  const payloadType = bytes[ENVELOPE_OFFSETS.payloadType];
  const payloadLength = readUInt16BE(bytes, ENVELOPE_OFFSETS.payloadLength);
  const hmacLength = bytes.length - ENVELOPE_HEADER_SIZE - payloadLength;

  if (hmacLength < 0) {
    throw createDecoderError(
      "ENVELOPE_LENGTH_MISMATCH",
      "PayloadLength is larger than available bytes.",
      `PayloadLength=${payloadLength}, totalLength=${bytes.length}.`,
      "Check payload truncation or incorrect PayloadLength field."
    );
  }

  validateHmacLength(hmacLength);
  const expectedHmacLength = options?.expectedHmacLength;
  if (expectedHmacLength !== undefined && expectedHmacLength !== null) {
    validateHmacLength(expectedHmacLength);
    if (expectedHmacLength !== hmacLength) {
      throw createDecoderError(
        "ENVELOPE_HMAC_LENGTH_MISMATCH",
        `Derived HMAC length is ${hmacLength}.`,
        `Expected HMAC length is ${expectedHmacLength}.`,
        "Use the correct envelope HMAC length setting."
      );
    }
  }

  const payloadStart = ENVELOPE_HEADER_SIZE;
  const payloadEnd = payloadStart + payloadLength;
  const payloadBytes = bytes.slice(payloadStart, payloadEnd);
  const hmacBytes = bytes.slice(payloadEnd, payloadEnd + hmacLength);
  const signedDataBytes = bytes.slice(0, payloadEnd);

  let nestedUet = null;
  if (payloadType === 0 && payloadBytes.length === UET_BYTE_LENGTH) {
    try {
      nestedUet = decodeUetFromBytes(payloadBytes);
    } catch (_) {
      nestedUet = null;
    }
  }

  return {
    magic,
    magicHex: toPaddedHex(magic, 4),
    version,
    flags,
    flagLabels: decodeEnvelopeFlags(flags),
    priority,
    priorityLabel: PRIORITY_LABELS[priority] ?? `Priority ${priority}`,
    ttl,
    keyVersion,
    messageId,
    messageIdHex: toPaddedHexBigInt(messageId, 16),
    timestampSeconds,
    timestampIso: new Date(timestampSeconds * 1000).toISOString(),
    payloadType,
    payloadTypeLabel: PAYLOAD_TYPE_LABELS[payloadType] ?? `Type ${payloadType}`,
    payloadLength,
    hmacLength,
    signed: hmacLength > 0,
    isValid: hmacLength === 0,
    payloadBytes,
    payloadHex: bytesToHex(payloadBytes),
    payloadBase64: bytesToBase64(payloadBytes),
    hmacBytes,
    hmacHex: bytesToHex(hmacBytes),
    headerHex: bytesToHex(bytes.slice(0, ENVELOPE_HEADER_SIZE)),
    valueHex: bytesToHex(bytes),
    valueBase64: bytesToBase64(bytes),
    bytes,
    signedDataBytes,
    nestedUet
  };
}

function decodeEnvelope(rawInput, options = {}) {
  const parsed = parseInputToBytes(rawInput);
  const envelope = decodeEnvelopeFromBytes(parsed.bytes, options);
  return {
    ...envelope,
    inputFormat: parsed.format,
    normalizedInput: parsed.normalizedInput
  };
}

function tryDecodeEnvelope(rawInput, options = {}) {
  try {
    return {
      ok: true,
      value: decodeEnvelope(rawInput, options),
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

async function decodeEnvelopeWithHmac(rawInput, keyHex, expectedHmacLength) {
  const envelope = decodeEnvelope(rawInput, { expectedHmacLength });
  if (envelope.hmacLength === 0) {
    return {
      ...envelope,
      isValid: true
    };
  }

  const keyBytes = hexToBytes(keyHex);
  const isValid = await verifyHmac(keyBytes, envelope.signedDataBytes, envelope.hmacBytes);
  return {
    ...envelope,
    isValid
  };
}

async function tryDecodeEnvelopeWithHmac(rawInput, keyHex, expectedHmacLength) {
  try {
    return {
      ok: true,
      value: await decodeEnvelopeWithHmac(rawInput, keyHex, expectedHmacLength),
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

async function verifyEnvelopeHmacHex(rawEnvelopeInput, keyHex, expectedHmacLength) {
  try {
    const decoded = await decodeEnvelopeWithHmac(rawEnvelopeInput, keyHex, expectedHmacLength);
    return {
      ok: true,
      isValid: decoded.isValid,
      hmacLength: decoded.hmacLength,
      valueHex: decoded.valueHex,
      error: null
    };
  } catch (error) {
    return {
      ok: false,
      isValid: false,
      hmacLength: null,
      valueHex: "",
      error: serializeError(error)
    };
  }
}

function decodeMessageFromBytes(bytes) {
  assertByteArray(bytes);

  if (bytes.length === UET_BYTE_LENGTH) {
    return {
      kind: "uet",
      value: decodeUetFromBytes(bytes)
    };
  }

  if (hasEnvelopeMagic(bytes) && bytes.length < ENVELOPE_HEADER_SIZE) {
    throw createDecoderError(
      "ENVELOPE_TRUNCATED_HEADER",
      `Input is ${bytes.length} bytes.`,
      `Envelope header requires at least ${ENVELOPE_HEADER_SIZE} bytes.`,
      "Provide a complete envelope payload."
    );
  }

  if (bytes.length >= ENVELOPE_HEADER_SIZE) {
    if (!hasEnvelopeMagic(bytes)) {
      throw createDecoderError(
        "ENVELOPE_MAGIC_MISMATCH",
        `Found first bytes 0x${toPaddedHex(bytes[0], 2)}${toPaddedHex(bytes[1], 2)}.`,
        "Envelope payloads must start with magic 0xEC50.",
        "Provide a valid envelope or an 8-byte UET."
      );
    }

    return {
      kind: "envelope",
      value: decodeEnvelopeFromBytes(bytes)
    };
  }

  throw createDecoderError(
    "MESSAGE_LENGTH_UNSUPPORTED",
    `Input has ${bytes.length} bytes.`,
    "Expected either 8-byte UET or envelope (>=22 bytes with magic 0xEC50).",
    "Use a complete UET or envelope payload."
  );
}

function decodeMessage(rawInput) {
  const parsed = parseInputToBytes(rawInput);
  const decoded = decodeMessageFromBytes(parsed.bytes);
  return {
    kind: decoded.kind,
    value: {
      ...decoded.value,
      inputFormat: parsed.format,
      normalizedInput: parsed.normalizedInput
    }
  };
}

function tryDecodeMessage(rawInput) {
  try {
    return {
      ok: true,
      value: decodeMessage(rawInput),
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

function verifySingleCoreVector(vector) {
  const result = {
    id: vector.id,
    group: "uet",
    decodePassed: false,
    roundtripPassed: false,
    base64Passed: false,
    passed: false,
    expectedHex: vector.expectedHex,
    expectedBase64: vector.expectedBase64,
    roundtripHex: "",
    roundtripBase64: "",
    error: null
  };

  try {
    const decoded = decodeUetFromHex(vector.expectedHex);
    const decodedFields = pickComparableFields(decoded);
    result.decodePassed = compareUetFields(decodedFields, vector.fields);
    if (!result.decodePassed) {
      throw createDecoderError(
        "VECTOR_DECODE_MISMATCH",
        `Decoded fields mismatch for vector '${vector.id}'.`,
        "Extracted values do not match expected deterministic fields.",
        "Review shifts/masks and Big-Endian extraction."
      );
    }

    const encoded = encodeUet(decodedFields);
    result.roundtripHex = encoded.hex;
    result.roundtripBase64 = encoded.base64;
    result.roundtripPassed = encoded.hex === vector.expectedHex;
    result.base64Passed = encoded.base64 === vector.expectedBase64;
    result.passed = result.decodePassed && result.roundtripPassed && result.base64Passed;
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

async function verifySingleEnvelopeVector(vector) {
  const result = {
    id: vector.id,
    group: "envelope",
    encodePassed: false,
    decodePassed: false,
    base64Passed: false,
    hmacPassed: false,
    passed: false,
    expectedHex: vector.expectedHex,
    expectedBase64: vector.expectedBase64,
    actualHex: "",
    actualBase64: "",
    error: null
  };

  try {
    const encoded = await encodeEnvelope(vector.input);
    result.actualHex = encoded.hex;
    result.actualBase64 = encoded.base64;
    result.encodePassed = encoded.hex === vector.expectedHex;
    result.base64Passed = encoded.base64 === vector.expectedBase64;

    const decoded = decodeEnvelopeFromBytes(hexToBytes(vector.expectedHex));
    const expectedMessageId = parseMessageId(vector.input.messageId);
    const expectedPayloadHex = bytesToHex(hexToBytes(vector.input.payloadHex));
    result.decodePassed =
      decoded.flags === vector.input.flags &&
      decoded.priority === vector.input.priority &&
      decoded.ttl === vector.input.ttl &&
      decoded.keyVersion === vector.input.keyVersion &&
      decoded.messageId === expectedMessageId &&
      decoded.timestampSeconds === vector.input.timestampSeconds &&
      decoded.payloadType === vector.input.payloadType &&
      decoded.payloadHex === expectedPayloadHex &&
      decoded.hmacLength === vector.input.hmacLength;

    if (vector.input.hmacLength === 0) {
      result.hmacPassed = true;
    } else {
      const keyBytes = hexToBytes(vector.input.hmacKeyHex);
      result.hmacPassed = await verifyHmac(keyBytes, decoded.signedDataBytes, decoded.hmacBytes);
    }

    result.passed = result.encodePassed && result.decodePassed && result.base64Passed && result.hmacPassed;
  } catch (error) {
    result.error = serializeError(error);
    result.passed = false;
  }

  return result;
}

async function verifySingleHmacVector(vector) {
  const result = {
    id: vector.id,
    group: "hmac",
    passed: false,
    detail: "",
    error: null
  };

  try {
    if (vector.kind === "hmac-compute") {
      const keyBytes = hexToBytes(vector.input.keyHex);
      const dataBytes = hexToBytes(vector.input.dataHex);
      const tag = await computeHmac(keyBytes, dataBytes, vector.input.hmacLength);
      const tagHex = bytesToHex(tag);
      result.passed = tagHex === vector.expectedTagHex;
      result.detail = `tag=${tagHex}`;
      return result;
    }

    if (vector.kind === "hmac-verify") {
      const keyBytes = hexToBytes(vector.input.keyHex);
      const dataBytes = hexToBytes(vector.input.dataHex);
      const tagBytes = hexToBytes(vector.input.tagHex);
      const actualVerify = await verifyHmac(keyBytes, dataBytes, tagBytes);
      result.passed = actualVerify === vector.expectedVerify;
      result.detail = `verify=${actualVerify}`;
      return result;
    }

    throw createDecoderError(
      "HMAC_VECTOR_KIND_UNSUPPORTED",
      `Unsupported HMAC vector kind '${vector.kind}'.`,
      "Only hmac-compute and hmac-verify are currently supported.",
      "Update vector parser or vector definitions."
    );
  } catch (error) {
    result.error = serializeError(error);
    result.passed = false;
    return result;
  }
}

async function verifySingleNegativeVector(vector) {
  const result = {
    id: vector.id,
    group: "negative",
    passed: false,
    detail: "",
    error: null
  };

  try {
    if (vector.kind === "try-decode") {
      const actual = tryDecodeMessage(vector.inputHex).ok;
      const expected = Boolean(vector.expected?.tryDecode);
      result.passed = actual === expected;
      result.detail = `tryDecode=${actual}`;
      return result;
    }

    if (vector.kind === "try-decode-envelope") {
      const response = await tryDecodeEnvelopeWithHmac(
        vector.inputHex,
        vector.input.hmacKeyHex,
        vector.input.hmacLength
      );
      const actualTryDecode = response.ok;
      const expectedTryDecode = Boolean(vector.expected?.tryDecodeEnvelope);
      const actualIsValid = response.ok ? response.value.isValid : false;
      const expectedIsValid = Boolean(vector.expected?.isValid);
      result.passed = actualTryDecode === expectedTryDecode && actualIsValid === expectedIsValid;
      result.detail = `tryDecodeEnvelope=${actualTryDecode}, isValid=${actualIsValid}`;
      return result;
    }

    throw createDecoderError(
      "NEGATIVE_VECTOR_KIND_UNSUPPORTED",
      `Unsupported negative vector kind '${vector.kind}'.`,
      "Only try-decode and try-decode-envelope are currently supported.",
      "Update vector parser or vector definitions."
    );
  } catch (error) {
    result.error = serializeError(error);
    result.passed = false;
    return result;
  }
}

function summarizeGroup(name, results) {
  const passed = results.filter((item) => item.passed).length;
  return {
    name,
    total: results.length,
    passed,
    failed: results.length - passed,
    allPassed: passed === results.length
  };
}

async function verifyFullVectors() {
  const uetResults = CORE_TEST_VECTORS.map(verifySingleCoreVector);

  const envelopeResults = [];
  for (let i = 0; i < ENVELOPE_TEST_VECTORS.length; i += 1) {
    envelopeResults.push(await verifySingleEnvelopeVector(ENVELOPE_TEST_VECTORS[i]));
  }

  const hmacResults = [];
  for (let i = 0; i < HMAC_TEST_VECTORS.length; i += 1) {
    hmacResults.push(await verifySingleHmacVector(HMAC_TEST_VECTORS[i]));
  }

  const negativeResults = [];
  for (let i = 0; i < NEGATIVE_TEST_VECTORS.length; i += 1) {
    negativeResults.push(await verifySingleNegativeVector(NEGATIVE_TEST_VECTORS[i]));
  }

  const results = [...uetResults, ...envelopeResults, ...hmacResults, ...negativeResults];
  const passed = results.filter((entry) => entry.passed).length;
  const total = results.length;

  return {
    suiteId: "full-phase-b",
    total,
    passed,
    failed: total - passed,
    allPassed: passed === total,
    groups: [
      summarizeGroup("UET", uetResults),
      summarizeGroup("Envelope", envelopeResults),
      summarizeGroup("HMAC", hmacResults),
      summarizeGroup("Negative", negativeResults)
    ],
    results
  };
}

function formatVectorStatusLine(result) {
  const icon = result.passed ? "✅" : "❌";
  if (result.group === "uet") {
    const decodeText = result.decodePassed ? "decode ✓" : "decode ✗";
    const roundtripText = result.roundtripPassed ? "roundtrip ✓" : "roundtrip ✗";
    return `${icon} ${result.id}    ${decodeText}  ${roundtripText}`;
  }

  const detail = result.detail ? ` ${result.detail}` : "";
  return `${icon} ${result.id}${detail}`;
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
  ENVELOPE_MAGIC,
  ENVELOPE_VERSION,
  ENVELOPE_HEADER_SIZE,
  ENVELOPE_DEFAULT_HMAC_LENGTH,
  ENVELOPE_MIN_HMAC_LENGTH,
  ENVELOPE_MAX_HMAC_LENGTH,
  EMERGENCY_TYPE_LABELS,
  EMERGENCY_TYPE_ENUM_NAMES,
  PRIORITY_LABELS,
  PRIORITY_ENUM_NAMES,
  ACTION_FLAG_DEFINITIONS,
  PAYLOAD_TYPE_LABELS,
  ENVELOPE_FLAG_DEFINITIONS,
  CORE_TEST_VECTORS,
  ENVELOPE_TEST_VECTORS,
  HMAC_TEST_VECTORS,
  NEGATIVE_TEST_VECTORS,
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
  computeHmac,
  verifyHmac,
  encodeEnvelope,
  tryEncodeEnvelope,
  decodeEnvelopeFromBytes,
  decodeEnvelope,
  tryDecodeEnvelope,
  decodeEnvelopeWithHmac,
  tryDecodeEnvelopeWithHmac,
  verifyEnvelopeHmacHex,
  decodeMessageFromBytes,
  decodeMessage,
  tryDecodeMessage,
  verifySingleCoreVector,
  verifyCoreVectors,
  verifyFullVectors,
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
  ENVELOPE_MAGIC,
  ENVELOPE_VERSION,
  ENVELOPE_HEADER_SIZE,
  ENVELOPE_DEFAULT_HMAC_LENGTH,
  ENVELOPE_MIN_HMAC_LENGTH,
  ENVELOPE_MAX_HMAC_LENGTH,
  EMERGENCY_TYPE_LABELS,
  EMERGENCY_TYPE_ENUM_NAMES,
  PRIORITY_LABELS,
  PRIORITY_ENUM_NAMES,
  ACTION_FLAG_DEFINITIONS,
  PAYLOAD_TYPE_LABELS,
  ENVELOPE_FLAG_DEFINITIONS,
  CORE_TEST_VECTORS,
  ENVELOPE_TEST_VECTORS,
  HMAC_TEST_VECTORS,
  NEGATIVE_TEST_VECTORS,
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
  computeHmac,
  verifyHmac,
  encodeEnvelope,
  tryEncodeEnvelope,
  decodeEnvelopeFromBytes,
  decodeEnvelope,
  tryDecodeEnvelope,
  decodeEnvelopeWithHmac,
  tryDecodeEnvelopeWithHmac,
  verifyEnvelopeHmacHex,
  decodeMessageFromBytes,
  decodeMessage,
  tryDecodeMessage,
  verifySingleCoreVector,
  verifyCoreVectors,
  verifyFullVectors,
  formatVectorStatusLine,
  tryDecodeUet,
  tryEncodeUet,
  EcpStudioDecoder
};
