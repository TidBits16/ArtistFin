using System.Net.Mime;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ArtistFin;

[Authorize(Policy = Policies.RequiresElevation)]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Route("ArtistFin")]
public sealed class ArtistFinController : ControllerBase
{
    private readonly ArtistEngine _engine;
    private readonly ITaskManager _tasks;

    public ArtistFinController(ArtistEngine engine, ITaskManager tasks)
    {
        _engine = engine;
        _tasks = tasks;
    }

    /// <summary>Queue a force refresh of all music artists on the single scheduled task.</summary>
    [HttpPost("RefreshAll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<RefreshAllResponse> RefreshAll()
    {
        _engine.RequestForce();
        _tasks.CancelIfRunningAndQueue<ArtistLibraryTask>();
        return Ok(new RefreshAllResponse { Queued = true });
    }
}

public sealed class RefreshAllResponse
{
    public bool Queued { get; set; }
}
