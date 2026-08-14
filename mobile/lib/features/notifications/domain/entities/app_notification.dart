import 'package:flutter/material.dart';

enum NotificationKind { reminder, priceDrop, newEvent, booking, ally, system }

extension NotificationKindVisual on NotificationKind {
  IconData get icon => switch (this) {
        NotificationKind.reminder => Icons.alarm_rounded,
        NotificationKind.priceDrop => Icons.sell_outlined,
        NotificationKind.newEvent => Icons.auto_awesome_rounded,
        NotificationKind.booking => Icons.confirmation_number_rounded,
        NotificationKind.ally => Icons.handshake_outlined,
        NotificationKind.system => Icons.info_outline_rounded,
      };
}

/// An in-app notification. `route` deep-links to the relevant screen when tapped.
/// `connectionId` is only set for an `ally.requested` notification — the one kind whose card
/// renders inline Accept/Decline instead of just a tap target (D-20x).
class AppNotification {
  const AppNotification({
    required this.id,
    required this.kind,
    required this.title,
    required this.body,
    required this.at,
    this.route,
    this.connectionId,
    this.read = false,
  });

  final String id;
  final NotificationKind kind;
  final String title;
  final String body;
  final DateTime at;
  final String? route;
  final String? connectionId;
  final bool read;

  AppNotification copyWith({bool? read}) => AppNotification(
        id: id,
        kind: kind,
        title: title,
        body: body,
        at: at,
        route: route,
        connectionId: connectionId,
        read: read ?? this.read,
      );
}
