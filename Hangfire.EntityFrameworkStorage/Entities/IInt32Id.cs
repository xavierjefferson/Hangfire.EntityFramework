namespace Hangfire.EntityFrameworkStorage.Entities;

public interface IWithID<U>
{
    U Id { get; set; }
}
public interface IInt32Id:IWithID<int>
{
    
}
public interface IStringId:IWithID<string>
{

}