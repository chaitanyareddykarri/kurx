import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/common/util/content_type.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_attachment.dart';

ChatAttachment _attachment(String contentType, {String name = 'f'}) => ChatAttachment(
      id: 'a1',
      fileName: name,
      contentType: contentType,
      sizeBytes: 1024,
    );

/// D-298 — voice notes and video on the client side.
///
/// The content-type map is worth a test of its own: the server accepts a CLOSED set of types and
/// refuses anything else, so a missing entry here is not a cosmetic wrong guess — the upload fails
/// for a file the backend would happily have taken. That is exactly what happened before this: a
/// captured video went up as `application/octet-stream` and was rejected as unsupported.
void main() {
  group('content type for captured media', () {
    test('maps every media extension the server accepts', () {
      expect(guessContentType('mp3'), 'audio/mpeg');
      expect(guessContentType('m4a'), 'audio/mp4');
      expect(guessContentType('ogg'), 'audio/ogg');
      expect(guessContentType('opus'), 'audio/ogg');
      expect(guessContentType('weba'), 'audio/webm');
      expect(guessContentType('mp4'), 'video/mp4');
      expect(guessContentType('webm'), 'video/webm');
    });

    test('maps .mov to QuickTime, which is what an iPhone camera produces', () {
      // Not video/mp4: the server checks the extension against the DECLARED type, so claiming mp4 for
      // a .mov would be refused even though both are ISO-BMFF. iOS is the platform where video
      // capture matters most, so getting this wrong breaks the main case.
      expect(guessContentType('mov'), 'video/quicktime');
    });

    test('is case-insensitive, because a camera may hand back .MP4', () {
      expect(guessContentType('MP4'), 'video/mp4');
      expect(guessContentType('MOV'), 'video/quicktime');
    });

    test('an unknown extension falls back to octet-stream rather than a plausible guess', () {
      // The server refuses octet-stream. That is the honest outcome — inventing a type would only
      // move the refusal to the magic-byte check and make the failure harder to read.
      expect(guessContentType('exe'), 'application/octet-stream');
      expect(guessContentType(null), 'application/octet-stream');
    });
  });

  group('attachment media classification', () {
    test('audio, video and image are mutually exclusive', () {
      expect(_attachment('audio/ogg').isAudio, isTrue);
      expect(_attachment('audio/ogg').isVideo, isFalse);
      expect(_attachment('audio/ogg').isImage, isFalse);

      expect(_attachment('video/quicktime').isVideo, isTrue);
      expect(_attachment('video/quicktime').isAudio, isFalse);

      expect(_attachment('image/png').isImage, isTrue);
    });

    test('a document is none of the three, so it falls through to the file tile', () {
      final pdf = _attachment('application/pdf');
      expect([pdf.isImage, pdf.isAudio, pdf.isVideo], [false, false, false]);
    });

    test('an upload still in flight is pending, so it keeps the document card', () {
      // The media players mint a signed URL from the attachment id, and an unconfirmed upload has no
      // server-side row to mint against — rendering a player would give a control that cannot play.
      const uploading = ChatAttachment(
        id: 'local-1',
        fileName: 'voice-note.ogg',
        contentType: 'audio/ogg',
        sizeBytes: 2048,
        status: AttachmentUploadStatus.uploading,
        progress: 0.4,
      );
      expect(uploading.isAudio, isTrue);
      expect(uploading.isPending, isTrue);
    });
  });
}
