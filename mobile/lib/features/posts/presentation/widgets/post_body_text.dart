import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/theme/design_tokens.dart';

/// Splits a post/comment body into plain text, #hashtags and @mentions.
///
/// Pattern is kept identical to the web client's so both surfaces link exactly the same substrings
/// — and so the server, which extracts the same tokens authoritatively at write time, stores what
/// users see highlighted. Display only: the stored hashtags/mentions carry no positions, and the
/// same tag can appear more than once, so re-scanning here is what keeps every occurrence tappable.
final RegExp postTokenPattern = RegExp(r'(^|[\s(])([#@][A-Za-z0-9_]{1,30})(?=$|[\s).,!?:;])');

enum PostTokenKind { text, hashtag, mention }

class PostToken {
  const PostToken(this.kind, this.text, [this.value]);

  final PostTokenKind kind;

  /// Exactly as written by the author, including the leading # or @.
  final String text;

  /// Normalized target: the lowercased tag or username. Null for plain text.
  final String? value;
}

List<PostToken> tokenizePostBody(String body) {
  final tokens = <PostToken>[];
  var last = 0;

  for (final m in postTokenPattern.allMatches(body)) {
    final lead = m.group(1) ?? '';
    final token = m.group(2)!;
    // The match starts at the leading whitespace, which belongs to the preceding text run.
    final start = m.start + lead.length;
    if (start > last) tokens.add(PostToken(PostTokenKind.text, body.substring(last, start)));

    final rest = token.substring(1);
    tokens.add(PostToken(
      token.startsWith('#') ? PostTokenKind.hashtag : PostTokenKind.mention,
      token,
      rest.toLowerCase(),
    ));
    last = start + token.length;
  }

  if (last < body.length) tokens.add(PostToken(PostTokenKind.text, body.substring(last)));
  return tokens;
}

class PostBodyText extends StatefulWidget {
  const PostBodyText(this.body, {super.key, this.maxLines, this.muted = false});

  final String body;
  final int? maxLines;
  final bool muted;

  @override
  State<PostBodyText> createState() => _PostBodyTextState();
}

class _PostBodyTextState extends State<PostBodyText> {
  /// Recognizers hold a gesture arena entry and leak if they outlive the widget, so they are owned
  /// here and disposed rather than created inline in `build`.
  final List<TapGestureRecognizer> _recognizers = [];

  @override
  void dispose() {
    for (final r in _recognizers) {
      r.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (widget.body.isEmpty) return const SizedBox.shrink();

    for (final r in _recognizers) {
      r.dispose();
    }
    _recognizers.clear();

    final c = context.kurx;
    final base = Theme.of(context)
        .textTheme
        .bodyMedium
        ?.copyWith(color: widget.muted ? c.muted : c.text);

    final spans = <InlineSpan>[];
    for (final t in tokenizePostBody(widget.body)) {
      if (t.kind == PostTokenKind.text) {
        spans.add(TextSpan(text: t.text, style: base));
        continue;
      }
      final recognizer = TapGestureRecognizer()
        ..onTap = () {
          if (!mounted) return;
          context.push(
            t.kind == PostTokenKind.hashtag ? '/posts/tag/${t.value}' : '/u/${t.value}',
          );
        };
      _recognizers.add(recognizer);
      spans.add(TextSpan(
        text: t.text,
        style: base?.copyWith(color: c.accent, fontWeight: FontWeight.w600),
        recognizer: recognizer,
      ));
    }

    return Text.rich(
      TextSpan(children: spans),
      maxLines: widget.maxLines,
      overflow: widget.maxLines == null ? TextOverflow.clip : TextOverflow.ellipsis,
    );
  }
}
