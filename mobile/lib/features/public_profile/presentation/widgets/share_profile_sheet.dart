import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../../../common/widgets/kurx_feedback.dart';
import '../../../../core/network/app_config.dart';
import '../../../../core/theme/design_tokens.dart';

/// Share + QR for a public profile — the Flutter twin of `web/components/profile/share-profile.tsx`.
///
/// **The QR is encoded on-device from [AppConfig.profileUrl].** A server-rendered PNG would have kept
/// the two surfaces byte-identical, but the backend has no configured public web origin — it would
/// have had to guess one from the request host, which for an API call is the *API* host, so every
/// scanned code would open a URL that serves no profile. The client is the only party that reliably
/// knows the right origin.
Future<void> showShareProfileSheet(
  BuildContext context, {
  required String username,
  required String name,
}) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    builder: (_) => _ShareProfileSheet(username: username, name: name),
  );
}

class _ShareProfileSheet extends StatelessWidget {
  const _ShareProfileSheet({required this.username, required this.name});

  final String username;
  final String name;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final url = AppConfig.profileUrl(username);

    return SafeArea(
      child: Container(
        margin: const EdgeInsets.all(KSpace.md),
        padding: const EdgeInsets.all(KSpace.xl),
        decoration: BoxDecoration(
          color: c.cardSurface,
          borderRadius: BorderRadius.circular(KRadius.lg),
          border: Border.all(color: c.border),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'Share @$username',
              style: TextStyle(color: c.text, fontSize: 17, fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: KSpace.xl),

            // White plate and explicit black modules regardless of theme: a QR is read by a camera,
            // and inheriting dark-mode colours inverts it, which many readers refuse to decode.
            Semantics(
              image: true,
              label: 'QR code linking to the profile of $name',
              child: Container(
                padding: const EdgeInsets.all(KSpace.md),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(KRadius.md),
                  border: Border.all(color: c.border),
                ),
                child: QrImageView(
                  data: url,
                  version: QrVersions.auto,
                  size: 200,
                  backgroundColor: Colors.white,
                  // Medium recovery — stays scannable when partly obscured on a phone screen
                  // without inflating module count the way High would at this size.
                  errorCorrectionLevel: QrErrorCorrectLevel.M,
                  eyeStyle: const QrEyeStyle(
                    eyeShape: QrEyeShape.square,
                    color: Colors.black,
                  ),
                  dataModuleStyle: const QrDataModuleStyle(
                    dataModuleShape: QrDataModuleShape.square,
                    color: Colors.black,
                  ),
                ),
              ),
            ),

            const SizedBox(height: KSpace.lg),
            Text(
              "Point a camera at this code to open $name's profile.",
              textAlign: TextAlign.center,
              style: TextStyle(color: c.muted, fontSize: 13, height: 1.4),
            ),

            const SizedBox(height: KSpace.lg),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: KSpace.md, vertical: KSpace.md),
              decoration: BoxDecoration(
                color: c.elevated,
                borderRadius: BorderRadius.circular(KRadius.md),
                border: Border.all(color: c.border),
              ),
              child: Row(
                children: [
                  Icon(Icons.link_rounded, size: 15, color: c.muted),
                  const SizedBox(width: KSpace.sm),
                  Expanded(
                    child: Text(
                      url,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(color: c.text, fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),

            const SizedBox(height: KSpace.lg),
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: () async {
                  await Clipboard.setData(ClipboardData(text: url));
                  if (!context.mounted) return;
                  // Dismiss first: the confirmation belongs to the page behind, and a snackbar
                  // raised over a sheet that is about to close is never read.
                  Navigator.of(context).pop();
                  KurxFeedback.success(context, 'Profile link copied.');
                },
                icon: const Icon(Icons.copy_rounded, size: 16),
                label: const Text('Copy link'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
