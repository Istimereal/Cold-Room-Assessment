import { request } from "./http";

export type Equipment = {
  id: string;
  facilityId: string;
  facilityName: string;
  equipmentTypeId: string;
  equipmentTypeName: string;
  name: string;
  placement: string;
};

export type CreateEquipmentInput = {
  equipmentTypeId?: string;
  name: string;
  placement: string;
};

const fallback = "The equipment could not be created.";

export function createEquipment(
  facilityId: string,
  input: CreateEquipmentInput,
  signal?: AbortSignal,
): Promise<Equipment> {
  const body: CreateEquipmentInput = {
    name: input.name,
    placement: input.placement,
  };
  if (input.equipmentTypeId) {
    body.equipmentTypeId = input.equipmentTypeId;
  }

  return request<Equipment>(
    `/api/facilities/${encodeURIComponent(facilityId)}/equipment`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      signal,
    },
    fallback,
  );
}
