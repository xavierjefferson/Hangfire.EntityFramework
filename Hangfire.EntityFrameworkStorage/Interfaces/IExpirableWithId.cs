namespace Hangfire.EntityFrameworkStorage.Interfaces;

internal interface IExpirableWithId : IExpirable, IInt32Id
{
}