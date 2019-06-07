using MessageEmitter.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessageEmitter.Controllers
{
    [Route("emitter/[controller]")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly MessageEmitterService _emitterService;

        public StatusController(MessageEmitterService emitterService)
        {
            _emitterService = emitterService;
        }

        [HttpGet]
        public ActionResult<object> Get()
        {
            return new
            {
                startTime = _emitterService.StartTime,
                messageCount = _emitterService.MessageCount
            };
        }
    }
}