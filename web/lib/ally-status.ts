import type { AllyRelationStatus } from "@/lib/api";
import type { AllyRelation } from "@/components/profile/ally-connect-button";

/** Maps the batch-status API's relation strings onto AllyConnectButton's prop shape — the one
 * translation every person-list surface needs when wiring up initial Connect/Pending/Ally state. */
export function toAllyRelation(status: AllyRelationStatus | undefined): AllyRelation {
  switch (status) {
    case "accepted": return "accepted";
    case "pending_outgoing": return "outgoing";
    case "pending_incoming": return "incoming";
    default: return "none";
  }
}
