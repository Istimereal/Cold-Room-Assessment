import { Component, useEffect, useState, type ReactNode } from "react";
import { getAnalysis, type FacilityAnalysis } from "./api/analysis";
import { listFacilities, type FacilityListItem } from "./api/facilities";
import { visibleError } from "./api/http";
import { runInvestigation, type FacilityInvestigation } from "./api/investigations";
import { AnalysisControls } from "./components/AnalysisControls";
import { DataQualitySummary } from "./components/DataQualitySummary";
import { EquipmentOverview } from "./components/EquipmentOverview";
import { InvestigationPanel } from "./components/InvestigationPanel";
import { normalizeTimestamp } from "./format";

const analysisFallback = "The analysis service could not be reached.";
const investigationFallback = "The investigation could not be completed.";

export function App() {
  const [facilities, setFacilities] = useState<FacilityListItem[]>([]);
  const [facilitiesError, setFacilitiesError] = useState<string | null>(null);
  const [facilitiesLoading, setFacilitiesLoading] = useState(true);
  const [facilityId, setFacilityId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [analysis, setAnalysis] = useState<FacilityAnalysis | null>(null);
  const [analysisError, setAnalysisError] = useState<string | null>(null);
  const [analysisLoading, setAnalysisLoading] = useState(false);
  const [investigation, setInvestigation] = useState<FacilityInvestigation | null>(null);
  const [investigationError, setInvestigationError] = useState<string | null>(null);
  const [investigationLoading, setInvestigationLoading] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    listFacilities(controller.signal)
      .then((items) => {
        setFacilities(items);
        setFacilitiesError(null);
      })
      .catch((error: unknown) => {
        if (isAbort(error)) {
          return;
        }

        setFacilities([]);
        setFacilitiesError(visibleError(error, analysisFallback));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setFacilitiesLoading(false);
        }
      });

    return () => controller.abort();
  }, []);

  const requestedFrom = normalizeTimestamp(from);
  const requestedTo = normalizeTimestamp(to);
  const requestMatchesAnalysis =
    analysis !== null &&
    analysis.facilityId.toLowerCase() === facilityId.toLowerCase() &&
    analysis.from === requestedFrom &&
    analysis.to === requestedTo;

  function changeFacility(nextFacilityId: string) {
    setFacilityId(nextFacilityId);
    clearInvestigation();
  }

  function changeFrom(nextFrom: string) {
    setFrom(nextFrom);
    clearInvestigation();
  }

  function changeTo(nextTo: string) {
    setTo(nextTo);
    clearInvestigation();
  }

  function clearInvestigation() {
    setInvestigation(null);
    setInvestigationError(null);
  }

  async function analyze() {
    setAnalysisLoading(true);
    setAnalysisError(null);
    setAnalysis(null);
    clearInvestigation();
    try {
      const result = await getAnalysis(facilityId, requestedFrom, requestedTo);
      setAnalysis(result);
    } catch (error: unknown) {
      if (isAbort(error)) {
        return;
      }

      setAnalysisError(visibleError(error, analysisFallback));
    } finally {
      setAnalysisLoading(false);
    }
  }

  async function investigate() {
    if (!requestMatchesAnalysis || analysis === null) {
      return;
    }

    setInvestigationLoading(true);
    setInvestigationError(null);
    setInvestigation(null);
    try {
      const result = await runInvestigation(analysis.facilityId, analysis.from, analysis.to);
      setInvestigation(result);
    } catch (error: unknown) {
      if (isAbort(error)) {
        return;
      }

      setInvestigationError(visibleError(error, investigationFallback));
    } finally {
      setInvestigationLoading(false);
    }
  }

  return (
    <main className="page">
      <header className="page-header">
        <h1>Cold Room Assessment</h1>
        <p className="subtitle">Deterministic sensor analysis with AI-assisted investigation.</p>
        <p className="quiet">ASP.NET Core · PostgreSQL · React · OpenAI</p>
      </header>
      <AnalysisControls
        facilities={facilities}
        facilitiesLoading={facilitiesLoading}
        facilitiesError={facilitiesError}
        facilityId={facilityId}
        from={from}
        to={to}
        analysisLoading={analysisLoading}
        onFacilityId={changeFacility}
        onFrom={changeFrom}
        onTo={changeTo}
        onAnalyze={() => void analyze()}
      />
      <section className="region" aria-labelledby="analysis-heading">
        <h2 id="analysis-heading">Deterministic analysis</h2>
        {analysisLoading ? <p role="status">Analyzing…</p> : null}
        {analysisError ? (
          <p className="error" role="alert">
            {analysisError}
          </p>
        ) : null}
        {!analysisLoading && !analysisError && analysis === null ? (
          <p className="empty">Choose a facility and interval, then analyze. The page does not call the investigator on its own.</p>
        ) : null}
        {analysis ? (
          <ResultBoundary resetKey={`${analysis.facilityId}:${analysis.from}:${analysis.to}`}>
            <DataQualitySummary analysis={analysis} />
            <EquipmentOverview equipment={analysis.equipment} />
          </ResultBoundary>
        ) : null}
      </section>
      <section className="region" aria-labelledby="investigation-heading">
        <h2 id="investigation-heading">AI investigation</h2>
        <ResultBoundary resetKey={investigation?.overallSummary ?? investigationError ?? "idle"}>
          <InvestigationPanel
            analysis={analysis}
            requestMatchesAnalysis={requestMatchesAnalysis}
            loading={investigationLoading}
            error={investigationError}
            result={investigation}
            onRun={() => void investigate()}
          />
        </ResultBoundary>
      </section>
    </main>
  );
}

function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}

type BoundaryProps = { resetKey: string; children: ReactNode };
type BoundaryState = { failed: boolean; resetKey: string };

class ResultBoundary extends Component<BoundaryProps, BoundaryState> {
  state: BoundaryState = { failed: false, resetKey: this.props.resetKey };

  static getDerivedStateFromError(): Partial<BoundaryState> {
    return { failed: true };
  }

  static getDerivedStateFromProps(props: BoundaryProps, state: BoundaryState): Partial<BoundaryState> | null {
    if (props.resetKey !== state.resetKey) {
      return { failed: false, resetKey: props.resetKey };
    }

    return null;
  }

  render() {
    if (this.state.failed) {
      return <p role="alert">The page could not show this result.</p>;
    }

    return this.props.children;
  }
}
