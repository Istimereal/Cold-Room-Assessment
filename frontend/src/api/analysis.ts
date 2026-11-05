import { request } from "./http";

export type SeriesEvent = {
  timestamp: string;
  value: number;
  kind: "Peak" | "Dip";
};

export type KindAnalysis = {
  count: number;
  min: number | null;
  minAt: string | null;
  max: number | null;
  maxAt: string | null;
  mean: number | null;
  median: number | null;
  populationStdDev: number | null;
  first: number | null;
  firstAt: string | null;
  last: number | null;
  lastAt: string | null;
  change: number | null;
  durationSeconds: number | null;
  events: SeriesEvent[];
};

export type EquipmentAnalysis = {
  equipmentId: string;
  equipmentName: string;
  equipmentTypeId: string;
  equipmentTypeName: string;
  placement: string;
  sourceRowCount: number;
  measurementCount: number;
  coveragePercent: number;
  hasCompleteCoverage: boolean;
  ambiguousSampleCount: number;
  missingExpectedSampleCount: number;
  temperature: KindAnalysis;
  relativeHumidity: KindAnalysis;
};

export type FacilityAnalysis = {
  facilityId: string;
  facilityName: string;
  from: string;
  to: string;
  equipmentCount: number;
  equipmentWithTemperatureCount: number;
  equipmentWithRelativeHumidityCount: number;
  validTemperatureCount: number;
  validRelativeHumidityCount: number;
  equipment: EquipmentAnalysis[];
};

const fallback = "The analysis service could not be reached.";

export function getAnalysis(
  facilityId: string,
  from: string,
  to: string,
  signal?: AbortSignal,
): Promise<FacilityAnalysis> {
  const params = new URLSearchParams({ from, to });
  return request<FacilityAnalysis>(
    `/api/facilities/${encodeURIComponent(facilityId)}/analysis?${params}`,
    { signal },
    fallback,
  );
}
