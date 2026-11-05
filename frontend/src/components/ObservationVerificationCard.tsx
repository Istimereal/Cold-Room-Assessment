import type { FacilityAnalysis } from "../api/analysis";
import type { InvestigationObservation } from "../api/investigations";
import type { ObservationVerification } from "../api/verifications";
import { CitedEvidence } from "./CitedEvidence";
import { StatusBadge, verdictLabel, verdictTone } from "./StatusBadge";

export function ObservationVerificationCard({
  analysis,
  verification,
  observation,
}: {
  analysis: FacilityAnalysis;
  verification: ObservationVerification;
  observation: InvestigationObservation | undefined;
}) {
  return (
    <article className="card">
      <header className="card-heading">
        <h3>{observation?.title ?? verification.observationId}</h3>
        <StatusBadge tone={verdictTone(verification.verdict)}>{verdictLabel(verification.verdict)}</StatusBadge>
      </header>
      <p>{verification.explanation}</p>
      <CitedEvidence title="Supporting evidence" ids={verification.evidenceIds} analysis={analysis} />
      <CitedEvidence title="Conflicting evidence" ids={verification.conflictingEvidenceIds} analysis={analysis} />
      {verification.missingEvidenceIds.length > 0 ? (
        <CitedEvidence title="Missing evidence" ids={verification.missingEvidenceIds} analysis={analysis} />
      ) : null}
    </article>
  );
}
