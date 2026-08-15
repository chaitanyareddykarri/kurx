import { describe, expect, it } from "vitest";
import {
  assign, missingRequired, resolveRow, rowsMissingValues, unassign,
  unmappedColumns, unmappedFields, type ColumnMapping, type MappableField
} from "@/lib/certificate-mapping";

const FIELDS: MappableField[] = [
  { key: "participant_name", label: "Participant name", required: true },
  { key: "team_name", label: "Team", required: false },
  { key: "achievement", label: "Achievement", required: false },
];

const COLUMNS = ["Name", "Team", "Award"];
const MAPPING: ColumnMapping = { Name: "participant_name", Team: "team_name" };

describe("assign", () => {
  it("maps a column to a field", () => {
    expect(assign({}, "Name", "participant_name")).toEqual({ Name: "participant_name" });
  });

  /** Two columns feeding one placeholder is never what someone means — the second silently wins at
   *  render time. */
  it("releases whichever column previously fed that field", () => {
    const next = assign({ Name: "participant_name" }, "Full Name", "participant_name");

    expect(next).toEqual({ "Full Name": "participant_name" });
  });

  it("replaces a column's own previous field rather than duplicating it", () => {
    const next = assign({ Name: "team_name" }, "Name", "participant_name");

    expect(next).toEqual({ Name: "participant_name" });
  });

  it("leaves other pairs untouched", () => {
    expect(assign(MAPPING, "Award", "achievement")).toEqual({ ...MAPPING, Award: "achievement" });
  });

  it("clearing a field unmaps the column", () => {
    expect(assign(MAPPING, "Team", "")).toEqual({ Name: "participant_name" });
  });
});

describe("unassign", () => {
  it("removes one column without disturbing the rest", () => {
    expect(unassign(MAPPING, "Team")).toEqual({ Name: "participant_name" });
  });

  it("is a no-op for a column that was never mapped", () => {
    expect(unassign(MAPPING, "Award")).toEqual(MAPPING);
  });

  it("does not mutate its input", () => {
    const original = { ...MAPPING };
    unassign(MAPPING, "Team");
    expect(MAPPING).toEqual(original);
  });
});

describe("what is left over", () => {
  it("reports placeholders with no column", () => {
    expect(unmappedFields(FIELDS, MAPPING).map((f) => f.key)).toEqual(["achievement"]);
  });

  /** Generation stays blocked on these: a certificate with a blank name is worse than no certificate. */
  it("separates the required ones", () => {
    expect(missingRequired(FIELDS, {}).map((f) => f.key)).toEqual(["participant_name"]);
    expect(missingRequired(FIELDS, MAPPING)).toEqual([]);
  });

  /** "We ignored three of your columns" is something to say out loud, in case one of them mattered. */
  it("reports columns the organiser did not use", () => {
    expect(unmappedColumns(COLUMNS, MAPPING)).toEqual(["Award"]);
  });

  it("does not count blank headers as ignored columns", () => {
    expect(unmappedColumns(["Name", "", "  "], { Name: "participant_name" })).toEqual([]);
  });
});

describe("resolveRow", () => {
  it("produces what the renderer takes", () => {
    expect(resolveRow(["Rahul Sharma", "ByteBuilders", "First"], COLUMNS, MAPPING))
      .toEqual({ participant_name: "Rahul Sharma", team_name: "ByteBuilders" });
  });

  /** A ragged tail is normal in a hand-edited sheet; the renderer should draw a gap, not "undefined". */
  it("yields an empty string for a short row", () => {
    expect(resolveRow(["Rahul Sharma"], COLUMNS, MAPPING))
      .toEqual({ participant_name: "Rahul Sharma", team_name: "" });
  });

  it("never reinterprets a value", () => {
    const values = resolveRow(["007", "12/09/2026"], ["Name", "Team"], MAPPING);

    expect(values.participant_name).toBe("007");
    expect(values.team_name).toBe("12/09/2026");
  });

  it("ignores columns that feed nothing", () => {
    expect(resolveRow(["Rahul", "x"], ["Name", "Internal Notes"], { Name: "participant_name" }))
      .toEqual({ participant_name: "Rahul" });
  });
});

describe("rowsMissingValues", () => {
  /** Surfaced before generation, so the organiser fixes their sheet rather than discovering four blank
   *  certificates in a batch of two hundred. */
  it("finds rows whose required field is blank", () => {
    const rows = [["Rahul", "ByteBuilders"], ["", "CodeCrafters"], ["   ", "Alpha"]];

    const problems = rowsMissingValues(rows, COLUMNS, MAPPING, FIELDS);

    expect(problems).toEqual([
      { index: 1, missing: ["Participant name"] },
      { index: 2, missing: ["Participant name"] },
    ]);
  });

  it("says nothing about blank optional fields", () => {
    const problems = rowsMissingValues([["Rahul", ""]], COLUMNS, MAPPING, FIELDS);

    expect(problems).toEqual([]);
  });

  it("is empty when every row is complete", () => {
    expect(rowsMissingValues([["Rahul", "ByteBuilders"]], COLUMNS, MAPPING, FIELDS)).toEqual([]);
  });
});
