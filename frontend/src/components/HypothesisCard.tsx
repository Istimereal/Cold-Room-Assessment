import type { InvestigationHypothesis, InvestigationObservation } from "../api/investigations";
import { StatusBadge, confidenceTone } from "./StatusBadge";

export function HypothesisCard({
  hypothesis,
  observations,
}: {
  hypothesis: InvestigationHypothesis;
  observations: InvestigationObservation[];
}) {
  return (
    <article className="card">
      <header className="card-heading">
        <h3>{hypothesis.id}</h3>
        <StatusBadge tone={confidenceTone(hypothesis.confidence)}>{hypothesis.confidence}</StatusBadge>
      </header>
      <p>{hypothesis.statement}</p>
      <p className="meta">Supports: {supportTitles(hypothesis.supportingObservationIds, observations)}</p>
      <LabeledList title="Evidence for" items={hypothesis.evidenceFor} />
      <LabeledList title="Uncertainties" items={hypothesis.uncertainties} />
    </article>
  );
}

function supportTitles(ids: string[], observations: InvestigationObservation[]): string {
  if (ids.length === 0) {
    return "None cited";
  }

  return ids
    .map((id) => observations.find((item) => item.id === id)?.title ?? id)
    .join(", ");
}

function LabeledList({ title, items }: { title: string; items: string[] }) {
  return (
    <div>
      <h4>{title}</h4>
      {items.length === 0 ? (
        <p className="empty">None.</p>
      ) : (
        <ul>
          {items.map((item, index) => (
            <li key={`${item}-${index}`}>{item}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
