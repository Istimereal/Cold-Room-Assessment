import type { FacilityAnalysis } from "../api/analysis";
import { evidenceLabel } from "../evidenceLabel";

export function CitedEvidence({
  title,
  ids,
  analysis,
}: {
  title: string;
  ids: string[];
  analysis: FacilityAnalysis;
}) {
  return (
    <div>
      <h4>{title}</h4>
      {ids.length === 0 ? (
        <p className="empty">None cited.</p>
      ) : (
        <ul className="evidence-list">
          {ids.map((id, index) => {
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
    </div>
  );
}
