import { cache } from "react";
import { z } from "zod";
import { api } from "@/lib/api";

/// The permission-gated organizer workspace is driven entirely by this backend contract
/// (GET /v1/orgs/{orgId}/workspace-capabilities). The frontend never infers a permission — it only
/// renders what the backend reports here. See docs/api + OrgEndpoints.MapOrgEndpoints.
const workspaceSchema = z.object({ key: z.string(), label: z.string() });

export const workspaceCapabilitiesSchema = z.object({
  /// What this workspace represents (D-268) — not who owns it. `kind: "personal"` carries no
  /// organization identity, because representing yourself is not an organization.
  representation: z.object({
    kind: z.enum(["personal", "organization"]),
    organization_id: z.string().nullable(),
    name: z.string().nullable(),
    verified: z.boolean(),
    verification_status: z.string().nullable(),
    represented_as: z.string(),
    authority: z.string().nullable()
  }),
  trust: z.object({
    trust_level: z.string(),
    organizer_level: z.string(),
    can_host_paid_events: z.boolean()
  }),
  permissions: z.record(z.array(z.string())),
  workspaces: z.array(workspaceSchema)
});

export type WorkspaceCapabilities = z.infer<typeof workspaceCapabilitiesSchema>;

/// Cached per request so a layout and its page share a single backend call.
export const getWorkspaceCapabilities = cache(
  async (accessToken: string, orgId: string): Promise<WorkspaceCapabilities> => {
    const { data } = await api.get(`/v1/orgs/${orgId}/workspace-capabilities`, {
      headers: { Authorization: `Bearer ${accessToken}` }
    });
    return workspaceCapabilitiesSchema.parse(data);
  }
);

export function can(caps: WorkspaceCapabilities, module: string, action: string): boolean {
  return caps.permissions[module]?.includes(action) ?? false;
}

export function hasWorkspace(caps: WorkspaceCapabilities, key: string): boolean {
  return caps.workspaces.some((w) => w.key === key);
}
