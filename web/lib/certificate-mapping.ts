/**
 * Column mapping — pure, so it can be tested without a DOM (D-344, Phase 6).
 *
 * The mapping decides which spreadsheet column feeds which placeholder on the design. Getting it wrong
 * is not a rendering bug the organiser notices in a preview: it prints the wrong words on every
 * certificate in the run, correctly and confidently, and is discovered after they are sent. So the whole
 * screen is built around making a wrong mapping VISIBLE — the server's guess is pre-filled, real sample
 * values sit beside every choice, and unmapped placeholders are called out before generation is allowed.
 */

/** A column mapped to a placeholder. Keyed by column name because that is what the file gives us; two
 *  columns may share a name, which is why the index is carried too. */
export type ColumnMapping = Record<string, string>;

export interface SpreadsheetPreview {
  columns: string[];
  sampleRows: string[][];
  totalRows: number;
  truncated: boolean;
  suggestedMapping: ColumnMapping;
}

/** A placeholder on the design that needs a value. */
export interface MappableField {
  key: string;
  label: string;
  required: boolean;
}

/** Maps a column to a field, clearing whatever else pointed at that field.
 *
 *  Two columns feeding one placeholder is never what someone means — the second silently wins at render
 *  time — so choosing a new column for a field releases the old one rather than leaving both set. */
export function assign(mapping: ColumnMapping, column: string, field: string): ColumnMapping {
  const next: ColumnMapping = {};
  for (const [col, f] of Object.entries(mapping)) {
    if (col !== column && f !== field) next[col] = f;
  }
  if (field) next[column] = field;
  return next;
}

export function unassign(mapping: ColumnMapping, column: string): ColumnMapping {
  const next = { ...mapping };
  delete next[column];
  return next;
}

/** The placeholders that still have no column. Generation stays blocked while any REQUIRED one is here,
 *  because a certificate with a blank name is worse than no certificate. */
export function unmappedFields(fields: MappableField[], mapping: ColumnMapping): MappableField[] {
  const mapped = new Set(Object.values(mapping));
  return fields.filter((f) => !mapped.has(f.key));
}

export function missingRequired(fields: MappableField[], mapping: ColumnMapping): MappableField[] {
  return unmappedFields(fields, mapping).filter((f) => f.required);
}

/** Columns the organiser chose not to use. Shown, not hidden: "we ignored three of your columns" is
 *  something to say out loud, in case one of them was the one that mattered. */
export function unmappedColumns(columns: string[], mapping: ColumnMapping): string[] {
  return columns.filter((c) => c.trim().length > 0 && !(c in mapping));
}

/** Resolves one row into `{ fieldKey: value }` — exactly what the renderer takes.
 *
 *  A short row yields an empty string rather than `undefined`: a ragged tail is normal in a hand-edited
 *  sheet, and the renderer should draw a gap, not the word "undefined". */
export function resolveRow(
  row: string[], columns: string[], mapping: ColumnMapping
): Record<string, string> {
  const values: Record<string, string> = {};
  columns.forEach((column, index) => {
    const field = mapping[column];
    if (field) values[field] = row[index] ?? "";
  });
  return values;
}

/** Rows whose required fields are blank. Surfaced BEFORE generation so the organiser fixes their sheet
 *  rather than discovering four blank certificates in a batch of two hundred. */
export function rowsMissingValues(
  rows: string[][], columns: string[], mapping: ColumnMapping, fields: MappableField[]
): { index: number; missing: string[] }[] {
  const required = fields.filter((f) => f.required);
  const problems: { index: number; missing: string[] }[] = [];

  rows.forEach((row, index) => {
    const values = resolveRow(row, columns, mapping);
    const missing = required
      .filter((f) => !(values[f.key] ?? "").trim())
      .map((f) => f.label);
    if (missing.length) problems.push({ index, missing });
  });

  return problems;
}
