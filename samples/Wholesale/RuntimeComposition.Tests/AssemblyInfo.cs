using Xunit.Sdk;
using Xunit.v3;

// Aspire graphs create and remove container networks on the same machine.
// Keep their lifetimes separate so one test's teardown cannot interrupt
// another test's Chromium navigation with ERR_NETWORK_CHANGED.
[assembly: Parallelization(Mode = ParallelMode.None)]
