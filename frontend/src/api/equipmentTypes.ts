import { request } from "./http";

export type EquipmentType = {
  id: string;
  name: string;
};

const listFallback = "The equipment type list could not be loaded.";
const createFallback = "The equipment type could not be created.";

export function listEquipmentTypes(signal?: AbortSignal): Promise<EquipmentType[]> {
  return request<EquipmentType[]>("/api/equipment-types", { signal }, listFallback);
}

export function createEquipmentType(name: string, signal?: AbortSignal): Promise<EquipmentType> {
  return request<EquipmentType>(
    "/api/equipment-types",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
      signal,
    },
    createFallback,
  );
}
