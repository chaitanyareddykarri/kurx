import 'package:flutter/material.dart';

import '../../../../common/widgets/content_width.dart';

/// A simple scrollable static-text page for Privacy / Terms.
class LegalPage extends StatelessWidget {
  const LegalPage({super.key, required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: ContentWidth(
        child: ListView(
          padding: const EdgeInsets.all(20),
          children: [
            Text(body, style: Theme.of(context).textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}

class LegalText {
  const LegalText._();

  static const privacy =
      'Kurx respects your privacy. This attendee app stores your login tokens only in your '
      'device\'s secure storage (Keychain/Keystore) and never shares them. Public event data '
      'you browse is fetched from the Kurx API; no analytics or advertising identifiers are '
      'collected in this build.\n\nThis is placeholder policy text for the mobile client and '
      'does not replace the official Kurx privacy policy.';

  static const terms =
      'By using the Kurx app you agree to browse and (when signed in) register for events in '
      'accordance with the Kurx terms of service. Event availability, pricing and refunds are '
      'governed by each organiser.\n\nThis is placeholder terms text for the mobile client and '
      'does not replace the official Kurx terms of service.';
}
