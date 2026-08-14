import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/features/posts/presentation/widgets/post_body_text.dart';

/// The same cases as `web/test/posts-logic.test.ts`. Both surfaces link the same substrings, and the
/// server extracts the same tokens at write time — if these three ever disagree, a hashtag renders
/// as a link on one client and plain text on the other while the DB holds a third answer.
void main() {
  List<String> tagsIn(String body) => tokenizePostBody(body)
      .where((t) => t.kind == PostTokenKind.hashtag)
      .map((t) => t.value!)
      .toList();

  List<String> mentionsIn(String body) => tokenizePostBody(body)
      .where((t) => t.kind == PostTokenKind.mention)
      .map((t) => t.value!)
      .toList();

  test('plain text yields a single text token', () {
    final tokens = tokenizePostBody('just a plain post');
    expect(tokens, hasLength(1));
    expect(tokens.single.kind, PostTokenKind.text);
    expect(tokens.single.text, 'just a plain post');
  });

  test('a hashtag lowercases its target but keeps the shown text', () {
    final tokens = tokenizePostBody('ship it #HackDay');
    expect(tokens.last.kind, PostTokenKind.hashtag);
    expect(tokens.last.text, '#HackDay');
    expect(tokens.last.value, 'hackday');
  });

  test('a mention is linked', () {
    expect(mentionsIn('cc @Asha'), ['asha']);
  });

  test('every occurrence of a repeated tag is linked, not just the first', () {
    expect(tagsIn('#kurx and again #kurx'), ['kurx', 'kurx']);
  });

  test('an email address is not a mention', () {
    // A token must follow start-of-string or whitespace, so "asha@kurx.in" never matches.
    expect(mentionsIn('mail me at asha@kurx.in'), isEmpty);
  });

  test('trailing punctuation is not swallowed into the tag', () {
    final tokens = tokenizePostBody('see #finals, then go');
    expect(tokens[1].value, 'finals');
    expect(tokens[2].text, ', then go');
  });

  test('a tag at the very start of the body is linked', () {
    final tokens = tokenizePostBody('#first post');
    expect(tokens.first.kind, PostTokenKind.hashtag);
    expect(tokens.first.value, 'first');
  });

  test('a bare # or @ with no word after it is plain text', () {
    final tokens = tokenizePostBody('a # b @ c');
    expect(tokens, hasLength(1));
    expect(tokens.single.kind, PostTokenKind.text);
  });

  test('reassembling the tokens reproduces the original body exactly', () {
    // Guards the substring arithmetic: an off-by-one in the lead-offset handling would drop or
    // duplicate a character, which no per-token assertion above would necessarily catch.
    for (final body in [
      'hello #world and @asha, plus (#nested) end',
      '#a #b #c',
      'no tokens at all',
      '@start of line',
    ]) {
      expect(tokenizePostBody(body).map((t) => t.text).join(), body, reason: body);
    }
  });
}
