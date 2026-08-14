import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AttachmentView } from "@/components/chat/attachment-view";
import { VoiceRecorder } from "@/components/chat/voice-recorder";
import { isAudio, isImage, isVideo } from "@/lib/chat-upload";
import type { ChatAttachment } from "@/lib/chat-api";

vi.mock("@/lib/api", () => ({
  api: { get: vi.fn(), post: vi.fn(), delete: vi.fn(), patch: vi.fn() }
}));
vi.mock("@/lib/chat-actions", () => ({ attachmentUrlAction: vi.fn() }));

function attachment(extra: Partial<ChatAttachment> = {}): ChatAttachment {
  return {
    id: "a1",
    url: "https://signed.example/file",
    fileName: "clip.mp4",
    contentType: "video/mp4",
    sizeBytes: 4096,
    ...extra
  };
}

/// Minimal MediaRecorder stand-in. jsdom has none, and the recorder is the one piece of this that
/// cannot be exercised any other way.
class FakeMediaRecorder {
  static supported = ["audio/webm;codecs=opus", "audio/webm"];
  static isTypeSupported = (t: string) => FakeMediaRecorder.supported.includes(t);
  static last: FakeMediaRecorder | null = null;

  state: "inactive" | "recording" = "inactive";
  ondataavailable: ((e: { data: Blob }) => void) | null = null;
  onstop: (() => void) | null = null;

  constructor(_stream: MediaStream, readonly options: { mimeType: string }) {
    FakeMediaRecorder.last = this;
  }
  start() {
    this.state = "recording";
  }
  stop() {
    this.state = "inactive";
    this.ondataavailable?.({ data: new Blob(["audio-bytes"], { type: "audio/webm" }) });
    this.onstop?.();
  }
}

const tracks = { stop: vi.fn() };
const getUserMedia = vi.fn();

beforeEach(() => {
  tracks.stop.mockReset();
  getUserMedia.mockReset();
  getUserMedia.mockResolvedValue({ getTracks: () => [tracks] } as unknown as MediaStream);
  FakeMediaRecorder.last = null;
  FakeMediaRecorder.supported = ["audio/webm;codecs=opus", "audio/webm"];
  vi.stubGlobal("MediaRecorder", FakeMediaRecorder);
  vi.stubGlobal("navigator", { ...navigator, mediaDevices: { getUserMedia } });
});

describe("media type predicates (D-298)", () => {
  it("classifies each family exactly once", () => {
    expect(isAudio("audio/ogg")).toBe(true);
    expect(isVideo("audio/ogg")).toBe(false);
    expect(isVideo("video/mp4")).toBe(true);
    expect(isImage("video/mp4")).toBe(false);
    // A document must fall through all three to the file card.
    expect([isImage, isAudio, isVideo].map((f) => f("application/pdf"))).toEqual([
      false,
      false,
      false
    ]);
  });
});

describe("AttachmentView media (D-298)", () => {
  it("plays a voice note in place rather than offering it as a download", () => {
    render(
      <AttachmentView
        attachment={attachment({ contentType: "audio/ogg", fileName: "voice-note.ogg" })}
      />
    );
    expect(screen.getByLabelText("Voice note voice-note.ogg")).toBeInTheDocument();
  });

  it("plays a video in place", () => {
    render(<AttachmentView attachment={attachment()} />);
    expect(screen.getByLabelText("Video clip.mp4")).toBeInTheDocument();
  });

  it("loads only metadata, so a room of voice notes does not download on open", () => {
    render(
      <AttachmentView
        attachment={attachment({ contentType: "audio/ogg", fileName: "v.ogg" })}
      />
    );
    expect(screen.getByLabelText("Voice note v.ogg")).toHaveAttribute("preload", "metadata");
  });

  it("keeps an in-flight recording as a document card, because it has no URL to play yet", () => {
    render(
      <AttachmentView
        attachment={attachment({ contentType: "audio/ogg", fileName: "v.ogg", url: "" })}
        pending={{
          localId: "l1",
          file: new File(["x"], "v.ogg", { type: "audio/ogg" }),
          fileName: "v.ogg",
          contentType: "audio/ogg",
          sizeBytes: 4096,
          status: "uploading",
          progress: 0.5
        }}
      />
    );
    expect(screen.queryByLabelText("Voice note v.ogg")).not.toBeInTheDocument();
    expect(screen.getByText("v.ogg")).toBeInTheDocument();
  });
});

describe("VoiceRecorder (D-298)", () => {
  it("hands the finished recording to the caller as an ordinary file", async () => {
    const onRecorded = vi.fn();
    render(<VoiceRecorder onRecorded={onRecorded} />);

    await userEvent.click(screen.getByLabelText("Record a voice note"));
    await screen.findByLabelText("Finish recording");
    await userEvent.click(screen.getByLabelText("Finish recording"));

    expect(onRecorded).toHaveBeenCalledTimes(1);
    const file: File = onRecorded.mock.calls[0][0];
    // The base type, not "audio/webm;codecs=opus" — the server matches on the MIME type and has no
    // entry for one carrying a codec parameter, so the upload would be refused.
    expect(file.type).toBe("audio/webm");
    expect(file.name).toMatch(/^voice-note-\d+\.weba$/);
  });

  it("releases the microphone when the recording finishes", async () => {
    render(<VoiceRecorder onRecorded={vi.fn()} />);
    await userEvent.click(screen.getByLabelText("Record a voice note"));
    await userEvent.click(await screen.findByLabelText("Finish recording"));

    // A live getUserMedia track keeps the browser's recording indicator lit. Leaving it on after the
    // user believes they stopped is the failure nobody forgives.
    expect(tracks.stop).toHaveBeenCalled();
  });

  it("releases the microphone and sends nothing when the recording is discarded", async () => {
    const onRecorded = vi.fn();
    render(<VoiceRecorder onRecorded={onRecorded} />);
    await userEvent.click(screen.getByLabelText("Record a voice note"));
    await userEvent.click(await screen.findByLabelText("Discard recording"));

    expect(onRecorded).not.toHaveBeenCalled();
    expect(tracks.stop).toHaveBeenCalled();
  });

  it("says so when the microphone is unavailable instead of appearing to record", async () => {
    getUserMedia.mockRejectedValue(new Error("NotAllowedError"));
    render(<VoiceRecorder onRecorded={vi.fn()} />);

    await userEvent.click(screen.getByLabelText("Record a voice note"));

    expect(await screen.findByText("Microphone unavailable.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Finish recording")).not.toBeInTheDocument();
  });

  it("says so when the browser supports no recordable audio format", async () => {
    FakeMediaRecorder.supported = [];
    render(<VoiceRecorder onRecorded={vi.fn()} />);

    await userEvent.click(screen.getByLabelText("Record a voice note"));

    // Constructing MediaRecorder with an unsupported mimeType throws, so the format is picked before
    // the microphone is ever requested — no permission prompt for a recording that cannot happen.
    expect(await screen.findByText("This browser cannot record audio.")).toBeInTheDocument();
    expect(getUserMedia).not.toHaveBeenCalled();
  });
});
