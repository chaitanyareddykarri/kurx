/// Where a locally-originated attachment is in its upload lifecycle.
///
/// A [ChatAttachment] with a status other than [uploaded] has no server row yet — it exists only on
/// this device, inside the pending message that owns it.
enum AttachmentUploadStatus {
  /// Chosen, not yet sent anywhere.
  queued,

  /// Bytes are moving. [ChatAttachment.progress] is meaningful.
  uploading,

  /// Confirmed by the server; [ChatAttachment.id] is a real attachment id.
  uploaded,

  /// The server refused it, or the network gave out. [ChatAttachment.error] says which.
  failed,
}

/// A file on a chat message.
///
/// The same type covers a confirmed server attachment and one still being uploaded, because the
/// message bubble renders both — a pending image should look like the image it will become, not like
/// a placeholder that swaps out.
class ChatAttachment {
  const ChatAttachment({
    required this.id,
    required this.fileName,
    required this.contentType,
    required this.sizeBytes,
    this.url,
    this.width,
    this.height,
    this.localPath,
    this.status = AttachmentUploadStatus.uploaded,
    this.progress = 0,
    this.error,
  });

  /// Server attachment id once confirmed; a local placeholder id before that.
  final String id;

  /// Server-sanitized on confirm. The backend sends no Content-Disposition (it deliberately serves
  /// everything as octet-stream), so this is the only trustworthy filename — never parse the URL.
  final String fileName;

  final String contentType;
  final int sizeBytes;

  /// Short-lived signed download URL. Null while pending, and **never persisted** — a stored URL
  /// would outlive its signature and fail confusingly. Re-fetched on demand instead.
  final String? url;

  final int? width;
  final int? height;

  /// Set only while uploading, so a pending image can be previewed from disk before it exists
  /// server-side.
  final String? localPath;

  final AttachmentUploadStatus status;

  /// 0..1, meaningful while [status] is [AttachmentUploadStatus.uploading].
  final double progress;

  /// Server error code (`file_too_large`, `unsupported_file_type`, `file_infected`, …) or a
  /// transport failure. Rendered to the user, never swallowed.
  final String? error;

  bool get isImage => contentType.startsWith('image/');

  /// D-298 — a voice note that has to be downloaded to be heard is not a voice note, and a video the
  /// reader must open elsewhere is not a video. Both play in place.
  bool get isAudio => contentType.startsWith('audio/');
  bool get isVideo => contentType.startsWith('video/');
  bool get isPending => status != AttachmentUploadStatus.uploaded;
  bool get hasFailed => status == AttachmentUploadStatus.failed;

  ChatAttachment copyWith({
    String? id,
    String? url,
    AttachmentUploadStatus? status,
    double? progress,
    String? error,
    bool clearError = false,
    int? width,
    int? height,
  }) =>
      ChatAttachment(
        id: id ?? this.id,
        fileName: fileName,
        contentType: contentType,
        sizeBytes: sizeBytes,
        url: url ?? this.url,
        width: width ?? this.width,
        height: height ?? this.height,
        localPath: localPath,
        status: status ?? this.status,
        progress: progress ?? this.progress,
        error: clearError ? null : (error ?? this.error),
      );

  /// Human-readable size for the document tile. Binary units, one decimal past KB.
  String get readableSize {
    if (sizeBytes < 1024) return '$sizeBytes B';
    if (sizeBytes < 1024 * 1024) return '${(sizeBytes / 1024).toStringAsFixed(0)} KB';
    return '${(sizeBytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}
