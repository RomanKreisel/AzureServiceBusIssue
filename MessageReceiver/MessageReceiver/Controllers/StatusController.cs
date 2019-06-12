using MessageReceiver.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessageReceiver.Controllers
{
    [Route("receiver/[controller]")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly ReceiverStatusService _receiverStatusService;

        public StatusController(ReceiverStatusService receiverStatusService)
        {
            _receiverStatusService = receiverStatusService;
        }

        [HttpGet]
        public ActionResult<object> Get()
        {
            var instanceList = _receiverStatusService.GetStatus();
            var allOk = true;
            foreach (var instance in instanceList)
                if (instance.Value.Critical)
                {
                    allOk = false;
                    break;
                }

            return new
            {
                critical = !allOk,
                instanceList
            };
        }
    }
}