using System.Threading.Tasks;
using Hangfire.EntityFrameworkStorage.SampleStuff;
using Microsoft.AspNetCore.SignalR;

namespace Hangfire.EntityFrameworkStorage.WebApplication
{
    public class ChatHub : Hub<IChatHub>
    {
        private readonly ILogPersistenceService _logPersistenceService;

        public ChatHub(ILogPersistenceService logPersistenceService)
        {
            _logPersistenceService = logPersistenceService;
        }

        public async Task GetRecentLog()
        {
            var logItems = _logPersistenceService.GetRecent();
            foreach (var logItem in logItems)
                await Clients.Caller.SendLogAsObject(logItem);
        }
    }
}