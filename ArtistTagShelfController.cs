using System.Net.Mime;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ArtistTagShelf;

[Authorize(Policy = Policies.RequiresElevation)]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[Route("ArtistTagShelf")]
public sealed class ArtistTagShelfController : ControllerBase
{
    private readonly ArtistEngine _engine;
    private readonly ITaskManager _tasks;

    public ArtistTagShelfController(ArtistEngine engine, ITaskManager tasks)
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
