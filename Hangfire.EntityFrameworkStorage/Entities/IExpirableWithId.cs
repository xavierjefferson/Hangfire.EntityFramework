namespace Hangfire.EntityFrameworkStorage.Entities
{
    internal interface IExpirableWithId : IExpirable, IInt32Id
    {
    }
}