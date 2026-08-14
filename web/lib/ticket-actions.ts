"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import {
  createTicketType,
  updateTicketType,
  deleteTicketType,
  addTicketTypeField,
  updateTicketTypeField,
  deleteTicketTypeField,
  apiErrorMessage
} from "@/lib/api";

function str(formData: FormData, key: string): string | undefined {
  const v = formData.get(key);
  return typeof v === "string" && v.length > 0 ? v : undefined;
}

function num(formData: FormData, key: string): number | undefined {
  const v = str(formData, key);
  return v === undefined ? undefined : Number(v);
}

function bool(formData: FormData, key: string): boolean {
  return formData.get(key) != null;
}

export async function createTicketTypeAction(orgId: string, eventId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const priceRupees = num(formData, "price") ?? 0;
  try {
    await createTicketType(session.accessToken, orgId, eventId, {
      name: str(formData, "name"),
      pricePaise: Math.round(priceRupees * 100),
      pricingUnit: str(formData, "pricingUnit") ?? "PerTicket",
      registrationMode: str(formData, "registrationMode") ?? "Individual",
      groupMin: num(formData, "groupMin"),
      groupMax: num(formData, "groupMax"),
      quantity: num(formData, "quantity") ?? 0,
      saleStarts: str(formData, "saleStarts"),
      saleEnds: str(formData, "saleEnds"),
      perUserLimit: num(formData, "perUserLimit") ?? 5,
      isAllAccess: bool(formData, "isAllAccess"),
      isCompetition: bool(formData, "isCompetition")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/host/tickets");
  return { ok: true };
}

export async function updateTicketTypeAction(orgId: string, eventId: string, ticketTypeId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  const priceRupees = num(formData, "price") ?? 0;
  try {
    await updateTicketType(session.accessToken, orgId, eventId, ticketTypeId, {
      name: str(formData, "name"),
      pricePaise: Math.round(priceRupees * 100),
      pricingUnit: str(formData, "pricingUnit") ?? "PerTicket",
      registrationMode: str(formData, "registrationMode") ?? "Individual",
      groupMin: num(formData, "groupMin"),
      groupMax: num(formData, "groupMax"),
      quantity: num(formData, "quantity") ?? 0,
      saleStarts: str(formData, "saleStarts"),
      saleEnds: str(formData, "saleEnds"),
      perUserLimit: num(formData, "perUserLimit") ?? 5,
      isAllAccess: bool(formData, "isAllAccess"),
      isCompetition: bool(formData, "isCompetition")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/host/tickets");
  return { ok: true };
}

export async function deleteTicketTypeAction(orgId: string, eventId: string, ticketTypeId: string) {
  const session = await requireSession();
  await deleteTicketType(session.accessToken, orgId, eventId, ticketTypeId);
  revalidatePath("/host/tickets");
}

export async function addFieldAction(orgId: string, eventId: string, ticketTypeId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await addTicketTypeField(session.accessToken, orgId, eventId, ticketTypeId, {
      key: str(formData, "key"),
      label: str(formData, "label"),
      type: str(formData, "type") ?? "Text",
      scope: str(formData, "scope") ?? "PerRegistration",
      required: bool(formData, "required"),
      optionsJson: str(formData, "optionsJson"),
      sort: num(formData, "sort")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/host/tickets");
  return { ok: true };
}

export async function deleteFieldAction(orgId: string, eventId: string, ticketTypeId: string, fieldId: string) {
  const session = await requireSession();
  await deleteTicketTypeField(session.accessToken, orgId, eventId, ticketTypeId, fieldId);
  revalidatePath(`/host/events/${eventId}/tickets`);
}

export async function updateFieldAction(orgId: string, eventId: string, ticketTypeId: string, fieldId: string, _: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await updateTicketTypeField(session.accessToken, orgId, eventId, ticketTypeId, fieldId, {
      key: str(formData, "key"),
      label: str(formData, "label"),
      type: str(formData, "type") ?? "Text",
      scope: str(formData, "scope") ?? "PerRegistration",
      required: bool(formData, "required"),
      optionsJson: str(formData, "optionsJson"),
      sort: num(formData, "sort")
    });
  } catch {
    /* the re-render reflects the unchanged field */
  }
  revalidatePath(`/host/events/${eventId}/tickets`);
}
