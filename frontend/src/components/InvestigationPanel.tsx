import type { FacilityAnalysis } from "../api/analysis";
import type { FacilityInvestigation } from "../api/investigations";
import { HypothesisCard } from "./HypothesisCard";
import { LimitationsPanel } from "./LimitationsPanel";
import { ObservationCard } from "./ObservationCard";

type InvestigationPanelProps = {
  analysis: FacilityAnalysis | null;
  requestMatchesAnalysis: boolean;
  loading: boolean;
  error: string | null;
  result: FacilityInvestigation | null;
  onRun: () => void;
};

export function InvestigationPanel({
  analysis,
  requestMatchesAnalysis,
  loading,
  error,
  result,
  onRun,
}: InvestigationPanelProps) {
  const canRun = analysis !== null && requestMatchesAnalysis && !loading;
  return (
    <div className="investigation">
      <div className="investigation-bar">
        <button type="button" onClick={onRun} disabled={!canRun}>
          {loading ? "Running AI investigation…" : "Run AI investigation"}
        </button>
        {!loading && analysis === null ? (
          <p className="quiet">Analyze a facility and interval first. This button is the only way to call the investigator.</p>
        ) : null}
        {!loading && analysis !== null && !requestMatchesAnalysis ? (
          <p className="quiet">The selected facility or interval no longer matches the analysis. Analyze again before running an investigation.</p>
        ) : null}
      </div>
      {loading ? <p role="status">Running AI investigation…</p> : null}
      {error ? (
        <p className="error" role="alert">
          {error}
        </p>
      ) : null}
      {!loading && !error && result === null ? (
        <p className="empty">The investigation stays idle until you run it.</p>
      ) : null}
      {result && analysis ? (
        <div className="investigation-result">
          <p className="summary">{result.overallSummary}</p>
          <section aria-labelledby="observations-heading">
            <h3 id="observations-heading">Observations</h3>
            {result.observations.length === 0 ? (
              <p className="empty">No observations were returned.</p>
            ) : (
              <div className="stack">
                {result.observations.map((observation) => (
                  <ObservationCard key={observation.id} analysis={analysis} observation={observation} />
                ))}
              </div>
            )}
          </section>
          <section aria-labelledby="hypotheses-heading">
            <h3 id="hypotheses-heading">Hypotheses</h3>
            {result.hypotheses.length === 0 ? (
              <p className="empty">No hypotheses were returned.</p>
            ) : (
              <div className="stack">
                {result.hypotheses.map((hypothesis) => (
                  <HypothesisCard
                    key={hypothesis.id}
                    hypothesis={hypothesis}
                    observations={result.observations}
                  />
                ))}
              </div>
            )}
          </section>
          <LimitationsPanel analysis={analysis} limitations={result.limitations} />
        </div>
      ) : null}
    </div>
  );
}
