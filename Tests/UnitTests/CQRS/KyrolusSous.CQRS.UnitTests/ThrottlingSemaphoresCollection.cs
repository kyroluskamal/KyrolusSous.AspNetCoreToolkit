using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// Groups every test that touches <c>KyrolusThrottlingBehavior</c>'s semaphore pool so they run
/// sequentially rather than in parallel.
/// </summary>
/// <remarks>
/// The pool is process-wide static storage (deliberately - see the fix on
/// <c>KyrolusThrottlingSemaphores</c>, which stopped it from being silently partitioned per closed
/// generic type). A test that calls <c>KyrolusThrottlingBehavior&lt;,&gt;.ClearSemaphores()</c> now
/// genuinely clears state shared with every other test using that pool, including one mid-throttle in
/// another test class; running them in xUnit's default parallel-by-class mode let one test's clear
/// silently unblock another test's in-flight semaphore wait.
/// </remarks>
[CollectionDefinition("ThrottlingSemaphores")]
public sealed class ThrottlingSemaphoresCollection;
