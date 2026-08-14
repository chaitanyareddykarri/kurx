/// Static app metadata. `version` mirrors `pubspec.yaml`'s `version:` — update both
/// together (kept a const to avoid pulling a platform plugin just for a version string).
class AppInfo {
  const AppInfo._();

  static const String name = 'Kurx';
  static const String tagline = 'Discover events across India';
  static const String version = '1.0.0';
}
