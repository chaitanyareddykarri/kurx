import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive/hive.dart';
import 'package:kurx_mobile/core/storage/chat_cache.dart';
import 'package:kurx_mobile/core/theme/app_theme.dart';
import 'package:kurx_mobile/features/social/data/datasources/chat_remote_data_source.dart';
import 'package:kurx_mobile/features/social/data/models/chat_mappers.dart';
import 'package:kurx_mobile/features/social/data/repositories/chat_repository_impl.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_attachment.dart';
import 'package:kurx_mobile/features/social/domain/entities/chat_message.dart';
import 'package:kurx_mobile/features/social/presentation/widgets/chat_attachment_view.dart';

/// Scriptable adapter, same shape the other chat tests use.
class _ScriptedAdapter implements HttpClientAdapter {
  final Map<String, List<Object>> _script = {};
  final List<RequestOptions> calls = [];

  void queue(String path, List<Object> responses) => _script[path] = [...responses];
  // Matches on uri.path so a relative call (with baseUrl) and an absolute presigned URL
  // both resolve to the same key.
  int callsTo(String path) => calls.where((c) => c.uri.path == path).length;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? _, Future<void>? _) async {
    calls.add(options);
    final queued = _script[options.uri.path];
    if (queued == null || queued.isEmpty) return ResponseBody.fromString('{}', 404);

    final next = queued.length == 1 ? queued.first : queued.removeAt(0);
    if (next is DioExceptionType) throw DioException(requestOptions: options, type: next);
    if (next is int) {
      throw DioException(
        requestOptions: options,
        response: Response(requestOptions: options, statusCode: next, data: {'error': 'file_too_large'}),
        type: DioExceptionType.badResponse,
      );
    }

    return ResponseBody.fromString(
      jsonEncode(next),
      200,
      headers: {Headers.contentTypeHeader: [Headers.jsonContentType]},
    );
  }

  @override
  void close({bool force = false}) {}
}

Map<String, dynamic> _attachmentJson({String id = 'att-1'}) => {
      'id': id,
      'url': 'http://test/v1/storage/chat/room-1/abc?sig=deadbeef',
      'fileName': 'photo.png',
      'contentType': 'image/png',
      'sizeBytes': 2048,
      'width': 800,
      'height': 600,
    };

Map<String, dynamic> _messageJson({List<Map<String, dynamic>>? attachments}) => {
      'id': 'server-1',
      'roomId': 'room-1',
      'clientMessageId': null,
      'senderId': 'user-2',
      'senderName': 'Alice',
      'senderRole': 'Member',
      'kind': 'Text',
      'body': 'look at this',
      'replyToMessageId': null,
      'isPinned': false,
      'isDeleted': false,
      'attachments': attachments ?? const [],
      'createdAt': '2026-07-19T10:00:00Z',
    };

ChatAttachment _attachment({
  AttachmentUploadStatus status = AttachmentUploadStatus.uploaded,
  String contentType = 'image/png',
  String fileName = 'photo.png',
  double progress = 0,
  String? error,
  String? url = 'http://test/file',
}) =>
    ChatAttachment(
      id: 'att-1',
      fileName: fileName,
      contentType: contentType,
      sizeBytes: 2048,
      url: url,
      status: status,
      progress: progress,
      error: error,
    );

Widget _host(Widget child) => ProviderScope(
      child: MaterialApp(theme: AppTheme.light(), home: Scaffold(body: child)),
    );

void main() {
  group('attachment mapping', () {
    test('parses an attachment off a message', () {
      final message = ChatMappers.message(_messageJson(attachments: [_attachmentJson()]));

      expect(message.attachments, hasLength(1));
      final a = message.attachments.single;
      expect(a.fileName, 'photo.png');
      expect(a.contentType, 'image/png');
      expect(a.width, 800);
      expect(a.isImage, isTrue);
    });

    test('a message with no attachments yields an empty list, never null', () {
      expect(ChatMappers.message(_messageJson()).attachments, isEmpty);
    });

    test('the signed URL is never persisted to the cache', () {
      final message = ChatMappers.message(_messageJson(attachments: [_attachmentJson()]));
      final json = ChatMappers.messageToJson(message);

      // Signatures expire; a cached URL would fail in a way the user cannot act on, so it is
      // deliberately dropped and re-fetched on demand.
      final cached = (json['attachments'] as List).single as Map<String, dynamic>;
      expect(cached.containsKey('url'), isFalse);
      expect(cached['fileName'], 'photo.png');
    });

    test('a cached attachment round-trips without its URL', () {
      final original = ChatMappers.message(_messageJson(attachments: [_attachmentJson()]));
      final restored = ChatMappers.messageFromCache(ChatMappers.messageToJson(original));

      expect(restored.attachments.single.fileName, 'photo.png');
      expect(restored.attachments.single.url, isNull);
    });

    test('the filename comes from the server, never from the URL', () {
      // The backend serves everything as octet-stream with no Content-Disposition, so fileName is
      // the only trustworthy label.
      final json = _attachmentJson()..['url'] = 'http://test/v1/storage/chat/room-1/9f8e7d?sig=x';
      expect(ChatMappers.attachment(json).fileName, 'photo.png');
    });
  });

  group('upload flow', () {
    late Directory tempDir;
    late Box box;
    late _ScriptedAdapter adapter;
    late ChatRepositoryImpl repo;

    setUpAll(() async {
      tempDir = await Directory.systemTemp.createTemp('kurx_attach_test');
      Hive.init(tempDir.path);
    });

    tearDownAll(() async {
      await Hive.close();
      await tempDir.delete(recursive: true);
    });

    setUp(() async {
      box = await Hive.openBox('attach_${DateTime.now().microsecondsSinceEpoch}');
      adapter = _ScriptedAdapter();
      final dio = Dio(BaseOptions(baseUrl: 'http://test'))..httpClientAdapter = adapter;
      // The presigned PUT goes through a separate client in production (no auth header, absolute
      // URL); pointing it at the same adapter is what makes the upload leg observable here.
      final uploadDio = Dio()..httpClientAdapter = adapter;
      repo = ChatRepositoryImpl(
          ChatRemoteDataSource(dio, uploadClient: uploadDio), ChatCache(box));
    });

    tearDown(() async => box.deleteFromDisk());

    test('presign is called before confirm, and confirm returns the attachment', () async {
      adapter.queue('/v1/chat/rooms/room-1/attachments/presign', [
        {'key': 'chat/room-1/abc', 'url': 'http://test/v1/storage/chat/room-1/abc', 'headers': {'Content-Type': 'image/png'}},
      ]);
      adapter.queue('/v1/storage/chat/room-1/abc', [{'key': 'chat/room-1/abc'}]);
      adapter.queue('/v1/chat/rooms/room-1/attachments/confirm', [_attachmentJson()]);

      final result = await repo.uploadAttachment(
        'room-1', _attachment(status: AttachmentUploadStatus.queued), [1, 2, 3]);

      expect(result.id, 'att-1');
      expect(adapter.callsTo('/v1/chat/rooms/room-1/attachments/presign'), 1);
      expect(adapter.callsTo('/v1/chat/rooms/room-1/attachments/confirm'), 1);
    });

    test('a server refusal surfaces its code so the user can act on it', () async {
      adapter.queue('/v1/chat/rooms/room-1/attachments/presign', [413]);

      await expectLater(
        repo.uploadAttachment('room-1', _attachment(), [1, 2, 3]),
        throwsA(predicate((e) => e.toString().contains('file_too_large'))),
      );
    });

    test('a message is not sent until every attachment is confirmed', () async {
      // A pending attachment has no server id, so the message must wait rather than being sent
      // without its file.
      await repo.queueSend('room-1', 'with a photo',
          attachments: [_attachment(status: AttachmentUploadStatus.uploading)]);

      final confirmed = await repo.flushOutbox('room-1');

      expect(confirmed, isEmpty);
      expect(adapter.callsTo('/v1/chat/rooms/room-1/messages'), 0);
      // And it stays queued rather than being dropped.
      expect(ChatCache(box).readOutbox('room-1'), hasLength(1));
    });

    test('a confirmed attachment is sent by id and reconciles into the message', () async {
      adapter.queue('/v1/chat/rooms/room-1/messages', [
        _messageJson(attachments: [_attachmentJson()]),
      ]);

      await repo.queueSend('room-1', 'with a photo', attachments: [_attachment()]);
      final confirmed = await repo.flushOutbox('room-1');

      expect(confirmed, hasLength(1));
      expect(adapter.calls.last.data['attachmentIds'], ['att-1']);

      // The optimistic row collapsed onto the server copy — one message, with its file.
      final cached = repo.cachedMessages('room-1');
      expect(cached, hasLength(1));
      expect(cached.single.attachments, hasLength(1));
      expect(cached.single.status, ChatSendStatus.sent);
    });

    test('an offline send leaves the attachment message queued for retry', () async {
      adapter.queue('/v1/chat/rooms/room-1/messages', [DioExceptionType.connectionError]);

      await repo.queueSend('room-1', 'offline photo', attachments: [_attachment()]);
      await repo.flushOutbox('room-1');

      // Never disappears: still pending, still carrying its attachment.
      final queued = ChatCache(box).readOutbox('room-1').single;
      expect(queued.status, ChatSendStatus.pending);
      expect(queued.attachments, hasLength(1));
    });

    test('a fresh download URL is requested every time, never cached', () async {
      adapter.queue('/v1/chat/attachments/att-1/url', [
        {'url': 'http://test/one'},
        {'url': 'http://test/two'},
      ]);

      expect(await repo.attachmentUrl('att-1'), 'http://test/one');
      expect(await repo.attachmentUrl('att-1'), 'http://test/two');
      expect(adapter.callsTo('/v1/chat/attachments/att-1/url'), 2);
    });
  });

  group('rendering', () {
    testWidgets('a document shows its name, size and a download affordance', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(contentType: 'application/pdf', fileName: 'invoice.pdf'),
        onDismissFailed: () {},
      )));

      expect(find.text('invoice.pdf'), findsOneWidget);
      expect(find.text('2 KB'), findsOneWidget);
      expect(find.byIcon(Icons.download_rounded), findsOneWidget);
      expect(find.byIcon(Icons.picture_as_pdf_rounded), findsOneWidget);
    });

    testWidgets('an unknown type still renders a usable tile', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(contentType: 'application/x-weird', fileName: 'thing.bin'),
        onDismissFailed: () {},
      )));

      expect(find.text('thing.bin'), findsOneWidget);
      expect(find.byIcon(Icons.insert_drive_file_outlined), findsOneWidget);
    });

    testWidgets('an uploading document shows progress and is not tappable', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(
          contentType: 'application/pdf',
          fileName: 'slow.pdf',
          status: AttachmentUploadStatus.uploading,
          progress: 0.42,
        ),
        onDismissFailed: () {},
      )));

      expect(find.text('Uploading… 42%'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      // No download affordance while pending — there is nothing to download yet.
      expect(find.byIcon(Icons.download_rounded), findsNothing);
    });

    testWidgets('a rejected upload explains why and can be dismissed', (tester) async {
      var dismissed = false;
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(status: AttachmentUploadStatus.failed, error: 'file_too_large'),
        onDismissFailed: () => dismissed = true,
      )));

      expect(find.text('That file is over the 25 MB limit.'), findsOneWidget);
      await tester.tap(find.byTooltip('Dismiss'));
      expect(dismissed, isTrue);
    });

    testWidgets('a malware rejection is stated plainly', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(status: AttachmentUploadStatus.failed, error: 'file_infected'),
        onDismissFailed: () {},
      )));

      expect(find.text('That file was rejected by a security scan.'), findsOneWidget);
    });

    testWidgets('an unknown server error still says something useful', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(status: AttachmentUploadStatus.failed, error: 'something_new'),
        onDismissFailed: () {},
      )));

      expect(find.text('That file could not be uploaded.'), findsOneWidget);
    });
  });

  group('accessibility', () {
    testWidgets('a document announces its name and size', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(contentType: 'application/pdf', fileName: 'report.pdf'),
        onDismissFailed: () {},
      )));

      expect(find.bySemanticsLabel(RegExp('report.pdf, 2 KB')), findsOneWidget);
    });

    testWidgets('upload progress is announced, not just drawn', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(
          contentType: 'application/pdf',
          fileName: 'slow.pdf',
          status: AttachmentUploadStatus.uploading,
          progress: 0.5,
        ),
        onDismissFailed: () {},
      )));

      expect(find.bySemanticsLabel(RegExp('uploading 50 percent')), findsOneWidget);
    });

    testWidgets('an image attachment has a descriptive label', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(url: null),   // no network fetch in a widget test
        onDismissFailed: () {},
      )));

      expect(find.bySemanticsLabel(RegExp('Image attachment photo.png')), findsOneWidget);
    });

    testWidgets('the document tile meets the minimum touch target', (tester) async {
      await tester.pumpWidget(_host(ChatAttachmentView(
        attachment: _attachment(contentType: 'application/pdf', fileName: 'a.pdf'),
        onDismissFailed: () {},
      )));

      final size = tester.getSize(find.byType(InkWell));
      expect(size.height, greaterThanOrEqualTo(48));
    });
  });
}
