using Microsoft.AspNetCore.Mvc;

namespace ProformaFarm.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ErrorController : ControllerBase
{
    [Route("/error")]
    public IActionResult Error() => Problem();
}
