namespace Hangfire.EntityFrameworkStorage.Entities;

internal interface IExpirableWithKey : IExpirable
{
    string Key { get; set; }
}