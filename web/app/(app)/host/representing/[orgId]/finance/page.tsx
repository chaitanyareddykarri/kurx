import { Card, Stat } from "@kurx/ui";
import { requireRepresentedOrg } from "@/lib/represented-org";
import { getWorkspaceCapabilities, can } from "@/lib/capabilities";
import { getOrgWallet, getOrgWalletLedger, apiErrorMessage } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/formatters";

// Finance workspace (Owner/Finance only — enforced by the backend and gated here off the live capability
// contract). All figures are read live from the wallet + ledger; nothing is placeholder or computed client-side.
const ledgerStateLabel: Record<string, string> = {
  collected: "Collected",
  available: "Available",
  advanced: "Advanced",
  reserved: "Reserved",
  settled: "Settled"
};

const refTypeLabel: Record<string, string> = {
  payment: "Ticket payment",
  refund: "Refund",
  transfer: "Payout transfer",
  reserve: "Reserve hold"
};

export default async function FinancePage({ params }: { params: { orgId: string } }) {
  const { session, org } = await requireRepresentedOrg(params.orgId);

  const caps = await getWorkspaceCapabilities(session.accessToken, org.organization_id);
  if (!can(caps, "wallet", "view")) {
    return (
      <div className="space-y-4">
        <p className="text-sm font-semibold text-accent-text">{org.name}</p>
        <h1 className="text-3xl font-semibold">Finance</h1>
        <Card>
          <p className="text-sm text-muted">
            Only <strong className="text-text">Owner</strong> and <strong className="text-text">Finance</strong> roles can view finances.
            Your authority representing this organization is <strong className="text-text">{org.authority}</strong>.
          </p>
        </Card>
      </div>
    );
  }

  try {
    const [wallet, ledger] = await Promise.all([
      getOrgWallet(session.accessToken, org.organization_id),
      getOrgWalletLedger(session.accessToken, org.organization_id, 1, 25)
    ]);

    return (
      <div className="space-y-6">
        <div>
          <p className="text-sm font-semibold text-accent-text">{org.name}</p>
          <h1 className="mt-2 text-3xl font-semibold">Finance</h1>
          <p className="mt-1 text-sm text-muted">Balances as of {formatDate(wallet.updated_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}, live from the ledger.</p>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <Stat label="Available to withdraw" value={formatCurrency(wallet.available_paise, "en-IN", wallet.currency)} />
          <Stat label="Collected (clearing)" value={formatCurrency(wallet.collected_paise, "en-IN", wallet.currency)} />
          <Stat label="Reserved" value={formatCurrency(wallet.reserved_paise, "en-IN", wallet.currency)} />
          <Stat label="Advanced" value={formatCurrency(wallet.advanced_paise, "en-IN", wallet.currency)} />
          <Stat label="Settled (paid out)" value={formatCurrency(wallet.settled_paise, "en-IN", wallet.currency)} />
        </div>

        <Card>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <p className="text-sm text-muted">Lifetime earned</p>
              <strong className="mt-1 block text-xl text-text">{formatCurrency(wallet.lifetime_earned_paise, "en-IN", wallet.currency)}</strong>
            </div>
            <div>
              <p className="text-sm text-muted">Lifetime withdrawn</p>
              <strong className="mt-1 block text-xl text-text">{formatCurrency(wallet.lifetime_withdrawn_paise, "en-IN", wallet.currency)}</strong>
            </div>
          </div>
          <p className="mt-4 text-xs text-muted">
            Withdrawals to a bank account open once a payout account (Razorpay Route) is connected. The balances
            above are live; settlement history appears in the ledger below as funds move between states.
          </p>
        </Card>

        <section className="space-y-3">
          <h2 className="text-lg font-semibold">Transactions</h2>
          {ledger.length === 0 ? (
            <Card><p className="text-sm text-muted">No ledger activity yet. Entries appear here as tickets are paid and funds settle.</p></Card>
          ) : (
            <Card className="overflow-x-auto p-0">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border text-left text-muted">
                    <th scope="col" className="px-5 py-3 font-medium">Date</th>
                    <th scope="col" className="px-5 py-3 font-medium">Type</th>
                    <th scope="col" className="px-5 py-3 font-medium">State</th>
                    <th scope="col" className="px-5 py-3 text-right font-medium">Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {ledger.map((entry) => (
                    <tr key={entry.id} className="border-b border-border last:border-0">
                      <td className="whitespace-nowrap px-5 py-3 text-muted">{formatDate(entry.created_at, "en-IN", { day: "numeric", month: "short", year: "numeric" })}</td>
                      <td className="px-5 py-3">{refTypeLabel[entry.ref_type.toLowerCase()] ?? entry.ref_type}</td>
                      <td className="px-5 py-3">{ledgerStateLabel[entry.state.toLowerCase()] ?? entry.state}</td>
                      <td className="whitespace-nowrap px-5 py-3 text-right font-medium text-text">{formatCurrency(entry.amount_paise, "en-IN", entry.currency)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>
          )}
        </section>
      </div>
    );
  } catch (err) {
    return (
      <div className="space-y-4">
        <p className="text-sm font-semibold text-accent-text">{org.name}</p>
        <h1 className="text-3xl font-semibold">Finance</h1>
        <Card><p className="text-sm text-muted">Couldn&apos;t load your wallet: <span className="text-text">{apiErrorMessage(err)}</span></p></Card>
      </div>
    );
  }
}
