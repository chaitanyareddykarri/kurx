"use client";

import { Mic, Square, Trash2 } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

/// Voice notes (D-298). Records with the browser's own `MediaRecorder` — no library: this is four
/// calls, and an audio dependency on the message composer is more code to audit than the code it saves.
///
/// The recording is handed to the SAME attachment pipeline as any other file, so the server's size,
/// MIME, magic-byte and malware checks all apply unchanged. Nothing about a voice note is trusted
/// because it came from our own recorder.
///
/// The microphone track is stopped on every exit path — finish, discard, unmount, an error. A live
/// `getUserMedia` track keeps the browser's recording indicator lit, and a UI that leaves it lit after
/// the user thinks they stopped is the kind of thing people rightly never forgive.
const MAX_SECONDS = 300;

/// `audio/webm;codecs=opus` is what Chrome and Firefox produce; Safari only offers mp4. Picked at
/// runtime because an unsupported mimeType makes the constructor throw.
function pickMimeType(): { mimeType: string; extension: string } | null {
  if (typeof MediaRecorder === "undefined") return null;
  const candidates: { mimeType: string; extension: string }[] = [
    { mimeType: "audio/webm;codecs=opus", extension: "weba" },
    { mimeType: "audio/webm", extension: "weba" },
    { mimeType: "audio/mp4", extension: "m4a" },
    { mimeType: "audio/ogg;codecs=opus", extension: "ogg" }
  ];
  return candidates.find((c) => MediaRecorder.isTypeSupported(c.mimeType)) ?? null;
}

export function VoiceRecorder({ onRecorded }: { onRecorded: (file: File) => void }) {
  const [recording, setRecording] = useState(false);
  const [seconds, setSeconds] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const recorder = useRef<MediaRecorder | null>(null);
  const stream = useRef<MediaStream | null>(null);
  const chunks = useRef<Blob[]>([]);
  // Set when the user discards, so the stop handler knows not to hand the blob on.
  const discarded = useRef(false);

  const releaseMic = useCallback(() => {
    stream.current?.getTracks().forEach((t) => t.stop());
    stream.current = null;
    recorder.current = null;
  }, []);

  // Unmounting mid-recording must not leave the microphone open.
  useEffect(() => releaseMic, [releaseMic]);

  useEffect(() => {
    if (!recording) return;
    const timer = setInterval(() => setSeconds((s) => s + 1), 1000);
    return () => clearInterval(timer);
  }, [recording]);

  const stop = useCallback(() => {
    recorder.current?.state === "recording" && recorder.current.stop();
  }, []);

  // A cap rather than an open-ended recording: the upload has a size limit the server enforces, and
  // discovering that after speaking for ten minutes is a worse experience than being stopped at five.
  useEffect(() => {
    if (recording && seconds >= MAX_SECONDS) stop();
  }, [recording, seconds, stop]);

  async function start() {
    setError(null);
    const format = pickMimeType();
    if (!format) {
      setError("This browser cannot record audio.");
      return;
    }

    try {
      const media = await navigator.mediaDevices.getUserMedia({ audio: true });
      stream.current = media;
      chunks.current = [];
      discarded.current = false;

      const rec = new MediaRecorder(media, { mimeType: format.mimeType });
      rec.ondataavailable = (e) => e.data.size > 0 && chunks.current.push(e.data);
      rec.onstop = () => {
        releaseMic();
        setRecording(false);
        if (discarded.current || chunks.current.length === 0) return;

        // The base type without the codec parameter: the server matches on the MIME type, and
        // "audio/webm;codecs=opus" is not a type it has an entry for.
        const contentType = format.mimeType.split(";")[0];
        const blob = new Blob(chunks.current, { type: contentType });
        onRecorded(
          new File([blob], `voice-note-${Date.now()}.${format.extension}`, { type: contentType })
        );
      };

      recorder.current = rec;
      rec.start();
      setSeconds(0);
      setRecording(true);
    } catch {
      // Denied permission, no microphone, or an insecure origin. All three read the same to the user
      // and none of them is worth distinguishing in a composer.
      releaseMic();
      setError("Microphone unavailable.");
    }
  }

  function discard() {
    discarded.current = true;
    stop();
    releaseMic();
    setRecording(false);
  }

  if (!recording) {
    return (
      <>
        <button
          type="button"
          onClick={() => void start()}
          aria-label="Record a voice note"
          title="Record a voice note"
          className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-md border border-border text-muted hover:bg-elevated hover:text-text"
        >
          <Mic size={16} aria-hidden />
        </button>
        {error && (
          <span role="status" className="text-xs text-danger">
            {error}
          </span>
        )}
      </>
    );
  }

  return (
    <span className="inline-flex h-10 shrink-0 items-center gap-2 rounded-md border border-danger/40 px-2">
      {/* The elapsed time is the recording indicator: an animated dot alone would say nothing to a
          screen reader, and nothing at all to someone who cannot see colour. */}
      <span aria-live="polite" className="text-xs tabular-nums text-danger">
        Recording {Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, "0")}
      </span>
      <button
        type="button"
        onClick={stop}
        aria-label="Finish recording"
        title="Finish recording"
        className="inline-flex h-10 w-10 items-center justify-center text-muted hover:text-text lg:h-auto lg:w-auto"
      >
        <Square size={14} aria-hidden />
      </button>
      <button
        type="button"
        onClick={discard}
        aria-label="Discard recording"
        title="Discard recording"
        className="inline-flex h-10 w-10 items-center justify-center text-muted hover:text-text lg:h-auto lg:w-auto"
      >
        <Trash2 size={14} aria-hidden />
      </button>
    </span>
  );
}
