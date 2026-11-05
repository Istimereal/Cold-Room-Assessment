import type { FacilityAnalysis } from "../api/analysis";
import type { InvestigationLimitation, InvestigationLimitationCode } from "../api/investigations";

const limitationNames: Record<InvestigationLimitationCode, string> = {
  IncompleteCoverage: "Incomplete coverage",
  AmbiguousData: "Ambiguous data",
  MissingData: "Missing data",
  InsufficientEvidence: "Insufficient evidence",
};

export function LimitationsPanel({
  analysis,
  limitations,
}: {
  analysis: FacilityAnalysis;
  limitations: InvestigationLimitation[];
}) {
  return (
    <section className="limitations" aria-labelledby="limitations-heading">
      <h3 id="limitations-heading">Limitations</h3>
      {limitations.length === 0 ? (
        <p className="empty">No limitations were returned.</p>
      ) : (
        <ul>
          {limitations.map((item, index) => (
            <li key={`${item.code}-${item.equipmentId ?? "facility"}-${index}`}>
              <strong>{limitationLabel(item.code)}</strong>
              <span>{equipmentName(analysis, item.equipmentId)}</span>
              <p>{item.description}</p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function limitationLabel(code: string): string {
  if (code in limitationNames) {
    return limitationNames[code as InvestigationLimitationCode];
  }

  return "Unrecognized limitation";
}

function equipmentName(analysis: FacilityAnalysis, equipmentId: string | null): string {
  if (equipmentId === null) {
    return "Whole facility";
  }

  return (
    analysis.equipment.find((item) => item.equipmentId.toLowerCase() === equipmentId.toLowerCase())
      ?.equipmentName ?? "Unknown equipment"
  );
}
