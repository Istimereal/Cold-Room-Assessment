import type { FacilityAnalysis } from "../api/analysis";
import type { InvestigationHypothesis } from "../api/investigations";
import type { HypothesisVerification } from "../api/verifications";
import { CitedEvidence } from "./CitedEvidence";
import { StatusBadge, verdictLabel, verdictTone } from "./StatusBadge";

export function HypothesisVerificationCard({
  analysis,
  verification,
  hypothesis,
}: {
  analysis: FacilityAnalysis;
  verification: HypothesisVerification;
  hypothesis: InvestigationHypothesis | undefined;
}) {
  return (
    <article className="card">
      <header className="card-heading">
        <h3>{hypothesis?.statement ?? verification.hypothesisId}</h3>
        <StatusBadge tone={verdictTone(verification.verdict)}>{verdictLabel(verification.verdict)}</StatusBadge>
      </header>
      <p>{verification.explanation}</p>
      <CitedEvidence title="Supporting evidence" ids={verification.supportingEvidenceIds} analysis={analysis} />
      <CitedEvidence title="Contradicting evidence" ids={verification.contradictingEvidenceIds} analysis={analysis} />
      <h4>Uncertainty</h4>
      <p>{verification.uncertainty}</p>
    </article>
  );
}
