import type { FacilityAnalysis } from "../api/analysis";
import type { InvestigationObservation } from "../api/investigations";
import { evidenceLabel } from "../evidenceLabel";
import { formatTimestamp } from "../format";
import { StatusBadge, confidenceTone } from "./StatusBadge";

export function ObservationCard({
  analysis,
  observation,
}: {
  analysis: FacilityAnalysis;
  observation: InvestigationObservation;
}) {
  return (
    <article className="card">
      <header className="card-heading">
        <h3>{observation.title}</h3>
        <StatusBadge tone={confidenceTone(observation.confidence)}>{observation.confidence}</StatusBadge>
      </header>
      <p>{observation.description}</p>
      <p className="meta">
        {kindText(observation.measurementKind)} · {formatTimestamp(observation.from)} to{" "}
        {formatTimestamp(observation.to)}
      </p>
      <p className="meta">Equipment: {equipmentNames(analysis, observation.equipmentIds)}</p>
      {observation.evidenceReferenceIds.length === 0 ? (
        <p className="empty">No evidence cited.</p>
      ) : (
        <ul className="evidence-list">
          {observation.evidenceReferenceIds.map((id, index) => {
            const label = evidenceLabel(analysis, id);
            return (
              <li key={`${id}-${index}`}>
                <span>{label.text}</span>
                <code>{id}</code>
              </li>
            );
          })}
        </ul>
      )}
    </article>
  );
}

function kindText(kind: InvestigationObservation["measurementKind"]): string {
  if (kind === "Temperature") {
    return "Temperature";
  }

  if (kind === "RelativeHumidity") {
    return "Relative humidity";
  }

  return "Not tied to one measurement kind";
}

function equipmentNames(analysis: FacilityAnalysis, ids: string[]): string {
  if (ids.length === 0) {
    return "None cited";
  }

  return ids
    .map((id) => analysis.equipment.find((item) => item.equipmentId.toLowerCase() === id.toLowerCase())?.equipmentName ?? "Unknown equipment")
    .join(", ");
}
