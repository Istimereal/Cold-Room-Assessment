import { Component, useEffect, useRef, useState, type ReactNode } from "react";
import { getAnalysis, type FacilityAnalysis } from "./api/analysis";
import { getFacility, listFacilities, type EquipmentSummary, type FacilityListItem } from "./api/facilities";
import { visibleError } from "./api/http";
import { uploadMeasurements, type ImportSummary } from "./api/imports";
import { runInvestigation, type FacilityInvestigation } from "./api/investigations";
import { runVerification, type FacilityVerification } from "./api/verifications";
import { AnalysisControls } from "./components/AnalysisControls";
import { DataQualitySummary } from "./components/DataQualitySummary";
import { DataUpload } from "./components/DataUpload";
import { EquipmentOverview } from "./components/EquipmentOverview";
import { InvestigationPanel } from "./components/InvestigationPanel";
import { VerificationPanel } from "./components/VerificationPanel";
import { normalizeTimestamp } from "./format";

const analysisFallback = "The analysis service could not be reached.";
const investigationFallback = "The investigation could not be completed.";
const verificationFallback = "The verification could not be completed.";
const uploadFallback = "The file could not be uploaded.";

export function App() {
  const [facilities, setFacilities] = useState<FacilityListItem[]>([]);
  const [facilitiesError, setFacilitiesError] = useState<string | null>(null);
  const [facilitiesLoading, setFacilitiesLoading] = useState(true);
  const [facilityId, setFacilityId] = useState("");
  const [equipment, setEquipment] = useState<EquipmentSummary[]>([]);
  const [equipmentError, setEquipmentError] = useState<string | null>(null);
  const [equipmentLoading, setEquipmentLoading] = useState(false);
  const [equipmentId, setEquipmentId] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [uploadResult, setUploadResult] = useState<ImportSummary | null>(null);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadLoading, setUploadLoading] = useState(false);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [analysis, setAnalysis] = useState<FacilityAnalysis | null>(null);
  const [analysisError, setAnalysisError] = useState<string | null>(null);
  const [analysisLoading, setAnalysisLoading] = useState(false);
  const [investigation, setInvestigation] = useState<FacilityInvestigation | null>(null);
  const [investigationError, setInvestigationError] = useState<string | null>(null);
  const [investigationLoading, setInvestigationLoading] = useState(false);
  const [verification, setVerification] = useState<FacilityVerification | null>(null);
  const [verificationError, setVerificationError] = useState<string | null>(null);
  const [verificationLoading, setVerificationLoading] = useState(false);
  const uploadRequest = useRef(0);
  const investigationRequest = useRef(0);
  const verificationRequest = useRef(0);

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

  useEffect(() => {
    if (facilityId.length === 0) {
      setEquipment([]);
      setEquipmentError(null);
      setEquipmentLoading(false);
      return;
    }

    const controller = new AbortController();
    setEquipmentLoading(true);
    setEquipmentError(null);
    getFacility(facilityId, controller.signal)
      .then((facility) => {
        setEquipment(facility.equipment);
        setEquipmentError(null);
      })
      .catch((error: unknown) => {
        if (isAbort(error)) {
          return;
        }

        setEquipment([]);
        setEquipmentError(visibleError(error, "The equipment list could not be loaded."));
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setEquipmentLoading(false);
        }
      });

    return () => controller.abort();
  }, [facilityId]);

  const requestedFrom = normalizeTimestamp(from);
  const requestedTo = normalizeTimestamp(to);
  const requestMatchesAnalysis =
    analysis !== null &&
    analysis.facilityId.toLowerCase() === facilityId.toLowerCase() &&
    analysis.from === requestedFrom &&
    analysis.to === requestedTo;

  function changeFacility(nextFacilityId: string) {
    uploadRequest.current += 1;
    setFacilityId(nextFacilityId);
    setEquipmentId("");
    setFile(null);
    setFileInputKey((key) => key + 1);
    setUploadResult(null);
    setUploadError(null);
    setUploadLoading(false);
    setAnalysis(null);
    setAnalysisError(null);
    clearInvestigation();
  }

  function changeEquipment(nextEquipmentId: string) {
    uploadRequest.current += 1;
    setEquipmentId(nextEquipmentId);
    setUploadResult(null);
    setUploadError(null);
    setUploadLoading(false);
  }

  function changeFile(nextFile: File | null) {
    setFile(nextFile);
    if (nextFile) {
      setUploadResult(null);
      setUploadError(null);
    }
  }

  function changeFrom(nextFrom: string) {
    setFrom(nextFrom);
    clearInvestigation();
  }

  function changeTo(nextTo: string) {
    setTo(nextTo);
    clearInvestigation();
  }

  function clearVerification() {
    verificationRequest.current += 1;
    setVerification(null);
    setVerificationError(null);
    setVerificationLoading(false);
  }

  function clearInvestigation() {
    investigationRequest.current += 1;
    setInvestigation(null);
    setInvestigationError(null);
    setInvestigationLoading(false);
    clearVerification();
  }

  async function upload() {
    if (facilityId.length === 0 || equipmentId.length === 0 || file === null) {
      return;
    }

    const requestId = ++uploadRequest.current;
    const analyzed = analysis;
    setUploadLoading(true);
    setUploadError(null);
    try {
      const summary = await uploadMeasurements(equipmentId, file);
      if (requestId !== uploadRequest.current) {
        return;
      }

      setUploadResult(summary);
      setFile(null);
      setFileInputKey((key) => key + 1);
      clearInvestigation();
      if (
        analyzed !== null &&
        analyzed.facilityId.toLowerCase() === facilityId.toLowerCase()
      ) {
        setAnalysisLoading(true);
        setAnalysisError(null);
        try {
          const refreshed = await getAnalysis(analyzed.facilityId, analyzed.from, analyzed.to);
          if (requestId !== uploadRequest.current) {
            return;
          }

          setAnalysis(refreshed);
        } catch (error: unknown) {
          if (isAbort(error) || requestId !== uploadRequest.current) {
            return;
          }

          setAnalysis(null);
          setAnalysisError(visibleError(error, analysisFallback));
        } finally {
          if (requestId === uploadRequest.current) {
            setAnalysisLoading(false);
          }
        }
      }
    } catch (error: unknown) {
      if (isAbort(error) || requestId !== uploadRequest.current) {
        return;
      }

      setUploadResult(null);
      setUploadError(visibleError(error, uploadFallback));
    } finally {
      if (requestId === uploadRequest.current) {
        setUploadLoading(false);
      }
    }
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

    const requestId = ++investigationRequest.current;
    setInvestigationLoading(true);
    setInvestigationError(null);
    setInvestigation(null);
    clearVerification();
    try {
      const result = await runInvestigation(analysis.facilityId, analysis.from, analysis.to);
      if (requestId !== investigationRequest.current) {
        return;
      }

      setInvestigation(result);
    } catch (error: unknown) {
      if (isAbort(error) || requestId !== investigationRequest.current) {
        return;
      }

      setInvestigationError(visibleError(error, investigationFallback));
    } finally {
      if (requestId === investigationRequest.current) {
        setInvestigationLoading(false);
      }
    }
  }

  async function verify() {
    if (!requestMatchesAnalysis || analysis === null || investigation === null) {
      return;
    }

    const requestId = ++verificationRequest.current;
    setVerificationLoading(true);
    setVerificationError(null);
    setVerification(null);
    try {
      const result = await runVerification(
        investigation.facilityId,
        investigation.from,
        investigation.to,
        investigation,
      );
      if (requestId !== verificationRequest.current) {
        return;
      }

      setVerification(result);
    } catch (error: unknown) {
      if (isAbort(error) || requestId !== verificationRequest.current) {
        return;
      }

      setVerificationError(visibleError(error, verificationFallback));
    } finally {
      if (requestId === verificationRequest.current) {
        setVerificationLoading(false);
      }
    }
  }

  const canVerify =
    investigation !== null && requestMatchesAnalysis && !verificationLoading && !investigationLoading;

  return (
    <main className="page">
      <header className="page-header">
        <h1>Cold Room Assessment</h1>
        <p className="subtitle">Deterministic sensor analysis, AI investigation, and an independent check.</p>
        <p className="flow">Deterministic analysis → AI Investigator → AI Verifier</p>
        <p className="quiet">ASP.NET Core · PostgreSQL · React · OpenAI</p>
      </header>
      <section className="region" aria-labelledby="upload-heading">
        <h2 id="upload-heading">Upload data</h2>
        <DataUpload
          facilities={facilities}
          facilitiesLoading={facilitiesLoading}
          facilitiesError={facilitiesError}
          facilityId={facilityId}
          equipment={equipment}
          equipmentLoading={equipmentLoading}
          equipmentError={equipmentError}
          equipmentId={equipmentId}
          fileInputKey={fileInputKey}
          hasFile={file !== null}
          uploadLoading={uploadLoading}
          uploadError={uploadError}
          uploadResult={uploadResult}
          onFacilityId={changeFacility}
          onEquipmentId={changeEquipment}
          onFile={changeFile}
          onUpload={() => void upload()}
        />
      </section>
      <AnalysisControls
        facilitySelected={facilityId.length > 0}
        from={from}
        to={to}
        analysisLoading={analysisLoading}
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
          <p className="empty">
            Choose a facility and interval, then analyze. The page does not call the investigator or verifier on its
            own.
          </p>
        ) : null}
        {analysis ? (
          <ResultBoundary resetKey={`${analysis.facilityId}:${analysis.from}:${analysis.to}`}>
            <DataQualitySummary analysis={analysis} />
            <EquipmentOverview equipment={analysis.equipment} />
          </ResultBoundary>
        ) : null}
      </section>
      <section className="region" aria-labelledby="investigation-heading">
        <h2 id="investigation-heading">AI Investigator</h2>
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
      <section className="region" aria-labelledby="verification-heading">
        <h2 id="verification-heading">AI Verifier</h2>
        <ResultBoundary resetKey={verification?.overallVerdict ?? verificationError ?? "idle"}>
          <VerificationPanel
            analysis={analysis}
            investigation={investigation}
            canVerify={canVerify}
            loading={verificationLoading}
            error={verificationError}
            result={verification}
            onRun={() => void verify()}
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
