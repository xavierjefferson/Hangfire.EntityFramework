namespace Hangfire.EntityFrameworkStorage.Entities;

internal interface IExpirableWithId : IExpirable, IInt32Id
{
}
internal interface IExpirableWithStringId : IExpirable, IStringId
{
}