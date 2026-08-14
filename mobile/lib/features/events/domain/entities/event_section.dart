/// The public discovery lists surfaced on the home dashboard, each backed by its
/// own `/v1/events/{section}` endpoint.
enum EventSection {
  featured,
  trending,
  upcoming,
  latest;

  String get title => switch (this) {
        EventSection.featured => 'Featured',
        EventSection.trending => 'Trending',
        EventSection.upcoming => 'Upcoming',
        EventSection.latest => 'Latest',
      };
}
