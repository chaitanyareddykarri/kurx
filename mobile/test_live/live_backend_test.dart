// Live end-to-end check against a running Kurx backend (localhost:5080).
// NOT part of the hermetic `flutter test` suite — run explicitly:
//   flutter test test_live/live_backend_test.dart
// Uses the app's real datasources + DTOs (no mocks) to prove the contract.
@Timeout(Duration(seconds: 40))
library;

import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:kurx_mobile/core/network/api_error.dart';
import 'package:kurx_mobile/core/network/app_config.dart';
import 'package:kurx_mobile/features/auth/data/datasources/auth_remote_data_source.dart';
import 'package:kurx_mobile/features/events/data/datasources/events_remote_data_source.dart';
import 'package:kurx_mobile/features/events/domain/entities/event_query.dart';

String _readOtpFromLog(String phone) {
  final log = File('/tmp/kurx-api.log').readAsStringSync();
  final matches =
      RegExp('to=91$phone.*?login code is (\\d{6})', dotAll: true).allMatches(log).toList();
  if (matches.isEmpty) throw StateError('No OTP found in API log for $phone');
  return matches.last.group(1)!;
}

void main() {
  final dio = Dio(BaseOptions(
    baseUrl: AppConfig.apiBase,
    contentType: Headers.jsonContentType,
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 15),
  ));

  test('OTP login → /v1/me → upcoming events, all live', () async {
    // Unique phone to dodge per-phone OTP rate limits across runs.
    final phone = '97${DateTime.now().millisecondsSinceEpoch.toString().substring(5, 13)}';
    final auth = AuthRemoteDataSource(dio);

    await auth.requestOtp(phone);
    await Future<void>.delayed(const Duration(milliseconds: 400));
    final code = _readOtpFromLog(phone);

    final tokens = await auth.verifyOtp(phone, code);
    expect(tokens.accessToken, isNotEmpty);
    expect(tokens.refreshToken, isNotEmpty);

    // Authenticated call with the freshly minted access token.
    dio.options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
    final me = await auth.me();
    expect(me.phone, '91$phone');
    expect(me.needsOnboarding, isTrue); // brand-new user

    // Public discovery: the two seeded published events must be present.
    dio.options.headers.remove('Authorization');
    final events = (await EventsRemoteDataSource(dio).upcoming(limit: 20)).map((d) => d.toEntity()).toList();
    expect(events.length, greaterThanOrEqualTo(2));
    expect(events.any((e) => e.title.contains('Sunburn')), isTrue);
    expect(events.every((e) => e.status == 'published'), isTrue);
  });

  // Requires the seeded 'nh7-weekender-pune' event with an on-sale ticket type.
  test('event detail + related + public ticket types, all live', () async {
    final events = EventsRemoteDataSource(dio);

    final detail = (await events.detail('nh7-weekender-pune')).toEntity();
    expect(detail.title, 'NH7 Weekender Pune');
    expect(detail.description, isNotEmpty);
    expect(detail.location, contains('Pune'));

    final tickets = (await events.ticketTypes(detail.id)).map((d) => d.toEntity()).toList();
    expect(tickets, isNotEmpty);
    expect(tickets.first.name, 'General Admission');
    expect(tickets.first.pricePaise, 150000);
    expect(tickets.first.soldOut, isFalse);

    final related = await events.related('nh7-weekender-pune');
    expect(related, isA<List<dynamic>>()); // same category may be empty; shape must hold
  });

  test('search, filters, sort and pagination, all live', () async {
    final events = EventsRemoteDataSource(dio);

    // Full-text search on title.
    final byText = await events.search(const EventQuery(q: 'Sunburn'));
    expect(byText.total, greaterThanOrEqualTo(1));
    expect(byText.items.any((e) => e.title.contains('Sunburn')), isTrue);

    // City filter (exact, case-insensitive) — NH7 is in Pune.
    final byCity = await events.search(const EventQuery(city: 'pune'));
    expect(byCity.items.every((e) => (e.city ?? '').toLowerCase() == 'pune'), isTrue);
    expect(byCity.items.any((e) => e.title.contains('NH7')), isTrue);

    // Category filter — all seeded events are in "Music".
    final categories = await events.categories();
    final music = categories.firstWhere((c) => c.name == 'Music');
    final byCat = await events.search(EventQuery(categoryId: music.id));
    expect(byCat.total, greaterThanOrEqualTo(3));

    // Sort accepted by the backend.
    final popular = await events.search(const EventQuery(sort: 'popular'));
    expect(popular.items, isNotEmpty);

    // Pagination: page size 1 returns a single item but the full total.
    final p1 = await events.search(const EventQuery(pageSize: 1, page: 1));
    final p2 = await events.search(const EventQuery(pageSize: 1, page: 2));
    expect(p1.items.length, 1);
    expect(p2.items.length, 1);
    expect(p1.total, greaterThanOrEqualTo(3));
    expect(p1.items.first.id, isNot(p2.items.first.id));
  });

  test('guest (no token ever) reads all public content but is denied protected endpoints', () async {
    // A fresh client that has never held a token — exactly a guest session.
    final guest = Dio(BaseOptions(baseUrl: AppConfig.apiBase, contentType: Headers.jsonContentType));
    final events = EventsRemoteDataSource(guest);

    expect(await events.upcoming(), isNotEmpty);
    expect((await events.search(const EventQuery())).total, greaterThanOrEqualTo(3));
    expect(await events.categories(), isNotEmpty);
    final detail = (await events.detail('nh7-weekender-pune')).toEntity();
    expect(detail.title, isNotEmpty);
    expect(await events.ticketTypes(detail.id), isNotEmpty);
    expect(await events.related('nh7-weekender-pune'), isA<List<dynamic>>());

    // Security boundary: a protected endpoint must reject the tokenless guest.
    await expectLater(AuthRemoteDataSource(guest).me(), throwsA(isA<ApiError>()));
  });

  test('discovery home sections (featured/trending/upcoming/latest + categories), all live', () async {
    final events = EventsRemoteDataSource(dio);

    final featured = await events.featured(limit: 10);
    final trending = await events.trending(limit: 10);
    final upcoming = await events.upcoming(limit: 10);
    final latest = await events.latest(limit: 10);
    final categories = await events.categories();

    expect(featured.any((e) => e.title.contains('NH7')), isTrue); // seeded featured
    expect(trending, isNotEmpty);
    expect(upcoming, isNotEmpty);
    expect(latest, isNotEmpty);
    expect(categories.any((c) => c.name == 'Music'), isTrue);
  });
}
