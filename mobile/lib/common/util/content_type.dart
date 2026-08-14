/// Maps a file extension to the content type sent alongside a presigned upload.
///
/// Shared by chat attachments and organiser event media — both presign against the same
/// `IStorage` contract, so they must agree on what they claim to be uploading.
///
/// The value is a **hint**: the server re-derives the real type from the bytes and refuses a
/// mismatch, so a wrong guess here can never widen what the backend accepts.
String guessContentType(String? extension) => switch (extension?.toLowerCase()) {
      'jpg' || 'jpeg' => 'image/jpeg',
      'png' => 'image/png',
      'gif' => 'image/gif',
      'webp' => 'image/webp',
      'heic' => 'image/heic',
      'pdf' => 'application/pdf',
      'doc' => 'application/msword',
      'docx' => 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      // Voice notes and video (D-298). Absent, a captured .mp4 went up as octet-stream and the server
      // refused it as an unsupported type — the upload failed for a file it actually accepts.
      'mp3' => 'audio/mpeg',
      'm4a' => 'audio/mp4',
      'ogg' || 'opus' => 'audio/ogg',
      'weba' => 'audio/webm',
      'mp4' => 'video/mp4',
      // iOS camera capture produces QuickTime, which the server accepts as its own type (D-298).
      'mov' => 'video/quicktime',
      'webm' => 'video/webm',
      // Anything unlisted. The server refuses octet-stream, which is the honest outcome: it accepts a
      // closed set of types, and guessing one we do not recognise would only move the refusal later.
      _ => 'application/octet-stream',
    };
