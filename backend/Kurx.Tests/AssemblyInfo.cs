// Each test class gets its OWN database (kurx_test_<guid>, cloned from a migrated template in
// KurxApiFactory), so this is no longer about sharing one database — that comment outlived the D-128
// hardening that removed the sharing. Classes still must not interleave: every class boots its own
// host and clones a database, and running several at once contends for Postgres connections and for
// the template clone itself.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
