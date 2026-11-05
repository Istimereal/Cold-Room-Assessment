import type { FacilityAnalysis } from "../api/analysis";
import type { FacilityInvestigation } from "../api/investigations";
import type { FacilityVerification } from "../api/verifications";
import { HypothesisVerificationCard } from "./HypothesisVerificationCard";
import { ObservationVerificationCard } from "./ObservationVerificationCard";
import { StatusBadge, verdictLabel, verdictTone } from "./StatusBadge";

type VerificationPanelProps = {
  analysis: FacilityAnalysis | null;
  investigation: FacilityInvestigation | null;
  canVerify: boolean;
  loading: boolean;
  error: string | null;
  result: FacilityVerification | null;
  onRun: () => void;
};

export function VerificationPanel({
  analysis,
  investigation,
  canVerify,
  loading,
  error,
  result,
  onRun,
}: VerificationPanelProps) {
  return (
    <div className="verification">
      <div className="investigation-bar">
        <button type="button" onClick={onRun} disabled={!canVerify}>
          {loading ? "Verifying investigation…" : "Verify investigation"}
        </button>
        {!loading && investigation === null ? (
          <p className="quiet">Run an investigation first. Verification does not start on its own.</p>
        ) : null}
        {!loading && investigation !== null && !canVerify ? (
          <p className="quiet">The investigation no longer matches the analysis. Run it again before verification.</p>
        ) : null}
      </div>
      {loading ? <p role="status">Verifying investigation…</p> : null}
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      {!loading && !error && result === null ? (
        <p className="empty">The verifier stays idle until you run it.</p>
      ) : null}
      {result && analysis && investigation ? (
        <div className="verification-result">
          <header className="card-heading">
            <h3>Overall verdict</h3>
            <StatusBadge tone={verdictTone(result.overallVerdict)}>{verdictLabel(result.overallVerdict)}</StatusBadge>
          </header>
          <section aria-labelledby="verified-observations-heading">
            <h3 id="verified-observations-heading">Observation checks</h3>
            {result.observationVerifications.length === 0 ? (
              <p className="empty">No observation checks were returned.</p>
            ) : (
              <div className="stack">
                {result.observationVerifications.map((item) => (
                  <ObservationVerificationCard
                    key={item.observationId}
                    analysis={analysis}
                    verification={item}
                    observation={investigation.observations.find((observation) => observation.id === item.observationId)}
                  />
                ))}
              </div>
            )}
          </section>
          <section aria-labelledby="verified-hypotheses-heading">
            <h3 id="verified-hypotheses-heading">Hypothesis checks</h3>
            {result.hypothesisVerifications.length === 0 ? (
              <p className="empty">No hypothesis checks were returned.</p>
            ) : (
              <div className="stack">
                {result.hypothesisVerifications.map((item) => (
                  <HypothesisVerificationCard
                    key={item.hypothesisId}
                    analysis={analysis}
                    verification={item}
                    hypothesis={investigation.hypotheses.find((hypothesis) => hypothesis.id === item.hypothesisId)}
                  />
                ))}
              </div>
            )}
          </section>
          <section className="limitations" aria-labelledby="verification-limitations-heading">
            <h3 id="verification-limitations-heading">Verification limitations</h3>
            {result.limitations.length === 0 ? (
              <p className="empty">No verification limitations were returned.</p>
            ) : (
              <ul>
                {result.limitations.map((item, index) => (
                  <li key={`${item}-${index}`}>{item}</li>
                ))}
              </ul>
            )}
          </section>
        </div>
      ) : null}
    </div>
  );
}
