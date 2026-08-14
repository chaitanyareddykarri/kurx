/// A taxonomy node from `/v1/categories`.
class EventCategory {
  const EventCategory({
    required this.id,
    required this.name,
    this.level = 'category',
    this.parentId,
    this.productClass,
  });

  final String id;
  final String name;
  final String level;
  final String? parentId;

  /// "Public" | "Private" on a Type node; null on Audience/Category nodes and on Types no admin has
  /// classified. **Null means Public** — the same fallback the server applies when deriving
  /// `Event.Product` from the Type (D-266 M1). The Create-Event gate filters on this (D-305).
  final String? productClass;

  /// Whether this Type may be used for an event of [product]. Kept here rather than in the page so web
  /// and Flutter apply one rule: web's `typesFor()` is the same predicate.
  bool allowsProduct(String product) =>
      product == 'Private' ? productClass == 'Private' : productClass != 'Private';
}
