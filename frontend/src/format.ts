export function formatMeasurement(value: number): string {
  if (!Number.isFinite(value)) {
    return "no value";
  }

  const text = value.toFixed(6).replace(/\.?0+$/, "");
  return text === "-0" ? "0" : text;
}

export function formatTimestamp(value: string): string {
  return value.replace("T", " ");
}

export function normalizeTimestamp(value: string): string {
  const trimmed = value.trim();
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(trimmed)) {
    return `${trimmed}:00`;
  }

  return trimmed;
}
