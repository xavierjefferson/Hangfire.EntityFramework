using System.Threading.Tasks;
using Hangfire.EntityFrameworkStorage.SampleStuff;

namespace Hangfire.EntityFrameworkStorage.WebApplication
{
    public interface IHub
    {
        Task SendLogAsString(string message);

        Task SendLogAsObject(LogItem messageObject);
    }
}