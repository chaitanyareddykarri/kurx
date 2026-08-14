import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/design_tokens.dart';
import 'kurx_avatar.dart';

/// A compact person identity row — the shared primitive for allies, search results, attendee/team/
/// speaker/member lists, and anywhere else a user is rendered as a row. Mirrors web's `UserCard`
/// one-for-one so both platforms present the same information architecture (D-20x). `trailing`
/// sits outside the tap target so a Connect button never nests inside the profile-navigation tap.
class KurxUserTile extends StatelessWidget {
  const KurxUserTile({
    super.key,
    required this.name,
    this.username,
    this.avatarKey,
    this.subtitle,
    this.trailing,
  });

  final String name;
  final String? username;
  final String? avatarKey;
  final String? subtitle;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Container(
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        color: c.cardSurface,
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: Border.all(color: c.border),
      ),
      child: Row(
        children: [
          Expanded(
            child: InkWell(
              onTap: username != null ? () => context.push('/u/$username') : null,
              borderRadius: BorderRadius.circular(KRadius.md),
              child: Row(
                children: [
                  KurxAvatar(name: name, imageUrl: avatarKey, size: 44),
                  const SizedBox(width: KSpace.md),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(name,
                            style: TextStyle(color: c.text, fontWeight: FontWeight.w700, fontSize: 14),
                            overflow: TextOverflow.ellipsis),
                        if (username != null)
                          Text('@$username', style: TextStyle(color: c.muted, fontSize: 12.5)),
                        if (subtitle != null)
                          Text(subtitle!,
                              style: TextStyle(color: c.muted, fontSize: 12),
                              overflow: TextOverflow.ellipsis),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
          if (trailing != null) ...[const SizedBox(width: KSpace.sm), trailing!],
        ],
      ),
    );
  }
}
