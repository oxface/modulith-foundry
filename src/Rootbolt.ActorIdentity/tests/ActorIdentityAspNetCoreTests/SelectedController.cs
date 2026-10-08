using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Rootbolt.ActorIdentity.AspNetCore.Tests;

[ApiController]
[Route("selected-controller")]
[Authorize(AuthenticationSchemes = "secondary", Policy = "ObserveActor")]
public sealed class SelectedController(IActorContextAccessor accessor) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Content(TestApplication.Snapshot(accessor));
}
