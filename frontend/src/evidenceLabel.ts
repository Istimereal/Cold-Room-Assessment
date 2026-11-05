import type { EquipmentAnalysis, FacilityAnalysis, KindAnalysis } from "./api/analysis";
import { formatMeasurement } from "./format";

const evidenceIdPattern =
  /^eq:([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}):(temperature|relative-humidity):([a-z]+(?:-[a-z]+)*(?::[1-9][0-9]*)?)$/i;

const factTitles: Record<string, string> = {
  minimum: "Minimum",
  maximum: "Maximum",
  mean: "Mean",
  median: "Median",
  "population-standard-deviation": "Population standard deviation",
  first: "First",
  last: "Last",
  change: "Change",
};

export type EvidenceLabel = {
  text: string;
  recognized: boolean;
};

export function evidenceLabel(analysis: FacilityAnalysis, evidenceId: string): EvidenceLabel {
  const unrecognized: EvidenceLabel = { text: "Unrecognized evidence", recognized: false };
  const match = evidenceIdPattern.exec(evidenceId);
  if (!match) {
    return unrecognized;
  }

  const [, equipmentId, kindSlug, fact] = match;
  const equipment = analysis.equipment.find(
    (item) => item.equipmentId.toLowerCase() === equipmentId.toLowerCase(),
  );
  if (!equipment) {
    return unrecognized;
  }

  const temperature = kindSlug.toLowerCase() === "temperature";
  const kind = temperature ? equipment.temperature : equipment.relativeHumidity;
  const kindName = temperature ? "Temperature" : "Relative humidity";
  const unit = temperature ? "°C" : "%";
  const factText = describeFact(equipment, kind, fact.toLowerCase(), unit);
  if (factText === null) {
    return unrecognized;
  }

  return {
    text: `${equipment.equipmentName} · ${kindName} · ${factText}`,
    recognized: true,
  };
}

function describeFact(
  equipment: EquipmentAnalysis,
  kind: KindAnalysis,
  fact: string,
  unit: string,
): string | null {
  if (fact === "coverage") {
    return `Coverage · ${formatMeasurement(equipment.coveragePercent)}%`;
  }

  if (fact === "missing-count") {
    return `Missing samples · ${equipment.missingExpectedSampleCount}`;
  }

  if (fact === "ambiguous-count") {
    return `Ambiguous samples · ${equipment.ambiguousSampleCount}`;
  }

  if (fact === "count") {
    return `Count · ${kind.count}`;
  }

  if (fact === "duration-seconds") {
    return kind.durationSeconds === null
      ? "Duration · no value"
      : `Duration · ${formatMeasurement(kind.durationSeconds)} s`;
  }

  const title = factTitles[fact];
  if (title) {
    const value = kindValue(kind, fact);
    return value === null ? `${title} · no value` : `${title} · ${formatMeasurement(value)} ${unit}`;
  }

  return describeEvent(kind, fact, unit);
}

function kindValue(kind: KindAnalysis, fact: string): number | null {
  switch (fact) {
    case "minimum":
      return kind.min;
    case "maximum":
      return kind.max;
    case "mean":
      return kind.mean;
    case "median":
      return kind.median;
    case "population-standard-deviation":
      return kind.populationStdDev;
    case "first":
      return kind.first;
    case "last":
      return kind.last;
    case "change":
      return kind.change;
    default:
      return null;
  }
}

function describeEvent(kind: KindAnalysis, fact: string, unit: string): string | null {
  const match = /^(peak|dip):([1-9][0-9]*)$/.exec(fact);
  if (!match) {
    return null;
  }

  const wanted = match[1] === "peak" ? "Peak" : "Dip";
  const index = Number(match[2]);
  const events = kind.events.filter((item) => item.kind === wanted);
  const event = events[index - 1];
  if (!event) {
    return null;
  }

  const title = wanted === "Peak" ? "Peak" : "Dip";
  return `${title} ${index} · ${formatMeasurement(event.value)} ${unit}`;
}
