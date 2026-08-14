import { Button, Field, Input, Select } from "@kurx/ui";
import { Search } from "lucide-react";

type Category = { id: string; name: string };

/** Plain GET form — same navigation-driven filtering every other admin list already uses, so every
 *  filter is a real, bookmarkable, back-button-safe URL. `tab` travels as a hidden field so filtering
 *  never knocks the operator off the tab they're on. */
export function EventsFilterBar({
  tab,
  categories,
  values
}: {
  tab: string;
  categories: Category[];
  values: {
    q?: string; category?: string; city?: string; visibility?: string; paid?: string; verified?: string;
    dateFrom?: string; dateTo?: string; revenueMin?: string; revenueMax?: string; regMin?: string; regMax?: string; sort?: string;
  };
}) {
  return (
    <form method="get" className="space-y-3 rounded-lg border border-border bg-surface p-4">
      <input type="hidden" name="tab" value={tab} />
      <div className="flex flex-wrap items-end gap-3">
        <div className="relative">
          <Search size={15} className="pointer-events-none absolute left-2.5 top-[1.9rem] text-muted" />
          <Field label="Search" htmlFor="ev-q">
            <Input id="ev-q" name="q" defaultValue={values.q} placeholder="Name, ID, organizer, org" className="w-64 pl-8" />
          </Field>
        </div>
        <Field label="Category" htmlFor="ev-category">
          <Select id="ev-category" name="category" defaultValue={values.category ?? ""} className="w-40">
            <option value="">Any category</option>
            {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="City" htmlFor="ev-city">
          <Input id="ev-city" name="city" defaultValue={values.city} placeholder="Any city" className="w-32" />
        </Field>
        <Field label="Visibility" htmlFor="ev-visibility">
          <Select id="ev-visibility" name="visibility" defaultValue={values.visibility ?? ""} className="w-32">
            <option value="">Any</option>
            <option value="listed">Listed</option>
            <option value="unlisted">Unlisted</option>
            <option value="private">Private</option>
            <option value="inviteonly">Invite only</option>
          </Select>
        </Field>
        <Field label="Price" htmlFor="ev-paid">
          <Select id="ev-paid" name="paid" defaultValue={values.paid ?? ""} className="w-28">
            <option value="">Any</option>
            <option value="free">Free</option>
            <option value="paid">Paid</option>
          </Select>
        </Field>
        <Field label="Sort" htmlFor="ev-sort">
          <Select id="ev-sort" name="sort" defaultValue={values.sort ?? ""} className="w-36">
            <option value="">Newest created</option>
            <option value="date">Start date</option>
            <option value="revenue">Revenue</option>
            <option value="registrations">Registrations</option>
            <option value="attendance">Attendance</option>
            <option value="updated">Last updated</option>
          </Select>
        </Field>
        <Button type="submit" variant="secondary">Apply</Button>
      </div>

      <details className="text-sm">
        <summary className="cursor-pointer select-none text-xs font-semibold text-muted hover:text-text">More filters</summary>
        <div className="mt-3 flex flex-wrap items-end gap-3">
          <label className="flex items-center gap-2 text-xs text-muted">
            <input type="checkbox" name="verified" value="1" defaultChecked={values.verified === "1"} className="h-4 w-4 accent-accent" />
            Verified organizers only
          </label>
          <Field label="Start from" htmlFor="ev-date-from">
            <Input id="ev-date-from" name="dateFrom" type="date" defaultValue={values.dateFrom?.slice(0, 10)} className="w-36" />
          </Field>
          <Field label="Start to" htmlFor="ev-date-to">
            <Input id="ev-date-to" name="dateTo" type="date" defaultValue={values.dateTo?.slice(0, 10)} className="w-36" />
          </Field>
          <Field label="Revenue min (₹)" htmlFor="ev-rev-min">
            <Input id="ev-rev-min" name="revenueMin" type="number" min={0} defaultValue={values.revenueMin} className="w-28" />
          </Field>
          <Field label="Revenue max (₹)" htmlFor="ev-rev-max">
            <Input id="ev-rev-max" name="revenueMax" type="number" min={0} defaultValue={values.revenueMax} className="w-28" />
          </Field>
          <Field label="Registrations min" htmlFor="ev-reg-min">
            <Input id="ev-reg-min" name="regMin" type="number" min={0} defaultValue={values.regMin} className="w-28" />
          </Field>
          <Field label="Registrations max" htmlFor="ev-reg-max">
            <Input id="ev-reg-max" name="regMax" type="number" min={0} defaultValue={values.regMax} className="w-28" />
          </Field>
        </div>
      </details>
    </form>
  );
}
