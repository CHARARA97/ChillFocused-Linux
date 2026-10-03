using Xunit;

// The plugin's state is process-global on purpose -- Unity gives it one process, and
// the panels read statics (the text language, the timer snapshot, the DI caches).
// xUnit runs test classes in parallel by default, which turns that into tests
// fighting over the same statics: the language test switches the panel to English
// and an unrelated assertion about Chinese wording fails *somewhere else*.
//
// This surfaced when the repository was split and the assembly's class order
// changed -- exactly the kind of failure that only shows up on someone else's
// machine. Serialising the tests is the honest fix: the code under test really is
// global.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
