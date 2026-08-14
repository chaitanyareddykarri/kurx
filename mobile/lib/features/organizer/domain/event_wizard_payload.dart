/// Payload shaping for the create-event wizard (D-265).
///
/// Kept out of the page so it can be tested without pumping a widget: it encodes a server contract,
/// and getting it wrong corrupts data silently rather than failing loudly.
library;

/// Drops nulls and blank strings, returning null when nothing survives.
///
/// This is not tidiness. On the server a **null field means "leave alone"** and an **empty string
/// means "clear"**, so a wizard step the organiser never opened must send *nothing* — sending its
/// blanks would wipe fields set in a different step.
///
/// `false` and `0` are deliberately kept: an unchecked switch and a zero value are real answers.
/// Strings are trimmed, because a field holding only spaces is a field the organiser left empty.
Map<String, dynamic>? compactGroup(Map<String, dynamic> raw) {
  final out = <String, dynamic>{};
  raw.forEach((key, value) {
    if (value == null) return;
    if (value is String) {
      final trimmed = value.trim();
      if (trimmed.isEmpty) return;
      out[key] = trimmed;
      return;
    }
    out[key] = value;
  });
  return out.isEmpty ? null : out;
}
