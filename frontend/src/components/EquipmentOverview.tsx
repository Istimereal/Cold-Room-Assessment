import type { EquipmentAnalysis } from "../api/analysis";
import { EquipmentCard } from "./EquipmentCard";

export function EquipmentOverview({ equipment }: { equipment: EquipmentAnalysis[] }) {
  if (equipment.length === 0) {
    return null;
  }

  return (
    <div className="equipment-grid">
      {equipment.map((item) => (
        <EquipmentCard key={item.equipmentId} equipment={item} />
      ))}
    </div>
  );
}
