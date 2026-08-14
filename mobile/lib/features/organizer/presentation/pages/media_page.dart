import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../common/util/content_type.dart';
import '../../../../common/widgets/async_value_view.dart';
import '../../../../common/widgets/empty_state.dart';
import '../../../../core/network/api_error.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../data/models/event_content_dto.dart';
import '../providers/event_content_providers.dart';
import '../widgets/organizer_form_sheet.dart';

/// Event media gallery — `MediaEndpoints`.
///
/// Upload is presign → PUT → attach, three calls behind one button. `withData: true` gives the
/// bytes directly so the PUT never needs device storage permission, matching how chat attachments
/// already work.
///
/// **The list is read off the org-scoped event detail**, because there is no `GET …/media` route —
/// `ToEventJson` carries the `media` array and attach/remove reply only `{ok:true}`.
class MediaPage extends ConsumerStatefulWidget {
  const MediaPage({super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  ConsumerState<MediaPage> createState() => _MediaPageState();
}

class _MediaPageState extends ConsumerState<MediaPage> {
  bool _uploading = false;

  OrgEventRef get _ref => (orgId: widget.orgId, eventId: widget.eventId);

  Future<void> _pickAndUpload() async {
    final picked = await FilePicker.pickFiles(type: FileType.any, withData: true);
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    setState(() => _uploading = true);
    try {
      await ref.read(eventContentActionsProvider).uploadMedia(
            _ref,
            bytes: bytes,
            contentType: guessContentType(file.extension),
            kind: file.extension == 'pdf' ? 'document' : 'gallery',
            caption: file.name,
          );
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('Uploaded ${file.name}.')));
      }
    } on ApiError catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
      }
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  /// The banner is a field on the event, not a gallery row (D-302), so this ends in a PATCH rather
  /// than an attach. Image types only — this key is rendered in an `Image.network` on public surfaces.
  static const _bannerTypes = ['jpg', 'jpeg', 'png', 'webp', 'avif'];

  Future<void> _pickAndUploadBanner() async {
    final picked = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: _bannerTypes,
      withData: true,
    );
    final file = picked?.files.singleOrNull;
    final bytes = file?.bytes;
    if (file == null || bytes == null) return;

    // FilePicker's extension filter is advisory on some platforms, so the check is repeated here
    // rather than trusted.
    if (!_bannerTypes.contains(file.extension?.toLowerCase())) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Use a JPEG, PNG, WebP or AVIF image.')),
        );
      }
      return;
    }

    setState(() => _uploading = true);
    try {
      await ref.read(eventContentActionsProvider).uploadBanner(
            _ref,
            bytes: bytes,
            contentType: guessContentType(file.extension),
          );
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('Banner updated.')));
      }
    } on ApiError catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
      }
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(
        title: const Text('Media Gallery'),
        actions: [
          if (_uploading)
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: KSpace.lg),
              child: Center(
                child: SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            )
          else
            TextButton.icon(
              onPressed: _pickAndUpload,
              icon: const Icon(Icons.upload_outlined, size: 18),
              label: const Text('Upload'),
            ),
        ],
      ),
      body: Column(
        children: [
          // Banner above the gallery: it is the only image that reaches a discovery card, and until
          // D-302 nothing in either client could set it.
          _BannerCard(
            banner: ref.watch(eventBannerProvider(_ref)),
            busy: _uploading,
            onPick: _pickAndUploadBanner,
            onRemove: () => confirmAndRun(
              context,
              title: 'Remove banner',
              message: 'Attendees will see generated artwork on this event\'s card instead.',
              confirmLabel: 'Remove',
              successMessage: 'Banner removed.',
              action: () => ref.read(eventContentActionsProvider).removeBanner(_ref),
            ),
          ),
          const Divider(height: 1),
          Expanded(child: _gallery(context)),
        ],
      ),
    );
  }

  Widget _gallery(BuildContext context) {
    return RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(eventMediaProvider(_ref));
          await ref.read(eventMediaProvider(_ref).future);
        },
        child: AsyncValueView(
          value: ref.watch(eventMediaProvider(_ref)),
          onRetry: () => ref.invalidate(eventMediaProvider(_ref)),
          isEmpty: (list) => list.isEmpty,
          empty: EmptyState(
            icon: Icons.photo_library_outlined,
            title: 'No media yet',
            message: 'Upload photos, videos and documents for this event.',
            actionLabel: 'Upload',
            onAction: _uploading ? null : _pickAndUpload,
          ),
          data: (items) => ListView.separated(
            padding: const EdgeInsets.all(KSpace.lg),
            itemCount: items.length,
            separatorBuilder: (_, _) => const SizedBox(height: KSpace.sm),
            itemBuilder: (_, i) => _MediaTile(
              media: items[i],
              onDelete: () => confirmAndRun(
                context,
                title: 'Remove media',
                message: 'Remove ${items[i].caption ?? 'this file'} from the event gallery?',
                confirmLabel: 'Remove',
                successMessage: 'Removed.',
                action: () =>
                    ref.read(eventContentActionsProvider).removeMedia(_ref, items[i].id),
              ),
            ),
          ),
        ),
      );
  }
}

/// The event banner, with its own preview / replace / remove (D-302). Deliberately not a `_MediaTile`:
/// the gallery lists `event_media` rows keyed by id, and the banner is a single field on the event.
class _BannerCard extends StatelessWidget {
  const _BannerCard({
    required this.banner,
    required this.busy,
    required this.onPick,
    required this.onRemove,
  });

  final AsyncValue<String?> banner;
  final bool busy;
  final VoidCallback onPick;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final url = banner.valueOrNull;

    return Padding(
      padding: const EdgeInsets.all(KSpace.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Banner',
              style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 15)),
          const SizedBox(height: KSpace.xs),
          Text('Shown on this event\'s card in discovery and at the top of its page.',
              style: TextStyle(color: c.muted, fontSize: 12)),
          const SizedBox(height: KSpace.md),
          AspectRatio(
            aspectRatio: 3 / 2,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(KRadius.md),
              child: ColoredBox(
                color: c.elevated,
                child: url == null
                    ? Center(
                        child: Text(
                          banner.isLoading ? '' : 'No banner yet',
                          style: TextStyle(color: c.muted, fontSize: 13),
                        ),
                      )
                    : Image.network(url, fit: BoxFit.cover),
              ),
            ),
          ),
          const SizedBox(height: KSpace.sm),
          Row(
            children: [
              TextButton.icon(
                onPressed: busy ? null : onPick,
                icon: const Icon(Icons.image_outlined, size: 18),
                label: Text(url == null ? 'Upload banner' : 'Replace'),
              ),
              if (url != null)
                TextButton(
                  onPressed: busy ? null : onRemove,
                  child: const Text('Remove'),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

class _MediaTile extends StatelessWidget {
  const _MediaTile({required this.media, required this.onDelete});

  final EventMediaDto media;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final isDoc = media.kind == 'document';

    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.xs),
      tileColor: c.cardSurface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(KRadius.md)),
      leading: CircleAvatar(
        backgroundColor: c.accent.withValues(alpha: 0.15),
        child: Icon(
          isDoc ? Icons.description_outlined : Icons.image_outlined,
          color: c.accent,
          size: 20,
        ),
      ),
      title: Text(
        media.caption?.isNotEmpty == true ? media.caption! : media.key.split('/').last,
        style: TextStyle(color: c.text),
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      subtitle: Text(isDoc ? 'Document' : 'Gallery', style: TextStyle(color: c.muted)),
      trailing: IconButton(
        onPressed: onDelete,
        icon: const Icon(Icons.delete_outline),
        tooltip: 'Remove from gallery',
      ),
    );
  }
}
