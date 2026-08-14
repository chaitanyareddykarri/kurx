import { redirect } from "next/navigation";

// Wallet, revenue, transactions and settlement are consolidated into the Finance workspace.
export default function PayoutsRedirect() {
  redirect("/host/representing");
}
