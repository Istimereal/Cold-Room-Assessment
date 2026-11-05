import type { FacilityAnalysis } from "../api/analysis";
import { formatMeasurement, formatTimestamp } from "../format";
import { StatusBadge } from "./StatusBadge";

export function DataQualitySummary({ analysis }: { analysis: FacilityAnalysis }) {
  return (
    <section className="quality" aria-labelledby="quality-heading">
      <div className="section-heading">
        <h3 id="quality-heading">Data quality</h3>
        <p>
          {analysis.equipmentCount} equipment · {formatTimestamp(analysis.from)} to {formatTimestamp(analysis.to)}
        </p>
      </div>
      {analysis.equipment.length === 0 ? (
        <p className="empty">This facility has no equipment.</p>
      ) : (
        <ul className="quality-list">
          {analysis.equipment.map((item) => (
            <li key={item.equipmentId}>
              <strong>{item.equipmentName}</strong>
              <span>Coverage {formatMeasurement(item.coveragePercent)}%</span>
              <span>Missing {item.missingExpectedSampleCount}</span>
              <span>Ambiguous {item.ambiguousSampleCount}</span>
              <StatusBadge tone={item.hasCompleteCoverage ? "complete" : "incomplete"}>
                {item.hasCompleteCoverage ? "Complete" : "Incomplete"}
              </StatusBadge>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
