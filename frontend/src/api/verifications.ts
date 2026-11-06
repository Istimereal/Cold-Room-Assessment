import type { FacilityInvestigation } from "./investigations";
import { request } from "./http";

export type VerificationVerdict =
  | "Supported"
  | "PartiallySupported"
  | "NotSupported"
  | "InsufficientEvidence";

export type ObservationVerification = {
  observationId: string;
  verdict: VerificationVerdict;
  explanation: string;
  evidenceIds: string[];
  missingEvidenceIds: string[];
  conflictingEvidenceIds: string[];
};

export type HypothesisVerification = {
  hypothesisId: string;
  verdict: VerificationVerdict;
  explanation: string;
  supportingEvidenceIds: string[];
  contradictingEvidenceIds: string[];
  uncertainty: string;
};

export type FacilityVerification = {
  facilityId: string;
  from: string;
  to: string;
  overallVerdict: VerificationVerdict;
  observationVerifications: ObservationVerification[];
  hypothesisVerifications: HypothesisVerification[];
  limitations: string[];
};

const fallback = "The verification could not be completed.";

export function runVerification(
  facilityId: string,
  from: string,
  to: string,
  investigation: FacilityInvestigation,
  includeContext = false,
  signal?: AbortSignal,
): Promise<FacilityVerification> {
  const params = new URLSearchParams({ from, to });
  if (includeContext) {
    params.set("includeContext", "true");
  }

  return request<FacilityVerification>(
    `/api/facilities/${encodeURIComponent(facilityId)}/verifications?${params}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(investigation),
      signal,
    },
    fallback,
  );
}
