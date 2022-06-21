using Microsoft.AspNetCore.Mvc;

namespace MergerService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MergerController : ControllerBase
    {
        private readonly ILogger<MergerController> _logger;

        public MergerController(ILogger<MergerController> logger)
        {
            this._logger = logger;
        }
    }
}
