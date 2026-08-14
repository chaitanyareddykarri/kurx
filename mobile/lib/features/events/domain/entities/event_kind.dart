/// A V3 Kind registry entry from `/v1/kinds` (Phase 16 discovery filter/quick-browse source).
class EventKind {
  const EventKind({
    required this.slug,
    required this.name,
    required this.groupSlug,
    required this.groupName,
    required this.sort,
  });

  final String slug;
  final String name;
  final String groupSlug;
  final String groupName;
  final int sort;
}
