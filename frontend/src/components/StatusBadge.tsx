export type BadgeTone =
  | "complete"
  | "incomplete"
  | "neutral"
  | "low"
  | "medium"
  | "high"
  | "supported"
  | "partial"
  | "unsupported"
  | "insufficient";

export function StatusBadge({ tone, children }: { tone: BadgeTone; children: string }) {
  return <span className={`badge badge-${tone}`}>{children}</span>;
}

export function confidenceTone(confidence: string): BadgeTone {
  if (confidence === "Low") {
    return "low";
  }

  if (confidence === "High") {
    return "high";
  }

  if (confidence === "Medium") {
    return "medium";
  }

  return "neutral";
}

export function verdictTone(verdict: string): BadgeTone {
  if (verdict === "Supported") {
    return "supported";
  }

  if (verdict === "PartiallySupported") {
    return "partial";
  }

  if (verdict === "NotSupported") {
    return "unsupported";
  }

  if (verdict === "InsufficientEvidence") {
    return "insufficient";
  }

  return "neutral";
}

export function verdictLabel(verdict: string): string {
  if (verdict === "Supported") {
    return "Supported";
  }

  if (verdict === "PartiallySupported") {
    return "Partially supported";
  }

  if (verdict === "NotSupported") {
    return "Not supported";
  }

  if (verdict === "InsufficientEvidence") {
    return "Insufficient evidence";
  }

  return "Unrecognized verdict";
}
