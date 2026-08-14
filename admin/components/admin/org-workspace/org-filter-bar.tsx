import { Button, Field, Input, Select } from "@kurx/ui";
import { Search } from "lucide-react";

/** Plain GET form — same navigation-driven filtering every other admin list already uses (D-186/D-188). */
export function OrgFilterBar({ values }: { values: { q?: string; status?: string; type?: string } }) {
  return (
    <form method="get" className="flex flex-wrap items-end gap-3 rounded-lg border border-border bg-surface p-4">
      <div className="relative">
        <Search size={15} className="pointer-events-none absolute left-2.5 top-[1.9rem] text-muted" />
        <Field label="Search" htmlFor="org-q">
          <Input id="org-q" name="q" defaultValue={values.q} placeholder="Name, slug, domain" className="w-64 pl-8" />
        </Field>
      </div>
      <Field label="Verification" htmlFor="org-status">
        <Select id="org-status" name="status" defaultValue={values.status ?? ""} className="w-40">
          <option value="">Any status</option>
          <option value="Unverified">Unverified</option>
          <option value="PendingReview">Pending review</option>
          <option value="ChangesRequested">Changes requested</option>
          <option value="Verified">Verified</option>
          <option value="Rejected">Rejected</option>
          <option value="Suspended">Suspended</option>
          <option value="Blacklisted">Blacklisted</option>
        </Select>
      </Field>
      <Field label="Type" htmlFor="org-type">
        <Select id="org-type" name="type" defaultValue={values.type ?? ""} className="w-36">
          <option value="">Any type</option>
          <option value="College">College</option>
          <option value="School">School</option>
          <option value="University">University</option>
          <option value="Company">Company</option>
          <option value="Startup">Startup</option>
          <option value="NGO">NGO</option>
          <option value="Club">Club</option>
          <option value="Community">Community</option>
          <option value="Government">Government</option>
          <option value="Other">Other</option>
        </Select>
      </Field>
      <Button type="submit" variant="secondary">Apply</Button>
    </form>
  );
}
