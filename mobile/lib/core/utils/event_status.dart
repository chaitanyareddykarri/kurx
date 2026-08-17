/// Event lifecycle status → the words an organiser reads.
///
/// The API serialises `EventStatus` as the lowercased enum name with no separator, and three screens
/// printed that value straight into the UI: the status page's banner (`PENDINGREVIEW`), the workspace
/// hub's subtitle (`Opens after approval · pendingreview`) and the manage screen's badge. Web hit the
/// same defect and fixed it in `web/lib/event-status.ts`; these are the same labels, word for word, so
/// an organiser reading the same event on a phone and a laptop is told the same thing.
///
/// An unrecognised status is returned AS-IS rather than mapped to a guess: showing a raw word is worse
/// than showing the wrong one only if the wrong one is invisible, and telling someone their event is
/// "Published" because we did not recognise the value is exactly that.
library;

const Map<String, String> _labels = {
  'draft': 'Draft',
  'pendingreview': 'Pending review',
  'underreview': 'Under review',
  'changesrequested': 'Changes requested',
  'rejected': 'Rejected',
  'approved': 'Approved',
  'published': 'Published',
  'scheduled': 'Scheduled',
  'live': 'Live',
  'completed': 'Completed',
  'closed': 'Closed',
  'archived': 'Archived',
  'cancelled': 'Cancelled',
  'canceled': 'Cancelled',
};

/// Matched on the normalised key so a separator the server never sends (`under_review`, `Under-Review`)
/// still lands — the review states were serialised three different ways across this repo's history.
String eventStatusLabel(String status) {
  final key = status.toLowerCase().replaceAll(RegExp(r'[\s_-]'), '');
  return _labels[key] ?? status;
}
