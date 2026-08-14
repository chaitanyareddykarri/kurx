import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/chat_attachment.dart';
import '../providers/chat_providers.dart';
import 'chat_media_player.dart';

/// Renders one attachment inside a message bubble.
///
/// Images get a thumbnail with a tap-to-zoom preview; everything else gets a document tile. A file
/// still uploading renders as the thing it will become — with a progress ring over it — rather than
/// as a placeholder that swaps out, so the layout does not jump when it confirms.
class ChatAttachmentView extends ConsumerWidget {
  const ChatAttachmentView({
    super.key,
    required this.attachment,
    required this.onDismissFailed,
  });

  final ChatAttachment attachment;
  final VoidCallback onDismissFailed;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (attachment.hasFailed) return _FailedTile(attachment: attachment, onDismiss: onDismissFailed);
    if (attachment.isImage) return _ImageAttachment(attachment: attachment);
    // D-298 — media plays in place, but only once it exists on the server: an upload still in flight
    // has no id to mint a URL for, so it keeps the document card and its progress.
    if (!attachment.isPending && attachment.isAudio) return ChatAudioPlayer(attachment: attachment);
    if (!attachment.isPending && attachment.isVideo) return ChatVideoPlayer(attachment: attachment);
    return _DocumentAttachment(attachment: attachment);
  }
}

/// Opens an attachment through the platform handler.
///
/// The URL is fetched at the moment of use and never cached: signatures expire, and the server
/// re-checks room membership on every mint, so a stale URL would fail in a way the user cannot act
/// on. Nothing is written to the device — the OS handles the download.
Future<void> _openAttachment(BuildContext context, WidgetRef ref, ChatAttachment attachment) async {
  final messenger = ScaffoldMessenger.of(context);
  try {
    final url = await ref.read(chatRepositoryProvider).attachmentUrl(attachment.id);
    final launched = await launchUrl(Uri.parse(url), mode: LaunchMode.externalApplication);
    if (!launched) {
      messenger.showSnackBar(const SnackBar(content: Text('No app can open this file.')));
    }
  } catch (_) {
    // Most often offline. Say so plainly rather than failing silently.
    messenger.showSnackBar(
      const SnackBar(content: Text('This file is unavailable. Check your connection and try again.')),
    );
  }
}

class _ImageAttachment extends ConsumerWidget {
  const _ImageAttachment({required this.attachment});
  final ChatAttachment attachment;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    // A pending upload previews from the local file; a confirmed one from its signed URL.
    final local = attachment.localPath;
    final provider = local != null
        ? FileImage(File(local)) as ImageProvider
        : (attachment.url != null ? NetworkImage(attachment.url!) : null);

    return Semantics(
      label: 'Image attachment ${attachment.fileName}',
      button: true,
      child: Padding(
        padding: const EdgeInsets.only(bottom: KSpace.xs),
        child: ClipRRect(
          borderRadius: BorderRadius.circular(KRadius.md),
          child: Stack(
            alignment: Alignment.center,
            children: [
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 260, maxHeight: 260),
                child: provider == null
                    ? _Placeholder(color: c.elevated)
                    : Semantics(
                        button: !attachment.isPending,
                        label: attachment.isPending
                            ? 'Attachment still uploading'
                            : 'Open ${attachment.fileName}',
                        child: GestureDetector(
                        onTap: attachment.isPending
                            ? null
                            : () => _openPreview(context, provider, attachment.fileName),
                        child: Image(
                          // ResizeImage bounds the decode so a 25MB photo is never decoded at full
                          // size on the UI thread just to fill a 260px thumbnail.
                          image: ResizeImage(provider, width: 520, allowUpscaling: false),
                          fit: BoxFit.cover,
                          loadingBuilder: (context, child, progress) =>
                              progress == null ? child : _Placeholder(color: c.elevated),
                          errorBuilder: (_, _, _) => _Placeholder(color: c.elevated),
                        ),
                        ),
                      ),
              ),
              if (attachment.status == AttachmentUploadStatus.uploading)
                _UploadOverlay(progress: attachment.progress),
            ],
          ),
        ),
      ),
    );
  }

  /// Full-screen zoomable preview. InteractiveViewer is built in — no gallery package needed.
  void _openPreview(BuildContext context, ImageProvider provider, String fileName) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        fullscreenDialog: true,
        builder: (_) => Scaffold(
          backgroundColor: Colors.black,
          appBar: AppBar(
            backgroundColor: Colors.black,
            foregroundColor: Colors.white,
            title: Text(fileName, style: const TextStyle(fontSize: 14)),
          ),
          body: Center(
            child: InteractiveViewer(
              minScale: 1,
              maxScale: 4,
              child: Image(image: provider, fit: BoxFit.contain),
            ),
          ),
        ),
      ),
    );
  }
}

class _DocumentAttachment extends ConsumerWidget {
  const _DocumentAttachment({required this.attachment});
  final ChatAttachment attachment;

  static IconData _iconFor(String contentType) {
    if (contentType == 'application/pdf') return Icons.picture_as_pdf_rounded;
    if (contentType.startsWith('text/')) return Icons.description_outlined;
    if (contentType == 'application/zip') return Icons.folder_zip_outlined;
    if (contentType.contains('word')) return Icons.article_outlined;
    if (contentType.contains('sheet')) return Icons.table_chart_outlined;
    if (contentType.contains('presentation')) return Icons.slideshow_outlined;
    return Icons.insert_drive_file_outlined;   // unknown type: still a usable tile
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final c = context.kurx;
    final uploading = attachment.status == AttachmentUploadStatus.uploading;

    return Semantics(
      label: '${attachment.fileName}, ${attachment.readableSize}'
          '${uploading ? ', uploading ${(attachment.progress * 100).round()} percent' : ''}',
      button: !attachment.isPending,
      child: Padding(
        padding: const EdgeInsets.only(bottom: KSpace.xs),
        child: InkWell(
          onTap: attachment.isPending ? null : () => _openAttachment(context, ref, attachment),
          borderRadius: BorderRadius.circular(KRadius.md),
          child: Container(
            // Comfortably above the 48dp minimum touch target.
            constraints: const BoxConstraints(minWidth: 220, minHeight: 56),
            padding: const EdgeInsets.all(KSpace.sm),
            decoration: BoxDecoration(
              color: c.elevated,
              borderRadius: BorderRadius.circular(KRadius.md),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                SizedBox(
                  width: 36,
                  height: 36,
                  child: uploading
                      ? CircularProgressIndicator(
                          value: attachment.progress > 0 ? attachment.progress : null,
                          strokeWidth: 2,
                        )
                      : Icon(_iconFor(attachment.contentType), color: c.accent),
                ),
                const SizedBox(width: KSpace.sm),
                Flexible(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        attachment.fileName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(color: c.text, fontSize: 13, fontWeight: FontWeight.w600),
                      ),
                      Text(
                        uploading
                            ? 'Uploading… ${(attachment.progress * 100).round()}%'
                            : attachment.readableSize,
                        style: TextStyle(color: c.muted, fontSize: 11.5),
                      ),
                    ],
                  ),
                ),
                if (!attachment.isPending) ...[
                  const SizedBox(width: KSpace.xs),
                  Icon(Icons.download_rounded, size: 18, color: c.muted),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// A refusal from the server, shown with the reason. "File too large" is actionable;
/// "upload failed" is not, so the code is translated rather than swallowed.
class _FailedTile extends StatelessWidget {
  const _FailedTile({required this.attachment, required this.onDismiss});

  final ChatAttachment attachment;
  final VoidCallback onDismiss;

  static String describe(String? code) => switch (code) {
        'file_too_large' => 'That file is over the 25 MB limit.',
        'unsupported_file_type' => 'That file type is not allowed.',
        'extension_mismatch' => 'That file extension does not match its contents.',
        'file_infected' => 'That file was rejected by a security scan.',
        'scan_unavailable' => 'The file could not be checked right now. Try again shortly.',
        'upload_not_allowed' => 'You cannot upload files in this chat.',
        'upload_not_found' => 'The upload expired. Pick the file again.',
        // Kept in step with the web client's describeUploadError (D-113): the same server code must
        // read the same way on both platforms.
        'forbidden' => 'You do not have permission to upload here.',
        'network' || 'upload_failed' => 'The upload was interrupted. Check your connection and retry.',
        _ => 'That file could not be uploaded.',
      };

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Padding(
      padding: const EdgeInsets.only(bottom: KSpace.xs),
      child: Container(
        padding: const EdgeInsets.all(KSpace.sm),
        decoration: BoxDecoration(
          border: Border.all(color: c.danger),
          borderRadius: BorderRadius.circular(KRadius.md),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.error_outline_rounded, size: 18, color: c.danger),
            const SizedBox(width: KSpace.sm),
            Flexible(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(attachment.fileName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(color: c.text, fontSize: 13, fontWeight: FontWeight.w600)),
                  Text(describe(attachment.error),
                      style: TextStyle(color: c.danger, fontSize: 11.5)),
                ],
              ),
            ),
            IconButton(
              onPressed: onDismiss,
              icon: const Icon(Icons.close_rounded, size: 18),
              tooltip: 'Dismiss',
            ),
          ],
        ),
      ),
    );
  }
}

class _UploadOverlay extends StatelessWidget {
  const _UploadOverlay({required this.progress});
  final double progress;

  @override
  Widget build(BuildContext context) => Container(
        color: Colors.black.withValues(alpha: 0.35),
        padding: const EdgeInsets.all(KSpace.lg),
        child: Center(
          child: CircularProgressIndicator(
            value: progress > 0 ? progress : null,
            color: Colors.white,
            strokeWidth: 3,
          ),
        ),
      );
}

class _Placeholder extends StatelessWidget {
  const _Placeholder({required this.color});
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
        width: 200,
        height: 140,
        color: color,
        child: const Center(child: Icon(Icons.image_outlined, size: 28)),
      );
}
