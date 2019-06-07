using System;
using MessageReceiver.Services;
using Microsoft.AspNetCore.Mvc;

namespace MessageReceiver.Controllers
{
    [Route("receiver/[controller]")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly MessageReceiverService _messageReceiverService;

        public StatusController(MessageReceiverService messageReceiverService)
        {
            _messageReceiverService = messageReceiverService;
        }

        [HttpGet]
        public ActionResult<object> Get()
        {
            var critical = DateTimeOffset.Now - _messageReceiverService.LastMessageReceived.DateTime >
                           TimeSpan.FromMinutes(5);
            return new
            {
                startTime = _messageReceiverService.StartTime,
                messageCount = _messageReceiverService.MessagesReceived,
                lastMessageReceived = _messageReceiverService.LastMessageReceived,
                critical
            };
        }
    }
}