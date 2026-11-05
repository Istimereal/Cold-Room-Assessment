import { request } from "./http";

export type InvestigationConfidence = "Low" | "Medium" | "High";

export type InvestigationLimitationCode =
  | "IncompleteCoverage"
  | "AmbiguousData"
  | "MissingData"
  | "InsufficientEvidence";

export type InvestigationObservation = {
  id: string;
  title: string;
  description: string;
  equipmentIds: string[];
  measurementKind: "Temperature" | "RelativeHumidity" | null;
  from: string;
  to: string;
  evidenceReferenceIds: string[];
  confidence: InvestigationConfidence;
};

export type InvestigationHypothesis = {
  id: string;
  statement: string;
  supportingObservationIds: string[];
  evidenceFor: string[];
  uncertainties: string[];
  confidence: InvestigationConfidence;
};

export type InvestigationLimitation = {
  code: InvestigationLimitationCode;
  equipmentId: string | null;
  description: string;
};

export type FacilityInvestigation = {
  facilityId: string;
  from: string;
  to: string;
  overallSummary: string;
  observations: InvestigationObservation[];
  hypotheses: InvestigationHypothesis[];
  limitations: InvestigationLimitation[];
};

const fallback = "The investigation could not be completed.";

export function runInvestigation(
  facilityId: string,
  from: string,
  to: string,
  signal?: AbortSignal,
): Promise<FacilityInvestigation> {
  const params = new URLSearchParams({ from, to });
  return request<FacilityInvestigation>(
    `/api/facilities/${encodeURIComponent(facilityId)}/investigations?${params}`,
    { method: "POST", signal },
    fallback,
  );
}
