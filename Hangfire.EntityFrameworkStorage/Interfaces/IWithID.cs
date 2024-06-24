namespace Hangfire.EntityFrameworkStorage.Interfaces;

public interface IWithID<U>
{
    U Id { get; set; }
}