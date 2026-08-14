import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';

/// Static FAQ page (D-060 — no backend, no chat integration).
class HelpSupportPage extends StatefulWidget {
  const HelpSupportPage({super.key});

  @override
  State<HelpSupportPage> createState() => _HelpSupportPageState();
}

class _HelpSupportPageState extends State<HelpSupportPage> {
  String _query = '';
  String? _category;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final filtered = _faqs.where((f) {
      final matchesCategory =
          _category == null || f.category == _category;
      final matchesQuery = _query.isEmpty ||
          f.question.toLowerCase().contains(_query.toLowerCase()) ||
          f.answer.toLowerCase().contains(_query.toLowerCase());
      return matchesCategory && matchesQuery;
    }).toList();

    return Scaffold(
      backgroundColor: c.background,
      appBar: AppBar(title: const Text('Help & Support')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(
                KSpace.lg, KSpace.md, KSpace.lg, 0),
            child: TextField(
              onChanged: (v) => setState(() => _query = v),
              decoration: const InputDecoration(
                hintText: 'Search FAQ…',
                prefixIcon: Icon(Icons.search_rounded),
              ),
            ),
          ),
          const SizedBox(height: KSpace.md),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: KSpace.lg),
            child: Row(
              children: [
                for (final cat in ['Booking', 'Payments', 'Tickets', 'Account'])
                  Padding(
                    padding: const EdgeInsets.only(right: KSpace.sm),
                    child: Semantics(
                      button: true,
                      selected: _category == cat,
                      label: cat,
                      child: GestureDetector(
                      onTap: () => setState(() {
                        _category = _category == cat ? null : cat;
                      }),
                      child: AnimatedContainer(
                        duration: context.motion(KMotion.fast),
                        padding: const EdgeInsets.symmetric(
                            horizontal: KSpace.lg, vertical: KSpace.sm),
                        decoration: BoxDecoration(
                          color: _category == cat ? c.accent : c.elevated,
                          borderRadius: BorderRadius.circular(KRadius.pill),
                        ),
                        child: Text(
                          cat,
                          style: TextStyle(
                            color: _category == cat ? c.onAccent : c.muted,
                            fontWeight: FontWeight.w700,
                            fontSize: 13,
                          ),
                        ),
                      ),
                    ),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(height: KSpace.md),
          Expanded(
            child: filtered.isEmpty
                ? Center(
                    child: Text('No results found',
                        style: TextStyle(color: c.muted)),
                  )
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(
                        KSpace.lg, 0, KSpace.lg, KSpace.xxl),
                    itemCount: filtered.length,
                    separatorBuilder: (_, _) =>
                        Divider(color: c.border),
                    itemBuilder: (_, i) => _FaqTile(faq: filtered[i]),
                  ),
          ),
        ],
      ),
    );
  }
}

class _Faq {
  const _Faq(this.question, this.answer, this.category);
  final String question;
  final String answer;
  final String category;
}

const _faqs = [
  _Faq(
    'How do I book a ticket?',
    'Open any event, choose your ticket type and quantity, fill in your details, and tap "Proceed to Payment". Your ticket will arrive in My Tickets once payment is confirmed.',
    'Booking',
  ),
  _Faq(
    'Can I book for someone else?',
    'Yes — toggle "Guest checkout" on the booking screen and enter the attendee\'s details. The ticket will be linked to your account.',
    'Booking',
  ),
  _Faq(
    'Can I cancel my booking?',
    'Cancellations are subject to the event organizer\'s policy. Check the event\'s Refund Policy before booking.',
    'Booking',
  ),
  _Faq(
    'What payment methods are accepted?',
    'Kurx accepts UPI, credit/debit cards, net banking, and popular wallets via Razorpay.',
    'Payments',
  ),
  _Faq(
    'When will I get my refund?',
    'Refunds are processed within 5-7 business days to your original payment method once the organizer approves the request.',
    'Payments',
  ),
  _Faq(
    'My payment failed but money was deducted — what do I do?',
    'If the payment failed, any deducted amount will be automatically refunded within 5-7 days. Contact support if it takes longer.',
    'Payments',
  ),
  _Faq(
    'Where do I find my tickets?',
    'Go to the Tickets tab at the bottom of the app. Active tickets will appear there with their QR codes.',
    'Tickets',
  ),
  _Faq(
    'How do I transfer a ticket to someone?',
    'Open the ticket from the Tickets tab, tap "Transfer Ticket", enter the recipient\'s phone number, and confirm.',
    'Tickets',
  ),
  _Faq(
    'How do I change my phone number?',
    'Phone number changes require identity verification. Contact support for assistance.',
    'Account',
  ),
  _Faq(
    'How do I delete my account?',
    'Go to Settings → Delete Account. Your data will be removed within 30 days per our Privacy Policy.',
    'Account',
  ),
];

class _FaqTile extends StatefulWidget {
  const _FaqTile({required this.faq});
  final _Faq faq;

  @override
  State<_FaqTile> createState() => _FaqTileState();
}

class _FaqTileState extends State<_FaqTile> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    // An FAQ row that expands on tap and announced as neither a control nor a state — so a screen
    // reader user could not tell there was an answer underneath, let alone whether it was open.
    return Semantics(
      button: true,
      expanded: _expanded,
      child: GestureDetector(
      onTap: () => setState(() => _expanded = !_expanded),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: KSpace.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    widget.faq.question,
                    style: TextStyle(
                      color: c.text,
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                ),
                Icon(
                  _expanded
                      ? Icons.expand_less_rounded
                      : Icons.expand_more_rounded,
                  color: c.muted,
                ),
              ],
            ),
            AnimatedCrossFade(
              firstChild: const SizedBox.shrink(),
              secondChild: Padding(
                padding: const EdgeInsets.only(top: KSpace.sm),
                child: Text(
                  widget.faq.answer,
                  style: TextStyle(
                      color: c.muted, fontSize: 13.5, height: 1.45),
                ),
              ),
              crossFadeState: _expanded
                  ? CrossFadeState.showSecond
                  : CrossFadeState.showFirst,
              duration: context.motion(KMotion.base),
            ),
          ],
        ),
      ),
      ),
    );
  }
}
