// @vitest-environment node
//
// Node, deliberately. The rest of the web suite runs in jsdom, where axios picks the XHR adapter — but
// this upload runs inside a Next server action, on the Node adapter, and the bug being pinned here is a
// property of what THAT adapter puts on the wire.

import axios from "axios";
import http from "node:http";
import { afterAll, beforeAll, describe, expect, it } from "vitest";

/**
 * The file has to actually leave the process (D-355, Phase 6).
 *
 * The shared axios instance defaults to `Content-Type: application/json`. Handed FormData with that
 * header, axios does not warn or fail — it serialises the form to JSON, so the body becomes `{"file":{}}`
 * and the participant list is silently dropped. The upload then fails somewhere far away, looking like a
 * slow or broken server rather than a client that never sent anything.
 *
 * This pins the behaviour against a real HTTP server rather than a mock, because the bug lives in what
 * axios puts on the wire — exactly what a mocked adapter would paper over.
 */

const CSV = "Name,Team\nRahul Sharma,ByteBuilders\n";

let server: http.Server;
let received: { contentType?: string; body: string };
let baseURL: string;

beforeAll(async () => {
  server = http.createServer((req, res) => {
    let body = "";
    req.on("data", (c) => (body += c));
    req.on("end", () => {
      received = { contentType: req.headers["content-type"], body };
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end("{}");
    });
  });
  await new Promise<void>((resolve) => server.listen(0, resolve));
  baseURL = `http://127.0.0.1:${(server.address() as { port: number }).port}/`;
});

afterAll(() => server.close());

/** The same instance shape `web/lib/api.ts` creates. Built lazily: the port is only known once the
 *  server is listening. */
const api = () => axios.create({ baseURL, headers: { "Content-Type": "application/json" } });

function form() {
  const body = new FormData();
  body.append("file", new File([new TextEncoder().encode(CSV)], "participants.csv", {
    type: "text/csv",
  }));
  return body;
}

/** What `uploadHeaders` produces. */
const uploadHeaders = { Authorization: "Bearer token", "Content-Type": undefined };

describe("multipart uploads", () => {
  it("sends the file as multipart with a boundary", async () => {
    await api().post("/", form(), { headers: uploadHeaders });

    expect(received.contentType).toMatch(/^multipart\/form-data; boundary=/);
  });

  it("puts the file's actual bytes on the wire", async () => {
    await api().post("/", form(), { headers: uploadHeaders });

    expect(received.body).toContain("Rahul Sharma,ByteBuilders");
    expect(received.body).toContain('filename="participants.csv"');
  });

  /** The regression itself: this is what the code did before, and it fails silently. Asserted so the
   *  reason the header override exists is visible rather than folklore. */
  it("would silently drop the file if the JSON content type were left in place", async () => {
    await api().post("/", form(), { headers: { Authorization: "Bearer token" } });

    expect(received.contentType).toBe("application/json");
    expect(received.body).toBe('{"file":{}}');
    expect(received.body).not.toContain("Rahul Sharma");
  });
});
