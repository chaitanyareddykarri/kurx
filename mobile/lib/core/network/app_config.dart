/// Build-time configuration. The API base URL comes from `--dart-define=KURX_API_BASE=...`
/// (the exact key documented in `.env.example`); the default matches the dev backend port — 5080,
/// which is what both `dotnet run` and `docker compose` serve.
/// There is deliberately no stale alternate fallback — the D-017 bug class.
///
/// **`localhost` is correct on-device and needs no rewriting.** A phone or emulator resolves
/// `localhost` to itself, not to the developer's machine — but `adb reverse tcp:5080 tcp:5080`
/// tunnels that port back over USB, so the default below reaches the dev backend as-is on a physical
/// device and an emulator alike. One mechanism, no platform branch, and nothing to pass. A
/// hard-coded `10.0.2.2` was tried and reverted (D-318): it is emulator-only, so it *breaks* the
/// tunnel on a real device — the stale-fallback bug class again, wearing a platform check.
class AppConfig {
  static const String apiBase = String.fromEnvironment(
    'KURX_API_BASE',
    defaultValue: 'http://localhost:5080',
  );

  /// The public **web** origin — where a shared profile link actually resolves. Distinct from
  /// [apiBase] and not derivable from it: profiles are served by the Next.js app, not the API, so a
  /// QR or share link built from the API host would open a URL that serves no profile page. Same
  /// `--dart-define` convention, same no-stale-fallback rule (D-017). The `adb reverse` trick that
  /// makes [apiBase]'s `localhost` work on-device does **not** rescue this one: the string is encoded
  /// into share links and QR codes that other people open on their own devices, where any loopback
  /// address resolves to their phone. Set it explicitly for anything a third party will open.
  static const String webBase = String.fromEnvironment(
    'KURX_WEB_BASE',
    defaultValue: 'http://localhost:3000',
  );

  /// The canonical public URL for a username — the one string both the share sheet and the QR
  /// encode, so the two can never point at different places.
  static String profileUrl(String username) => '$webBase/u/$username';
}
