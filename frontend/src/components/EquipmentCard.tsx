import type { EquipmentAnalysis, KindAnalysis } from "../api/analysis";
import { formatMeasurement } from "../format";
import { StatusBadge } from "./StatusBadge";

export function EquipmentCard({ equipment }: { equipment: EquipmentAnalysis }) {
  const noReadings = equipment.temperature.count === 0 && equipment.relativeHumidity.count === 0;
  return (
    <article className="card">
      <header className="card-heading">
        <div>
          <h3>{equipment.equipmentName}</h3>
          <p>
            {equipment.equipmentTypeName} · {equipment.placement}
          </p>
        </div>
        <StatusBadge tone={equipment.hasCompleteCoverage ? "complete" : "incomplete"}>
          {equipment.hasCompleteCoverage
            ? `${formatMeasurement(equipment.coveragePercent)}% complete`
            : `${formatMeasurement(equipment.coveragePercent)}% incomplete`}
        </StatusBadge>
      </header>
      {noReadings ? <p className="empty">No valid readings.</p> : null}
      <div className="kind-grid">
        <KindStats title="Temperature" kind={equipment.temperature} unit="°C" />
        <KindStats title="Humidity" kind={equipment.relativeHumidity} unit="%" />
      </div>
    </article>
  );
}

function KindStats({ title, kind, unit }: { title: string; kind: KindAnalysis; unit: string }) {
  const peaks = kind.events.filter((item) => item.kind === "Peak").length;
  const dips = kind.events.filter((item) => item.kind === "Dip").length;
  return (
    <section>
      <h4>{title}</h4>
      <dl className="stats">
        <Stat label="Min" value={reading(kind.min, unit)} />
        <Stat label="Max" value={reading(kind.max, unit)} />
        <Stat label="Mean" value={reading(kind.mean, unit)} />
        <Stat label="First-to-last change" value={reading(kind.change, unit)} />
        <Stat label="Peaks" value={String(peaks)} />
        <Stat label="Dips" value={String(dips)} />
      </dl>
      <details>
        <summary>More statistics</summary>
        <dl className="stats">
          <Stat label="Median" value={reading(kind.median, unit)} />
          <Stat label="Standard deviation" value={reading(kind.populationStdDev, unit)} />
          <Stat label="First" value={reading(kind.first, unit)} />
          <Stat label="Last" value={reading(kind.last, unit)} />
          <Stat
            label="Duration"
            value={kind.durationSeconds === null ? "—" : `${formatMeasurement(kind.durationSeconds)} s`}
          />
        </dl>
      </details>
    </section>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}

function reading(value: number | null, unit: string): string {
  if (value === null) {
    return "—";
  }

  return `${formatMeasurement(value)} ${unit}`;
}
