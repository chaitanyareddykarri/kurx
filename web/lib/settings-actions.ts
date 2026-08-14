"use server";

import { revalidatePath } from "next/cache";
import { requireSession } from "@/lib/session";
import {
  submitIdentityGovernmentId, submitIdentityPan, submitIdentityBank,
  requestPhoneOtp, changePhone, apiErrorMessage
} from "@/lib/api";

function str(fd: FormData, k: string): string {
  const v = fd.get(k);
  return typeof v === "string" ? v.trim() : "";
}

export async function submitGovtIdAction(_: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await submitIdentityGovernmentId(session.accessToken, {
      kind: str(formData, "kind") || "aadhaar",
      idNumber: str(formData, "idNumber"),
      name: str(formData, "name")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/settings/identity");
  return { ok: true };
}

export async function submitPanAction(_: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await submitIdentityPan(session.accessToken, { pan: str(formData, "pan"), name: str(formData, "name") });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/settings/identity");
  return { ok: true };
}

export async function submitBankAction(_: unknown, formData: FormData) {
  const session = await requireSession();
  try {
    await submitIdentityBank(session.accessToken, {
      accountNumber: str(formData, "accountNumber"),
      ifsc: str(formData, "ifsc"),
      holderName: str(formData, "holderName")
    });
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/settings/identity");
  return { ok: true };
}

export async function requestPhoneOtpAction(_: unknown, formData: FormData) {
  const phone = str(formData, "phone");
  if (!phone) return { error: "Enter a phone number." };
  try {
    await requestPhoneOtp(phone);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  return { ok: true, phone };
}

export async function changePhoneAction(_: unknown, formData: FormData) {
  const session = await requireSession();
  const phone = str(formData, "phone");
  const code = str(formData, "code");
  if (!phone || !code) return { error: "Enter the new phone and the code." };
  try {
    await changePhone(session.accessToken, phone, code);
  } catch (err) {
    return { error: apiErrorMessage(err) };
  }
  revalidatePath("/settings/account");
  return { ok: true };
}
