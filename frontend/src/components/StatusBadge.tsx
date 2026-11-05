export type BadgeTone = "complete" | "incomplete" | "neutral" | "low" | "medium" | "high";

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
